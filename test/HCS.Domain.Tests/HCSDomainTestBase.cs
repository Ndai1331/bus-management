using Volo.Abp.Modularity;

namespace HCS;

/* Inherit from this class for your domain layer tests. */
public abstract class HCSDomainTestBase<TStartupModule> : HCSTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
