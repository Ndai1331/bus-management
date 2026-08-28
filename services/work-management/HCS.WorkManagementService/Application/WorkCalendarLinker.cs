using HCS.WorkManagementService.Data;
using HCS.WorkManagementService.Domain;
using Microsoft.EntityFrameworkCore;

namespace HCS.WorkManagementService.Application;

internal static class WorkCalendarLinker
{
    public static async Task SyncProjectAsync(WorkManagementDbContext db, Project project, CancellationToken ct)
    {
        var relatedId = project.Id.ToString();
        var existing = await FindSyncedAsync(db, WorkCalendarSync.ProjectRelatedType, relatedId, WorkCalendarSync.ProjectEventType, ct);
        if (existing is null)
        {
            var created = WorkCalendarSync.CreateProjectEvent(Guid.NewGuid(), project);
            db.CalendarEvents.Add(created);
            db.CalendarEventParticipants.Add(new CalendarEventParticipant(Guid.NewGuid(), created.Id, project.OwnerUserId));
            return;
        }
        existing.Change(project.Name, WorkCalendarSync.ProjectDescription(project.Name), project.StartDate, project.EndDate, true,
            WorkCalendarSync.ProjectEventType, existing.Location, WorkCalendarSync.ProjectRelatedType, relatedId, existing.Visibility);
    }

    public static async Task SyncTaskAsync(WorkManagementDbContext db, ProjectTask task, Guid ownerUserId, CancellationToken ct)
    {
        var relatedId = task.Id.ToString();
        var existing = await FindSyncedAsync(db, WorkCalendarSync.TaskRelatedType, relatedId, WorkCalendarSync.TaskEventType, ct);
        if (existing is null)
        {
            var created = WorkCalendarSync.CreateTaskEvent(Guid.NewGuid(), task, ownerUserId);
            db.CalendarEvents.Add(created);
            db.CalendarEventParticipants.Add(new CalendarEventParticipant(Guid.NewGuid(), created.Id, ownerUserId));
            return;
        }
        existing.Change(task.Title, WorkCalendarSync.TaskDescription(task.Title), task.StartDate, task.DueDate, false,
            WorkCalendarSync.TaskEventType, existing.Location, WorkCalendarSync.TaskRelatedType, relatedId, existing.Visibility);
    }

    public static async Task DeleteRelatedAsync(WorkManagementDbContext db, string relatedType, Guid relatedId, CancellationToken ct)
    {
        var id = relatedId.ToString();
        var eventType = WorkCalendarSync.SyncedEventType(relatedType);
        var events = await db.CalendarEvents
            .Where(x => x.RelatedType == relatedType && x.RelatedId == id && x.EventType == eventType)
            .ToListAsync(ct);
        var eventIds = events.Select(x => x.Id).ToList();
        var people = await db.CalendarEventParticipants.Where(x => eventIds.Contains(x.CalendarEventId)).ToListAsync(ct);
        db.CalendarEventParticipants.RemoveRange(people);
        db.CalendarEvents.RemoveRange(events);
    }

    public static async Task ReplaceParticipantsAsync(WorkManagementDbContext db, string relatedType, Guid relatedId,
        IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        var id = relatedId.ToString();
        var eventType = WorkCalendarSync.SyncedEventType(relatedType);
        var ev = await FindSyncedAsync(db, relatedType, id, eventType, ct);
        if (ev is null) return;
        var existing = await db.CalendarEventParticipants.Where(x => x.CalendarEventId == ev.Id).ToListAsync(ct);
        db.CalendarEventParticipants.RemoveRange(existing);
        db.CalendarEventParticipants.AddRange(userIds.Distinct().Select(userId => new CalendarEventParticipant(Guid.NewGuid(), ev.Id, userId)));
    }

    private static Task<CalendarEvent?> FindSyncedAsync(WorkManagementDbContext db, string relatedType, string relatedId,
        string eventType, CancellationToken ct) =>
        db.CalendarEvents.FirstOrDefaultAsync(x =>
            x.RelatedType == relatedType && x.RelatedId == relatedId && x.EventType == eventType, ct);
}
