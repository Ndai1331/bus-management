using Volo.Abp.Modularity;

namespace HCS;

public abstract class HCSApplicationTestBase<TStartupModule> : HCSTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
