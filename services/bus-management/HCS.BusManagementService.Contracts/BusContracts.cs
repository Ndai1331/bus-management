using HCS.IntegrationEvents;

namespace HCS.BusManagementService.Contracts;

public static class BusPermissions
{
    public const string Group = "HCS.BusManagement";
    public const string Stations = Group + ".Stations";
    public const string MasterData = Group + ".MasterData";
    public const string OperatorsContracts = Group + ".OperatorsContracts";
    public const string FleetCompliance = Group + ".FleetCompliance";
    public const string Departures = Group + ".Departures";
    public const string Revenue = Group + ".Revenue";
    public const string Expenses = Group + ".Expenses";
    public const string Premises = Group + ".Premises";
    public const string Reconciliation = Group + ".Reconciliation";
    public const string ReconciliationCheck = Reconciliation + ".Check";
    public const string ReconciliationClose = Reconciliation + ".Close";
    public const string ReconciliationAdjust = Reconciliation + ".Adjust";
    public const string Reports = Group + ".Reports";
    public const string StationAssignments = Group + ".StationAssignments";
    public const string StationsCreate = Stations + ".Create";
    public const string StationsUpdate = Stations + ".Update";
    public const string DeparturesUpdate = Departures + ".Update";
    public const string MasterDataCreate = MasterData + ".Create";
    public const string OperatorsContractsCreate = OperatorsContracts + ".Create";
    public const string FleetComplianceCreate = FleetCompliance + ".Create";
    public const string DeparturesCreate = Departures + ".Create";
    public const string RevenueCreate = Revenue + ".Create";
    public const string ExpensesCreate = Expenses + ".Create";
    public const string ExpensesApprove = Expenses + ".Approve";
    public const string PremisesCreate = Premises + ".Create";
    public const string ReconciliationCreate = Reconciliation + ".Create";
    public const string ReconciliationApprove = Reconciliation + ".Approve";
    public const string ReportsExport = Reports + ".Export";
    public const string StationAssignmentsCreate = StationAssignments + ".Create";

    public static readonly IReadOnlyList<string> All =
    [
        Stations, MasterData, OperatorsContracts, FleetCompliance, Departures,
        Revenue, Expenses, Premises, Reconciliation, ReconciliationCheck,
        ReconciliationClose, ReconciliationAdjust, Reports, StationAssignments, StationsCreate, StationsUpdate,
        MasterDataCreate, OperatorsContractsCreate, FleetComplianceCreate, DeparturesCreate, RevenueCreate,
        ExpensesCreate, ExpensesApprove, PremisesCreate, ReconciliationCreate, ReconciliationApprove,
        ReportsExport, StationAssignmentsCreate, DeparturesUpdate
    ];
}

public static class BusStatuses
{
    public const string Draft = "Draft";
    public const string Registered = "Registered";
    public const string Ready = "Ready";
    public const string Blocked = "Blocked";
    public const string Departed = "Departed";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string NoService = "NoService";
    public const string Submitted = "Submitted";
    public const string Checked = "Checked";
    public const string Approved = "Approved";
    public const string Closed = "Closed";
    public const string Issued = "Issued";
    public const string Voided = "Voided";
}

public static class RevenueSources
{
    public const string FixedRoute = "FixedRoute";
    public const string VisitingVehicle = "VisitingVehicle";
    public const string PublicBus = "PublicBus";
    public const string Parking = "Parking";
    public const string Premises = "Premises";
    public const string Other = "Other";
}

public sealed record PagedBusDto<T>(long TotalCount, IReadOnlyList<T> Items);
public sealed record BusStationDto(Guid Id, string Code, string Name, string? Address, string TimeZone, bool IsActive);
public sealed record CreateBusStationDto(string Code, string Name, string? Address = null, string? TimeZone = null);
public sealed record UpdateBusStationDto(string Name, string? Address, string TimeZone, bool IsActive);
public sealed record StationAssignmentDto(Guid Id, Guid UserId, Guid StationId, bool IsPrimary, bool IsActive,
    DateTime? ValidFrom, DateTime? ValidTo);
public sealed record AssignStationDto(Guid UserId, Guid StationId, bool IsPrimary = false,
    DateTime? ValidFrom = null, DateTime? ValidTo = null);

public sealed record OperatorDto(Guid Id, string Code, string Name, bool IsActive);
public sealed record CreateOperatorDto(string Code, string Name);
public sealed record RouteDto(Guid Id, string Code, string Name, Guid OperatorId, bool IsActive);
public sealed record CreateRouteDto(string Code, string Name, Guid OperatorId);
public sealed record VehicleDto(Guid Id, string PlateNumber, string VehicleType, Guid OperatorId, bool IsActive);
public sealed record CreateVehicleDto(string PlateNumber, string VehicleType, Guid OperatorId);
public sealed record DriverDto(Guid Id, string FullName, string LicenseNumber, bool IsActive);
public sealed record CreateDriverDto(string FullName, string LicenseNumber);
public sealed record CreateVehicleLegalDocumentDto(Guid VehicleId, string DocumentType, DateTime ExpiresOn, Guid? DocumentId = null);
public sealed record VehicleLegalDocumentDto(Guid Id, Guid VehicleId, string DocumentType, DateTime ExpiresOn, Guid? DocumentId, bool IsActive);
public sealed record CreateCarrierContractDto(Guid StationId, Guid OperatorId, string ContractNumber,
    DateTime StartDate, DateTime EndDate, Guid? DocumentId = null);
public sealed record CarrierContractDto(Guid Id, Guid StationId, Guid OperatorId, string ContractNumber,
    DateTime StartDate, DateTime EndDate, Guid? DocumentId, bool IsActive);

public sealed record CreateDepartureDto(Guid StationId, Guid OperatorId, Guid RouteId, Guid VehicleId, Guid DriverId,
    DateTime BusinessDate, string ShiftCode, DateTime ScheduledDepartureUtc, int PassengerCount = 0,
    bool InspectionValid = false, bool RouteBadgeValid = false, bool InsuranceValid = false,
    bool DriverLicenseValid = false, bool TransportOrderValid = false, bool ContractValid = false,
    bool FeePaid = false, bool ControlApproved = false);
public sealed record DepartureDto(Guid Id, Guid StationId, Guid OperatorId, Guid RouteId, Guid VehicleId,
    Guid DriverId, DateTime BusinessDate, string ShiftCode, DateTime ScheduledDepartureUtc,
    DateTime? ActualDepartureUtc, int PassengerCount, string Status, IReadOnlyList<DepartureCheckDto> Checks);
public sealed record DepartureCheckDto(Guid Id, string CheckType, bool IsPassed, string? Note);
public sealed record DepartureCheckInput(string CheckType, bool IsPassed, string? Note = null);
public sealed record UpdateDepartureChecksDto(IReadOnlyList<DepartureCheckInput> Checks);

public sealed record CreateTariffDto(Guid StationId, Guid? RouteId, string VehicleType, string FeeType,
    decimal Amount, DateTime EffectiveFrom, DateTime? EffectiveTo = null);
public sealed record TariffDto(Guid Id, Guid StationId, Guid? RouteId, string VehicleType, string FeeType,
    decimal Amount, DateTime EffectiveFrom, DateTime? EffectiveTo);
public sealed record RevenueLineInput(string Description, decimal Quantity, decimal UnitAmount, Guid? TariffId = null);
public sealed record CreateRevenueReceiptDto(Guid StationId, DateTime BusinessDate, string ShiftCode,
    string SourceType, Guid? DepartureId, Guid? OperatorId, string? IdempotencyKey,
    IReadOnlyList<RevenueLineInput> Lines);
public sealed record RevenueLineDto(Guid Id, string Description, decimal Quantity, decimal UnitAmount, decimal LineTotal,
    Guid? TariffId);
public sealed record RevenueReceiptDto(Guid Id, string ReceiptNumber, Guid StationId, DateTime BusinessDate,
    string ShiftCode, string SourceType, Guid? DepartureId, Guid? OperatorId, decimal TotalAmount, string Status,
    DateTime? IssuedAtUtc, IReadOnlyList<RevenueLineDto> Lines);

public sealed record CreateExpenseDto(Guid StationId, DateTime BusinessDate, string ShiftCode, string Category,
    decimal Amount, string Description, Guid? DocumentId = null);
public sealed record ExpenseDto(Guid Id, Guid StationId, DateTime BusinessDate, string ShiftCode, string Category,
    decimal Amount, string Description, Guid? DocumentId, string Status);

public sealed record CreatePremisesUnitDto(Guid StationId, string Code, string Name, decimal AreaSquareMeters,
    string? Location = null);
public sealed record PremisesUnitDto(Guid Id, Guid StationId, string Code, string Name, decimal AreaSquareMeters,
    string? Location, bool IsActive);
public sealed record CreateLeaseContractDto(Guid StationId, Guid PremisesUnitId, string TenantName,
    DateTime StartDate, DateTime EndDate, decimal RentAmount, string RentPeriod);
public sealed record LeaseContractDto(Guid Id, Guid StationId, Guid PremisesUnitId, string TenantName,
    DateTime StartDate, DateTime EndDate, decimal RentAmount, string RentPeriod, string Status);

public sealed record CreateShiftSettlementDto(Guid StationId, DateTime BusinessDate, string ShiftCode);
public sealed record SettlementDto(Guid Id, Guid StationId, DateTime BusinessDate, string ShiftCode,
    decimal TotalRevenue, decimal TotalExpense, string Status, Guid? SubmittedByUserId, Guid? CheckedByUserId,
    Guid? ApprovedByUserId);
public sealed record CloseDailyDto(Guid StationId, DateTime BusinessDate);
public sealed record DailyCloseDto(Guid Id, Guid StationId, DateTime BusinessDate, decimal TotalRevenue,
    decimal TotalExpense, int ShiftCount, string Status, Guid? ClosedByUserId, DateTime? ClosedAtUtc);
public sealed record DashboardSummaryDto(DateTime From, DateTime To, decimal TotalRevenue, decimal TotalExpense,
    int DepartureCount, int ReceiptCount, int UnreconciledShiftCount, int ExpiringDocumentCount,
    DateTime AsOfUtc);
public sealed record RevenueReportRowDto(Guid StationId, string SourceType, decimal TotalAmount, int ReceiptCount);
public sealed record DepartureReportRowDto(Guid StationId, DateTime BusinessDate, string Status, int TripCount, int PassengerCount);
public sealed record ReconciliationReportRowDto(Guid StationId, DateTime BusinessDate, string ShiftCode, string Status,
    decimal TotalRevenue, decimal TotalExpense);
public sealed record ComplianceReportRowDto(Guid StationId, int ExpiringDocumentCount);

public sealed record BusDepartureChangedEto(Guid EventId, DateTimeOffset OccurredAtUtc, string? CorrelationId,
    Guid DepartureId, Guid StationId, string Status) : IntegrationEvent(EventId, OccurredAtUtc, CorrelationId)
{
    public const string EventName = "hcs.bus.departure.changed.v1";
}
public sealed record BusRevenueRecordedEto(Guid EventId, DateTimeOffset OccurredAtUtc, string? CorrelationId,
    Guid ReceiptId, Guid StationId, decimal Amount, string SourceType) : IntegrationEvent(EventId, OccurredAtUtc, CorrelationId)
{
    public const string EventName = "hcs.bus.revenue.recorded.v1";
}
public sealed record BusExpenseChangedEto(Guid EventId, DateTimeOffset OccurredAtUtc, string? CorrelationId,
    Guid ExpenseId, Guid StationId, decimal Amount, string Status) : IntegrationEvent(EventId, OccurredAtUtc, CorrelationId)
{
    public const string EventName = "hcs.bus.expense.changed.v1";
}
public sealed record BusReconciliationClosedEto(Guid EventId, DateTimeOffset OccurredAtUtc, string? CorrelationId,
    Guid DailyCloseId, Guid StationId, DateTime BusinessDate) : IntegrationEvent(EventId, OccurredAtUtc, CorrelationId)
{
    public const string EventName = "hcs.bus.reconciliation.closed.v1";
}
