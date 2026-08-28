using HCS.Blazor.Client.Navigation;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HCS.WebGateway.Tests;

public sealed class BffLoginUrlBuilderTests
{
    [Fact]
    public void Builds_bff_login_url_that_preserves_the_current_deep_link()
    {
        var configuration = CreateConfiguration("https://localhost:44402/");

        var actual = BffLoginUrlBuilder.Build(
            configuration,
            "https://localhost:44403/manage-documents?sourceType=2");

        Assert.Equal(
            "https://localhost:44402/bff/login?returnUrl=https%3A%2F%2Flocalhost%3A44403%2Fmanage-documents%3FsourceType%3D2",
            actual);
    }

    [Theory]
    [InlineData("http://localhost:44402")]
    [InlineData("https://localhost:44402/bff")]
    [InlineData("not-a-url")]
    public void Rejects_an_invalid_bff_origin(string origin)
    {
        Assert.Throws<InvalidOperationException>(() =>
            BffLoginUrlBuilder.Build(CreateConfiguration(origin), "https://localhost:44403/chat"));
    }

    [Fact]
    public void Rejects_a_relative_return_url()
    {
        Assert.Throws<InvalidOperationException>(() =>
            BffLoginUrlBuilder.Build(CreateConfiguration("https://localhost:44402"), "/chat"));
    }

    private static IConfiguration CreateConfiguration(string origin) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Bff:PublicOrigin"] = origin })
        .Build();
}
