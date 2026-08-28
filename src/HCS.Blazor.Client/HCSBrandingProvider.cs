using Volo.Abp.Ui.Branding;
using Volo.Abp.DependencyInjection;
using Microsoft.Extensions.Localization;
using HCS.Localization;

namespace HCS.Blazor.Client;

[Dependency(ReplaceServices = true)]
public class HCSBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<HCSResource> _localizer;

    public HCSBrandingProvider(IStringLocalizer<HCSResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}
