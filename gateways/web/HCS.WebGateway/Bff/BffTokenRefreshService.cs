using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace HCS.WebGateway;

internal sealed class BffTokenRefreshService
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ConcurrentResultLifetime = TimeSpan.FromMinutes(2);
    private readonly SemaphoreSlim[] refreshLocks = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    private readonly BffRefreshResultCache? refreshResults;
    private readonly IDatabase? redis;
    private readonly IDataProtector? refreshResultProtector;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IConfiguration configuration;
    private readonly TimeProvider timeProvider;

    public BffTokenRefreshService(
        BffRefreshResultCache refreshResults,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        TimeProvider timeProvider)
    {
        this.refreshResults = refreshResults;
        this.httpClientFactory = httpClientFactory;
        this.configuration = configuration;
        this.timeProvider = timeProvider;
    }

    public BffTokenRefreshService(
        IConnectionMultiplexer redis,
        IDataProtectionProvider dataProtectionProvider,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        TimeProvider timeProvider)
    {
        this.redis = redis.GetDatabase();
        refreshResultProtector = dataProtectionProvider.CreateProtector("HCS.Bff.TokenRefreshResult.v1");
        this.httpClientFactory = httpClientFactory;
        this.configuration = configuration;
        this.timeProvider = timeProvider;
    }

    internal async Task<string?> GetValidAccessTokenAsync(HttpContext context)
    {
        var authentication = await context.AuthenticateAsync(HCSWebGatewayModule.CookieScheme);
        if (!authentication.Succeeded || authentication.Principal is null || authentication.Properties is null)
        {
            return null;
        }

        var accessToken = authentication.Properties.GetTokenValue("access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        if (!NeedsRefresh(authentication.Properties, timeProvider.GetUtcNow()))
        {
            return accessToken;
        }

        var refreshToken = authentication.Properties.GetTokenValue("refresh_token");
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        try
        {
            var refreshed = await RefreshForTokenAsync(refreshToken, context.RequestAborted);
            ApplyTokens(authentication.Properties, refreshed);
            await context.SignInAsync(
                HCSWebGatewayModule.CookieScheme,
                authentication.Principal,
                authentication.Properties);
            return refreshed.AccessToken;
        }
        catch (InvalidRefreshTokenException) when (!context.RequestAborted.IsCancellationRequested)
        {
            await context.SignOutAsync(HCSWebGatewayModule.CookieScheme);
            return null;
        }
        catch (Exception) when (!context.RequestAborted.IsCancellationRequested)
        {
            context.Items[BffAccessTokenMiddleware.TokenRefreshUnavailableItemKey] = true;
            return null;
        }
    }

    internal async Task<TokenRefreshResult> RefreshForTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
        if (redis is not null && refreshResultProtector is not null)
        {
            return await RefreshDistributedAsync(cacheKey, refreshToken, cancellationToken);
        }

        // This constructor exists only for isolated unit tests. Runtime registration always
        // uses Redis because refresh-token rotation must coordinate every gateway replica.
        var refreshLock = refreshLocks[Convert.ToInt32(cacheKey[..2], 16) % refreshLocks.Length];
        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (refreshResults is null || !refreshResults.TryGet(cacheKey, out var refreshed) || refreshed is null)
            {
                refreshed = await RefreshAsync(refreshToken, cancellationToken);
                refreshResults!.Set(cacheKey, refreshed, ConcurrentResultLifetime);
            }

            return refreshed;
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private async Task<TokenRefreshResult> RefreshDistributedAsync(
        string cacheKey,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var resultKey = (RedisKey)$"hcs:bff:refresh:result:{cacheKey}";
        var lockKey = (RedisKey)$"hcs:bff:refresh:lock:{cacheKey}";
        var lockValue = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var waitLimit = TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("Authentication:RefreshCoordinationTimeoutSeconds", 35), 5, 60));
        var lockLifetime = TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("Authentication:RefreshLockLifetimeSeconds", 45), 10, 90));
        var deadline = timeProvider.GetUtcNow().Add(waitLimit);

        while (timeProvider.GetUtcNow() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cached = await TryGetDistributedResultAsync(resultKey);
            if (cached is not null)
            {
                return cached;
            }

            if (await redis!.StringSetAsync(lockKey, lockValue, lockLifetime, When.NotExists))
            {
                try
                {
                    // A replica may have completed immediately before we took the lock.
                    cached = await TryGetDistributedResultAsync(resultKey);
                    if (cached is not null)
                    {
                        return cached;
                    }

                    var refreshed = await RefreshAsync(refreshToken, cancellationToken);
                    var serialized = JsonSerializer.Serialize(refreshed);
                    await redis.StringSetAsync(resultKey, refreshResultProtector!.Protect(serialized), ConcurrentResultLifetime);
                    return refreshed;
                }
                finally
                {
                    await redis.ScriptEvaluateAsync(
                        "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end",
                        [lockKey],
                        [lockValue]);
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        throw new TransientTokenRefreshException("Timed out waiting for another Gateway replica to refresh the session.");
    }

    private async Task<TokenRefreshResult?> TryGetDistributedResultAsync(RedisKey resultKey)
    {
        var protectedResult = await redis!.StringGetAsync(resultKey);
        if (!protectedResult.HasValue)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TokenRefreshResult>(refreshResultProtector!.Unprotect(protectedResult!));
        }
        catch (Exception)
        {
            // Corrupt/expired encrypted transient data must never become an access token.
            await redis.KeyDeleteAsync(resultKey);
            return null;
        }
    }

    internal static bool NeedsRefresh(AuthenticationProperties properties, DateTimeOffset now)
    {
        var expiresAt = properties.GetTokenValue("expires_at");
        return !DateTimeOffset.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiry) ||
               expiry <= now.Add(RefreshSkew);
    }

    private async Task<TokenRefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenEndpoint = GetTokenEndpoint();
        var clientId = HCSWebGatewayModule.GetRequiredValue(configuration, "Authentication:ClientId");
        var clientSecret = HCSWebGatewayModule.GetRequiredValue(configuration, "Authentication:ClientSecret");
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret
            })
        };

        HttpResponseMessage response;
        try
        {
            response = await httpClientFactory.CreateClient("HCS.Bff.TokenRefresh")
                .SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new TransientTokenRefreshException("Token endpoint could not be reached.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TransientTokenRefreshException("Token endpoint timed out.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<TokenErrorResponse>(cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest &&
                    string.Equals(error?.Error, "invalid_grant", StringComparison.Ordinal))
                {
                    throw new InvalidRefreshTokenException();
                }

                throw new TransientTokenRefreshException($"Token endpoint returned {(int)response.StatusCode}.");
            }

            TokenRefreshResponse? payload;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<TokenRefreshResponse>(cancellationToken);
            }
            catch (JsonException exception)
            {
                throw new TransientTokenRefreshException("Token endpoint returned malformed JSON.", exception);
            }

            if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken) || payload.ExpiresIn <= 0)
            {
                throw new TransientTokenRefreshException("Token endpoint returned an invalid access token response.");
            }

        return new TokenRefreshResult(
            payload.AccessToken,
            string.IsNullOrWhiteSpace(payload.RefreshToken) ? refreshToken : payload.RefreshToken,
            payload.ExpiresIn);
        }
    }

    private string GetTokenEndpoint()
    {
        var configuredEndpoint = configuration["Authentication:TokenEndpoint"];
        if (!string.IsNullOrWhiteSpace(configuredEndpoint))
        {
            if (!Uri.TryCreate(configuredEndpoint, UriKind.Absolute, out var endpoint) ||
                (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException("Authentication:TokenEndpoint must be an absolute HTTP(S) URL.");
            }

            return endpoint.AbsoluteUri;
        }

        var authority = HCSWebGatewayModule.GetRequiredAbsoluteHttpsUrl(configuration, "Authentication:Authority");
        return $"{authority}/connect/token";
    }

    private void ApplyTokens(AuthenticationProperties properties, TokenRefreshResult refreshed)
    {
        properties.UpdateTokenValue("access_token", refreshed.AccessToken);
        properties.UpdateTokenValue("refresh_token", refreshed.RefreshToken);
        properties.UpdateTokenValue(
            "expires_at",
            timeProvider.GetUtcNow().AddSeconds(refreshed.ExpiresInSeconds).ToString("O", CultureInfo.InvariantCulture));
    }

    internal sealed record TokenRefreshResult(string AccessToken, string RefreshToken, long ExpiresInSeconds);

    private sealed record TokenRefreshResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")] long ExpiresIn);

    private sealed record TokenErrorResponse([property: JsonPropertyName("error")] string? Error);
}

internal sealed class TransientTokenRefreshException(string message, Exception? innerException = null)
    : Exception(message, innerException);

internal sealed class InvalidRefreshTokenException() : InvalidOperationException("Refresh token is no longer valid.");

internal sealed class BffRefreshResultCache : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 1024 });

    internal bool TryGet(string key, out BffTokenRefreshService.TokenRefreshResult? result) =>
        cache.TryGetValue(key, out result);

    internal void Set(string key, BffTokenRefreshService.TokenRefreshResult result, TimeSpan lifetime) =>
        cache.Set(key, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = lifetime,
            Size = 1
        });

    public void Dispose() => cache.Dispose();
}
