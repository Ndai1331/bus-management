using HCS.Localization;
using Volo.Abp.Application.Services;

namespace HCS;

/* Inherit your application services from this class.
 */
public abstract class HCSAppService : ApplicationService
{
    protected HCSAppService()
    {
        LocalizationResource = typeof(HCSResource);
    }
}
