using HCS.BusManagementService.Contracts;
using HCS.BusManagementService.Domain;
using Volo.Abp;

namespace HCS.BusManagementService.Tests;

public sealed class DomainInvariantTests
{
    [Fact]
    public void Departure_cannot_skip_registration_before_ready()
    {
        var trip = new DepartureTrip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 8, 28), "AM", DateTime.UtcNow);
        Assert.Throws<BusinessException>(() => trip.MarkReady());
    }

    [Fact]
    public void Departure_transitions_in_order()
    {
        var trip = new DepartureTrip(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateTime(2026, 8, 28), "AM", DateTime.UtcNow);
        trip.Register();
        trip.MarkReady();
        trip.MarkDeparted(DateTime.UtcNow);
        trip.Complete();
        Assert.Equal(BusStatuses.Completed, trip.Status);
    }

    [Fact]
    public void Issued_receipt_is_immutable()
    {
        var receipt = new RevenueReceipt(Guid.NewGuid(), "RC-1", Guid.NewGuid(), DateTime.Today, "AM", RevenueSources.FixedRoute, null, null, Guid.NewGuid(), null);
        receipt.AddLine(Guid.NewGuid(), "Phí bến", 1, 100_000);
        receipt.Issue(DateTime.UtcNow);
        Assert.Throws<BusinessException>(() => receipt.AddLine(Guid.NewGuid(), "Không được thêm", 1, 1));
        Assert.Equal(100_000, receipt.TotalAmount);
    }

    [Fact]
    public void Settlement_enforces_maker_checker()
    {
        var maker = Guid.NewGuid();
        var settlement = new ShiftSettlement(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, "AM", 100, 10, maker);
        settlement.Submit(maker);
        Assert.Throws<BusinessException>(() => settlement.Check(maker));
        var checker = Guid.NewGuid();
        settlement.Check(checker);
        Assert.Throws<BusinessException>(() => settlement.Approve(checker));
        settlement.Approve(Guid.NewGuid());
        settlement.Close();
        Assert.Equal(BusStatuses.Closed, settlement.Status);
    }

    [Fact]
    public void Lease_rejects_inverted_dates()
    {
        Assert.Throws<BusinessException>(() => new LeaseContract(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Tenant",
            DateTime.Today.AddDays(1), DateTime.Today, 100, "Monthly"));
    }

    [Fact]
    public void Daily_close_is_immutable_after_close()
    {
        var close = new DailyClose(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, 100, 10, 1);
        close.Close(Guid.NewGuid(), DateTime.UtcNow);
        Assert.Throws<BusinessException>(() => close.Close(Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void Expense_requires_submission_and_separate_approver()
    {
        var maker = Guid.NewGuid();
        var expense = new ExpenseEntry(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, "AM", "Fuel", 100, "Fuel", null, maker);
        Assert.Throws<BusinessException>(() => expense.Approve(Guid.NewGuid()));
        expense.Submit();
        Assert.Throws<BusinessException>(() => expense.Approve(maker));
        expense.Approve(Guid.NewGuid());
        Assert.Equal(BusStatuses.Approved, expense.Status);
    }
}
