using HCS.OrganizationService.Integration;

namespace HCS.OrganizationService.Tests;

public sealed class OutboxInboxTests : OrganizationTestBase
{
    [Fact]
    public async Task Inbox_executes_the_same_event_and_handler_only_once()
    {
        var executor = GetRequiredService<IInboxExecutor>();
        var eventId = Guid.NewGuid();
        var executions = 0;
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.True(await executor.ExecuteOnceAsync(eventId, "organization-projection", _ =>
        {
            executions++;
            return Task.CompletedTask;
        }, cancellationToken));
        Assert.False(await executor.ExecuteOnceAsync(eventId, "organization-projection", _ =>
        {
            executions++;
            return Task.CompletedTask;
        }, cancellationToken));

        Assert.Equal(1, executions);
    }

    [Fact]
    public async Task Failed_handler_does_not_claim_the_inbox_marker()
    {
        var executor = GetRequiredService<IInboxExecutor>();
        var eventId = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteOnceAsync(eventId, "retryable-handler",
            _ => throw new InvalidOperationException("transient handler failure"), cancellationToken));

        var completed = await executor.ExecuteOnceAsync(eventId, "retryable-handler", _ => Task.CompletedTask, cancellationToken);

        Assert.True(completed);
    }
}
