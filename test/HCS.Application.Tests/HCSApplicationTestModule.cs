using Volo.Abp.Modularity;

namespace HCS;

[DependsOn(
    typeof(HCSApplicationModule),
    typeof(HCSDomainTestModule)
)]
public class HCSApplicationTestModule : AbpModule
{

}
