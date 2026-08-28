using HCS.BusManagementService.Contracts;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;

namespace HCS.BusManagementService.Domain;

public static class BusConsts
{
    public const int CodeLength = 64;
    public const int NameLength = 256;
    public const int StatusLength = 32;
    public const int TypeLength = 64;
    public const int DescriptionLength = 2000;
    public const string DefaultTimeZone = "Asia/Ho_Chi_Minh";
}

public static class BusDates
{
    public static DateTime BusinessDate(DateTime value) => DateTime.SpecifyKind(value.Date, DateTimeKind.Unspecified);

    public static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Utc
        ? value
        : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

public sealed class BusStation : FullAuditedAggregateRoot<Guid>
{
    private BusStation() { }

    public BusStation(Guid id, string code, string name, string? address = null,
        string? timeZone = null) : base(id)
    {
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), BusConsts.CodeLength);
        Change(name, address, timeZone ?? BusConsts.DefaultTimeZone, true);
    }

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public string TimeZone { get; private set; } = BusConsts.DefaultTimeZone;
    public bool IsActive { get; private set; }

    public void Change(string name, string? address, string timeZone, bool isActive)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), BusConsts.NameLength);
        Address = address?.Trim();
        TimeZone = Check.NotNullOrWhiteSpace(timeZone, nameof(timeZone), 64);
        IsActive = isActive;
    }
}

public sealed class UserStationAssignment : Entity<Guid>
{
    private UserStationAssignment() { }

    public UserStationAssignment(Guid id, Guid userId, Guid stationId, bool isPrimary,
        DateTime? validFrom = null, DateTime? validTo = null) : base(id)
    {
        if (userId == Guid.Empty) throw new BusinessException("Bus:UserRequired");
        if (stationId == Guid.Empty) throw new BusinessException("Bus:StationRequired");
        if (validFrom.HasValue && validTo.HasValue && validTo < validFrom)
            throw new BusinessException("Bus:AssignmentDateRange");
        UserId = userId; StationId = stationId; IsPrimary = isPrimary; IsActive = true;
        ValidFrom = validFrom?.Date; ValidTo = validTo?.Date;
    }

    public Guid UserId { get; private set; }
    public Guid StationId { get; private set; }
    public bool IsPrimary { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? ValidFrom { get; private set; }
    public DateTime? ValidTo { get; private set; }

    public void Change(bool isPrimary, DateTime? validFrom, DateTime? validTo)
    {
        if (validFrom.HasValue && validTo.HasValue && validTo < validFrom)
            throw new BusinessException("Bus:AssignmentDateRange");
        IsPrimary = isPrimary; ValidFrom = validFrom?.Date; ValidTo = validTo?.Date;
    }

    public void Deactivate() => IsActive = false;
}

public sealed class TransportOperator : FullAuditedAggregateRoot<Guid>
{
    private TransportOperator() { }
    public TransportOperator(Guid id, string code, string name) : base(id)
    {
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), BusConsts.CodeLength);
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), BusConsts.NameLength);
        IsActive = true;
    }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
}

public sealed class FixedRoute : FullAuditedAggregateRoot<Guid>
{
    private FixedRoute() { }
    public FixedRoute(Guid id, string code, string name, Guid operatorId) : base(id)
    {
        if (operatorId == Guid.Empty) throw new BusinessException("Bus:OperatorRequired");
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), BusConsts.CodeLength);
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), BusConsts.NameLength);
        OperatorId = operatorId; IsActive = true;
    }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public Guid OperatorId { get; private set; }
    public bool IsActive { get; private set; }
}

public sealed class Vehicle : FullAuditedAggregateRoot<Guid>
{
    private Vehicle() { }
    public Vehicle(Guid id, string plateNumber, string vehicleType, Guid operatorId) : base(id)
    {
        if (operatorId == Guid.Empty) throw new BusinessException("Bus:OperatorRequired");
        PlateNumber = Check.NotNullOrWhiteSpace(plateNumber, nameof(plateNumber), BusConsts.CodeLength);
        VehicleType = Check.NotNullOrWhiteSpace(vehicleType, nameof(vehicleType), BusConsts.TypeLength);
        OperatorId = operatorId; IsActive = true;
    }
    public string PlateNumber { get; private set; } = string.Empty;
    public string VehicleType { get; private set; } = string.Empty;
    public Guid OperatorId { get; private set; }
    public bool IsActive { get; private set; }
}

public sealed class Driver : FullAuditedAggregateRoot<Guid>
{
    private Driver() { }
    public Driver(Guid id, string fullName, string licenseNumber) : base(id)
    {
        FullName = Check.NotNullOrWhiteSpace(fullName, nameof(fullName), BusConsts.NameLength);
        LicenseNumber = Check.NotNullOrWhiteSpace(licenseNumber, nameof(licenseNumber), BusConsts.CodeLength);
        IsActive = true;
    }
    public string FullName { get; private set; } = string.Empty;
    public string LicenseNumber { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
}

public sealed class VehicleLegalDocument : FullAuditedAggregateRoot<Guid>
{
    private VehicleLegalDocument() { }
    public VehicleLegalDocument(Guid id, Guid vehicleId, string documentType, DateTime expiresOn, Guid? documentId = null) : base(id)
    {
        if (vehicleId == Guid.Empty) throw new BusinessException("Bus:VehicleRequired");
        VehicleId = vehicleId;
        DocumentType = Check.NotNullOrWhiteSpace(documentType, nameof(documentType), BusConsts.TypeLength);
        ExpiresOn = expiresOn.Date; DocumentId = documentId; IsActive = true;
    }
    public Guid VehicleId { get; private set; }
    public string DocumentType { get; private set; } = string.Empty;
    public DateTime ExpiresOn { get; private set; }
    public Guid? DocumentId { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsValidOn(DateTime date) => IsActive && ExpiresOn.Date >= date.Date;
}

public sealed class CarrierContract : FullAuditedAggregateRoot<Guid>
{
    private CarrierContract() { }
    public CarrierContract(Guid id, Guid stationId, Guid operatorId, string contractNumber,
        DateTime startDate, DateTime endDate, Guid? documentId = null) : base(id)
    {
        StationId = stationId; OperatorId = operatorId;
        ContractNumber = Check.NotNullOrWhiteSpace(contractNumber, nameof(contractNumber), BusConsts.CodeLength);
        if (endDate.Date < startDate.Date) throw new BusinessException("Bus:ContractDateRange");
        StartDate = startDate.Date; EndDate = endDate.Date; DocumentId = documentId; IsActive = true;
    }
    public Guid StationId { get; private set; }
    public Guid OperatorId { get; private set; }
    public string ContractNumber { get; private set; } = string.Empty;
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public Guid? DocumentId { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsValidOn(DateTime date) => IsActive && date.Date >= StartDate && date.Date <= EndDate;
}

public sealed class DepartureTrip : FullAuditedAggregateRoot<Guid>
{
    private DepartureTrip() { }
    public DepartureTrip(Guid id, Guid stationId, Guid operatorId, Guid routeId, Guid vehicleId, Guid driverId,
        DateTime businessDate, string shiftCode, DateTime scheduledDepartureUtc, int passengerCount = 0) : base(id)
    {
        if (stationId == Guid.Empty || operatorId == Guid.Empty || routeId == Guid.Empty || vehicleId == Guid.Empty || driverId == Guid.Empty)
            throw new BusinessException("Bus:DepartureReferenceRequired");
        if (passengerCount < 0) throw new BusinessException("Bus:PassengerCountInvalid");
        StationId = stationId; OperatorId = operatorId; RouteId = routeId; VehicleId = vehicleId; DriverId = driverId;
        BusinessDate = BusDates.BusinessDate(businessDate); ShiftCode = Check.NotNullOrWhiteSpace(shiftCode, nameof(shiftCode), BusConsts.CodeLength);
        ScheduledDepartureUtc = BusDates.Utc(scheduledDepartureUtc); PassengerCount = passengerCount; Status = BusStatuses.Draft;
    }
    public Guid StationId { get; private set; }
    public Guid OperatorId { get; private set; }
    public Guid RouteId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid DriverId { get; private set; }
    public DateTime BusinessDate { get; private set; }
    public string ShiftCode { get; private set; } = string.Empty;
    public DateTime ScheduledDepartureUtc { get; private set; }
    public DateTime? ActualDepartureUtc { get; private set; }
    public int PassengerCount { get; private set; }
    public string Status { get; private set; } = BusStatuses.Draft;

    public void Register() => RequireStatus(BusStatuses.Draft, () => { Status = BusStatuses.Registered; Touch(); });
    public void ResetToRegistered()
    {
        if (Status != BusStatuses.Blocked) throw new BusinessException("Bus:InvalidDepartureTransition");
        Status = BusStatuses.Registered; Touch();
    }
    public void MarkReady() => RequireStatus(BusStatuses.Registered, () => { Status = BusStatuses.Ready; Touch(); });
    public void Block() { Status = BusStatuses.Blocked; Touch(); }
    public void MarkDeparted(DateTime atUtc)
    {
        RequireStatus(BusStatuses.Ready, () => { Status = BusStatuses.Departed; ActualDepartureUtc = BusDates.Utc(atUtc); Touch(); });
    }
    public void Complete() => RequireStatus(BusStatuses.Departed, () => { Status = BusStatuses.Completed; Touch(); });
    public void Cancel() => RequireStatus(BusStatuses.Registered, () => { Status = BusStatuses.Cancelled; Touch(); });
    public void MarkNoService() => RequireStatus(BusStatuses.Registered, () => { Status = BusStatuses.NoService; Touch(); });

    private void Touch() => ConcurrencyStamp = Guid.NewGuid().ToString("N");

    private void RequireStatus(string expected, Action action)
    {
        if (!string.Equals(Status, expected, StringComparison.Ordinal))
            throw new BusinessException("Bus:InvalidDepartureTransition").WithData("Expected", expected).WithData("Actual", Status);
        action();
    }
}

public sealed class DepartureCheck : Entity<Guid>
{
    private DepartureCheck() { }
    public DepartureCheck(Guid id, Guid departureId, string checkType, bool isPassed, string? note = null) : base(id)
    {
        DepartureId = departureId; CheckType = Check.NotNullOrWhiteSpace(checkType, nameof(checkType), BusConsts.TypeLength);
        IsPassed = isPassed; Note = note?.Trim();
    }
    public Guid DepartureId { get; private set; }
    public string CheckType { get; private set; } = string.Empty;
    public bool IsPassed { get; private set; }
    public string? Note { get; private set; }
    public void Record(bool isPassed, string? note = null) { IsPassed = isPassed; Note = note?.Trim(); }
}

public sealed class Tariff : FullAuditedAggregateRoot<Guid>
{
    private Tariff() { }
    public Tariff(Guid id, Guid stationId, Guid? routeId, string vehicleType, string feeType,
        decimal amount, DateTime effectiveFrom, DateTime? effectiveTo = null) : base(id)
    {
        if (stationId == Guid.Empty) throw new BusinessException("Bus:StationRequired");
        if (amount < 0) throw new BusinessException("Bus:AmountInvalid");
        if (effectiveTo.HasValue && effectiveTo.Value.Date < effectiveFrom.Date)
            throw new BusinessException("Bus:TariffDateRange");
        StationId = stationId; RouteId = routeId;
        VehicleType = Check.NotNullOrWhiteSpace(vehicleType, nameof(vehicleType), BusConsts.TypeLength);
        FeeType = Check.NotNullOrWhiteSpace(feeType, nameof(feeType), BusConsts.TypeLength);
        Amount = decimal.Round(amount, 2); EffectiveFrom = effectiveFrom.Date; EffectiveTo = effectiveTo?.Date; IsActive = true;
    }
    public Guid StationId { get; private set; }
    public Guid? RouteId { get; private set; }
    public string VehicleType { get; private set; } = string.Empty;
    public string FeeType { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public DateTime EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsEffectiveOn(DateTime date) => IsActive && date.Date >= EffectiveFrom && (!EffectiveTo.HasValue || date.Date <= EffectiveTo.Value);
}

public sealed class RevenueReceipt : FullAuditedAggregateRoot<Guid>
{
    private readonly List<RevenueLine> _lines = [];
    private RevenueReceipt() { }
    public RevenueReceipt(Guid id, string receiptNumber, Guid stationId, DateTime businessDate, string shiftCode,
        string sourceType, Guid? departureId, Guid? operatorId, Guid? createdByUserId, string? idempotencyKey) : base(id)
    {
        ReceiptNumber = Check.NotNullOrWhiteSpace(receiptNumber, nameof(receiptNumber), BusConsts.CodeLength);
        if (stationId == Guid.Empty) throw new BusinessException("Bus:StationRequired");
        StationId = stationId; BusinessDate = BusDates.BusinessDate(businessDate);
        ShiftCode = Check.NotNullOrWhiteSpace(shiftCode, nameof(shiftCode), BusConsts.CodeLength);
        SourceType = Check.NotNullOrWhiteSpace(sourceType, nameof(sourceType), BusConsts.TypeLength);
        DepartureId = departureId; OperatorId = operatorId; CreatedByUserId = createdByUserId;
        IdempotencyKey = idempotencyKey?.Trim(); Status = BusStatuses.Draft;
    }
    public string ReceiptNumber { get; private set; } = string.Empty;
    public Guid StationId { get; private set; }
    public DateTime BusinessDate { get; private set; }
    public string ShiftCode { get; private set; } = string.Empty;
    public string SourceType { get; private set; } = string.Empty;
    public Guid? DepartureId { get; private set; }
    public Guid? OperatorId { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public string? IdempotencyKey { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string Status { get; private set; } = BusStatuses.Draft;
    public DateTime? IssuedAtUtc { get; private set; }
    public IReadOnlyList<RevenueLine> Lines => _lines;

    public void AddLine(Guid id, string description, decimal quantity, decimal unitAmount, Guid? tariffId = null)
    {
        if (Status != BusStatuses.Draft) throw new BusinessException("Bus:ReceiptImmutable");
        if (quantity <= 0 || unitAmount < 0) throw new BusinessException("Bus:LineAmountInvalid");
        var line = new RevenueLine(id, Id, description, quantity, unitAmount, tariffId);
        _lines.Add(line); TotalAmount = decimal.Round(_lines.Sum(x => x.LineTotal), 2); Touch();
    }

    public void Issue(DateTime issuedAtUtc)
    {
        if (Status != BusStatuses.Draft || _lines.Count == 0) throw new BusinessException("Bus:ReceiptCannotIssue");
        Status = BusStatuses.Issued; IssuedAtUtc = BusDates.Utc(issuedAtUtc); Touch();
    }

    public void Void() { if (Status != BusStatuses.Issued) throw new BusinessException("Bus:ReceiptCannotVoid"); Status = BusStatuses.Voided; Touch(); }
    private void Touch() => ConcurrencyStamp = Guid.NewGuid().ToString("N");
}

public sealed class RevenueLine : Entity<Guid>
{
    private RevenueLine() { }
    public RevenueLine(Guid id, Guid receiptId, string description, decimal quantity, decimal unitAmount, Guid? tariffId) : base(id)
    {
        ReceiptId = receiptId; Description = Check.NotNullOrWhiteSpace(description, nameof(description), BusConsts.DescriptionLength);
        Quantity = decimal.Round(quantity, 2); UnitAmount = decimal.Round(unitAmount, 2); TariffId = tariffId;
        LineTotal = decimal.Round(Quantity * UnitAmount, 2);
    }
    public Guid ReceiptId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitAmount { get; private set; }
    public decimal LineTotal { get; private set; }
    public Guid? TariffId { get; private set; }
}

public sealed class ExpenseEntry : FullAuditedAggregateRoot<Guid>
{
    private ExpenseEntry() { }
    public ExpenseEntry(Guid id, Guid stationId, DateTime businessDate, string shiftCode, string category,
        decimal amount, string description, Guid? documentId, Guid? createdByUserId) : base(id)
    {
        if (stationId == Guid.Empty || amount < 0) throw new BusinessException("Bus:ExpenseInvalid");
        StationId = stationId; BusinessDate = BusDates.BusinessDate(businessDate);
        ShiftCode = Check.NotNullOrWhiteSpace(shiftCode, nameof(shiftCode), BusConsts.CodeLength);
        Category = Check.NotNullOrWhiteSpace(category, nameof(category), BusConsts.TypeLength);
        Amount = decimal.Round(amount, 2); Description = Check.NotNullOrWhiteSpace(description, nameof(description), BusConsts.DescriptionLength);
        DocumentId = documentId; CreatedByUserId = createdByUserId; Status = BusStatuses.Draft;
    }
    public Guid StationId { get; private set; }
    public DateTime BusinessDate { get; private set; }
    public string ShiftCode { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Guid? DocumentId { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public void Submit() { if (Status != BusStatuses.Draft) throw new BusinessException("Bus:InvalidExpenseTransition"); Status = BusStatuses.Submitted; Touch(); }
    public void Approve(Guid? approverUserId)
    {
        if (Status != BusStatuses.Submitted) throw new BusinessException("Bus:InvalidExpenseTransition");
        if (approverUserId.HasValue && approverUserId == CreatedByUserId)
            throw new BusinessException("Bus:MakerCheckerViolation");
        Status = BusStatuses.Approved; Touch();
    }
    private void Touch() => ConcurrencyStamp = Guid.NewGuid().ToString("N");
}

public sealed class PremisesUnit : FullAuditedAggregateRoot<Guid>
{
    private PremisesUnit() { }
    public PremisesUnit(Guid id, Guid stationId, string code, string name, decimal areaSquareMeters, string? location) : base(id)
    {
        if (stationId == Guid.Empty || areaSquareMeters <= 0) throw new BusinessException("Bus:PremisesInvalid");
        StationId = stationId; Code = Check.NotNullOrWhiteSpace(code, nameof(code), BusConsts.CodeLength);
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), BusConsts.NameLength); AreaSquareMeters = decimal.Round(areaSquareMeters, 2);
        Location = location?.Trim(); IsActive = true;
    }
    public Guid StationId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public decimal AreaSquareMeters { get; private set; }
    public string? Location { get; private set; }
    public bool IsActive { get; private set; }
}

public sealed class LeaseContract : FullAuditedAggregateRoot<Guid>
{
    private LeaseContract() { }
    public LeaseContract(Guid id, Guid stationId, Guid premisesUnitId, string tenantName, DateTime startDate,
        DateTime endDate, decimal rentAmount, string rentPeriod) : base(id)
    {
        if (stationId == Guid.Empty || premisesUnitId == Guid.Empty || rentAmount < 0) throw new BusinessException("Bus:LeaseInvalid");
        if (endDate.Date < startDate.Date) throw new BusinessException("Bus:LeaseDateRange");
        StationId = stationId; PremisesUnitId = premisesUnitId;
        TenantName = Check.NotNullOrWhiteSpace(tenantName, nameof(tenantName), BusConsts.NameLength);
        StartDate = startDate.Date; EndDate = endDate.Date; RentAmount = decimal.Round(rentAmount, 2);
        RentPeriod = Check.NotNullOrWhiteSpace(rentPeriod, nameof(rentPeriod), BusConsts.TypeLength); Status = BusStatuses.Draft;
    }
    public Guid StationId { get; private set; }
    public Guid PremisesUnitId { get; private set; }
    public string TenantName { get; private set; } = string.Empty;
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public decimal RentAmount { get; private set; }
    public string RentPeriod { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public void Approve() { if (Status != BusStatuses.Draft) throw new BusinessException("Bus:InvalidLeaseTransition"); Status = BusStatuses.Approved; }
}

public sealed class ShiftSettlement : FullAuditedAggregateRoot<Guid>
{
    private ShiftSettlement() { }
    public ShiftSettlement(Guid id, Guid stationId, DateTime businessDate, string shiftCode, decimal totalRevenue,
        decimal totalExpense, Guid? createdByUserId) : base(id)
    {
        if (stationId == Guid.Empty || totalRevenue < 0 || totalExpense < 0) throw new BusinessException("Bus:SettlementInvalid");
        StationId = stationId; BusinessDate = BusDates.BusinessDate(businessDate);
        ShiftCode = Volo.Abp.Check.NotNullOrWhiteSpace(shiftCode, nameof(shiftCode), BusConsts.CodeLength);
        TotalRevenue = decimal.Round(totalRevenue, 2); TotalExpense = decimal.Round(totalExpense, 2);
        CreatedByUserId = createdByUserId; Status = BusStatuses.Draft;
    }
    public Guid StationId { get; private set; }
    public DateTime BusinessDate { get; private set; }
    public string ShiftCode { get; private set; } = string.Empty;
    public decimal TotalRevenue { get; private set; }
    public decimal TotalExpense { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public Guid? CreatedByUserId { get; private set; }
    public Guid? SubmittedByUserId { get; private set; }
    public Guid? CheckedByUserId { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public void Submit(Guid? userId) { Require(BusStatuses.Draft); Status = BusStatuses.Submitted; SubmittedByUserId = userId; Touch(); }
    public void Check(Guid? userId) { Require(BusStatuses.Submitted); EnsureDifferent(userId, CreatedByUserId); Status = BusStatuses.Checked; CheckedByUserId = userId; Touch(); }
    public void Approve(Guid? userId) { Require(BusStatuses.Checked); EnsureDifferent(userId, CreatedByUserId, SubmittedByUserId, CheckedByUserId); Status = BusStatuses.Approved; ApprovedByUserId = userId; Touch(); }
    public void Close() { Require(BusStatuses.Approved); Status = BusStatuses.Closed; Touch(); }
    private void Touch() => ConcurrencyStamp = Guid.NewGuid().ToString("N");
    private void Require(string expected) { if (Status != expected) throw new BusinessException("Bus:InvalidSettlementTransition"); }
    private static void EnsureDifferent(Guid? current, params Guid?[] previous) { if (current.HasValue && previous.Any(x => x == current)) throw new BusinessException("Bus:MakerCheckerViolation"); }
}

public sealed class DailyClose : FullAuditedAggregateRoot<Guid>
{
    private DailyClose() { }
    public DailyClose(Guid id, Guid stationId, DateTime businessDate, decimal totalRevenue, decimal totalExpense, int shiftCount) : base(id)
    {
        if (stationId == Guid.Empty || totalRevenue < 0 || totalExpense < 0 || shiftCount < 0) throw new BusinessException("Bus:DailyCloseInvalid");
        StationId = stationId; BusinessDate = BusDates.BusinessDate(businessDate); TotalRevenue = decimal.Round(totalRevenue, 2);
        TotalExpense = decimal.Round(totalExpense, 2); ShiftCount = shiftCount; Status = BusStatuses.Draft;
    }
    public Guid StationId { get; private set; }
    public DateTime BusinessDate { get; private set; }
    public decimal TotalRevenue { get; private set; }
    public decimal TotalExpense { get; private set; }
    public int ShiftCount { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public Guid? ClosedByUserId { get; private set; }
    public DateTime? ClosedAtUtc { get; private set; }
    public void Close(Guid? userId, DateTime atUtc) { if (Status == BusStatuses.Closed) throw new BusinessException("Bus:DailyCloseImmutable"); Status = BusStatuses.Closed; ClosedByUserId = userId; ClosedAtUtc = BusDates.Utc(atUtc); Touch(); }
    private void Touch() => ConcurrencyStamp = Guid.NewGuid().ToString("N");
}

public sealed class AdjustmentEntry : FullAuditedAggregateRoot<Guid>
{
    private AdjustmentEntry() { }
    public AdjustmentEntry(Guid id, Guid stationId, Guid? receiptId, Guid? expenseId, decimal amount, string reason, Guid createdByUserId) : base(id)
    {
        if (stationId == Guid.Empty || (receiptId is null && expenseId is null) || amount == 0) throw new BusinessException("Bus:AdjustmentInvalid");
        StationId = stationId; ReceiptId = receiptId; ExpenseId = expenseId; Amount = decimal.Round(amount, 2);
        Reason = Check.NotNullOrWhiteSpace(reason, nameof(reason), BusConsts.DescriptionLength); CreatedByUserId = createdByUserId; Status = BusStatuses.Submitted;
    }
    public Guid StationId { get; private set; }
    public Guid? ReceiptId { get; private set; }
    public Guid? ExpenseId { get; private set; }
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid CreatedByUserId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public void Approve() { if (Status != BusStatuses.Submitted) throw new BusinessException("Bus:InvalidAdjustmentTransition"); Status = BusStatuses.Approved; Touch(); }
    private void Touch() => ConcurrencyStamp = Guid.NewGuid().ToString("N");
}
