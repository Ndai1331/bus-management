using HCS.DocumentService.Integration;
using Microsoft.EntityFrameworkCore;

namespace HCS.DocumentService.Tests;

public sealed class InboxIdempotencyTests
{
    [Fact]
    public async Task Same_event_and_handler_execute_once()
    {
        var options = new DbContextOptionsBuilder<DocumentServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new DocumentServiceDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var executor = new EfInboxExecutor(db);
        var eventId = Guid.NewGuid();
        var executions = 0;
        Assert.True(await executor.ExecuteOnceAsync(eventId, "notification", _ => { executions++; return Task.CompletedTask; }, default));
        Assert.False(await executor.ExecuteOnceAsync(eventId, "notification", _ => { executions++; return Task.CompletedTask; }, default));
        Assert.Equal(1, executions);
    }
}
