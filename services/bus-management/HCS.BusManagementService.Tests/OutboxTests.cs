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

    [Fact]
    public void Adjustment_event_uses_canonical_versioned_name()
    {
        var adjustmentId = Guid.NewGuid();
        var message = BusOutbox.Create(new BusAdjustmentChangedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, "corr",
            adjustmentId, Guid.NewGuid(), -20, BusStatuses.Approved), "corr");

        Assert.Equal(BusAdjustmentChangedEto.EventName, message.EventName);
        Assert.Contains(adjustmentId.ToString(), message.Payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Settlement_event_uses_canonical_versioned_name()
    {
        var settlementId = Guid.NewGuid();
        var message = BusOutbox.Create(new BusSettlementChangedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, "corr",
            settlementId, Guid.NewGuid(), DateTime.Today, "AM", BusStatuses.Approved, 100, 20), "corr");

        Assert.Equal(BusSettlementChangedEto.EventName, message.EventName);
        Assert.Contains(settlementId.ToString(), message.Payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parking_session_event_uses_canonical_versioned_name()
    {
        var sessionId = Guid.NewGuid();
        var message = BusOutbox.Create(new BusParkingSessionChangedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, "corr",
            sessionId, Guid.NewGuid(), DateTime.Today, BusStatuses.ParkingClosed, 75_000, Guid.NewGuid(), 60, null,
            "Truck", Guid.NewGuid(), 75_000, 100_000, "Phí bãi", DateTime.UtcNow.AddHours(-2), DateTime.UtcNow, 120, 2), "corr");

        Assert.Equal(BusParkingSessionChangedEto.EventName, message.EventName);
        Assert.Contains(sessionId.ToString(), message.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("billingUnitMinutes", message.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tariffDescription", message.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("billedUnits", message.Payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parking_reservation_event_uses_canonical_versioned_name()
    {
        var reservationId = Guid.NewGuid();
        var message = BusOutbox.Create(new BusParkingReservationChangedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, "corr",
            reservationId, Guid.NewGuid(), Guid.NewGuid(), "51A-123.45", DateTime.UtcNow, DateTime.UtcNow.AddHours(1),
            BusStatuses.ParkingReserved), "corr");

        Assert.Equal(BusParkingReservationChangedEto.EventName, message.EventName);
        Assert.Contains(reservationId.ToString(), message.Payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("parkingSpotId", message.Payload, StringComparison.OrdinalIgnoreCase);
    }
}
