using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.Mvc.UI.Alerts;
using Volo.Abp.Identity;

namespace HCS.AuthServer.Pages.Account;

public class LoginModel : Volo.Abp.Account.Web.Pages.Account.LoginModel
{
    private readonly IConfiguration _configuration;

    public IReadOnlyList<AlertMessage> VisibleAlerts => Alerts;

    public LoginModel(
        IAuthenticationSchemeProvider schemeProvider,
        IOptions<AbpAccountOptions> accountOptions,
        IOptions<IdentityOptions> identityOptions,
        IdentityDynamicClaimsPrincipalContributorCache identityDynamicClaimsPrincipalContributorCache,
        IWebHostEnvironment webHostEnvironment,
        IConfiguration configuration)
        : base(
            schemeProvider,
            accountOptions,
            identityOptions,
            identityDynamicClaimsPrincipalContributorCache,
            webHostEnvironment)
    {
        _configuration = configuration;
    }

    public override Task<IActionResult> OnGetAsync()
    {
        ApplyDefaultReturnUrl();
        return base.OnGetAsync();
    }

    public override Task<IActionResult> OnPostAsync(string action)
    {
        ApplyDefaultReturnUrl();
        return base.OnPostAsync(action);
    }

    private void ApplyDefaultReturnUrl()
    {
        if (!string.IsNullOrWhiteSpace(ReturnUrl) &&
            !string.Equals(ReturnUrl, "/", StringComparison.Ordinal))
        {
            return;
        }

        ReturnUrl = GetClientAppUrl();
    }

    private string GetClientAppUrl()
    {
        var configured = _configuration["App:ClientUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.TrimEnd('/') + "/workspace";
        }

        return "https://hcs.localhost/workspace";
    }
}
