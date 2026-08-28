using System;
using Microsoft.Extensions.Configuration;

namespace HCS.Blazor.Client.Collaboration;

internal static class ChatAttachmentUrlBuilder
{
    public static string Build(IConfiguration configuration, Guid attachmentId)
    {
        var configuredOrigin = configuration["Bff:PublicOrigin"];
        if (!Uri.TryCreate(configuredOrigin, UriKind.Absolute, out var origin) ||
            origin.Scheme != Uri.UriSchemeHttps || origin.AbsolutePath != "/")
        {
            throw new InvalidOperationException("Bff:PublicOrigin must be an absolute HTTPS origin without a path.");
        }

        return new Uri(origin, $"api/chat/attachments/{attachmentId:D}").AbsoluteUri;
    }
}
