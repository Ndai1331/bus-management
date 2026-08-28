using Volo.Abp.AspNetCore.Mvc.AntiForgery;

namespace HCS.BusManagementService;

public static class BearerApiAntiforgery
{
    public static void DisableCookieValidation(AbpAntiForgeryOptions options) => options.AutoValidate = false;
}
