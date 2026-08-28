using Volo.Abp.AspNetCore.Mvc.AntiForgery;

namespace HCS.CollaborationService;

/// <summary>
/// Browser writes (REST and SignalR negotiate) are validated at the BFF.
/// Collaboration only sees bearer tokens and does not share the BFF cookie pair.
/// </summary>
public static class BearerApiAntiforgery
{
    public static void DisableCookieValidation(AbpAntiForgeryOptions options) =>
        options.AutoValidate = false;
}
