using HCS.IntegrationEvents.Auditing;
using HCS.WorkManagementService.Data;
using HCS.WorkManagementService.Domain;
using HCS.WorkManagementService.Integration;
using Microsoft.EntityFrameworkCore;

namespace HCS.WorkManagementService.Tests;

public sealed class RelationalAndAuditTests
{
    [Theory]
    [InlineData(typeof(ProjectMember), nameof(ProjectMember.ProjectId))]
    [InlineData(typeof(ProjectTask), nameof(ProjectTask.ProjectId))]
    [InlineData(typeof(ProjectTaskAssignment), nameof(ProjectTaskAssignment.ProjectTaskId))]
    [InlineData(typeof(CalendarEventParticipant), nameof(CalendarEventParticipant.CalendarEventId))]
    [InlineData(typeof(SurveyResult), nameof(SurveyResult.SessionId))]
    [InlineData(typeof(SurveyFileReference), nameof(SurveyFileReference.SessionId))]
    public void Same_database_child_records_have_foreign_keys(Type entityType, string property)
    {
        using var db = new WorkManagementDbContext(new DbContextOptionsBuilder<WorkManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var entity = db.Model.FindEntityType(entityType)!;
        Assert.Contains(entity.GetForeignKeys(), fk => fk.Properties.Any(p => p.Name == property));
    }

    [Fact]
    public void Audit_outbox_uses_canonical_name_and_typed_json()
    {
        var audit = new AuditRecordCapturedEto(Guid.NewGuid(), "work", null, null, null, DateTime.UtcNow,
            10, "action", "POST", "/api/projects", 200, "correlation", null, null, null, null, [], []);
        var message = WorkOutbox.CreateAudit(audit, "correlation");
        Assert.Equal(AuditRecordCapturedEto.EventName, message.EventName);
        Assert.Contains(audit.Id.ToString(), message.Payload, StringComparison.OrdinalIgnoreCase);
    }
}
