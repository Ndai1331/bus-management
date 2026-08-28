using System.Text.Json;
using HCS.DocumentService.Integration;
using HCS.IntegrationEvents;
using HCS.IntegrationEvents.Auditing;
using HCS.IntegrationEvents.Documents;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EventBus.Distributed;

namespace HCS.DocumentService.Integration;

public sealed class OutboxMessage
{
    private OutboxMessage() { }

    public OutboxMessage(Guid id, string eventName, string payload, string correlationId, DateTime now)
        => (Id, EventName, Payload, CorrelationId, CreationTime, NextAttemptAt) =
            (id, eventName, payload, correlationId, now, now);

    public Guid Id { get; private set; }
    public string EventName { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTime CreationTime { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public Guid? LeaseId { get; private set; }
    public DateTime? LeaseUntil { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public DateTime? DeadLetteredAt { get; private set; }

    public void Lease(Guid leaseId, DateTime until) => (LeaseId, LeaseUntil) = (leaseId, until);

    public void MarkPublished(DateTime now) =>
        (PublishedAt, LastError, LeaseId, LeaseUntil) = (now, null, null, null);

    public void MarkFailed(string error, DateTime now)
    {
        Attempts++;
        LastError = Truncate(error);
        LeaseId = null;
        LeaseUntil = null;
        NextAttemptAt = now.AddSeconds(Math.Min(300, Math.Pow(2, Attempts)));
        if (Attempts >= 10) DeadLetteredAt = now;
    }

    public void DeadLetter(string error, DateTime now) =>
        (DeadLetteredAt, LastError, LeaseId, LeaseUntil) = (now, Truncate(error), null, null);

    private static string Truncate(string value) => value[..Math.Min(value.Length, 1000)];
}

public sealed class InboxMessage
{
    private InboxMessage() { }
    public InboxMessage(Guid eventId, string handler, DateTime now) => (EventId, Handler, ProcessedAt) = (eventId, handler, now);
    public Guid EventId { get; private set; }
    public string Handler { get; private set; } = string.Empty;
    public DateTime ProcessedAt { get; private set; }
}

public static class OutboxFactory
{
    public static OutboxMessage CreateCanonical<T>(T value, string correlationId, DateTime now) where T : IntegrationEvent =>
        new(value.EventId, EventName(value), JsonSerializer.Serialize(value), correlationId, now);

    public static OutboxMessage CreateAudit(AuditRecordCapturedEto value, string correlationId, DateTime now) =>
        new(value.Id, AuditRecordCapturedEto.EventName, JsonSerializer.Serialize(value), correlationId, now);

    private static string EventName(IntegrationEvent value) => value switch
    {
        DocumentAssignedEto => DocumentIntegrationEventNames.DocumentAssigned,
        DocumentWorkflowChangedEto => DocumentIntegrationEventNames.WorkflowChanged,
        DocumentSignedEto => DocumentIntegrationEventNames.Signed,
        _ => throw new NotSupportedException($"No canonical name registered for {value.GetType().Name}.")
    };
}

public interface IInboxExecutor
{
    Task<bool> ExecuteOnceAsync(Guid eventId, string handler, Func<CancellationToken, Task> action, CancellationToken cancellationToken);
}

public sealed class EfInboxExecutor(DocumentServiceDbContext db) : IInboxExecutor
{
    public async Task<bool> ExecuteOnceAsync(Guid eventId, string handler, Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        if (await db.InboxMessages.AnyAsync(x => x.EventId == eventId && x.Handler == handler, cancellationToken)) return false;
        var ownsTransaction = db.Database.IsRelational() && db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        db.InboxMessages.Add(new InboxMessage(eventId, handler, DateTime.UtcNow));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateInboxMarker(exception))
        {
            db.ChangeTracker.Clear();
            return false;
        }

        try
        {
            await action(cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            db.ChangeTracker.Clear();
            throw;
        }
    }

    private static bool IsDuplicateInboxMarker(DbUpdateException exception) =>
        exception.InnerException?.GetType().FullName == "Npgsql.PostgresException" &&
        string.Equals(exception.InnerException.GetType().GetProperty("SqlState")?.GetValue(exception.InnerException)?.ToString(), "23505", StringComparison.Ordinal) &&
        string.Equals(exception.InnerException.GetType().GetProperty("ConstraintName")?.GetValue(exception.InnerException)?.ToString(), "PK_InboxMessages", StringComparison.Ordinal);
}

public interface IOutboxEventPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken);
}

public interface ITypedDistributedEventPublisher
{
    Task PublishAsync<T>(T eventData) where T : class;
}

public sealed class AbpTypedDistributedEventPublisher(IDistributedEventBus eventBus) : ITypedDistributedEventPublisher
{
    public Task PublishAsync<T>(T eventData) where T : class =>
        eventBus.PublishAsync(eventData, onUnitOfWorkComplete: false);
}

public sealed class AbpOutboxEventPublisher(ITypedDistributedEventPublisher eventBus) : IOutboxEventPublisher
{
    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken) => message.EventName switch
    {
        DocumentIntegrationEventNames.DocumentAssigned => PublishTypedAsync<DocumentAssignedEto>(message),
        AuditRecordCapturedEto.EventName => PublishTypedAsync<AuditRecordCapturedEto>(message),
        DocumentIntegrationEventNames.WorkflowChanged => PublishTypedAsync<DocumentWorkflowChangedEto>(message),
        DocumentIntegrationEventNames.Signed => PublishTypedAsync<DocumentSignedEto>(message),
        _ => throw new NotSupportedException($"Unknown outbox event type: {message.EventName}")
    };

    private Task PublishTypedAsync<T>(OutboxMessage message) where T : class
    {
        var value = JsonSerializer.Deserialize<T>(message.Payload)
                    ?? throw new JsonException($"Outbox event {message.Id} deserialized to null.");
        return eventBus.PublishAsync(value);
    }
}

public sealed class OutboxDispatcher(
    DocumentServiceDbContext db,
    IOutboxEventPublisher publisher,
    ILogger<OutboxDispatcher> logger)
{
    public async Task<int> DispatchAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var leaseId = Guid.NewGuid();
        var ids = await db.OutboxMessages
            .Where(x => x.PublishedAt == null && x.DeadLetteredAt == null && x.Attempts < 10 &&
                        x.NextAttemptAt <= now && (x.LeaseUntil == null || x.LeaseUntil < now))
            .OrderBy(x => x.CreationTime).Select(x => x.Id).Take(50).ToListAsync(cancellationToken);
        if (ids.Count == 0) return 0;

        await db.OutboxMessages
            .Where(x => ids.Contains(x.Id) && x.PublishedAt == null && x.DeadLetteredAt == null &&
                        (x.LeaseUntil == null || x.LeaseUntil < now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.LeaseId, leaseId)
                .SetProperty(x => x.LeaseUntil, now.AddMinutes(1)), cancellationToken);

        var messages = await db.OutboxMessages.Where(x => x.LeaseId == leaseId)
            .OrderBy(x => x.CreationTime).ToListAsync(cancellationToken);
        foreach (var message in messages)
        {
            try
            {
                await publisher.PublishAsync(message, cancellationToken);
                message.MarkPublished(DateTime.UtcNow);
            }
            catch (NotSupportedException exception)
            {
                message.DeadLetter(exception.Message, DateTime.UtcNow);
                logger.LogError(exception, "Dead-lettered unknown Document outbox event {EventId}", message.Id);
            }
            catch (Exception exception)
            {
                message.MarkFailed(exception.Message, DateTime.UtcNow);
                logger.LogWarning(exception, "Document outbox event {EventId} publish failed", message.Id);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        return messages.Count;
    }
}
