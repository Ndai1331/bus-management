namespace HCS.WorkManagementService.Domain;

public static class WorkCalendarSync
{
    public const string ProjectRelatedType = "PROJECT";
    public const string TaskRelatedType = "TASK";
    public const string ProjectEventType = "PROJECT";
    public const string TaskEventType = "TASK";
    public const string SyncedVisibility = "Participants";

    public static bool IsSyncedEventType(string? eventType) =>
        eventType is ProjectEventType or TaskEventType;

    public static string SyncedEventType(string relatedType) =>
        relatedType == TaskRelatedType ? TaskEventType : ProjectEventType;

    public static string ProjectDescription(string name) => $"Project: {name}";

    public static string TaskDescription(string title) => $"Task: {title}";

    public static CalendarEvent CreateProjectEvent(Guid eventId, Project project) =>
        new(eventId, project.Name, ProjectDescription(project.Name), project.StartDate, project.EndDate, true,
            ProjectEventType, null, ProjectRelatedType, project.Id.ToString(), SyncedVisibility, project.OwnerUserId);

    public static CalendarEvent CreateTaskEvent(Guid eventId, ProjectTask task, Guid ownerUserId) =>
        new(eventId, task.Title, TaskDescription(task.Title), task.StartDate, task.DueDate, false,
            TaskEventType, null, TaskRelatedType, task.Id.ToString(), SyncedVisibility, ownerUserId);
}
