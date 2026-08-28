namespace HCS.WorkManagementService.Contracts.Integration;

public interface IWorkIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}

public sealed record ProjectChangedEto(Guid EventId, DateTime OccurredAt, Guid ProjectId, string ChangeType,
    string Status) : IWorkIntegrationEvent;
public sealed record CalendarEventChangedEto(Guid EventId, DateTime OccurredAt, Guid CalendarEventId,
    string ChangeType, IReadOnlyList<Guid> ParticipantUserIds) : IWorkIntegrationEvent;
public sealed record SurveySessionChangedEto(Guid EventId, DateTime OccurredAt, Guid SurveySessionId,
    string ChangeType, string Status) : IWorkIntegrationEvent;

public sealed record WorkSubjectAccessChangedEto(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId,
    string SubjectType,
    Guid ProjectId,
    Guid? TaskId,
    bool IsDeleted,
    IReadOnlyList<Guid> AuthorizedUserIds)
    : HCS.IntegrationEvents.IntegrationEvent(EventId, OccurredAtUtc, CorrelationId);
