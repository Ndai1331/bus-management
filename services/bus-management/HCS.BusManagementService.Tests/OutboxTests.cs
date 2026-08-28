using HCS.BusManagementService.Contracts;
using HCS.BusManagementService.Integration;

namespace HCS.BusManagementService.Tests;

public sealed class OutboxTests
{
    [Fact]
    public void Revenue_event_uses_canonical_versioned_name()
    {
        var receiptId = Guid.NewGuid();
        var message = BusOutbox.Create(new BusRevenueRecordedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, "corr",
            receiptId, Guid.NewGuid(), 125_000, RevenueSources.FixedRoute), "corr");

        Assert.Equal(BusRevenueRecordedEto.EventName, message.EventName);
        Assert.Contains(receiptId.ToString(), message.Payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Expense_event_uses_canonical_versioned_name()
    {
        var expenseId = Guid.NewGuid();
        var message = BusOutbox.Create(new BusExpenseChangedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, "corr",
            expenseId, Guid.NewGuid(), 50_000, BusStatuses.Approved), "corr");

        Assert.Equal(BusExpenseChangedEto.EventName, message.EventName);
        Assert.Contains(expenseId.ToString(), message.Payload, StringComparison.OrdinalIgnoreCase);
    }
}
