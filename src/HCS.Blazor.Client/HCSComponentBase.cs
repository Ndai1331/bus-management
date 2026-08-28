using System;
using System.Threading.Tasks;
using System.Net;
using HCS.Blazor.Client.Services;
using HCS.Blazor.Client.Navigation;
using HCS.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Volo.Abp.AspNetCore.Components;
using Volo.Abp.AspNetCore.Components.Messages;

namespace HCS.Blazor.Client;

public abstract class HCSComponentBase : AbpComponentBase
{
    protected HCSComponentBase()
    {
        LocalizationResource = typeof(HCSResource);
    }

    [Inject] protected IUiMessageService UiMessageService { get; set; } = default!;
    [Inject] protected IConfiguration Configuration { get; set; } = default!;
    [Inject] protected NavigationManager LoginNavigation { get; set; } = default!;

    protected string MapBffError(Exception exception, BffErrorKind kind = BffErrorKind.Load) =>
        BffErrorMapper.From(L, exception, kind);

    protected Task NotifySuccessAsync(string message) => UiMessageService.Success(message);

    protected Task NotifySuccessAsync(LocalizedString message) => UiMessageService.Success(message);

    protected Task NotifyErrorAsync(Exception exception, BffErrorKind kind = BffErrorKind.Load) =>
        ShowErrorAsync(MapBffError(exception, kind), BffErrorMapper.GetStatusCode(exception));

    protected Task NotifyErrorAsync(string message) => ShowErrorAsync(message);

    protected Task NotifyErrorAsync(LocalizedString message) => ShowErrorAsync(message.Value);

    protected async Task ShowErrorAsync(string message, HttpStatusCode? statusCode = null)
    {
        if (statusCode != HttpStatusCode.Unauthorized)
        {
            await UiMessageService.Error(message);
            return;
        }

        var loginRequested = await UiMessageService.Confirm(message, options: options =>
        {
            options.CancelButtonText = L["Catalog:Close"].Value;
            options.ConfirmButtonText = L["Auth:LoginAgain"].Value;
        });

        if (loginRequested)
        {
            LoginNavigation.NavigateTo(
                BffLoginUrlBuilder.Build(Configuration, LoginNavigation.Uri),
                forceLoad: true);
        }
    }
}
