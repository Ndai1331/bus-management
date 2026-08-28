using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HCS.WebGateway.Tests;

public sealed class BffSecurityTests
{
    [Theory]
    [InlineData("/api/documents")]
    [InlineData("/hubs/chat")]
    [InlineData("/bff/user")]
    public async Task Anonymous_proxy_and_websocket_handshake_are_rejected(string path)
    {
        await using var app = await CreateAppAsync(authenticated: false);
        var response = await app.GetTestClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authenticated_proxy_request_exposes_saved_token_only_to_server_transform_stage()
    {
        await using var app = await CreateAppAsync(authenticated: true);
        var response = await app.GetTestClient().GetAsync("/api/documents");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("test-access-token", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Credentialed_cors_allows_configured_ui_and_rejects_unknown_origin()
    {
        await using var app = await CreateAppAsync(authenticated: false);
        var client = app.GetTestClient();
        using var allowed = new HttpRequestMessage(HttpMethod.Options, "/api/documents");
        allowed.Headers.Add("Origin", "https://localhost:44403");
        allowed.Headers.Add("Access-Control-Request-Method", "GET");
        var allowedResponse = await client.SendAsync(allowed);
        Assert.Equal("https://localhost:44403", allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", allowedResponse.Headers.GetValues("Access-Control-Allow-Credentials").Single());

        using var denied = new HttpRequestMessage(HttpMethod.Options, "/api/documents");
        denied.Headers.Add("Origin", "https://attacker.example");
        denied.Headers.Add("Access-Control-Request-Method", "GET");
        var deniedResponse = await client.SendAsync(denied);
        Assert.False(deniedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("GET", "/hubs/chat", false)]
    [InlineData("POST", "/hubs/chat/negotiate", true)]
    [InlineData("DELETE", "/api/documents/1", true)]
    [InlineData("POST", "/api/surveys/public/sessions", false)]
    public void Antiforgery_policy_covers_unsafe_api_and_hub_requests(string method, string path, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        Assert.Equal(expected, BffRequestPolicy.RequiresAntiforgery(context.Request));
    }

    [Fact]
    public void Public_survey_path_is_explicitly_anonymous_at_the_bff_boundary()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = $"/api/surveys/public/locations/{Guid.NewGuid():D}";
        context.Request.Method = "POST";
        Assert.True(BffRequestPolicy.IsAnonymousSurveyPath(context.Request.Path));
        Assert.False(BffRequestPolicy.RequiresAntiforgery(context.Request));
    }

    [Fact]
    public void Login_return_url_rejects_open_redirects()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["App:CorsOrigins:0"] = "https://localhost:44403"
        }).Build();

        Assert.Equal("https://localhost:44403/workspace", BffEndpoints.GetSafeReturnUrl(configuration, "https://localhost:44403/workspace"));
        Assert.Equal("https://localhost:44403", BffEndpoints.GetSafeReturnUrl(configuration, "https://attacker.example/steal"));
        Assert.Equal("https://localhost:44403/login", BffEndpoints.GetSafePostLogoutUrl(configuration, null));
        Assert.Equal("https://localhost:44403/login", BffEndpoints.GetSafePostLogoutUrl(configuration, "https://localhost:44403/login"));
        Assert.Equal("https://localhost:44403/login", BffEndpoints.GetSafePostLogoutUrl(configuration, "https://attacker.example/steal"));
    }

    [Fact]
    public async Task Anonymous_logout_redirects_to_the_login_page()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["App:CorsOrigins:0"] = "https://localhost:44403"
        });
        builder.Services.AddAuthentication(HCSWebGatewayModule.CookieScheme).AddCookie();
        builder.Services.AddAuthorization();
        builder.Services.AddAntiforgery();

        await using var app = builder.Build();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapBffEndpoints();
        await app.StartAsync();

        using var client = new HttpClient(app.GetTestServer().CreateHandler())
        {
            BaseAddress = new Uri("http://localhost")
        };
        using var response = await client.GetAsync("/bff/logout");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("https://localhost:44403/login", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public void Missing_bff_secret_or_redis_configuration_fails_closed()
    {
        var configuration = new ConfigurationBuilder().Build();
        Assert.Throws<InvalidOperationException>(() =>
            HCSWebGatewayModule.GetRequiredValue(configuration, "Authentication:ClientSecret"));
        Assert.Throws<InvalidOperationException>(() =>
            HCSWebGatewayModule.GetRequiredValue(configuration, "DataProtection:Redis"));
    }

    [Theory]
    [InlineData("http://localhost:44401")]
    [InlineData("https://localhost:44401/tenant")]
    [InlineData("not-a-url")]
    public void Authority_requires_an_https_origin(string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Authority"] = value
        }).Build();

        Assert.Throws<InvalidOperationException>(() =>
            HCSWebGatewayModule.GetRequiredAbsoluteHttpsUrl(configuration, "Authentication:Authority"));
    }

    [Fact]
    public void Expiring_access_token_requires_refresh_within_skew()
    {
        var now = new DateTimeOffset(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
        var properties = new AuthenticationProperties();
        properties.StoreTokens([
            new AuthenticationToken { Name = "expires_at", Value = now.AddSeconds(30).ToString("O") }
        ]);
        Assert.True(BffTokenRefreshService.NeedsRefresh(properties, now));

        properties.UpdateTokenValue("expires_at", now.AddMinutes(5).ToString("O"));
        Assert.False(BffTokenRefreshService.NeedsRefresh(properties, now));

        properties.UpdateTokenValue("expires_at", "invalid");
        Assert.True(BffTokenRefreshService.NeedsRefresh(properties, now));
    }

    [Fact]
    public async Task Concurrent_refreshes_for_same_session_rotate_token_once()
    {
        var handler = new CountingTokenEndpointHandler();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Authority"] = "https://localhost:44401",
            ["Authentication:ClientId"] = "HCS_App",
            ["Authentication:ClientSecret"] = "test-only-secret"
        }).Build();
        using var cache = new BffRefreshResultCache();
        var service = new BffTokenRefreshService(cache, new StubHttpClientFactory(handler), configuration, TimeProvider.System);

        var results = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => service.RefreshForTokenAsync("old-refresh-token", CancellationToken.None)));

        Assert.Equal(1, handler.RequestCount);
        Assert.All(results, result =>
        {
            Assert.Equal("rotated-access-token", result.AccessToken);
            Assert.Equal("rotated-refresh-token", result.RefreshToken);
        });
    }

    [Fact]
    public async Task Transient_refresh_failure_does_not_become_a_terminal_logout_error()
    {
        using var cache = new BffRefreshResultCache();
        var service = new BffTokenRefreshService(cache,
            new StubHttpClientFactory(new StatusTokenEndpointHandler(HttpStatusCode.ServiceUnavailable)),
            RefreshConfiguration(), TimeProvider.System);

        await Assert.ThrowsAsync<TransientTokenRefreshException>(() =>
            service.RefreshForTokenAsync("old-refresh-token", CancellationToken.None));
    }

    [Fact]
    public async Task Invalid_grant_is_a_terminal_refresh_failure()
    {
        using var cache = new BffRefreshResultCache();
        var service = new BffTokenRefreshService(cache,
            new StubHttpClientFactory(new StatusTokenEndpointHandler(HttpStatusCode.BadRequest, "{\"error\":\"invalid_grant\"}")),
            RefreshConfiguration(), TimeProvider.System);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            service.RefreshForTokenAsync("old-refresh-token", CancellationToken.None));
    }

    [Theory]
    [InlineData("https://localhost:44403", null, null)]
    [InlineData("https://ui.example.test", null, "throws")]
    [InlineData("https://ui.example.test", "example.test", ".example.test")]
    [InlineData("https://localhost:44403", "localhost", "throws")]
    public void Cookie_domain_enforces_same_host_or_valid_parent(string uiOrigin, string? domain, string? expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Urls"] = uiOrigin.Contains("localhost") ? "https://localhost:44402" : "https://gateway.example.test",
            ["App:CorsOrigins:0"] = uiOrigin,
            ["Bff:CookieDomain"] = domain
        }).Build();

        if (expected == "throws")
        {
            Assert.Throws<InvalidOperationException>(() => BffDeploymentPolicy.ValidateAndGetCookieDomain(configuration));
        }
        else
        {
            Assert.Equal(expected, BffDeploymentPolicy.ValidateAndGetCookieDomain(configuration));
        }
    }

    [Fact]
    public void Production_rejects_http_browser_origins_even_when_cors_can_parse_them()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["App:CorsOrigins:0"] = "http://localhost:44403",
            ["Bff:AllowInsecureDevelopmentOrigins"] = "true"
        }).Build();

        Assert.Throws<InvalidOperationException>(() => BffDeploymentPolicy.ValidateBrowserOrigins(configuration, isDevelopment: false));
        BffDeploymentPolicy.ValidateBrowserOrigins(configuration, isDevelopment: true);
    }

    [Fact]
    public void Websocket_origin_must_match_browser_allowlist()
    {
        var allowed = new DefaultHttpContext();
        allowed.Request.Path = "/hubs/chat";
        allowed.Request.Headers.Upgrade = "websocket";
        allowed.Request.Headers.Origin = "https://localhost:44403";
        Assert.True(BffDeploymentPolicy.IsAllowedWebSocketOrigin(
            allowed.Request, ["https://localhost:44403"]));

        var denied = new DefaultHttpContext();
        denied.Request.Path = "/hubs/chat";
        denied.Request.Headers.Upgrade = "websocket";
        denied.Request.Headers.Origin = "https://attacker.example";
        Assert.False(BffDeploymentPolicy.IsAllowedWebSocketOrigin(
            denied.Request, ["https://localhost:44403"]));
    }

    [Fact]
    public void Production_requires_certificate_for_redis_key_encryption()
    {
        var configuration = new ConfigurationBuilder().Build();
        Assert.Throws<InvalidOperationException>(() =>
            HCSWebGatewayModule.LoadDataProtectionCertificate(configuration, isDevelopment: false));
        Assert.Null(HCSWebGatewayModule.LoadDataProtectionCertificate(configuration, isDevelopment: true));
    }

    private static async Task<WebApplication> CreateAppAsync(bool authenticated)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication(HCSWebGatewayModule.CookieScheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(HCSWebGatewayModule.CookieScheme, options =>
                options.ClaimsIssuer = authenticated ? "authenticated" : "anonymous");
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<BffRefreshResultCache>();
        builder.Services.AddSingleton<BffTokenRefreshService>();
        builder.Services.AddHttpClient("HCS.Bff.TokenRefresh");
        builder.Services.AddCors(options => options.AddPolicy(HCSWebGatewayModule.CorsPolicyName, policy => policy
            .WithOrigins("https://localhost:44403")
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

        var app = builder.Build();
        app.UseRouting();
        app.UseCors(HCSWebGatewayModule.CorsPolicyName);
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<BffAccessTokenMiddleware>();
        app.MapGet("/api/documents", (HttpContext context) =>
            context.Items[BffAccessTokenMiddleware.AccessTokenItemKey]?.ToString() ?? "missing").RequireAuthorization();
        app.MapGet("/hubs/chat", () => "connected").RequireAuthorization();
        app.MapGet("/bff/user", () => "user").RequireAuthorization();
        await app.StartAsync();
        return app;
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Options.ClaimsIssuer != "authenticated")
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var properties = new AuthenticationProperties();
            properties.StoreTokens([
                new AuthenticationToken { Name = "access_token", Value = "test-access-token" },
                new AuthenticationToken { Name = "refresh_token", Value = "test-refresh-token" },
                new AuthenticationToken { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddMinutes(5).ToString("O") }
            ]);
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), properties, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static IConfiguration RefreshConfiguration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Authentication:Authority"] = "https://localhost:44401",
        ["Authentication:ClientId"] = "HCS_App",
        ["Authentication:ClientSecret"] = "test-only-secret"
    }).Build();

    private sealed class CountingTokenEndpointHandler : HttpMessageHandler
    {
        private int requestCount;
        internal int RequestCount => requestCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref requestCount);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://localhost:44401/connect/token", request.RequestUri?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"access_token\":\"rotated-access-token\",\"refresh_token\":\"rotated-refresh-token\",\"expires_in\":300}",
                    Encoding.UTF8,
                    "application/json")
            });
        }
    }

    private sealed class StatusTokenEndpointHandler(HttpStatusCode statusCode, string? body = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body ?? "{}", Encoding.UTF8, "application/json")
            });
    }
}
