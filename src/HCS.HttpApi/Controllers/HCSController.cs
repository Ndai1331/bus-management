using HCS.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace HCS.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class HCSController : AbpControllerBase
{
    protected HCSController()
    {
        LocalizationResource = typeof(HCSResource);
    }
}
