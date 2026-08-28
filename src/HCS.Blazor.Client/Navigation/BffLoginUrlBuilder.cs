using System;
using Microsoft.Extensions.Configuration;

namespace HCS.Blazor.Client.Navigation;

internal static class BffLoginUrlBuilder
{
    public static string Build(IConfiguration configuration, string returnUrl)
    {
        var configuredOrigin = configuration["Bff:PublicOrigin"];
        if (!Uri.TryCreate(configuredOrigin, UriKind.Absolute, out var origin) ||
            origin.Scheme != Uri.UriSchemeHttps || origin.AbsolutePath != "/")
        {
            throw new InvalidOperationException("Bff:PublicOrigin must be an absolute HTTPS origin without a path.");
        }

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var destination) ||
            !destination.IsAbsoluteUri || destination.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("The BFF login return URL must be an absolute HTTPS URL.");
        }

        return new Uri(origin, $"bff/login?returnUrl={Uri.EscapeDataString(returnUrl)}").AbsoluteUri;
    }
}
