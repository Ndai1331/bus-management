using System;
using System.Text.Json;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using HCS.IntegrationEvents.Auditing;

namespace HCS.Auditing;

public sealed class AuditRecordProjection : AggregateRoot<Guid>
{
    public string SourceService { get; private set; } = null!;
    public string? ApplicationName { get; private set; }
    public Guid? UserId { get; private set; }
    public string? UserName { get; private set; }
    public DateTime ExecutionTime { get; private set; }
    public int ExecutionDuration { get; private set; }
    public string? ActionName { get; private set; }
    public string? HttpMethod { get; private set; }
    public string? Url { get; private set; }
    public int? HttpStatusCode { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? ClientIpAddress { get; private set; }
    public string? BrowserInfo { get; private set; }
    public string? Exceptions { get; private set; }
    public string? Comments { get; private set; }
    public string ActionsJson { get; private set; } = "[]";
    public string EntityChangesJson { get; private set; } = "[]";

    private AuditRecordProjection()
    {
    }

    public AuditRecordProjection(AuditRecordCapturedEto source) : base(source.Id)
    {
        SourceService = source.SourceService;
        ApplicationName = source.ApplicationName;
        UserId = source.UserId;
        UserName = source.UserName;
        ExecutionTime = source.ExecutionTime;
        ExecutionDuration = source.ExecutionDuration;
        ActionName = source.ActionName;
        HttpMethod = source.HttpMethod;
        Url = source.Url;
        HttpStatusCode = source.HttpStatusCode;
        CorrelationId = source.CorrelationId;
        ClientIpAddress = source.ClientIpAddress;
        BrowserInfo = source.BrowserInfo;
        Exceptions = AuditExceptionSanitizer.SanitizeCapturedValue(source.Exceptions);
        Comments = source.Comments;
        ActionsJson = JsonSerializer.Serialize(source.Actions);
        EntityChangesJson = JsonSerializer.Serialize(source.EntityChanges);
    }
}

public interface IAuditRecordProjectionRepository : IRepository<AuditRecordProjection, Guid>;
