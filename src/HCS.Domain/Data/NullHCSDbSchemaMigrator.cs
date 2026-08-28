using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace HCS.Data;

/* This is used if database provider does't define
 * IHCSDbSchemaMigrator implementation.
 */
public class NullHCSDbSchemaMigrator : IHCSDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
