using System.Threading.Tasks;

namespace HCS.Data;

public interface IHCSDbSchemaMigrator
{
    Task MigrateAsync();
}
