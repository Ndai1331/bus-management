using HCS.Localization;
using Volo.Abp.AspNetCore.Components;

namespace HCS.Blazor;

public abstract class HCSComponentBase : AbpComponentBase
{
    protected HCSComponentBase()
    {
        LocalizationResource = typeof(HCSResource);
    }
}
