namespace HCS.IntegrationEvents;

public abstract record IntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId,
    int SchemaVersion = 1);
