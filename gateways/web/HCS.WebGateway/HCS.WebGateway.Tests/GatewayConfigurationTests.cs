using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;
using Xunit;

namespace HCS.WebGateway.Tests;

public sealed class GatewayConfigurationTests
{
    private static readonly IReadOnlyDictionary<string, string> ExpectedRoutes =
        new Dictionary<string, string>
        {
            ["/api/abp/{**catch-all}"] = "Platform",
            ["/api/account/{**catch-all}"] = "Platform",
            ["/api/identity/{**catch-all}"] = "Platform",
            ["/api/permission-management/{**catch-all}"] = "Platform",
            ["/api/setting-management/{**catch-all}"] = "Platform",
            ["/api/language-management/{**catch-all}"] = "Platform",
            ["/api/audit-logs/{**catch-all}"] = "Platform",
            ["/api/admin/{**catch-all}"] = "Platform",
            ["/api/chat/contacts/{**catch-all}"] = "Platform",
            ["/api/organization/{**catch-all}"] = "Organization",
            ["/api/departments/{**catch-all}"] = "Organization",
            ["/api/units/{**catch-all}"] = "Organization",
            ["/api/positions/{**catch-all}"] = "Organization",
            ["/api/master-data/{**catch-all}"] = "Organization",
            ["/api/documents/{**catch-all}"] = "Document",
            ["/api/workflows/{**catch-all}"] = "Document",
            ["/api/signing/{**catch-all}"] = "Document",
            ["/api/projects/{**catch-all}"] = "WorkManagement",
            ["/api/project-tasks/{**catch-all}"] = "WorkManagement",
            ["/api/calendar/{**catch-all}"] = "WorkManagement",
            ["/api/surveys/{**catch-all}"] = "WorkManagement",
            ["/api/reports/{**catch-all}"] = "WorkManagement",
            ["/api/dashboard/{**catch-all}"] = "WorkManagement",
            ["/api/chat/{**catch-all}"] = "Collaboration",
            ["/api/notifications/{**catch-all}"] = "Collaboration",
            ["/hubs/chat/{**catch-all}"] = "Collaboration",
            ["/api/bus-management/{**catch-all}"] = "BusManagement"
        };

    [Fact]
    public void Reverse_proxy_configuration_matches_service_boundaries()
    {
        var configuration = LoadConfiguration();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddReverseProxy().LoadFromConfig(configuration.GetSection("ReverseProxy"));

        using var provider = services.BuildServiceProvider();
        var proxyConfig = provider.GetRequiredService<IProxyConfigProvider>().GetConfig();
        var actualRoutes = proxyConfig.Routes.ToDictionary(route => route.Match.Path!, route => route.ClusterId!);

        Assert.Equal(ExpectedRoutes.Count, actualRoutes.Count);
        Assert.Equal(ExpectedRoutes, actualRoutes);
        Assert.DoesNotContain(proxyConfig.Routes.SelectMany(route => route.Transforms ?? []), transform =>
            transform.ContainsKey("RequestHeaderRemove") || transform.ContainsKey("RequestHeadersCopy"));
    }

    [Fact]
    public void Cluster_destinations_use_the_approved_local_ports()
    {
        var configuration = LoadConfiguration();
        var expected = new Dictionary<string, string>
        {
            ["Platform"] = "https://localhost:44411/",
            ["Organization"] = "https://localhost:44412/",
            ["Document"] = "https://localhost:44413/",
            ["WorkManagement"] = "https://localhost:44414/",
            ["Collaboration"] = "https://localhost:44415/",
            ["BusManagement"] = "https://localhost:44416/"
        };

        foreach (var (cluster, address) in expected)
        {
            Assert.Equal(address, configuration[$"ReverseProxy:Clusters:{cluster}:Destinations:primary:Address"]);
        }

        Assert.Equal("https://localhost:44402", configuration["Urls"]);
        Assert.NotEmpty(configuration.GetSection("App:CorsOrigins").Get<string[]>() ?? []);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("*")]
    [InlineData("ftp://example.test")]
    [InlineData("https://example.test/path")]
    [InlineData("https://example.test?query=1")]
    public void Cors_origins_reject_unsafe_values(string origin)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:CorsOrigins:0"] = origin
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => HCSWebGatewayModule.GetCorsOrigins(configuration));
    }

    [Fact]
    public void Cors_origins_are_normalized_and_deduplicated()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:CorsOrigins:0"] = " https://example.test/ ",
                ["App:CorsOrigins:1"] = "https://EXAMPLE.test"
            })
            .Build();

        Assert.Equal(["https://example.test"], HCSWebGatewayModule.GetCorsOrigins(configuration));
    }

    private static IConfigurationRoot LoadConfiguration() => new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false)
        .Build();
}
