using Volo.Abp.Modularity;

namespace HCS;

[DependsOn(
    typeof(HCSDomainModule),
    typeof(HCSTestBaseModule)
)]
public class HCSDomainTestModule : AbpModule
{

}
