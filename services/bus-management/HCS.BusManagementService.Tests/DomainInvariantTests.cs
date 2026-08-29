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

    [Fact]
    public void Adjustment_requires_exactly_one_target()
    {
        var maker = Guid.NewGuid();
        Assert.Throws<BusinessException>(() => new AdjustmentEntry(Guid.NewGuid(), Guid.NewGuid(), null, null, 10, "Correction", maker));
        Assert.Throws<BusinessException>(() => new AdjustmentEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10, "Correction", maker));
        Assert.Throws<BusinessException>(() => new AdjustmentEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 0, "Correction", maker));
    }

    [Fact]
    public void Adjustment_requires_separate_approver_and_records_audit_fields()
    {
        var maker = Guid.NewGuid();
        var adjustment = new AdjustmentEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, -25, "Receipt correction", maker);
        Assert.Throws<BusinessException>(() => adjustment.Approve(maker, DateTime.UtcNow));
        var approver = Guid.NewGuid();
        var approvedAt = DateTime.UtcNow;
        adjustment.Approve(approver, approvedAt);
        Assert.Equal(BusStatuses.Approved, adjustment.Status);
        Assert.Equal(approver, adjustment.ApprovedByUserId);
        Assert.Equal(approvedAt, adjustment.ApprovedAtUtc);
    }

    [Fact]
    public void Revenue_sources_are_explicitly_allow_listed()
    {
        Assert.All(new[]
        {
            RevenueSources.FixedRoute, RevenueSources.VisitingVehicle, RevenueSources.PublicBus,
            RevenueSources.Parking, RevenueSources.Premises, RevenueSources.Other
        }, source => Assert.Contains(source, RevenueSources.Supported));
        Assert.DoesNotContain("Unknown", RevenueSources.Supported);
    }

    [Fact]
    public void Vehicle_document_can_be_renewed_or_deactivated()
    {
        var document = new VehicleLegalDocument(Guid.NewGuid(), Guid.NewGuid(), "Inspection", DateTime.Today.AddDays(1));
        document.Renew(DateTime.Today.AddDays(90), Guid.NewGuid(), true);
        Assert.True(document.IsValidOn(DateTime.Today.AddDays(30)));
        document.Renew(DateTime.Today.AddDays(-1), null, false);
        Assert.False(document.IsActive);
    }

    [Fact]
    public void Settlement_refreshes_source_totals_before_close()
    {
        var settlement = new ShiftSettlement(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, "AM", 100, 10, Guid.NewGuid());
        settlement.RefreshTotals(250, 35);
        Assert.Equal(250, settlement.TotalRevenue);
        Assert.Equal(35, settlement.TotalExpense);
        settlement.Submit(Guid.NewGuid());
        settlement.Check(Guid.NewGuid());
        settlement.Approve(Guid.NewGuid());
        settlement.Close();
        Assert.Throws<BusinessException>(() => settlement.RefreshTotals(1, 1));
    }
}
