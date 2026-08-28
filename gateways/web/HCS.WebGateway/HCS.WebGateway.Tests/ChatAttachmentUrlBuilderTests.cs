using HCS.Blazor.Client.Collaboration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HCS.WebGateway.Tests;

public sealed class ChatAttachmentUrlBuilderTests
{
    [Fact]
    public void Builds_attachment_url_on_the_gateway_origin()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bff:PublicOrigin"] = "https://api-hcs.htltech.vn"
            })
            .Build();
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var result = ChatAttachmentUrlBuilder.Build(configuration, id);

        Assert.Equal("https://api-hcs.htltech.vn/api/chat/attachments/11111111-2222-3333-4444-555555555555", result);
    }

    [Fact]
    public void Rejects_a_non_gateway_origin()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bff:PublicOrigin"] = "/api"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(() => ChatAttachmentUrlBuilder.Build(configuration, Guid.NewGuid()));
    }
}
