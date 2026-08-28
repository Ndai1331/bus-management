using System;
using Microsoft.AspNetCore.Http;

namespace HCS.Blazor;

/// <summary>
/// Expires leftover ASP.NET chunk cookies (<c>.HCS.BffC1</c>, …) on the UI host so
/// the first HTML response can shrink the Cookie header before WASM initializes.
/// </summary>
internal static class BffCookieChunkCleanup
{
    private const string SessionCookieName = ".HCS.Bff";

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
        var prefix = SessionCookieName + "C";
        return name.StartsWith(prefix, StringComparison.Ordinal) &&
               name.Length > prefix.Length &&
               char.IsAsciiDigit(name[prefix.Length]);
    }
}
