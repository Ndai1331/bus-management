using Volo.Abp.EventBus;

namespace HCS.IntegrationEvents.Documents;

[EventName(DocumentIntegrationEventNames.DocumentAssigned)]
public sealed record DocumentAssignedEto(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId,
    Guid DocumentId,
    Guid AssignmentId,
    Guid AssigneeUserId,
    DateTimeOffset? DueAtUtc,
    string? Responsibility = null)
    : IntegrationEvent(EventId, OccurredAtUtc, CorrelationId);

public static class DocumentIntegrationEventNames
{
    public const string DocumentAssigned = "hcs.document.assigned.v1";
    public const string WorkflowChanged = "hcs.document.workflow-changed.v1";
    public const string Signed = "hcs.document.signed.v1";
}

[EventName(DocumentIntegrationEventNames.WorkflowChanged)]
public sealed record DocumentWorkflowChangedEto(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId,
    Guid DocumentId,
    Guid WorkflowInstanceId,
    string Status)
    : IntegrationEvent(EventId, OccurredAtUtc, CorrelationId);

[EventName(DocumentIntegrationEventNames.Signed)]
public sealed record DocumentSignedEto(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId,
    Guid DocumentId,
    Guid FileId,
    string InputSha256,
    string OutputSha256,
    string Adapter)
    : IntegrationEvent(EventId, OccurredAtUtc, CorrelationId);
