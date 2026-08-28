using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HCS.DocumentService;

public sealed class DocumentServiceDbContextFactory : IDesignTimeDbContextFactory<DocumentServiceDbContext>
{
    public DocumentServiceDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DocumentService")
            ?? "Host=localhost;Database=hcs_document;Username=hcs";
        var builder = new DbContextOptionsBuilder<DocumentServiceDbContext>();
        builder.UseNpgsql(connectionString);
        return new DocumentServiceDbContext(builder.Options);
    }
}
