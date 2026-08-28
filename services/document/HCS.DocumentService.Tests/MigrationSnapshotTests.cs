using Microsoft.EntityFrameworkCore;

namespace HCS.DocumentService.Tests;

public sealed class MigrationSnapshotTests
{
    [Fact]
    public void Snapshot_matches_current_model()
    {
        var options = new DbContextOptionsBuilder<DocumentServiceDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=hcs")
            .Options;
        using var db = new DocumentServiceDbContext(options);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
