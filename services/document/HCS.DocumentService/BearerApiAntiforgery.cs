using Volo.Abp.AspNetCore.Mvc.AntiForgery;

namespace HCS.DocumentService;

/// <summary>
/// Browser writes are validated at the BFF. Document only sees bearer tokens
/// and does not share the BFF antiforgery cookie pair.
/// </summary>
public static class BearerApiAntiforgery
{
    public static void DisableCookieValidation(AbpAntiForgeryOptions options) =>
        options.AutoValidate = false;
}
