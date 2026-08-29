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
    public void Parking_receipt_requires_a_session_link()
    {
        Assert.Throws<BusinessException>(() => new RevenueReceipt(Guid.NewGuid(), "RC-PARKING", Guid.NewGuid(),
            DateTime.Today, "AM", RevenueSources.Parking, null, null, Guid.NewGuid(), null));
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

    [Fact]
    public void Parking_session_calculates_billing_units_and_minimum_charge()
    {
        var arrival = new DateTime(2026, 8, 29, 1, 0, 0, DateTimeKind.Utc);
        var session = new ParkingSession(Guid.NewGuid(), Guid.NewGuid(), arrival.Date, "AM ", "51a-123.45", "Bus",
            arrival, Guid.NewGuid(), 60, 25_000, 40_000, "Phí bãi đỗ");

        var quote = session.Quote(arrival.AddMinutes(61));

        Assert.Equal(61, quote.DurationMinutes);
        Assert.Equal(2, quote.BilledUnits);
        Assert.Equal(50_000, quote.Amount);
        Assert.Equal("51A-123.45", session.VehiclePlateNumber);
        Assert.Equal("AM", session.ShiftCode);
    }

    [Fact]
    public void Parking_session_applies_minimum_charge_when_units_are_below_minimum()
    {
        var arrival = new DateTime(2026, 8, 29, 1, 0, 0, DateTimeKind.Utc);
        var session = new ParkingSession(Guid.NewGuid(), Guid.NewGuid(), arrival.Date, "AM", "51A-111.11", "Bus",
            arrival, Guid.NewGuid(), 60, 10_000, 40_000, "Phí bãi đỗ");

        var quote = session.Quote(arrival.AddMinutes(5));

        Assert.Equal(1, quote.BilledUnits);
        Assert.Equal(40_000, quote.Amount);
    }

    [Fact]
    public void Parking_session_requires_utc_timestamps()
    {
        var arrival = new DateTime(2026, 8, 29, 1, 0, 0, DateTimeKind.Unspecified);

        Assert.Throws<BusinessException>(() => new ParkingSession(Guid.NewGuid(), Guid.NewGuid(), arrival.Date, "AM", "51A-222.22", "Bus",
            arrival, Guid.NewGuid(), 60, 10_000, 0, "Phí bãi đỗ"));
    }

    [Fact]
    public void Parking_session_snapshot_is_immutable_after_close()
    {
        var session = new ParkingSession(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, "PM", "51B-999.99", "Truck",
            DateTime.UtcNow.AddHours(-1), Guid.NewGuid(), 30, 10_000, 10_000, "Parking rate v1");

        session.Close(session.ArrivalUtc.AddMinutes(31));

        Assert.Equal(BusStatuses.ParkingClosed, session.Status);
        Assert.Equal(20_000, session.ChargedAmount);
        Assert.Throws<BusinessException>(() => session.Quote(DateTime.UtcNow.AddHours(2)));
        Assert.Throws<BusinessException>(() => session.Cancel("late"));
    }

    [Fact]
    public void Parking_session_rejects_a_future_exit_time()
    {
        var session = new ParkingSession(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, "PM", "51B-777.77", "Truck",
            DateTime.UtcNow.AddMinutes(-1), Guid.NewGuid(), 30, 10_000, 10_000, "Parking rate");

        Assert.Throws<BusinessException>(() => session.Quote(DateTime.UtcNow.AddMinutes(1)));
    }

    [Fact]
    public void Cancelled_parking_session_cannot_be_closed()
    {
        var session = new ParkingSession(Guid.NewGuid(), Guid.NewGuid(), DateTime.Today, "PM", "51C-888.88", "Car",
            DateTime.UtcNow, Guid.NewGuid(), 60, 15_000, 0, "Parking rate");
        session.Cancel("Xe rời trước khi tính phí");

        Assert.Equal(BusStatuses.ParkingCancelled, session.Status);
        Assert.Throws<BusinessException>(() => session.Close(DateTime.UtcNow.AddMinutes(5)));
    }
}
