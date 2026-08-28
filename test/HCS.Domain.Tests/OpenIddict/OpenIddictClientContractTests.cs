using HCS.OpenIddict;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using Xunit;

namespace HCS.OpenIddict;

public sealed class OpenIddictClientContractTests
{
    [Fact]
    public void Hcs_app_uses_gateway_scope_and_blazor_oidc_callbacks()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Applications:HCS_App:ClientId"] = "HCS_App",
                ["Applications:HCS_App:ClientSecret"] = "test-only-secret"
            }).Build();

        var registration = OpenIddictDataSeedContributor.GetHcsAppRegistration(
            configuration.GetSection("Applications"))!;

        Assert.Equal("HCS", OpenIddictDataSeedContributor.GatewayScope);
        Assert.Equal("HCS", OpenIddictDataSeedContributor.GatewayAudience);
        Assert.Equal("https://localhost:44402", registration.GatewayRootUrl);
        Assert.Equal("https://localhost:44403", registration.BlazorRootUrl);
        Assert.Equal("https://localhost:44402/signin-oidc", registration.CallbackUrl);
        Assert.Equal("https://localhost:44402/signout-callback-oidc", registration.LogoutCallbackUrl);
    }

    [Theory]
    [InlineData("http://localhost:44403")]
    [InlineData("https://localhost:44403/path")]
    public void Hcs_app_rejects_unsafe_root_urls(string rootUrl)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Applications:HCS_App:ClientId"] = "HCS_App",
                ["Applications:HCS_App:ClientSecret"] = "test-only-secret",
                ["Applications:HCS_App:RootUrl"] = rootUrl
            }).Build();

        Assert.Throws<InvalidOperationException>(() =>
            OpenIddictDataSeedContributor.GetHcsAppRegistration(configuration.GetSection("Applications")));
    }
}
