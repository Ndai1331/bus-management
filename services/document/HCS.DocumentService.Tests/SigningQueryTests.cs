using HCS.DocumentService.Signing;
using Microsoft.EntityFrameworkCore;

namespace HCS.DocumentService.Tests;

public sealed class SigningQueryTests
{
    [Fact]
    public void Paired_file_lookup_is_translatable_by_postgresql()
    {
        var options = new DbContextOptionsBuilder<DocumentServiceDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=hcs").Options;
        using var db = new DocumentServiceDbContext(options);

        var sql = SigningAppService.QueryPairedFile(db.DocumentFiles, Guid.NewGuid(), Guid.NewGuid())
            .ToQueryString();

        Assert.Contains("\"DocumentFiles\"", sql);
        Assert.Contains("\"IsPendingDeletion\"", sql);
    }
}
