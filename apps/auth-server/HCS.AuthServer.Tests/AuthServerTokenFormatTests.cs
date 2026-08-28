using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Server;
using Xunit;

namespace HCS.AuthServer.Tests;

public sealed class AuthServerTokenFormatTests
{
    [Fact]
    public void Access_Tokens_Are_Signed_Jwts_Readable_By_Remote_Service_Hosts()
    {
        var services = new ServiceCollection();
        var serverBuilder = services.AddOpenIddict().AddServer();
        serverBuilder.AllowClientCredentialsFlow();
        serverBuilder.SetTokenEndpointUris("/connect/token");
        serverBuilder.AddDevelopmentEncryptionCertificate();
        serverBuilder.AddDevelopmentSigningCertificate();

        HCSAuthServerModule.ConfigureAccessTokenFormat(serverBuilder);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OpenIddictServerOptions>>().Value;
        Assert.True(options.DisableAccessTokenEncryption);
    }
}
