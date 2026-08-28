using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HCS.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Dtos;

namespace HCS.Auditing;

[Authorize(HCSPermissions.AuditViewer.Default)]
public class AuditViewerAppService(
    IAuditRecordProjectionRepository repository,
    ILogger<AuditViewerAppService> logger) : HCSAppService, IAuditViewerAppService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private const int MaxSkipCount = 100_000;
    private const int MaxFilterLength = 256;

    public virtual async Task<PagedResultDto<AuditLogDto>> GetListAsync(GetAuditLogsInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var filter = Normalize(input.Filter);
        var filterUserId = Guid.TryParse(filter, out var parsedFilterUserId) ? parsedFilterUserId : (Guid?)null;
        var userName = Normalize(input.UserName);
        var httpMethod = Normalize(input.HttpMethod)?.ToUpperInvariant();
        var clientIpAddress = Normalize(input.ClientIpAddress);
        var browserInfo = Normalize(input.BrowserInfo);
        var sourceService = Normalize(input.SourceService);
        var applicationName = Normalize(input.ApplicationName);
        var correlationId = Normalize(input.CorrelationId);
        var action = Normalize(input.Action);
        var url = Normalize(input.Url, 2048);
        var startTime = NormalizeUtc(input.StartTime);
        var endTime = NormalizeUtc(input.EndTime);
        var endTimeExclusive = NormalizeUtc(input.EndTimeExclusive);

        var query = await repository.GetQueryableAsync();
        query = query
            .WhereIf(input.UserId.HasValue, record => record.UserId == input.UserId)
            .WhereIf(userName != null, record => record.UserName == userName)
            .WhereIf(startTime.HasValue, record => record.ExecutionTime >= startTime)
            .WhereIf(endTimeExclusive.HasValue, record => record.ExecutionTime < endTimeExclusive)
            .WhereIf(!endTimeExclusive.HasValue && endTime.HasValue, record => record.ExecutionTime <= endTime)
            .WhereIf(input.HttpStatusCode.HasValue, record => record.HttpStatusCode == input.HttpStatusCode)
            .WhereIf(httpMethod != null, record => record.HttpMethod == httpMethod)
            .WhereIf(clientIpAddress != null, record => record.ClientIpAddress == clientIpAddress)
            .WhereIf(browserInfo != null, record => record.BrowserInfo != null && record.BrowserInfo.Contains(browserInfo!))
            .WhereIf(sourceService != null, record => record.SourceService == sourceService)
            .WhereIf(applicationName != null, record => record.ApplicationName == applicationName)
            .WhereIf(correlationId != null, record => record.CorrelationId == correlationId)
            .WhereIf(action != null, record => record.ActionName != null && record.ActionName.Contains(action!))
            .WhereIf(url != null, record => record.Url != null && record.Url.Contains(url!))
            .WhereIf(
                input.HasException.HasValue,
                record => input.HasException == true
                    ? record.Exceptions != null && record.Exceptions.Trim() != string.Empty
                    : record.Exceptions == null || record.Exceptions.Trim() == string.Empty)
            .WhereIf(
                filter != null,
                record =>
                    (filterUserId.HasValue && record.UserId == filterUserId) ||
                    (record.UserName != null && record.UserName.Contains(filter!)) ||
                    (record.ClientIpAddress != null && record.ClientIpAddress.Contains(filter!)) ||
                    (record.BrowserInfo != null && record.BrowserInfo.Contains(filter!)) ||
                    (record.ActionName != null && record.ActionName.Contains(filter!)) ||
                    (record.Url != null && record.Url.Contains(filter!)) ||
                    record.SourceService.Contains(filter!) ||
                    (record.ApplicationName != null && record.ApplicationName.Contains(filter!)) ||
                    (record.CorrelationId != null && record.CorrelationId.Contains(filter!)));

        var count = await AsyncExecuter.CountAsync(query);
        query = ApplySorting(query, input.Sorting);

        var skipCount = Math.Min(Math.Max(0, input.SkipCount), MaxSkipCount);
        var pageSize = input.MaxResultCount <= 0 ? DefaultPageSize : Math.Min(input.MaxResultCount, MaxPageSize);
        var records = await AsyncExecuter.ToListAsync(query.Skip(skipCount).Take(pageSize).Select(record => new AuditLogDto
        {
            Id = record.Id,
            UserId = record.UserId,
            UserName = record.UserName,
            ExecutionTime = record.ExecutionTime,
            ExecutionDuration = record.ExecutionDuration,
            HttpMethod = record.HttpMethod,
            Url = record.Url,
            HttpStatusCode = record.HttpStatusCode,
            CorrelationId = record.CorrelationId,
            ClientIpAddress = record.ClientIpAddress,
            HasException = record.Exceptions != null && record.Exceptions.Trim() != string.Empty,
            SourceService = record.SourceService,
            ActionName = record.ActionName,
            ApplicationName = record.ApplicationName
        }));

        return new PagedResultDto<AuditLogDto>(count, records);
    }

    public virtual async Task<AuditLogDetailDto> GetAsync(Guid id)
    {
        var record = await repository.GetAsync(id);
        var dto = new AuditLogDetailDto
        {
            BrowserInfo = record.BrowserInfo,
            Exceptions = record.Exceptions,
            Comments = record.Comments,
            Actions = DeserializeActions(record.ActionsJson, id),
            EntityChanges = DeserializeEntityChanges(record.EntityChangesJson, id)
        };
        Map(record, dto);
        return dto;
    }

    private List<AuditLogActionDto> DeserializeActions(string? json, Guid recordId)
    {
        var actions = Deserialize<List<AuditLogActionDto?>>(json, recordId, nameof(AuditRecordProjection.ActionsJson)) ?? [];
        return actions
            .Where(action => action is not null)
            .Select(action =>
            {
                action!.Parameters = null;
                return action;
            })
            .ToList();
    }

    private List<AuditEntityChangeDto> DeserializeEntityChanges(string? json, Guid recordId)
    {
        var changes = Deserialize<List<AuditEntityChangeDto?>>(json, recordId, nameof(AuditRecordProjection.EntityChangesJson)) ?? [];
        return changes.Where(change => change is not null).Select(change => change!).ToList();
    }

    private T? Deserialize<T>(string? json, Guid recordId, string fieldName)
        where T : class
    {
        if (json.IsNullOrWhiteSpace())
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Unable to deserialize audit detail field {FieldName} for record {RecordId}.", fieldName, recordId);
            return null;
        }
    }

    private static IQueryable<AuditRecordProjection> ApplySorting(
        IQueryable<AuditRecordProjection> query,
        string? sorting)
    {
        var parts = sorting.IsNullOrWhiteSpace()
            ? []
            : sorting.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var field = parts.Length == 0 ? nameof(AuditRecordProjection.ExecutionTime) : parts[0];
        var descending = parts.Length < 2 || !string.Equals(parts[1], "ASC", StringComparison.OrdinalIgnoreCase);

        return (field, descending) switch
        {
            (nameof(AuditRecordProjection.HttpStatusCode), false) => query.OrderBy(record => record.HttpStatusCode).ThenByDescending(record => record.Id),
            (nameof(AuditRecordProjection.HttpStatusCode), true) => query.OrderByDescending(record => record.HttpStatusCode).ThenByDescending(record => record.Id),
            (nameof(AuditRecordProjection.ExecutionDuration), false) => query.OrderBy(record => record.ExecutionDuration).ThenByDescending(record => record.Id),
            (nameof(AuditRecordProjection.ExecutionDuration), true) => query.OrderByDescending(record => record.ExecutionDuration).ThenByDescending(record => record.Id),
            (nameof(AuditRecordProjection.UserName), false) => query.OrderBy(record => record.UserName).ThenByDescending(record => record.Id),
            (nameof(AuditRecordProjection.UserName), true) => query.OrderByDescending(record => record.UserName).ThenByDescending(record => record.Id),
            (nameof(AuditRecordProjection.SourceService), false) => query.OrderBy(record => record.SourceService).ThenByDescending(record => record.Id),
            (nameof(AuditRecordProjection.SourceService), true) => query.OrderByDescending(record => record.SourceService).ThenByDescending(record => record.Id),
            (nameof(AuditRecordProjection.ApplicationName), false) => query.OrderBy(record => record.ApplicationName).ThenByDescending(record => record.Id),
            (nameof(AuditRecordProjection.ApplicationName), true) => query.OrderByDescending(record => record.ApplicationName).ThenByDescending(record => record.Id),
            (nameof(AuditRecordProjection.ExecutionTime), false) => query.OrderBy(record => record.ExecutionTime).ThenByDescending(record => record.Id),
            _ => query.OrderByDescending(record => record.ExecutionTime).ThenByDescending(record => record.Id)
        };
    }

    private static string? Normalize(string? value, int maxLength = MaxFilterLength)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private static AuditLogDto Map(AuditRecordProjection record)
    {
        var dto = new AuditLogDto();
        Map(record, dto);
        return dto;
    }

    private static void Map(AuditRecordProjection record, AuditLogDto dto)
    {
        dto.Id = record.Id;
        dto.UserId = record.UserId;
        dto.UserName = record.UserName;
        dto.ExecutionTime = record.ExecutionTime;
        dto.ExecutionDuration = record.ExecutionDuration;
        dto.HttpMethod = record.HttpMethod;
        dto.Url = record.Url;
        dto.HttpStatusCode = record.HttpStatusCode;
        dto.CorrelationId = record.CorrelationId;
        dto.ClientIpAddress = record.ClientIpAddress;
        dto.HasException = record.Exceptions != null && record.Exceptions.Trim() != string.Empty;
        dto.SourceService = record.SourceService;
        dto.ActionName = record.ActionName;
        dto.ApplicationName = record.ApplicationName;
    }
}
