using System.Diagnostics;
using System.Security.Claims;
using HCS.IntegrationEvents.Auditing;
using HCS.WorkManagementService.Data;

namespace HCS.WorkManagementService.Integration;

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
                var db = scope.ServiceProvider.GetRequiredService<WorkManagementDbContext>();
                var userIdText = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
                var audit = new AuditRecordCapturedEto(Guid.NewGuid(), "HCS.WorkManagementService", "HCS.WorkManagementService",
            Guid.TryParse(userIdText, out var userId) ? userId : null, AuditUserNameResolver.Resolve(context.User), started,
                    (int)Math.Min(timer.ElapsedMilliseconds, int.MaxValue), context.GetEndpoint()?.DisplayName,
                    context.Request.Method, context.Request.Path, failure is null ? context.Response.StatusCode : 500,
                    context.TraceIdentifier, context.Connection.RemoteIpAddress?.ToString(), context.Request.Headers.UserAgent,
                    AuditExceptionSanitizer.ToAuditValue(failure), null, [], []);
                db.OutboxMessages.Add(WorkOutbox.CreateAudit(audit, context.TraceIdentifier));
                await db.SaveChangesAsync(context.RequestAborted.IsCancellationRequested ? CancellationToken.None : context.RequestAborted);
            }
            catch (Exception auditError)
            {
                context.RequestServices.GetRequiredService<ILogger<HttpAuditOutboxMiddleware>>()
                    .LogError(auditError, "Failed to persist Work audit outbox record");
            }
        }
    }
}
