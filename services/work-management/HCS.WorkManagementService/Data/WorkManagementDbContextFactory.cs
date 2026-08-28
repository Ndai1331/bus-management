using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HCS.WorkManagementService.Data;

public sealed class WorkManagementDbContextFactory : IDesignTimeDbContextFactory<WorkManagementDbContext>
{
    public WorkManagementDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__WorkManagement")
            ?? "Host=localhost;Port=5432;Database=hcs_work;Username=postgres";
        return new WorkManagementDbContext(new DbContextOptionsBuilder<WorkManagementDbContext>()
            .UseNpgsql(connection).Options);
    }
}
