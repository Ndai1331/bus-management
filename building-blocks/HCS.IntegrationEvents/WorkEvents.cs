namespace HCS.IntegrationEvents.Work;

public sealed record ProjectTaskChangedEto(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId,
    Guid ProjectId,
    Guid TaskId,
    string ChangeType,
    string Status,
    Guid? AssigneeUserId,
    string? Title = null)
    : IntegrationEvent(EventId, OccurredAtUtc, CorrelationId);

public sealed record ProjectMemberAssignedEto(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId,
    Guid ProjectId,
    Guid UserId,
    string ProjectName)
    : IntegrationEvent(EventId, OccurredAtUtc, CorrelationId);
