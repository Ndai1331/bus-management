using HCS.BusManagementService.Contracts;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace HCS.BusManagementService.Domain;

public sealed class ParkingTariff : FullAuditedAggregateRoot<Guid>
{
    private ParkingTariff() { }

    public ParkingTariff(Guid id, Guid stationId, string vehicleType, int billingUnitMinutes,
        decimal ratePerUnit, decimal minimumCharge, string? description, DateTime effectiveFrom,
        DateTime? effectiveTo = null) : base(id)
    {
        if (stationId == Guid.Empty || billingUnitMinutes <= 0 || billingUnitMinutes > 1440 || ratePerUnit <= 0 || minimumCharge < 0)
            throw new BusinessException("Bus:ParkingTariffInvalid");
        if (effectiveTo.HasValue && effectiveTo.Value.Date < effectiveFrom.Date)
            throw new BusinessException("Bus:TariffDateRange");

        StationId = stationId;
        VehicleType = Check.NotNullOrWhiteSpace(vehicleType, nameof(vehicleType), BusConsts.TypeLength).Trim();
        BillingUnitMinutes = billingUnitMinutes;
        RatePerUnit = ParkingSession.RoundVnd(ratePerUnit);
        MinimumCharge = ParkingSession.RoundVnd(minimumCharge);
        if (RatePerUnit <= 0) throw new BusinessException("Bus:ParkingTariffInvalid");
        Description = string.IsNullOrWhiteSpace(description) ? "Phí bãi đỗ" : Check.NotNullOrWhiteSpace(description, nameof(description), BusConsts.DescriptionLength);
        EffectiveFrom = effectiveFrom.Date;
        EffectiveTo = effectiveTo?.Date;
        IsActive = true;
    }

    public Guid StationId { get; private set; }
    public string VehicleType { get; private set; } = string.Empty;
    public int BillingUnitMinutes { get; private set; }
    public decimal RatePerUnit { get; private set; }
    public decimal MinimumCharge { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTime EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }
    public bool IsActive { get; private set; }

    public bool IsEffectiveOn(DateTime date) => IsActive && date.Date >= EffectiveFrom &&
        (!EffectiveTo.HasValue || date.Date <= EffectiveTo.Value);
}

public sealed record ParkingChargeQuote(int DurationMinutes, int BilledUnits, decimal Amount);

public sealed class ParkingSession : FullAuditedAggregateRoot<Guid>
{
    private ParkingSession() { }

    public ParkingSession(Guid id, Guid stationId, DateTime businessDate, string shiftCode,
        string vehiclePlateNumber, string vehicleType, DateTime arrivalUtc, Guid parkingTariffId,
        int billingUnitMinutes, decimal ratePerUnit, decimal minimumCharge, string tariffDescription) : base(id)
    {
        if (stationId == Guid.Empty || parkingTariffId == Guid.Empty || billingUnitMinutes <= 0 ||
            ratePerUnit <= 0 || minimumCharge < 0 || arrivalUtc.Kind != DateTimeKind.Utc || arrivalUtc > DateTime.UtcNow)
            throw new BusinessException("Bus:ParkingSessionInvalid");

        StationId = stationId;
        BusinessDate = BusDates.BusinessDate(businessDate);
        ShiftCode = NormalizeShift(shiftCode);
        VehiclePlateNumber = NormalizePlate(vehiclePlateNumber);
        VehicleType = Check.NotNullOrWhiteSpace(vehicleType, nameof(vehicleType), BusConsts.TypeLength).Trim();
        ArrivalUtc = arrivalUtc;
        ParkingTariffId = parkingTariffId;
        BillingUnitMinutes = billingUnitMinutes;
        RatePerUnit = RoundVnd(ratePerUnit);
        MinimumCharge = RoundVnd(minimumCharge);
        if (RatePerUnit <= 0) throw new BusinessException("Bus:ParkingSessionInvalid");
        TariffDescription = Check.NotNullOrWhiteSpace(tariffDescription, nameof(tariffDescription), BusConsts.DescriptionLength);
        Status = BusStatuses.ParkingOpen;
    }

    public Guid StationId { get; private set; }
    public DateTime BusinessDate { get; private set; }
    public string ShiftCode { get; private set; } = string.Empty;
    public string VehiclePlateNumber { get; private set; } = string.Empty;
    public string VehicleType { get; private set; } = string.Empty;
    public DateTime ArrivalUtc { get; private set; }
    public DateTime? ExitUtc { get; private set; }
    public int? DurationMinutes { get; private set; }
    public int? BilledUnits { get; private set; }
    public Guid ParkingTariffId { get; private set; }
    public int BillingUnitMinutes { get; private set; }
    public decimal RatePerUnit { get; private set; }
    public decimal MinimumCharge { get; private set; }
    public string TariffDescription { get; private set; } = string.Empty;
    public decimal? ChargedAmount { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string? CancellationReason { get; private set; }

    public ParkingChargeQuote Quote(DateTime exitUtc)
    {
        if (Status != BusStatuses.ParkingOpen) throw new BusinessException("Bus:ParkingSessionImmutable");
        if (exitUtc.Kind != DateTimeKind.Utc) throw new BusinessException("Bus:ParkingUtcRequired");
        var normalizedExit = exitUtc;
        if (normalizedExit > DateTime.UtcNow) throw new BusinessException("Bus:ParkingExitInFuture");
        if (normalizedExit < ArrivalUtc) throw new BusinessException("Bus:ParkingExitBeforeEntry");
        var durationMinutes = Math.Max(1, (int)Math.Ceiling((normalizedExit - ArrivalUtc).TotalMinutes));
        var billedUnits = Math.Max(1, (int)Math.Ceiling((decimal)durationMinutes / BillingUnitMinutes));
        var amount = RoundVnd(Math.Max(MinimumCharge, billedUnits * RatePerUnit));
        return new(durationMinutes, billedUnits, amount);
    }

    public void Close(DateTime exitUtc)
    {
        var quote = Quote(exitUtc);
        ExitUtc = exitUtc;
        DurationMinutes = quote.DurationMinutes;
        BilledUnits = quote.BilledUnits;
        ChargedAmount = quote.Amount;
        Status = BusStatuses.ParkingClosed;
        Touch();
    }

    public void Cancel(string reason)
    {
        if (Status != BusStatuses.ParkingOpen) throw new BusinessException("Bus:ParkingSessionImmutable");
        CancellationReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), BusConsts.DescriptionLength);
        Status = BusStatuses.ParkingCancelled;
        Touch();
    }

    public static string NormalizePlate(string value)
    {
        var plate = Check.NotNullOrWhiteSpace(value, nameof(value), BusConsts.CodeLength)
            .Trim().ToUpperInvariant();
        return plate;
    }

    public static string NormalizeShift(string value) => Check.NotNullOrWhiteSpace(value, nameof(value), BusConsts.CodeLength)
        .Trim();

    private void Touch() => ConcurrencyStamp = Guid.NewGuid().ToString("N");

    public static decimal RoundVnd(decimal value) => decimal.Round(value, 0, MidpointRounding.AwayFromZero);
}
