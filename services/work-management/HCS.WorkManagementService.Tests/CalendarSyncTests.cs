using HCS.WorkManagementService.Application;
using HCS.WorkManagementService.Data;
using HCS.WorkManagementService.Domain;
using Microsoft.EntityFrameworkCore;

namespace HCS.WorkManagementService.Tests;

public sealed class CalendarSyncTests
{
    [Fact]
    public async Task Sync_project_creates_one_event_and_update_does_not_duplicate()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = CreateDb();
        var start = DateTime.UtcNow;
        var project = new Project(Guid.NewGuid(), "P-CAL", "Calendar Project", start, start.AddDays(3), "Active", null, Guid.NewGuid());
        db.Projects.Add(project);
        await WorkCalendarLinker.SyncProjectAsync(db, project, ct);
        await db.SaveChangesAsync(ct);
        project.Change("Renamed project", null, start, start.AddDays(4), "Active");
        await WorkCalendarLinker.SyncProjectAsync(db, project, ct);
        await db.SaveChangesAsync(ct);

        var events = await db.CalendarEvents.ToListAsync(ct);
        Assert.Single(events);
        Assert.Equal(WorkCalendarSync.ProjectRelatedType, events[0].RelatedType);
        Assert.Equal(project.Id.ToString(), events[0].RelatedId);
        Assert.Equal("Renamed project", events[0].Title);
    }

    [Fact]
    public async Task Delete_related_removes_event_and_participants()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = CreateDb();
        var start = DateTime.UtcNow;
        var owner = Guid.NewGuid();
        var task = new ProjectTask(Guid.NewGuid(), Guid.NewGuid(), null, "T-CAL", "Calendar Task",
            null, start, start.AddDays(1), "Normal", "Open", 0);
        db.ProjectTasks.Add(task);
        await WorkCalendarLinker.SyncTaskAsync(db, task, owner, ct);
        await db.SaveChangesAsync(ct);
        var userId = Guid.NewGuid();
        await WorkCalendarLinker.ReplaceParticipantsAsync(db, WorkCalendarSync.TaskRelatedType, task.Id, [userId], ct);
        await db.SaveChangesAsync(ct);
        Assert.Equal(1, await db.CalendarEventParticipants.CountAsync(ct));

        await WorkCalendarLinker.DeleteRelatedAsync(db, WorkCalendarSync.TaskRelatedType, task.Id, ct);
        await db.SaveChangesAsync(ct);
        Assert.Empty(db.CalendarEvents);
        Assert.Empty(db.CalendarEventParticipants);
    }

    [Fact]
    public async Task Sync_does_not_hijack_manual_meeting_with_same_related_id()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = CreateDb();
        var start = DateTime.UtcNow;
        var owner = Guid.NewGuid();
        var project = new Project(Guid.NewGuid(), "P-MEET", "Project", start, start.AddDays(2), "Active", null, owner);
        db.Projects.Add(project);
        db.CalendarEvents.Add(new CalendarEvent(Guid.NewGuid(), "Standup", null, start, start.AddHours(1), false,
            "Meeting", null, WorkCalendarSync.ProjectRelatedType, project.Id.ToString(), "Private", owner));
        await db.SaveChangesAsync(ct);

        await WorkCalendarLinker.SyncProjectAsync(db, project, ct);
        await db.SaveChangesAsync(ct);

        var events = await db.CalendarEvents.OrderBy(x => x.EventType).ToListAsync(ct);
        Assert.Equal(2, events.Count);
        Assert.Contains(events, x => x.EventType == "Meeting" && x.Title == "Standup");
        Assert.Contains(events, x => x.EventType == WorkCalendarSync.ProjectEventType && x.Title == project.Name);

        await WorkCalendarLinker.DeleteRelatedAsync(db, WorkCalendarSync.ProjectRelatedType, project.Id, ct);
        await db.SaveChangesAsync(ct);
        var remaining = Assert.Single(db.CalendarEvents);
        Assert.Equal("Standup", remaining.Title);
    }

    private static WorkManagementDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<WorkManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
