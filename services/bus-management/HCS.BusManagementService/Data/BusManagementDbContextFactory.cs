using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HCS.BusManagementService.Data;

public sealed class BusManagementDbContextFactory : IDesignTimeDbContextFactory<BusManagementDbContext>
{
    public BusManagementDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__BusManagement")
            ?? "Host=localhost;Port=5432;Database=hcs_bus_management;Username=postgres";
        return new BusManagementDbContext(new DbContextOptionsBuilder<BusManagementDbContext>().UseNpgsql(connection).Options);
    }
}
