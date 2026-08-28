namespace HCS.IntegrationEvents.Identity;

public sealed record UserProvisionedEto(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId,
    Guid UserId,
    string UserName,
    string? VerifiedEmail)
    : IntegrationEvent(EventId, OccurredAtUtc, CorrelationId);

public sealed record UserRolesSynchronizedEto(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId,
    Guid UserId,
    IReadOnlyList<string> Roles)
    : IntegrationEvent(EventId, OccurredAtUtc, CorrelationId);
