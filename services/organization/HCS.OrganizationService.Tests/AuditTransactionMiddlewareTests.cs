using HCS.OrganizationService.Data;
using HCS.OrganizationService.Domain;
using HCS.OrganizationService.Host.Integration;
using HCS.OrganizationService.Integration;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HCS.OrganizationService.Tests;

public sealed class AuditTransactionMiddlewareTests : OrganizationTestBase
{
    [Fact]
    public async Task Audit_outbox_write_failure_rolls_back_the_business_mutation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var db = GetRequiredService<OrganizationDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER RejectAuditOutbox BEFORE INSERT ON OutboxMessages
            BEGIN SELECT RAISE(ABORT, 'simulated audit outbox persistence failure'); END;
            """, cancellationToken);
        var middleware = new HttpAuditOutboxMiddleware(async context =>
        {
            db.Departments.Add(new Department(Guid.NewGuid(), "audit-tx", "Audit transaction", null, 1, true));
            await db.SaveChangesAsync(cancellationToken);
            context.Response.StatusCode = StatusCodes.Status201Created;
            await context.Response.WriteAsync("{\"id\":\"would-be-committed\"}", cancellationToken);
        }, NullLogger<HttpAuditOutboxMiddleware>.Instance);
        var context = new DefaultHttpContext();
        await using var response = new MemoryStream();
        context.Response.Body = response;
        context.Request.Path = "/api/organization/departments";
        context.Request.Method = HttpMethods.Post;

        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => middleware.InvokeAsync(context, db));

            Assert.Equal(0, await db.Departments.AsNoTracking().CountAsync(cancellationToken));
            Assert.Equal(0, await db.OutboxMessages.AsNoTracking().CountAsync(cancellationToken));
            Assert.Equal(0, response.Length);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS RejectAuditOutbox", cancellationToken);
        }
    }
}
