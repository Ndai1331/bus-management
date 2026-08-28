using HCS.OrganizationService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HCS.OrganizationService.Host.Data;

public sealed class OrganizationDbContextFactory : IDesignTimeDbContextFactory<OrganizationDbContext>
{
    public OrganizationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Organization");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Set ConnectionStrings__Organization before creating migrations.");
        var builder = new DbContextOptionsBuilder<OrganizationDbContext>().UseNpgsql(connectionString);
        return new OrganizationDbContext(builder.Options);
    }
}
