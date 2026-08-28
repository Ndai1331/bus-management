using Microsoft.Extensions.Localization;
using HCS.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace HCS.Blazor;

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
