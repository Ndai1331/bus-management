using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace HCS.Auditing;

public class GetAuditLogsInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public DateTime? EndTimeExclusive { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? HttpMethod { get; set; }
    public string? ClientIpAddress { get; set; }
    public string? BrowserInfo { get; set; }
    public string? SourceService { get; set; }
    public string? ApplicationName { get; set; }
    public bool? HasException { get; set; }
    public string? CorrelationId { get; set; }
    public string? Action { get; set; }
    public string? Url { get; set; }
}

public class AuditLogDto : EntityDto<Guid>
{
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public DateTime ExecutionTime { get; set; }
    public int ExecutionDuration { get; set; }
    public string? HttpMethod { get; set; }
    public string? Url { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? CorrelationId { get; set; }
    public string? ClientIpAddress { get; set; }
    public bool HasException { get; set; }
    public string? SourceService { get; set; }
    public string? ActionName { get; set; }
    public string? ApplicationName { get; set; }
}

public class AuditLogDetailDto : AuditLogDto
{
    public string? BrowserInfo { get; set; }
    public string? Exceptions { get; set; }
    public string? Comments { get; set; }
    public List<AuditLogActionDto> Actions { get; set; } = [];
    public List<AuditEntityChangeDto> EntityChanges { get; set; } = [];
}

public class AuditLogActionDto : EntityDto<Guid>
{
    public string? ServiceName { get; set; }
    public string? MethodName { get; set; }
    public string? Parameters { get; set; }
    public DateTime ExecutionTime { get; set; }
    public int ExecutionDuration { get; set; }
}

public class AuditEntityChangeDto : EntityDto<Guid>
{
    public DateTime ChangeTime { get; set; }
    public string ChangeType { get; set; } = null!;
    public string? EntityId { get; set; }
    public string? EntityTypeFullName { get; set; }
}
