namespace HCS.IntegrationEvents.Collaboration;

public sealed record ChatMessageSentEto(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId,
    Guid ConversationId,
    Guid MessageId,
    Guid SenderUserId,
    IReadOnlyList<Guid> RecipientUserIds,
    string? SenderDisplayName = null)
    : IntegrationEvent(EventId, OccurredAtUtc, CorrelationId);

public sealed record NotificationRequestedEto(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId,
    Guid NotificationId,
    Guid RecipientUserId,
    string Type,
    string SubjectId)
    : IntegrationEvent(EventId, OccurredAtUtc, CorrelationId);
