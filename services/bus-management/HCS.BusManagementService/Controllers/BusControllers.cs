using HCS.BusManagementService.Application;
using HCS.BusManagementService.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCS.BusManagementService.Controllers;

[ApiController, Authorize(Policy = BusPermissions.Stations), Route("api/bus-management/stations")]
public sealed class StationsController(BusManagementAppService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedBusDto<BusStationDto>> GetList(string? filter, int skip = 0, int take = 20, CancellationToken ct = default) => service.GetStationsAsync(filter, skip, take, ct);

    [HttpPost]
    [Authorize(Policy = BusPermissions.StationsCreate)]
    public Task<BusStationDto> Create(CreateBusStationDto input, CancellationToken ct) => service.CreateStationAsync(input, ct);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = BusPermissions.StationsUpdate)]
    public Task<BusStationDto> Update(Guid id, UpdateBusStationDto input, CancellationToken ct) => service.UpdateStationAsync(id, input, ct);

    [HttpPost("assignments")]
    [Authorize(Policy = BusPermissions.StationAssignmentsCreate)]
    public Task<StationAssignmentDto> Assign(AssignStationDto input, CancellationToken ct) => service.AssignStationAsync(input, ct);
}

[ApiController, Authorize(Policy = BusPermissions.MasterData), Route("api/bus-management/master-data")]
public sealed class MasterDataController(BusManagementAppService service) : ControllerBase
{
    [HttpGet("operators")]
    public Task<PagedBusDto<OperatorDto>> Operators(int skip = 0, int take = 100, CancellationToken ct = default) => service.GetOperatorsAsync(skip, take, ct);
    [HttpPost("operators")]
    [Authorize(Policy = BusPermissions.MasterDataCreate)]
    public Task<OperatorDto> CreateOperator(CreateOperatorDto input, CancellationToken ct) => service.CreateOperatorAsync(input, ct);
    [HttpGet("routes")]
    public Task<PagedBusDto<RouteDto>> Routes(int skip = 0, int take = 100, CancellationToken ct = default) => service.GetRoutesAsync(skip, take, ct);
    [HttpPost("routes")]
    [Authorize(Policy = BusPermissions.MasterDataCreate)]
    public Task<RouteDto> CreateRoute(CreateRouteDto input, CancellationToken ct) => service.CreateRouteAsync(input, ct);
    [HttpGet("vehicles")]
    public Task<PagedBusDto<VehicleDto>> Vehicles(int skip = 0, int take = 100, CancellationToken ct = default) => service.GetVehiclesAsync(skip, take, ct);
    [HttpPost("vehicles")]
    [Authorize(Policy = BusPermissions.MasterDataCreate)]
    public Task<VehicleDto> CreateVehicle(CreateVehicleDto input, CancellationToken ct) => service.CreateVehicleAsync(input, ct);
    [HttpGet("drivers")]
    public Task<PagedBusDto<DriverDto>> Drivers(int skip = 0, int take = 100, CancellationToken ct = default) => service.GetDriversAsync(skip, take, ct);
    [HttpPost("drivers")]
    [Authorize(Policy = BusPermissions.MasterDataCreate)]
    public Task<DriverDto> CreateDriver(CreateDriverDto input, CancellationToken ct) => service.CreateDriverAsync(input, ct);
}

[ApiController, Authorize(Policy = BusPermissions.OperatorsContracts), Route("api/bus-management/operators/contracts")]
public sealed class OperatorsContractsController(BusManagementAppService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedBusDto<CarrierContractDto>> GetList(Guid? stationId, DateTime? onDate, int skip = 0, int take = 20, CancellationToken ct = default) => service.GetCarrierContractsAsync(stationId, onDate, skip, take, ct);
    [HttpPost]
    [Authorize(Policy = BusPermissions.OperatorsContractsCreate)]
    public Task<CarrierContractDto> Create(CreateCarrierContractDto input, CancellationToken ct) => service.CreateCarrierContractAsync(input, ct);
}

[ApiController, Authorize(Policy = BusPermissions.FleetCompliance), Route("api/bus-management/compliance")]
public sealed class ComplianceController(BusManagementAppService service) : ControllerBase
{
    [HttpGet("vehicle-documents")]
    public Task<PagedBusDto<VehicleLegalDocumentDto>> GetVehicleDocuments(Guid? vehicleId, DateTime? expiringBefore, int skip = 0, int take = 20, CancellationToken ct = default) => service.GetVehicleLegalDocumentsAsync(vehicleId, expiringBefore, skip, take, ct);
    [HttpPost("vehicle-documents")]
    [Authorize(Policy = BusPermissions.FleetComplianceCreate)]
    public Task<VehicleLegalDocumentDto> CreateVehicleDocument(CreateVehicleLegalDocumentDto input, CancellationToken ct) => service.CreateVehicleLegalDocumentAsync(input, ct);
    [HttpPut("vehicle-documents/{id:guid}")]
    [Authorize(Policy = BusPermissions.FleetComplianceUpdate)]
    public Task<VehicleLegalDocumentDto> UpdateVehicleDocument(Guid id, UpdateVehicleLegalDocumentDto input, CancellationToken ct) => service.UpdateVehicleLegalDocumentAsync(id, input, ct);
}

[ApiController, Authorize(Policy = BusPermissions.Departures), Route("api/bus-management/departures")]
public sealed class DeparturesController(BusManagementAppService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedBusDto<DepartureDto>> GetList(Guid? stationId, DateTime? from, DateTime? to, string? status, int skip = 0, int take = 20, CancellationToken ct = default) => service.GetDeparturesAsync(stationId, from, to, status, skip, take, ct);
    [HttpPost]
    [Authorize(Policy = BusPermissions.DeparturesCreate)]
    public Task<DepartureDto> Create(CreateDepartureDto input, CancellationToken ct) => service.CreateDepartureAsync(input, ct);
    [HttpPost("{id:guid}/checks")]
    [Authorize(Policy = BusPermissions.DeparturesUpdate)]
    public Task<DepartureDto> UpdateChecks(Guid id, UpdateDepartureChecksDto input, CancellationToken ct) => service.UpdateDepartureChecksAsync(id, input, ct);
    [HttpPost("{id:guid}/ready")]
    [Authorize(Policy = BusPermissions.DeparturesUpdate)]
    public Task<DepartureDto> Ready(Guid id, CancellationToken ct) => service.TransitionDepartureAsync(id, "ready", ct);
    [HttpPost("{id:guid}/depart")]
    [Authorize(Policy = BusPermissions.DeparturesUpdate)]
    public Task<DepartureDto> Depart(Guid id, CancellationToken ct) => service.TransitionDepartureAsync(id, "depart", ct);
    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = BusPermissions.DeparturesUpdate)]
    public Task<DepartureDto> Complete(Guid id, CancellationToken ct) => service.TransitionDepartureAsync(id, "complete", ct);
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = BusPermissions.DeparturesUpdate)]
    public Task<DepartureDto> Cancel(Guid id, CancellationToken ct) => service.TransitionDepartureAsync(id, "cancel", ct);
    [HttpPost("{id:guid}/no-service")]
    [Authorize(Policy = BusPermissions.DeparturesUpdate)]
    public Task<DepartureDto> NoService(Guid id, CancellationToken ct) => service.TransitionDepartureAsync(id, "noservice", ct);
}

[ApiController, Authorize(Policy = BusPermissions.Revenue), Route("api/bus-management/revenue")]
public sealed class RevenueController(BusManagementAppService service) : ControllerBase
{
    [HttpGet("receipts")]
    public Task<PagedBusDto<RevenueReceiptDto>> GetReceipts(Guid? stationId, DateTime? from, DateTime? to, string? sourceType, int skip = 0, int take = 20, CancellationToken ct = default) => service.GetReceiptsAsync(stationId, from, to, sourceType, skip, take, ct);
    [HttpPost("receipts")]
    [Authorize(Policy = BusPermissions.RevenueCreate)]
    public Task<RevenueReceiptDto> CreateReceipt(CreateRevenueReceiptDto input, CancellationToken ct) => service.CreateReceiptAsync(input, ct);
    [HttpPost("tariffs")]
    [Authorize(Policy = BusPermissions.RevenueCreate)]
    public Task<TariffDto> CreateTariff(CreateTariffDto input, CancellationToken ct) => service.CreateTariffAsync(input, ct);
    [HttpGet("parking/tariffs")]
    [Authorize(Policy = BusPermissions.RevenueParking)]
    public Task<PagedBusDto<ParkingTariffDto>> GetParkingTariffs(Guid? stationId, string? vehicleType, DateTime? effectiveOn,
        int skip = 0, int take = 20, CancellationToken ct = default) => service.GetParkingTariffsAsync(stationId, vehicleType, effectiveOn, skip, take, ct);
    [HttpPost("parking/tariffs")]
    [Authorize(Policy = BusPermissions.RevenueParkingCreate)]
    public Task<ParkingTariffDto> CreateParkingTariff(CreateParkingTariffDto input, CancellationToken ct) => service.CreateParkingTariffAsync(input, ct);
    [HttpGet("parking/spots")]
    [Authorize(Policy = BusPermissions.RevenueParking)]
    public Task<PagedBusDto<ParkingSpotDto>> GetParkingSpots(Guid? stationId, string? filter, bool includeInactive = false,
        int skip = 0, int take = 50, CancellationToken ct = default) => service.GetParkingSpotsAsync(stationId, filter, includeInactive, skip, take, ct);
    [HttpPost("parking/spots")]
    [Authorize(Policy = BusPermissions.RevenueParkingCreate)]
    public Task<ParkingSpotDto> CreateParkingSpot(CreateParkingSpotDto input, CancellationToken ct) => service.CreateParkingSpotAsync(input, ct);
    [HttpPut("parking/spots/{id:guid}")]
    [Authorize(Policy = BusPermissions.RevenueParkingUpdate)]
    public Task<ParkingSpotDto> UpdateParkingSpot(Guid id, UpdateParkingSpotDto input, CancellationToken ct) => service.UpdateParkingSpotAsync(id, input, ct);
    [HttpGet("parking/reservations")]
    [Authorize(Policy = BusPermissions.RevenueParking)]
    public Task<PagedBusDto<ParkingReservationDto>> GetParkingReservations(Guid? stationId, DateTime? fromUtc, DateTime? toUtc,
        string? status, string? vehiclePlateNumber, int skip = 0, int take = 50, CancellationToken ct = default) =>
        service.GetParkingReservationsAsync(stationId, fromUtc, toUtc, status, vehiclePlateNumber, skip, take, ct);
    [HttpPost("parking/reservations")]
    [Authorize(Policy = BusPermissions.RevenueParkingCreate)]
    public Task<ParkingReservationDto> CreateParkingReservation(CreateParkingReservationDto input, CancellationToken ct) =>
        service.CreateParkingReservationAsync(input, ct);
    [HttpPost("parking/reservations/{id:guid}/check-in")]
    [Authorize(Policy = BusPermissions.RevenueParkingUpdate)]
    public Task<ParkingReservationDto> CheckInParkingReservation(Guid id, CancellationToken ct) =>
        service.TransitionParkingReservationAsync(id, "check-in", ct);
    [HttpPost("parking/reservations/{id:guid}/cancel")]
    [Authorize(Policy = BusPermissions.RevenueParkingUpdate)]
    public Task<ParkingReservationDto> CancelParkingReservation(Guid id, CancellationToken ct) =>
        service.TransitionParkingReservationAsync(id, "cancel", ct);
    [HttpPost("parking/reservations/{id:guid}/complete")]
    [Authorize(Policy = BusPermissions.RevenueParkingUpdate)]
    public Task<ParkingReservationDto> CompleteParkingReservation(Guid id, CancellationToken ct) =>
        service.TransitionParkingReservationAsync(id, "complete", ct);
    [HttpGet("parking/sessions")]
    [Authorize(Policy = BusPermissions.RevenueParking)]
    public Task<PagedBusDto<ParkingSessionDto>> GetParkingSessions(Guid? stationId, DateTime? from, DateTime? to, string? status,
        string? vehiclePlateNumber, int skip = 0, int take = 20, CancellationToken ct = default) => service.GetParkingSessionsAsync(stationId, from, to, status, vehiclePlateNumber, skip, take, ct);
    [HttpPost("parking/sessions")]
    [Authorize(Policy = BusPermissions.RevenueParkingCreate)]
    public Task<ParkingSessionDto> CreateParkingSession(CreateParkingSessionDto input, CancellationToken ct) => service.CreateParkingSessionAsync(input, ct);
    [HttpPost("parking/sessions/{id:guid}/close")]
    [Authorize(Policy = BusPermissions.RevenueParkingUpdate)]
    public Task<ParkingSessionDto> CloseParkingSession(Guid id, CloseParkingSessionDto input, CancellationToken ct) => service.CloseParkingSessionAsync(id, input, ct);
    [HttpPost("parking/sessions/{id:guid}/cancel")]
    [Authorize(Policy = BusPermissions.RevenueParkingUpdate)]
    public Task<ParkingSessionDto> CancelParkingSession(Guid id, CancelParkingSessionDto input, CancellationToken ct) => service.CancelParkingSessionAsync(id, input, ct);
}

[ApiController, Route("api/bus-management")]
public sealed class FinanceController(BusManagementAppService service) : ControllerBase
{
    [HttpGet("expenses")]
    [Authorize(Policy = BusPermissions.Expenses)]
    public Task<PagedBusDto<ExpenseDto>> GetExpenses(Guid? stationId, DateTime? from, DateTime? to, string? status,
        int skip = 0, int take = 20, CancellationToken ct = default) => service.GetExpensesAsync(stationId, from, to, status, skip, take, ct);
    [HttpPost("expenses")]
    [Authorize(Policy = BusPermissions.ExpensesCreate)]
    public Task<ExpenseDto> CreateExpense(CreateExpenseDto input, CancellationToken ct) => service.CreateExpenseAsync(input, ct);
    [HttpPost("expenses/{id:guid}/submit")]
    [Authorize(Policy = BusPermissions.ExpensesCreate)]
    public Task<ExpenseDto> SubmitExpense(Guid id, CancellationToken ct) => service.SubmitExpenseAsync(id, ct);
    [HttpPost("expenses/{id:guid}/approve")]
    [Authorize(Policy = BusPermissions.ExpensesApprove)]
    public Task<ExpenseDto> ApproveExpense(Guid id, CancellationToken ct) => service.ApproveExpenseAsync(id, ct);
    [HttpGet("premises")]
    [Authorize(Policy = BusPermissions.Premises)]
    public Task<PagedBusDto<PremisesUnitDto>> GetPremises(Guid? stationId, string? filter, int skip = 0, int take = 20, CancellationToken ct = default) => service.GetPremisesUnitsAsync(stationId, filter, skip, take, ct);
    [HttpPost("premises")]
    [Authorize(Policy = BusPermissions.PremisesCreate)]
    public Task<PremisesUnitDto> CreatePremises(CreatePremisesUnitDto input, CancellationToken ct) => service.CreatePremisesUnitAsync(input, ct);
    [HttpGet("premises/leases")]
    [Authorize(Policy = BusPermissions.Premises)]
    public Task<PagedBusDto<LeaseContractDto>> GetLeases(Guid? stationId, DateTime? from, DateTime? to, string? status, int skip = 0, int take = 20, CancellationToken ct = default) => service.GetLeasesAsync(stationId, from, to, status, skip, take, ct);
    [HttpPost("premises/leases")]
    [Authorize(Policy = BusPermissions.PremisesCreate)]
    public Task<LeaseContractDto> CreateLease(CreateLeaseContractDto input, CancellationToken ct) => service.CreateLeaseAsync(input, ct);
    [HttpPost("reconciliation/shifts")]
    [Authorize(Policy = BusPermissions.ReconciliationCreate)]
    public Task<SettlementDto> CreateShift(CreateShiftSettlementDto input, CancellationToken ct) => service.CreateShiftSettlementAsync(input, ct);
    [HttpPost("reconciliation/shifts/{id:guid}/submit")]
    [Authorize(Policy = BusPermissions.Reconciliation)]
    public Task<SettlementDto> SubmitShift(Guid id, CancellationToken ct) => service.TransitionSettlementAsync(id, "submit", ct);
    [HttpPost("reconciliation/shifts/{id:guid}/check")]
    [Authorize(Policy = BusPermissions.ReconciliationCheck)]
    public Task<SettlementDto> CheckShift(Guid id, CancellationToken ct) => service.TransitionSettlementAsync(id, "check", ct);
    [HttpPost("reconciliation/shifts/{id:guid}/approve")]
    [Authorize(Policy = BusPermissions.ReconciliationApprove)]
    public Task<SettlementDto> ApproveShift(Guid id, CancellationToken ct) => service.TransitionSettlementAsync(id, "approve", ct);
    [HttpPost("reconciliation/shifts/{id:guid}/close")]
    [Authorize(Policy = BusPermissions.ReconciliationClose)]
    public Task<SettlementDto> CloseShift(Guid id, CancellationToken ct) => service.TransitionSettlementAsync(id, "close", ct);
    [HttpPost("reconciliation/daily/close")]
    [Authorize(Policy = BusPermissions.ReconciliationClose)]
    public Task<DailyCloseDto> CloseDaily(CloseDailyDto input, CancellationToken ct) => service.CloseDailyAsync(input, ct);
    [HttpGet("reconciliation/adjustments")]
    [Authorize(Policy = BusPermissions.Reconciliation)]
    public Task<PagedBusDto<AdjustmentDto>> GetAdjustments(Guid? stationId, string? status, DateTime? from, DateTime? to,
        int skip = 0, int take = 20, CancellationToken ct = default) => service.GetAdjustmentsAsync(stationId, status, from, to, skip, take, ct);
    [HttpPost("reconciliation/adjustments")]
    [Authorize(Policy = BusPermissions.ReconciliationAdjust)]
    public Task<AdjustmentDto> CreateAdjustment(CreateAdjustmentDto input, CancellationToken ct) => service.CreateAdjustmentAsync(input, ct);
    [HttpPost("reconciliation/adjustments/{id:guid}/approve")]
    [Authorize(Policy = BusPermissions.ReconciliationAdjustApprove)]
    public Task<AdjustmentDto> ApproveAdjustment(Guid id, CancellationToken ct) => service.ApproveAdjustmentAsync(id, ct);
}

[ApiController, Route("api/bus-management")]
public sealed class ReportsController(BusManagementAppService service, BusReportExportService exportService) : ControllerBase
{
    [HttpGet("dashboard"), Authorize(Policy = BusPermissions.Dashboard)]
    public Task<DashboardSummaryDto> Dashboard(DateTime? from, DateTime? to, Guid? stationId, CancellationToken ct = default)
    {
        var end = to?.Date ?? DateTime.UtcNow.Date;
        var start = from?.Date ?? new DateTime(end.Year, end.Month, 1);
        return service.GetDashboardAsync(start, end, stationId, ct);
    }

    [HttpGet("reports/revenue"), Authorize(Policy = BusPermissions.Reports)]
    public Task<IReadOnlyList<RevenueReportRowDto>> Revenue(DateTime? from, DateTime? to, Guid? stationId, CancellationToken ct = default)
    {
        var end = to?.Date ?? DateTime.UtcNow.Date;
        var start = from?.Date ?? new DateTime(end.Year, end.Month, 1);
        return service.GetRevenueReportAsync(start, end, stationId, ct);
    }

    [HttpGet("reports/departures"), Authorize(Policy = BusPermissions.Reports)]
    public Task<IReadOnlyList<DepartureReportRowDto>> Departures(DateTime? from, DateTime? to, Guid? stationId, CancellationToken ct = default)
    {
        var end = to?.Date ?? DateTime.UtcNow.Date;
        var start = from?.Date ?? new DateTime(end.Year, end.Month, 1);
        return service.GetDepartureReportAsync(start, end, stationId, ct);
    }

    [HttpGet("reports/reconciliation"), Authorize(Policy = BusPermissions.Reports)]
    public Task<IReadOnlyList<ReconciliationReportRowDto>> Reconciliation(DateTime? from, DateTime? to, Guid? stationId, CancellationToken ct = default)
    {
        var end = to?.Date ?? DateTime.UtcNow.Date;
        var start = from?.Date ?? new DateTime(end.Year, end.Month, 1);
        return service.GetReconciliationReportAsync(start, end, stationId, ct);
    }

    [HttpGet("reports/compliance"), Authorize(Policy = BusPermissions.Reports)]
    public Task<IReadOnlyList<ComplianceReportRowDto>> Compliance(Guid? stationId, DateTime? asOf = null, CancellationToken ct = default) => service.GetComplianceReportAsync(stationId, asOf, ct);

    [HttpGet("exports/{reportType}")]
    [Authorize(Policy = BusPermissions.ReportsExport)]
    public async Task<IActionResult> Export(string reportType, string format = "xlsx", DateTime? from = null, DateTime? to = null,
        Guid? stationId = null, CancellationToken ct = default)
    {
        var end = to?.Date ?? DateTime.UtcNow.Date;
        var start = from?.Date ?? new DateTime(end.Year, end.Month, 1);
        if (start > end) return BadRequest("from must be before or equal to to.");
        try
        {
            var result = await exportService.ExportAsync(reportType, format, start, end, stationId, ct);
            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
