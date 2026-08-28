using System.Net;

namespace HCS.WebGateway;

internal static class BffDeploymentPolicy
{
    internal static void ValidateBrowserOrigins(IConfiguration configuration, bool isDevelopment)
    {
        var origins = HCSWebGatewayModule.GetCorsOrigins(configuration).Select(origin => new Uri(origin));
        foreach (var origin in origins)
        {
            if (origin.Scheme == Uri.UriSchemeHttps)
            {
                continue;
            }

            if (!isDevelopment || !origin.IsLoopback || !configuration.GetValue("Bff:AllowInsecureDevelopmentOrigins", false))
            {
                throw new InvalidOperationException(
                    "Browser BFF origins must use HTTPS. HTTP is permitted only for an explicit loopback development opt-in.");
            }
        }
    }

    internal static string? ValidateAndGetCookieDomain(IConfiguration configuration)
    {
        var gatewayOrigin = GetGatewayOrigin(configuration);
        var uiOrigins = HCSWebGatewayModule.GetCorsOrigins(configuration).Select(value => new Uri(value)).ToArray();
        var configuredDomain = configuration["Bff:CookieDomain"]?.Trim().TrimStart('.');

        if (string.IsNullOrWhiteSpace(configuredDomain))
        {
            if (uiOrigins.Any(origin => !origin.Host.Equals(gatewayOrigin.Host, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    "BFF and browser UI must use the same host unless Bff:CookieDomain explicitly covers both hosts.");
            }

            return null;
        }

        if (gatewayOrigin.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            IPAddress.TryParse(gatewayOrigin.Host, out _) || !configuredDomain.Contains('.'))
        {
            throw new InvalidOperationException("Bff:CookieDomain cannot be used for localhost, IP addresses, or a top-level hostname.");
        }

        if (!HostMatchesDomain(gatewayOrigin.Host, configuredDomain) ||
            uiOrigins.Any(origin => !HostMatchesDomain(origin.Host, configuredDomain)))
        {
            throw new InvalidOperationException("Bff:CookieDomain must be a parent domain of both Gateway and every configured UI origin.");
        }

        return $".{configuredDomain}";
    }

    internal static Uri GetGatewayOrigin(IConfiguration configuration)
    {
        var configuredPublicOrigin = configuration["Bff:PublicOrigin"];
        var value = string.IsNullOrWhiteSpace(configuredPublicOrigin)
            ? HCSWebGatewayModule.GetRequiredValue(configuration, "Urls").Split(';', StringSplitOptions.RemoveEmptyEntries)[0]
            : configuredPublicOrigin;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.AbsolutePath != "/")
        {
            throw new InvalidOperationException("Bff:PublicOrigin or Urls must begin with an absolute HTTPS Gateway origin.");
        }

        return new Uri(uri.GetLeftPart(UriPartial.Authority));
    }

    internal static bool IsAllowedWebSocketOrigin(HttpRequest request, IReadOnlyCollection<string> allowedOrigins)
    {
        if (!request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase) ||
            !request.Headers.TryGetValue("Upgrade", out var upgrade) ||
            !upgrade.ToString().Equals("websocket", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var origin = request.Headers.Origin.ToString().TrimEnd('/');
        return !string.IsNullOrWhiteSpace(origin) && allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HostMatchesDomain(string host, string domain) =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase);
}
