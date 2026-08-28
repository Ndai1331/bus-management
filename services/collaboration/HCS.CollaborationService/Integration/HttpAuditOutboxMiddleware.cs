using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using HCS.CollaborationService.Data;
using HCS.CollaborationService.Domain;
using HCS.IntegrationEvents.Auditing;

namespace HCS.CollaborationService.Integration;

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
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CollaborationDbContext>();
                var userIdText = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
                var audit = new AuditRecordCapturedEto(Guid.NewGuid(), "HCS.CollaborationService", "HCS.CollaborationService",
                    Guid.TryParse(userIdText, out var userId) ? userId : null, AuditUserNameResolver.Resolve(context.User), started,
                    (int)Math.Min(timer.ElapsedMilliseconds, int.MaxValue), context.GetEndpoint()?.DisplayName,
                    context.Request.Method, context.Request.Path, failure is null ? context.Response.StatusCode : 500,
                    context.TraceIdentifier, context.Connection.RemoteIpAddress?.ToString(), context.Request.Headers.UserAgent,
                    AuditExceptionSanitizer.ToAuditValue(failure), null, [], []);
                db.OutboxMessages.Add(new OutboxMessage(audit.Id, AuditRecordCapturedEto.EventName,
                    JsonSerializer.Serialize(audit), started));
                await db.SaveChangesAsync(context.RequestAborted.IsCancellationRequested ? CancellationToken.None : context.RequestAborted);
            }
            catch (Exception auditError)
            {
                context.RequestServices.GetRequiredService<ILogger<HttpAuditOutboxMiddleware>>()
                    .LogError(auditError, "Failed to persist Collaboration audit outbox record");
            }
        }
    }
}
