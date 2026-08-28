using System.Text.Json;
using HCS.WorkManagementService.Contracts.Integration;
using HCS.IntegrationEvents;
using HCS.WorkManagementService.Data;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EventBus.Distributed;
using HCS.IntegrationEvents.Auditing;
using HCS.IntegrationEvents.Work;

namespace HCS.WorkManagementService.Integration;

public sealed class OutboxMessage
{
    private OutboxMessage() { }
    public OutboxMessage(Guid id, string eventName, string payload, string correlationId, DateTime creationTime)
        => (Id, EventName, Payload, CorrelationId, CreationTime) = (id, eventName, payload, correlationId, creationTime);
    public Guid Id { get; private set; }
    public string EventName { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTime CreationTime { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? DeadLetteredAt { get; private set; }
    public Guid? LeaseId { get; private set; }
    public DateTime? LeaseUntil { get; private set; }
    public void MarkPublished(DateTime at) { PublishedAt = at; LastError = null; LeaseId = null; LeaseUntil = null; }
    public void MarkFailed(string error)
    {
        Attempts++; LastError = error.Length > 1000 ? error[..1000] : error; LeaseId = null; LeaseUntil = null;
        if (Attempts >= 10) DeadLetteredAt = DateTime.UtcNow;
    }
    public void Lease(Guid leaseId, DateTime until) { LeaseId = leaseId; LeaseUntil = until; }
    public void DeadLetter(string error) { DeadLetteredAt = DateTime.UtcNow; LastError = error.Length > 1000 ? error[..1000] : error; LeaseId = null; LeaseUntil = null; }
}

public sealed class InboxMessage
{
    private InboxMessage() { }
    public InboxMessage(Guid eventId, string handler, DateTime processedAt)
        => (EventId, Handler, ProcessedAt) = (eventId, handler, processedAt);
    public Guid EventId { get; private set; }
    public string Handler { get; private set; } = string.Empty;
    public DateTime ProcessedAt { get; private set; }
}

public static class WorkOutbox
{
    public static OutboxMessage Create<T>(T value, string correlationId) where T : IWorkIntegrationEvent =>
        new(value.EventId, typeof(T).FullName!, JsonSerializer.Serialize(value), correlationId, DateTime.UtcNow);
    public static OutboxMessage CreateCanonical<T>(T value, string correlationId) where T : IntegrationEvent =>
        new(value.EventId, typeof(T).FullName!, JsonSerializer.Serialize(value), correlationId, DateTime.UtcNow);
    public static OutboxMessage CreateAudit(AuditRecordCapturedEto value, string correlationId) =>
        new(value.Id, AuditRecordCapturedEto.EventName, JsonSerializer.Serialize(value), correlationId, DateTime.UtcNow);
}

public interface IInboxExecutor
{
    Task<bool> ExecuteOnceAsync(Guid eventId, string handler, Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);
}

public sealed class EfInboxExecutor(WorkManagementDbContext db) : IInboxExecutor
{
    public async Task<bool> ExecuteOnceAsync(Guid eventId, string handler, Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (await db.InboxMessages.AnyAsync(x => x.EventId == eventId && x.Handler == handler, cancellationToken)) return false;
        db.InboxMessages.Add(new InboxMessage(eventId, handler, DateTime.UtcNow));
        // Claim the marker before invoking the local projection. Both changes are
        // flushed together, so a failed action does not leave a processed marker.
        await action(cancellationToken);
        try { await db.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateException exception) when (WorkPostgresErrors.IsDuplicateInboxMarker(exception))
        { db.ChangeTracker.Clear(); return false; }
    }
}

internal static class WorkPostgresErrors
{
    public static bool IsDuplicateInboxMarker(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: "23505", ConstraintName: var constraint } &&
        constraint?.Equals("PK_InboxMessages", StringComparison.OrdinalIgnoreCase) == true;
}

public sealed class OutboxDispatcher(WorkManagementDbContext db, IDistributedEventBus eventBus,
    ILogger<OutboxDispatcher> logger)
{
    public async Task<int> DispatchAsync(CancellationToken cancellationToken)
    {
        var leaseId = Guid.NewGuid(); var now = DateTime.UtcNow;
        var ids = await db.OutboxMessages.Where(x => x.PublishedAt == null && x.DeadLetteredAt == null &&
                x.Attempts < 10 && (x.LeaseUntil == null || x.LeaseUntil < now))
            .OrderBy(x => x.CreationTime).Select(x => x.Id).Take(50).ToListAsync(cancellationToken);
        if (ids.Count != 0)
            await db.OutboxMessages.Where(x => ids.Contains(x.Id) && x.PublishedAt == null && x.DeadLetteredAt == null &&
                    x.Attempts < 10 && (x.LeaseUntil == null || x.LeaseUntil < now))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseId, leaseId)
                    .SetProperty(x => x.LeaseUntil, now.AddMinutes(1)), cancellationToken);
        var messages = await db.OutboxMessages.Where(x => x.LeaseId == leaseId).OrderBy(x => x.CreationTime)
            .ToListAsync(cancellationToken);
        foreach (var message in messages)
        {
            try
            {
                if (message.EventName == typeof(ProjectChangedEto).FullName)
                    await eventBus.PublishAsync(JsonSerializer.Deserialize<ProjectChangedEto>(message.Payload)!, onUnitOfWorkComplete: false);
                else if (message.EventName == typeof(CalendarEventChangedEto).FullName)
                    await eventBus.PublishAsync(JsonSerializer.Deserialize<CalendarEventChangedEto>(message.Payload)!, onUnitOfWorkComplete: false);
                else if (message.EventName == typeof(SurveySessionChangedEto).FullName)
                    await eventBus.PublishAsync(JsonSerializer.Deserialize<SurveySessionChangedEto>(message.Payload)!, onUnitOfWorkComplete: false);
                else if (message.EventName == typeof(ProjectTaskChangedEto).FullName)
                    await eventBus.PublishAsync(JsonSerializer.Deserialize<ProjectTaskChangedEto>(message.Payload)!, onUnitOfWorkComplete: false);
                else if (message.EventName == typeof(ProjectMemberAssignedEto).FullName)
                    await eventBus.PublishAsync(JsonSerializer.Deserialize<ProjectMemberAssignedEto>(message.Payload)!, onUnitOfWorkComplete: false);
                else if (message.EventName == typeof(WorkSubjectAccessChangedEto).FullName)
                    await eventBus.PublishAsync(JsonSerializer.Deserialize<WorkSubjectAccessChangedEto>(message.Payload)!, onUnitOfWorkComplete: false);
                else if (message.EventName == AuditRecordCapturedEto.EventName)
                    await eventBus.PublishAsync(JsonSerializer.Deserialize<AuditRecordCapturedEto>(message.Payload)!, onUnitOfWorkComplete: false);
                else
                {
                    message.DeadLetter($"Unknown event type: {message.EventName}");
                    logger.LogError("Dead-lettered unknown work outbox event {EventId} ({EventName})", message.Id, message.EventName);
                    continue;
                }
                message.MarkPublished(DateTime.UtcNow);
            }
            catch (Exception exception)
            {
                message.MarkFailed(exception.Message);
                logger.LogWarning(exception, "Work outbox event {EventId} publish failed", message.Id);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        return messages.Count;
    }
}

public sealed class OutboxWorker(IServiceScopeFactory scopeFactory, ILogger<OutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Work outbox cycle failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
