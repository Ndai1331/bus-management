using HCS.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace HCS.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(HCSEntityFrameworkCoreModule),
    typeof(HCSApplicationContractsModule)
)]
public class HCSDbMigratorModule : AbpModule
{
}
