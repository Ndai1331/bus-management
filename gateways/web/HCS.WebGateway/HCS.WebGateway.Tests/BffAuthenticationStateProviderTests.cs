using System.Net;
using System.Net.Http;
using HCS.Blazor.Client.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Xunit;

namespace HCS.WebGateway.Tests;

public sealed class BffAuthenticationStateProviderTests
{
    [Fact]
    public async Task RefreshAsync_maps_the_sanitized_bff_profile_to_an_authenticated_principal()
    {
        var provider = CreateProvider(HttpStatusCode.OK, """
            {
              "isAuthenticated": true,
              "name": "admin",
              "claims": [
                { "type": "sub", "value": "admin-id" },
                { "type": "role", "value": "admin" }
              ]
            }
            """);

        var state = await provider.RefreshAsync();

        Assert.True(state.User.Identity?.IsAuthenticated);
        Assert.Equal("admin", state.User.Identity?.Name);
        Assert.True(state.User.IsInRole("admin"));
        Assert.Contains(state.User.Claims, claim => claim.Type == "sub" && claim.Value == "admin-id");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "")]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    [InlineData(HttpStatusCode.OK, "not-json")]
    public async Task RefreshAsync_returns_anonymous_state_when_the_bff_profile_cannot_be_used(
        HttpStatusCode statusCode,
        string responseBody)
    {
        var provider = CreateProvider(statusCode, responseBody);

        var state = await provider.RefreshAsync();

        Assert.False(state.User.Identity?.IsAuthenticated ?? false);
    }

    [Fact]
    public async Task RefreshAsync_returns_anonymous_state_when_the_bff_request_is_cancelled()
    {
        var provider = new BffAuthenticationStateProvider(new StubHttpClientFactory(new HttpClient(new CancelledHandler())
        {
            BaseAddress = new Uri("https://hcs.localhost/")
        }));

        var state = await provider.RefreshAsync();

        Assert.False(state.User.Identity?.IsAuthenticated ?? false);
    }

    private static BffAuthenticationStateProvider CreateProvider(HttpStatusCode statusCode, string responseBody) =>
        new(new StubHttpClientFactory(new HttpClient(new StubHandler(statusCode, responseBody))
        {
            BaseAddress = new Uri("https://hcs.localhost/")
        }));

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody)
            });
    }

    private sealed class CancelledHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromCanceled<HttpResponseMessage>(new CancellationToken(canceled: true));
    }
}
