using System.Diagnostics;
using System.Security.Claims;
using HCS.IntegrationEvents.Auditing;
using Microsoft.EntityFrameworkCore;

namespace HCS.DocumentService.Integration;

public sealed class HttpAuditOutboxMiddleware(RequestDelegate next, ILogger<HttpAuditOutboxMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, DocumentServiceDbContext db)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await next(context);
            return;
        }

        if (!IsStateChanging(context.Request.Method))
        {
            await InvokeReadOnlyAsync(context, db, logger);
            return;
        }

        var started = DateTime.UtcNow;
        var timer = Stopwatch.StartNew();
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(CancellationToken.None)
            : null;
        var responseBody = context.Response.Body;
        await using var bufferedResponse = new MemoryStream();
        context.Response.Body = bufferedResponse;
        try
        {
            Exception? failure = null;
            try { await next(context); }
            catch (Exception exception) { failure = exception; }

            if (failure is not null)
            {
                if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                await PersistFailedAuditAsync(context, db, logger, started, timer, failure);
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }

            try
            {
                await PersistAuditAsync(context, db, started, timer, null);
                if (transaction is not null) await transaction.CommitAsync(CancellationToken.None);
                bufferedResponse.Position = 0;
                context.Response.Body = responseBody;
                await bufferedResponse.CopyToAsync(responseBody, CancellationToken.None);
            }
            catch
            {
                if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                throw;
            }
        }
        finally { context.Response.Body = responseBody; }
    }

    private async Task InvokeReadOnlyAsync(HttpContext context, DocumentServiceDbContext db, ILogger logger)
    {
        var started = DateTime.UtcNow;
        var timer = Stopwatch.StartNew();
        Exception? failure = null;
        try { await next(context); }
        catch (Exception exception) { failure = exception; }
        try { await PersistAuditAsync(context, db, started, timer, failure); }
        catch (Exception auditError)
        {
            db.ChangeTracker.Clear();
            logger.LogCritical(auditError, "Failed to persist Document read audit outbox record for {CorrelationId}", context.TraceIdentifier);
        }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static bool IsStateChanging(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    private static async Task PersistFailedAuditAsync(HttpContext context, DocumentServiceDbContext db,
        ILogger logger, DateTime started, Stopwatch timer, Exception failure)
    {
        try { await PersistAuditAsync(context, db, started, timer, failure); }
        catch (Exception auditError)
        {
            db.ChangeTracker.Clear();
            logger.LogCritical(auditError, "Failed to persist Document failure audit outbox record for {CorrelationId}", context.TraceIdentifier);
        }
    }

    private static async Task PersistAuditAsync(HttpContext context, DocumentServiceDbContext db,
        DateTime started, Stopwatch timer, Exception? failure)
    {
        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
        var audit = new AuditRecordCapturedEto(Guid.NewGuid(), "HCS.DocumentService", "HCS.DocumentService",
            Guid.TryParse(userIdValue, out var userId) ? userId : null, AuditUserNameResolver.Resolve(context.User), started,
            (int)Math.Min(timer.ElapsedMilliseconds, int.MaxValue), context.GetEndpoint()?.DisplayName,
            context.Request.Method, context.Request.Path, failure is null ? context.Response.StatusCode : 500,
            context.TraceIdentifier, context.Connection.RemoteIpAddress?.ToString(), context.Request.Headers.UserAgent,
            AuditExceptionSanitizer.ToAuditValue(failure), null,
            [new AuditActionCapturedEto(Guid.NewGuid(), "HTTP", context.GetEndpoint()?.DisplayName,
                null, started, (int)Math.Min(timer.ElapsedMilliseconds, int.MaxValue))], []);
        db.OutboxMessages.Add(OutboxFactory.CreateAudit(audit, context.TraceIdentifier, started));
        await db.SaveChangesAsync(CancellationToken.None);
    }
}
