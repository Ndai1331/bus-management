using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HCS.CollaborationService.Data;

public sealed class CollaborationDbContextFactory : IDesignTimeDbContextFactory<CollaborationDbContext>
{
    public CollaborationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("HCS_COLLABORATION_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=hcs_collaboration;Username=postgres";
        return new CollaborationDbContext(new DbContextOptionsBuilder<CollaborationDbContext>()
            .UseNpgsql(connectionString).Options);
    }
}
