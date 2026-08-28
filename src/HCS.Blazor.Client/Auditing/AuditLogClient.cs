using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HCS.Blazor.Client.Services;
using HCS.Auditing;
using Volo.Abp.Application.Dtos;

namespace HCS.Blazor.Client.Auditing;

public sealed class AuditLogClient(IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<PagedResultDto<AuditLogDto>> GetListAsync(
        GetAuditLogsInput input,
        CancellationToken cancellationToken = default) =>
        GetAsync<PagedResultDto<AuditLogDto>>(BuildListUri(input), cancellationToken);

    public Task<AuditLogDetailDto> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<AuditLogDetailDto>($"api/audit-logs/{id:D}", cancellationToken);

    private async Task<T> GetAsync<T>(string uri, CancellationToken cancellationToken)
    {
        using var response = await httpClientFactory.CreateClient("HCS.Bff").GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new BffApiException(response.StatusCode, body);
        }

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new BffApiException(HttpStatusCode.NoContent, "Gateway returned an empty response.");
    }

    private static string BuildListUri(GetAuditLogsInput input)
    {
        var query = $"api/audit-logs?skipCount={Math.Max(0, input.SkipCount)}&maxResultCount={Math.Clamp(input.MaxResultCount, 1, 100)}";
        Add(ref query, "sorting", input.Sorting);
        Add(ref query, "filter", input.Filter);
        Add(ref query, "userId", input.UserId?.ToString("D"));
        Add(ref query, "userName", input.UserName);
        Add(ref query, "startTime", ToUtcString(input.StartTime));
        Add(ref query, "endTime", ToUtcString(input.EndTime));
        Add(ref query, "endTimeExclusive", ToUtcString(input.EndTimeExclusive));
        Add(ref query, "httpStatusCode", input.HttpStatusCode?.ToString(CultureInfo.InvariantCulture));
        Add(ref query, "httpMethod", input.HttpMethod);
        Add(ref query, "clientIpAddress", input.ClientIpAddress);
        Add(ref query, "browserInfo", input.BrowserInfo);
        Add(ref query, "sourceService", input.SourceService);
        Add(ref query, "applicationName", input.ApplicationName);
        Add(ref query, "hasException", input.HasException?.ToString().ToLowerInvariant());
        Add(ref query, "correlationId", input.CorrelationId);
        Add(ref query, "action", input.Action);
        Add(ref query, "url", input.Url);
        return query;
    }

    private static string? ToUtcString(DateTime? value) =>
        value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static void Add(ref string query, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        query += $"&{name}={Uri.EscapeDataString(value)}";
    }
}
