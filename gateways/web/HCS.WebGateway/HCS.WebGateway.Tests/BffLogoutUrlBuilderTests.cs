using HCS.Blazor.Client.Navigation;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HCS.WebGateway.Tests;

public sealed class BffLogoutUrlBuilderTests
{
    [Fact]
    public void Builds_bff_logout_url_that_returns_to_the_login_page()
    {
        var configuration = CreateConfiguration("https://localhost:44402/");

        var actual = BffLogoutUrlBuilder.Build(configuration, "https://localhost:44403/login");

        Assert.Equal(
            "https://localhost:44402/bff/logout?returnUrl=https%3A%2F%2Flocalhost%3A44403%2Flogin",
            actual);
    }

    [Theory]
    [InlineData("http://localhost:44402")]
    [InlineData("https://localhost:44402/bff")]
    [InlineData("not-a-url")]
    public void Rejects_an_invalid_bff_origin(string origin)
    {
        Assert.Throws<InvalidOperationException>(() =>
            BffLogoutUrlBuilder.Build(CreateConfiguration(origin), "https://localhost:44403/login"));
    }

    [Fact]
    public void Rejects_a_relative_return_url()
    {
        Assert.Throws<InvalidOperationException>(() =>
            BffLogoutUrlBuilder.Build(CreateConfiguration("https://localhost:44402"), "/login"));
    }

    private static IConfiguration CreateConfiguration(string origin) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Bff:PublicOrigin"] = origin })
        .Build();
}
