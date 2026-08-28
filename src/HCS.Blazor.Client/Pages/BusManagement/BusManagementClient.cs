using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using HCS.BusManagementService.Contracts;
using Microsoft.Extensions.Http;

namespace HCS.Blazor.Client.Pages.BusManagement;

public sealed class BusManagementClient(IHttpClientFactory httpClientFactory)
{
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
}

internal sealed class BusManagementApiException(HttpStatusCode statusCode, string? body)
    : Exception($"Bus Management request failed with HTTP {(int)statusCode}.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string? ResponseBody { get; } = body;
}
