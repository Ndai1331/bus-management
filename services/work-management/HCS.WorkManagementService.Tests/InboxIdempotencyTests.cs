using HCS.WorkManagementService.Data;
using HCS.WorkManagementService.Integration;
using Microsoft.EntityFrameworkCore;

namespace HCS.WorkManagementService.Tests;

public sealed class InboxIdempotencyTests
{
    [Fact]
    public async Task Same_event_and_handler_executes_once()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<WorkManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new WorkManagementDbContext(options);
        await db.Database.EnsureCreatedAsync(ct);
        var executor = new EfInboxExecutor(db); var count = 0; var eventId = Guid.NewGuid();

        Assert.True(await executor.ExecuteOnceAsync(eventId, "work-handler", _ => { count++; return Task.CompletedTask; }, ct));
        Assert.False(await executor.ExecuteOnceAsync(eventId, "work-handler", _ => { count++; return Task.CompletedTask; }, ct));
        Assert.Equal(1, count);
    }

    [Fact]
    public void Work_outbox_releases_lease_and_dead_letters_exhausted_delivery()
    {
        var item = new OutboxMessage(Guid.NewGuid(), "event", "{}", "correlation", DateTime.UtcNow);
        item.Lease(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(1));
        for (var attempt = 0; attempt < 10; attempt++) item.MarkFailed("failure");
        Assert.Null(item.LeaseId); Assert.Null(item.LeaseUntil);
        Assert.NotNull(item.DeadLetteredAt); Assert.Equal(10, item.Attempts);
    }
}
