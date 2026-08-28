using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace HCS.Blazor.Client.Authentication;

public sealed class BffHttpMessageHandler(Uri gatewayBaseAddress) : DelegatingHandler
{
    private const long MaximumReplayBodyBytes = 256 * 1024;
    private readonly SemaphoreSlim tokenLock = new(1, 1);
    private string? antiforgeryToken;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        EnsureGatewayRequest(request);
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        var requiresAntiforgery = RequiresAntiforgery(request.Method) && !IsAnonymousSurveyRequest(request.RequestUri);
        // A request body may be a streaming upload. Never buffer it merely to retry an
        // antiforgery failure; only small, declared-size payloads are safely replayable.
        var retryRequest = requiresAntiforgery ? await TryCloneReplayableAsync(request, cancellationToken) : null;
        if (requiresAntiforgery)
        {
            var token = await GetAntiforgeryTokenAsync(cancellationToken);
            request.Headers.TryAddWithoutValidation("X-XSRF-TOKEN", token);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (retryRequest is not null && requiresAntiforgery && response.StatusCode == System.Net.HttpStatusCode.BadRequest &&
            response.Headers.TryGetValues("X-HCS-Antiforgery", out var values) && values.Contains("invalid"))
        {
            response.Dispose();
            await InvalidateAntiforgeryTokenAsync(cancellationToken);
            var token = await GetAntiforgeryTokenAsync(cancellationToken);
            retryRequest.Headers.TryAddWithoutValidation("X-XSRF-TOKEN", token);
            retryRequest.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            response = await base.SendAsync(retryRequest, cancellationToken);
        }

        retryRequest?.Dispose();

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
            request.RequestUri!.AbsolutePath.Equals("/bff/logout", StringComparison.OrdinalIgnoreCase))
        {
            await InvalidateAntiforgeryTokenAsync(cancellationToken);
        }

        return response;
    }

    private void EnsureGatewayRequest(HttpRequestMessage request)
    {
        if (request.RequestUri is null)
        {
            throw new InvalidOperationException("BFF request URI is required.");
        }

        request.RequestUri = request.RequestUri is { IsAbsoluteUri: true }
            ? request.RequestUri
            : new Uri(gatewayBaseAddress, request.RequestUri);
        if (!request.RequestUri.GetLeftPart(UriPartial.Authority)
                .Equals(gatewayBaseAddress.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The credentialed BFF handler can only send requests to the configured Gateway origin.");
        }
    }

    private async Task InvalidateAntiforgeryTokenAsync(CancellationToken cancellationToken)
    {
        await tokenLock.WaitAsync(cancellationToken);
        try
        {
            antiforgeryToken = null;
        }
        finally
        {
            tokenLock.Release();
        }
    }

    private async Task<string> GetAntiforgeryTokenAsync(CancellationToken cancellationToken)
    {
        if (antiforgeryToken is not null)
        {
            return antiforgeryToken;
        }

        await tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (antiforgeryToken is not null)
            {
                return antiforgeryToken;
            }

            using var tokenRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(gatewayBaseAddress, "bff/antiforgery"));
            tokenRequest.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            using var response = await base.SendAsync(tokenRequest, cancellationToken);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<AntiforgeryResponse>(cancellationToken);
            antiforgeryToken = payload?.Token ?? throw new InvalidOperationException("BFF did not return an antiforgery token.");
            return antiforgeryToken;
        }
        finally
        {
            tokenLock.Release();
        }
    }

    private static bool RequiresAntiforgery(HttpMethod method) =>
        method != HttpMethod.Get && method != HttpMethod.Head && method != HttpMethod.Options;

    private static bool IsAnonymousSurveyRequest(Uri? uri) =>
        uri?.AbsolutePath.StartsWith("/api/surveys/public", StringComparison.OrdinalIgnoreCase) == true;

    internal static async Task<HttpRequestMessage?> TryCloneReplayableAsync(
        HttpRequestMessage source,
        CancellationToken cancellationToken)
    {
        // Multipart content may contain a one-shot browser file stream. Do not read
        // or serialize it just to prepare an antiforgery retry; the initial request
        // already carries the freshly fetched token and can be sent as-is.
        if (source.Content is MultipartFormDataContent)
        {
            return null;
        }

        if (source.Content?.Headers.ContentLength is long contentLength && contentLength > MaximumReplayBodyBytes)
        {
            return null;
        }

        if (source.Content is not null && source.Content.Headers.ContentLength is null)
        {
            return null;
        }

        var clone = new HttpRequestMessage(source.Method, source.RequestUri)
        {
            Version = source.Version,
            VersionPolicy = source.VersionPolicy
        };
        foreach (var header in source.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (source.Content is not null)
        {
            var bytes = await source.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in source.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }

    private sealed record AntiforgeryResponse(string Token, string HeaderName);
}
