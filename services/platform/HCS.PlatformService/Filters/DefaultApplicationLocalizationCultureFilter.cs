using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HCS.PlatformService.Filters;

/// <summary>
/// Supplies the fallback culture for ABP's initial localization bootstrap
/// request when no browser culture has been selected yet.
/// </summary>
public sealed class DefaultApplicationLocalizationCultureFilter : IAsyncResourceFilter, IOrderedFilter
{
    public int Order => int.MinValue;

    public Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (request.Path.Equals("/api/abp/application-localization") &&
            string.IsNullOrWhiteSpace(request.Query["CultureName"]))
        {
            var query = request.Query.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
            var cookieCulture = ReadCultureCookie(request);
            query["CultureName"] = string.IsNullOrWhiteSpace(cookieCulture) ? "en" : cookieCulture;
            request.Query = new QueryCollection(query);
        }

        return next();
    }

    private static string? ReadCultureCookie(HttpRequest request)
    {
        if (request.Cookies.TryGetValue("hcs.culture", out var hcs) && IsSupported(hcs))
        {
            return hcs;
        }

        if (request.Cookies.TryGetValue("Abp.Localization.CultureName", out var abp) && IsSupported(abp))
        {
            return abp;
        }

        if (request.Cookies.TryGetValue(".AspNetCore.Culture", out var aspNet))
        {
            var decoded = Uri.UnescapeDataString(aspNet);
            var marker = "uic=";
            var start = decoded.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start >= 0)
            {
                var value = decoded[(start + marker.Length)..].Split('|')[0].Trim();
                if (value.Length >= 2)
                {
                    value = value[..2].ToLowerInvariant();
                    if (IsSupported(value))
                    {
                        return value;
                    }
                }
            }
        }

        return null;
    }

    private static bool IsSupported(string? culture) =>
        string.Equals(culture, "vi", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(culture, "en", StringComparison.OrdinalIgnoreCase);
}
