using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HCS.BusManagementService.Contracts;
using Microsoft.Extensions.Http;

namespace HCS.Blazor.Client.Pages.BusManagement;

public sealed class BusManagementClient(IHttpClientFactory httpClientFactory)
{
    public Task<PagedBusDto<BusStationDto>> GetStationsAsync(string? filter = null, int skip = 0, int take = 50,
        CancellationToken cancellationToken = default) =>
        GetRequiredAsync<PagedBusDto<BusStationDto>>(BuildUri("/api/bus-management/stations", ("filter", filter), ("skip", skip), ("take", take)), cancellationToken);

    public Task<PagedBusDto<BusStationDto>> GetStationScopeAsync(string? filter = null, int skip = 0, int take = 100,
        CancellationToken cancellationToken = default) =>
        GetRequiredAsync<PagedBusDto<BusStationDto>>(BuildUri("/api/bus-management/station-scope", ("filter", filter), ("skip", skip), ("take", take)), cancellationToken);

    public Task<PagedBusDto<OperatorDto>> GetOperatorsAsync(int skip = 0, int take = 100,
        CancellationToken cancellationToken = default) =>
        GetRequiredAsync<PagedBusDto<OperatorDto>>(BuildUri("/api/bus-management/master-data/operators", ("skip", skip), ("take", take)), cancellationToken);

    public Task<PagedBusDto<RouteDto>> GetRoutesAsync(int skip = 0, int take = 100,
        CancellationToken cancellationToken = default) =>
        GetRequiredAsync<PagedBusDto<RouteDto>>(BuildUri("/api/bus-management/master-data/routes", ("skip", skip), ("take", take)), cancellationToken);

    public Task<PagedBusDto<VehicleDto>> GetVehiclesAsync(int skip = 0, int take = 100,
        CancellationToken cancellationToken = default) =>
        GetRequiredAsync<PagedBusDto<VehicleDto>>(BuildUri("/api/bus-management/master-data/vehicles", ("skip", skip), ("take", take)), cancellationToken);

    public Task<PagedBusDto<DriverDto>> GetDriversAsync(int skip = 0, int take = 100,
        CancellationToken cancellationToken = default) =>
        GetRequiredAsync<PagedBusDto<DriverDto>>(BuildUri("/api/bus-management/master-data/drivers", ("skip", skip), ("take", take)), cancellationToken);

    public Task<PagedBusDto<CarrierContractDto>> GetCarrierContractsAsync(Guid? stationId = null, DateTime? onDate = null,
        int skip = 0, int take = 50, CancellationToken cancellationToken = default) =>
        GetRequiredAsync<PagedBusDto<CarrierContractDto>>(BuildUri("/api/bus-management/operators/contracts",
            ("stationId", stationId), ("onDate", onDate), ("skip", skip), ("take", take)), cancellationToken);

    public Task<PagedBusDto<VehicleLegalDocumentDto>> GetVehicleLegalDocumentsAsync(Guid? vehicleId = null,
        DateTime? expiringBefore = null, int skip = 0, int take = 50, CancellationToken cancellationToken = default) =>
        GetRequiredAsync<PagedBusDto<VehicleLegalDocumentDto>>(BuildUri("/api/bus-management/compliance/vehicle-documents",
            ("vehicleId", vehicleId), ("expiringBefore", expiringBefore), ("skip", skip), ("take", take)), cancellationToken);

    public Task<PagedBusDto<DepartureDto>> GetDeparturesAsync(Guid? stationId = null, DateTime? from = null,
        DateTime? to = null, string? status = null, int skip = 0, int take = 50,
        CancellationToken cancellationToken = default) =>
        GetRequiredAsync<PagedBusDto<DepartureDto>>(BuildUri("/api/bus-management/departures",
            ("stationId", stationId), ("from", from), ("to", to), ("status", status), ("skip", skip), ("take", take)), cancellationToken);

    public Task<PagedBusDto<RevenueReceiptDto>> GetReceiptsAsync(Guid? stationId = null, DateTime? from = null,
        DateTime? to = null, string? sourceType = null, int skip = 0, int take = 50,
        CancellationToken cancellationToken = default) =>
        GetRequiredAsync<PagedBusDto<RevenueReceiptDto>>(BuildUri("/api/bus-management/revenue/receipts",
            ("stationId", stationId), ("from", from), ("to", to), ("sourceType", sourceType), ("skip", skip), ("take", take)), cancellationToken);

    public Task<PagedBusDto<ExpenseDto>> GetExpensesAsync(Guid? stationId = null, DateTime? from = null,
        DateTime? to = null, string? status = null, int skip = 0, int take = 50,
        CancellationToken cancellationToken = default) =>
        GetRequiredAsync<PagedBusDto<ExpenseDto>>(BuildUri("/api/bus-management/expenses",
            ("stationId", stationId), ("from", from), ("to", to), ("status", status), ("skip", skip), ("take", take)), cancellationToken);

    public Task<PagedBusDto<PremisesUnitDto>> GetPremisesAsync(Guid? stationId = null, string? filter = null,
        int skip = 0, int take = 50, CancellationToken cancellationToken = default) =>
        GetRequiredAsync<PagedBusDto<PremisesUnitDto>>(BuildUri("/api/bus-management/premises",
            ("stationId", stationId), ("filter", filter), ("skip", skip), ("take", take)), cancellationToken);

    public Task<PagedBusDto<LeaseContractDto>> GetLeasesAsync(Guid? stationId = null, DateTime? from = null,
        DateTime? to = null, string? status = null, int skip = 0, int take = 50,
        CancellationToken cancellationToken = default) =>
        GetRequiredAsync<PagedBusDto<LeaseContractDto>>(BuildUri("/api/bus-management/premises/leases",
            ("stationId", stationId), ("from", from), ("to", to), ("status", status), ("skip", skip), ("take", take)), cancellationToken);

    public Task<PagedBusDto<AdjustmentDto>> GetAdjustmentsAsync(Guid? stationId = null, string? status = null,
        DateTime? from = null, DateTime? to = null, int skip = 0, int take = 50,
        CancellationToken cancellationToken = default) =>
        GetRequiredAsync<PagedBusDto<AdjustmentDto>>(BuildUri("/api/bus-management/reconciliation/adjustments",
            ("stationId", stationId), ("status", status), ("from", from), ("to", to), ("skip", skip), ("take", take)), cancellationToken);

    public Task<PagedBusDto<SettlementDto>> GetSettlementsAsync(Guid? stationId = null, DateTime? from = null,
        DateTime? to = null, string? status = null, int skip = 0, int take = 50,
        CancellationToken cancellationToken = default) =>
        GetRequiredAsync<PagedBusDto<SettlementDto>>(BuildUri("/api/bus-management/reconciliation/shifts",
            ("stationId", stationId), ("from", from), ("to", to), ("status", status), ("skip", skip), ("take", take)), cancellationToken);

    public Task<BusStationDto> CreateStationAsync(CreateBusStationDto input, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<BusStationDto>(HttpMethod.Post, "/api/bus-management/stations", input, cancellationToken);

    public Task<BusStationDto> UpdateStationAsync(Guid id, UpdateBusStationDto input, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<BusStationDto>(HttpMethod.Put, $"/api/bus-management/stations/{id:D}", input, cancellationToken);

    public Task<OperatorDto> CreateOperatorAsync(CreateOperatorDto input, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<OperatorDto>(HttpMethod.Post, "/api/bus-management/master-data/operators", input, cancellationToken);

    public Task<RouteDto> CreateRouteAsync(CreateRouteDto input, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<RouteDto>(HttpMethod.Post, "/api/bus-management/master-data/routes", input, cancellationToken);

    public Task<VehicleDto> CreateVehicleAsync(CreateVehicleDto input, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<VehicleDto>(HttpMethod.Post, "/api/bus-management/master-data/vehicles", input, cancellationToken);

    public Task<DriverDto> CreateDriverAsync(CreateDriverDto input, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<DriverDto>(HttpMethod.Post, "/api/bus-management/master-data/drivers", input, cancellationToken);

    public Task<CarrierContractDto> CreateCarrierContractAsync(CreateCarrierContractDto input, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<CarrierContractDto>(HttpMethod.Post, "/api/bus-management/operators/contracts", input, cancellationToken);

    public Task<VehicleLegalDocumentDto> CreateVehicleLegalDocumentAsync(CreateVehicleLegalDocumentDto input, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<VehicleLegalDocumentDto>(HttpMethod.Post, "/api/bus-management/compliance/vehicle-documents", input, cancellationToken);

    public Task<VehicleLegalDocumentDto> UpdateVehicleLegalDocumentAsync(Guid id, UpdateVehicleLegalDocumentDto input, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<VehicleLegalDocumentDto>(HttpMethod.Put, $"/api/bus-management/compliance/vehicle-documents/{id:D}", input, cancellationToken);

    public Task<DepartureDto> CreateDepartureAsync(CreateDepartureDto input, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<DepartureDto>(HttpMethod.Post, "/api/bus-management/departures", input, cancellationToken);

    public Task<DepartureDto> UpdateDepartureChecksAsync(Guid id, UpdateDepartureChecksDto input, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<DepartureDto>(HttpMethod.Post, $"/api/bus-management/departures/{id:D}/checks", input, cancellationToken);

    public Task<DepartureDto> TransitionDepartureAsync(Guid id, string action, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<DepartureDto>(HttpMethod.Post, $"/api/bus-management/departures/{id:D}/{action}", null, cancellationToken);

    public Task<RevenueReceiptDto> CreateReceiptAsync(CreateRevenueReceiptDto input, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<RevenueReceiptDto>(HttpMethod.Post, "/api/bus-management/revenue/receipts", input, cancellationToken);

    public Task<TariffDto> CreateTariffAsync(CreateTariffDto input, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<TariffDto>(HttpMethod.Post, "/api/bus-management/revenue/tariffs", input, cancellationToken);

    public Task<PagedBusDto<TariffDto>> GetTariffsAsync(Guid? stationId = null, Guid? routeId = null,
        string? vehicleType = null, DateTime? effectiveOn = null, int skip = 0, int take = 50,
        CancellationToken cancellationToken = default) =>
        GetRequiredAsync<PagedBusDto<TariffDto>>(BuildUri("/api/bus-management/revenue/tariffs",
            ("stationId", stationId), ("routeId", routeId), ("vehicleType", vehicleType), ("effectiveOn", effectiveOn), ("skip", skip), ("take", take)), cancellationToken);

    public Task<ExpenseDto> CreateExpenseAsync(CreateExpenseDto input, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<ExpenseDto>(HttpMethod.Post, "/api/bus-management/expenses", input, cancellationToken);

    public Task<ExpenseDto> SubmitExpenseAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<ExpenseDto>(HttpMethod.Post, $"/api/bus-management/expenses/{id:D}/submit", null, cancellationToken);

    public Task<ExpenseDto> ApproveExpenseAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<ExpenseDto>(HttpMethod.Post, $"/api/bus-management/expenses/{id:D}/approve", null, cancellationToken);

    public Task<PremisesUnitDto> CreatePremisesAsync(CreatePremisesUnitDto input, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<PremisesUnitDto>(HttpMethod.Post, "/api/bus-management/premises", input, cancellationToken);

    public Task<LeaseContractDto> CreateLeaseAsync(CreateLeaseContractDto input, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<LeaseContractDto>(HttpMethod.Post, "/api/bus-management/premises/leases", input, cancellationToken);

    public Task<SettlementDto> CreateShiftAsync(CreateShiftSettlementDto input, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<SettlementDto>(HttpMethod.Post, "/api/bus-management/reconciliation/shifts", input, cancellationToken);

    public Task<SettlementDto> TransitionShiftAsync(Guid id, string action, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<SettlementDto>(HttpMethod.Post, $"/api/bus-management/reconciliation/shifts/{id:D}/{action}", null, cancellationToken);

    public Task<DailyCloseDto> CloseDailyAsync(CloseDailyDto input, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<DailyCloseDto>(HttpMethod.Post, "/api/bus-management/reconciliation/daily/close", input, cancellationToken);

    public Task<AdjustmentDto> CreateAdjustmentAsync(CreateAdjustmentDto input, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<AdjustmentDto>(HttpMethod.Post, "/api/bus-management/reconciliation/adjustments", input, cancellationToken);

    public Task<AdjustmentDto> ApproveAdjustmentAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendRequiredAsync<AdjustmentDto>(HttpMethod.Post, $"/api/bus-management/reconciliation/adjustments/{id:D}/approve", null, cancellationToken);

    public Task<DashboardSummaryDto?> GetDashboardAsync(DateTime from, DateTime to, Guid? stationId = null,
        CancellationToken cancellationToken = default)
    {
        var query = $"from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";
        if (stationId.HasValue) query += $"&stationId={stationId.Value:D}";
        return GetAsync<DashboardSummaryDto>($"/api/bus-management/dashboard?{query}", cancellationToken);
    }

    public Task<IReadOnlyList<RevenueReportRowDto>?> GetRevenueReportAsync(DateTime from, DateTime to,
        Guid? stationId = null, CancellationToken cancellationToken = default)
    {
        var query = $"from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";
        if (stationId.HasValue) query += $"&stationId={stationId.Value:D}";
        return GetAsync<IReadOnlyList<RevenueReportRowDto>>($"/api/bus-management/reports/revenue?{query}", cancellationToken);
    }

    private async Task<T?> GetAsync<T>(string uri, CancellationToken cancellationToken)
    {
        using var response = await httpClientFactory.CreateClient("HCS.Bff").GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new BusManagementApiException(response.StatusCode, body);
        }
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    private async Task<T> GetRequiredAsync<T>(string uri, CancellationToken cancellationToken)
    {
        return await GetAsync<T>(uri, cancellationToken)
            ?? throw new BusManagementApiException(HttpStatusCode.NoContent, "Gateway returned an empty response.");
    }

    private async Task<T> SendRequiredAsync<T>(HttpMethod method, string uri, object? payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);
        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        using var response = await httpClientFactory.CreateClient("HCS.Bff").SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new BusManagementApiException(HttpStatusCode.NoContent, "Gateway returned an empty response.");
    }

    private static string BuildUri(string endpoint, params (string Name, object? Value)[] values)
    {
        var query = values.Where(x => x.Value is not null && (!x.Value.GetType().IsValueType || x.Value is not Guid guid || guid != Guid.Empty))
            .Select(x => $"{Uri.EscapeDataString(x.Name)}={Uri.EscapeDataString(FormatValue(x.Value!))}")
            .ToArray();
        return query.Length == 0 ? endpoint : $"{endpoint}?{string.Join('&', query)}";
    }

    private static string FormatValue(object value) => value switch
    {
        DateTime date => date.ToString("O"),
        Guid id => id.ToString("D"),
        bool boolean => boolean ? "true" : "false",
        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
    };

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new BusManagementApiException(response.StatusCode, body);
    }
}

internal sealed class BusManagementApiException(HttpStatusCode statusCode, string? body)
    : Exception($"Bus Management request failed with HTTP {(int)statusCode}.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string? ResponseBody { get; } = body;
}
