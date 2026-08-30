using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using HCS.BusManagementService.Contracts;
using HCS.BusManagementService.Data;
using HCS.IntegrationEvents;
using HCS.IntegrationEvents.Auditing;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EventBus.Distributed;

namespace HCS.BusManagementService.Integration;

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
    public void Lease(Guid leaseId, DateTime until) { LeaseId = leaseId; LeaseUntil = until; }
    public void MarkFailed(string error)
    {
        Attempts++; LastError = error.Length > 1000 ? error[..1000] : error; LeaseId = null; LeaseUntil = null;
        if (Attempts >= 10) DeadLetteredAt = DateTime.UtcNow;
    }
}

public static class BusOutbox
{
    public static OutboxMessage Create<T>(T value, string correlationId) where T : IntegrationEvent =>
        new(value.EventId, EventName<T>(), JsonSerializer.Serialize(value), correlationId, DateTime.UtcNow);
    public static OutboxMessage CreateAudit(AuditRecordCapturedEto value, string correlationId) =>
        new(value.Id, AuditRecordCapturedEto.EventName, JsonSerializer.Serialize(value), correlationId, DateTime.UtcNow);

    private static string EventName<T>() where T : IntegrationEvent => typeof(T) == typeof(BusDepartureChangedEto)
        ? BusDepartureChangedEto.EventName
        : typeof(T) == typeof(BusRevenueRecordedEto)
            ? BusRevenueRecordedEto.EventName
                : typeof(T) == typeof(BusExpenseChangedEto)
                    ? BusExpenseChangedEto.EventName
                : typeof(T) == typeof(BusSettlementChangedEto)
                    ? BusSettlementChangedEto.EventName
                : typeof(T) == typeof(BusAdjustmentChangedEto)
                    ? BusAdjustmentChangedEto.EventName
                : typeof(T) == typeof(BusParkingSessionChangedEto)
                    ? BusParkingSessionChangedEto.EventName
                : typeof(T) == typeof(BusParkingReservationChangedEto)
                    ? BusParkingReservationChangedEto.EventName
                : typeof(T) == typeof(BusReconciliationClosedEto)
                    ? BusReconciliationClosedEto.EventName
                    : typeof(T).FullName!;
}

public sealed class OutboxDispatcher(BusManagementDbContext db, IDistributedEventBus eventBus, ILogger<OutboxDispatcher> logger)
{
    public async Task<int> DispatchAsync(CancellationToken cancellationToken)
    {
        var leaseId = Guid.NewGuid(); var now = DateTime.UtcNow;
        var ids = await db.OutboxMessages.Where(x => x.PublishedAt == null && x.DeadLetteredAt == null && x.Attempts < 10 && (x.LeaseUntil == null || x.LeaseUntil < now))
            .OrderBy(x => x.CreationTime).Select(x => x.Id).Take(50).ToListAsync(cancellationToken);
        if (ids.Count == 0) return 0;
        await db.OutboxMessages.Where(x => ids.Contains(x.Id) && x.PublishedAt == null && x.DeadLetteredAt == null &&
                (x.LeaseUntil == null || x.LeaseUntil < now))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseId, leaseId).SetProperty(x => x.LeaseUntil, now.AddMinutes(1)), cancellationToken);
        var messages = await db.OutboxMessages.Where(x => x.LeaseId == leaseId).OrderBy(x => x.CreationTime).ToListAsync(cancellationToken);
        foreach (var message in messages)
        {
            try
            {
                if (message.EventName == AuditRecordCapturedEto.EventName)
                    await eventBus.PublishAsync(JsonSerializer.Deserialize<AuditRecordCapturedEto>(message.Payload)!, onUnitOfWorkComplete: false);
                else if (message.EventName == BusDepartureChangedEto.EventName)
                    await eventBus.PublishAsync(JsonSerializer.Deserialize<BusDepartureChangedEto>(message.Payload)!, onUnitOfWorkComplete: false);
                else if (message.EventName == BusRevenueRecordedEto.EventName)
                    await eventBus.PublishAsync(JsonSerializer.Deserialize<BusRevenueRecordedEto>(message.Payload)!, onUnitOfWorkComplete: false);
                else if (message.EventName == BusExpenseChangedEto.EventName)
                    await eventBus.PublishAsync(JsonSerializer.Deserialize<BusExpenseChangedEto>(message.Payload)!, onUnitOfWorkComplete: false);
                else if (message.EventName == BusSettlementChangedEto.EventName)
                    await eventBus.PublishAsync(JsonSerializer.Deserialize<BusSettlementChangedEto>(message.Payload)!, onUnitOfWorkComplete: false);
                else if (message.EventName == BusAdjustmentChangedEto.EventName)
                    await eventBus.PublishAsync(JsonSerializer.Deserialize<BusAdjustmentChangedEto>(message.Payload)!, onUnitOfWorkComplete: false);
                else if (message.EventName == BusParkingSessionChangedEto.EventName)
                    await eventBus.PublishAsync(JsonSerializer.Deserialize<BusParkingSessionChangedEto>(message.Payload)!, onUnitOfWorkComplete: false);
                else if (message.EventName == BusParkingReservationChangedEto.EventName)
                    await eventBus.PublishAsync(JsonSerializer.Deserialize<BusParkingReservationChangedEto>(message.Payload)!, onUnitOfWorkComplete: false);
                else if (message.EventName == BusReconciliationClosedEto.EventName)
                    await eventBus.PublishAsync(JsonSerializer.Deserialize<BusReconciliationClosedEto>(message.Payload)!, onUnitOfWorkComplete: false);
                else { message.MarkFailed($"Unknown event type: {message.EventName}"); continue; }
                message.MarkPublished(DateTime.UtcNow);
            }
            catch (Exception exception)
            {
                message.MarkFailed(exception.Message); logger.LogWarning(exception, "Bus outbox event {EventId} publish failed", message.Id);
            }
        }
        await db.SaveChangesAsync(cancellationToken); return messages.Count;
    }
}

public sealed class OutboxWorker(IServiceScopeFactory scopeFactory, ILogger<OutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { using var scope = scopeFactory.CreateScope(); await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().DispatchAsync(stoppingToken); }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning(exception, "Bus outbox cycle failed"); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}

public sealed class HttpAuditOutboxMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api")) { await next(context); return; }
        var started = DateTime.UtcNow; var timer = Stopwatch.StartNew(); Exception? failure = null;
        try { await next(context); }
        catch (Exception exception) { failure = exception; throw; }
        finally
        {
            try
            {
                using var scope = scopeFactory.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<BusManagementDbContext>();
                var userIdText = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
                var audit = new AuditRecordCapturedEto(Guid.NewGuid(), "HCS.BusManagementService", "HCS.BusManagementService",
                    Guid.TryParse(userIdText, out var userId) ? userId : null, AuditUserNameResolver.Resolve(context.User), started,
                    (int)Math.Min(timer.ElapsedMilliseconds, int.MaxValue), context.GetEndpoint()?.DisplayName, context.Request.Method,
                    context.Request.Path, failure is null ? context.Response.StatusCode : 500, context.TraceIdentifier,
                    context.Connection.RemoteIpAddress?.ToString(), context.Request.Headers.UserAgent,
                    AuditExceptionSanitizer.ToAuditValue(failure), null, [], []);
                db.OutboxMessages.Add(BusOutbox.CreateAudit(audit, context.TraceIdentifier));
                await db.SaveChangesAsync(context.RequestAborted.IsCancellationRequested ? CancellationToken.None : context.RequestAborted);
            }
            catch (Exception auditError) { context.RequestServices.GetRequiredService<ILogger<HttpAuditOutboxMiddleware>>().LogError(auditError, "Failed to persist bus audit outbox record"); }
        }
    }
}
