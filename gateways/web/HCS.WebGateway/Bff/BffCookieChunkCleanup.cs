using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace HCS.WebGateway;

/// <summary>
/// Expires leftover ASP.NET chunk cookies (<c>.HCS.BffC1</c>, <c>C2</c>, …). After the
/// session moves to Redis, those chunks must not be concatenated with the new
/// session-id cookie or authentication fails and the Cookie header stays oversized.
/// </summary>
internal sealed class BffCookieChunkCleanupMiddleware(
    RequestDelegate next,
    IOptionsMonitor<CookieAuthenticationOptions> cookieOptions)
{
    public async Task InvokeAsync(HttpContext context)
    {
        BffCookieChunkCleanup.ExpireLegacyChunks(
            context,
            cookieOptions.Get(HCSWebGatewayModule.CookieScheme).Cookie.Build(context));
        await next(context);
    }
}

internal static class BffCookieChunkCleanup
{
    internal static void ExpireLegacyChunks(HttpContext context, CookieOptions options)
    {
        foreach (var name in context.Request.Cookies.Keys)
        {
            if (!IsChunkCookie(name))
            {
                continue;
            }

            context.Response.Cookies.Delete(name, options);
        }
    }

    internal static bool IsChunkCookie(string name)
    {
        var prefix = HCSWebGatewayModule.CookieName + "C";
        return name.StartsWith(prefix, StringComparison.Ordinal) &&
               name.Length > prefix.Length &&
               char.IsAsciiDigit(name[prefix.Length]);
    }
}
