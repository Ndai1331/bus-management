using System;
using System.Globalization;

namespace HCS.Blazor.Client.Work;

internal static class WorkUi
{
    public static readonly string[] ProjectStatuses = ["Draft", "Active", "Completed", "Cancelled"];
    public static readonly string[] TaskStatuses = ["New", "InProgress", "Waiting", "Completed", "Cancelled"];
    public static readonly string[] TaskPriorities = ["Low", "Normal", "High", "Urgent"];

    public static string ProjectStatusKey(string status) => status switch
    {
        "Draft" => "Work:Status.Planning",
        "Active" => "Work:Status.InProgress",
        "Completed" => "Work:Status.Completed",
        "Cancelled" => "Work:Status.Cancelled",
        _ => "Work:Status.Planning"
    };

    public static string TaskStatusKey(string status) => status switch
    {
        "New" => "Work:Status.Todo",
        "InProgress" => "Work:Status.Doing",
        "Waiting" => "Work:Status.Waiting",
        "Completed" => "Work:Status.Done",
        "Cancelled" => "Work:Status.Cancelled",
        _ => "Work:Status.Todo"
    };

    public static string PriorityKey(string priority) => priority switch
    {
        "Low" => "Work:Priority.Low",
        "High" => "Work:Priority.High",
        "Urgent" => "Work:Priority.Urgent",
        _ => "Work:Priority.Normal"
    };

    public static string ProjectBadgeClass(string status) => status switch
    {
        "Active" => "hcs-status-badge hcs-status-badge--info",
        "Completed" => "hcs-status-badge hcs-status-badge--success",
        "Cancelled" => "hcs-status-badge hcs-status-badge--danger",
        _ => "hcs-status-badge hcs-status-badge--planning"
    };

    public static string TaskBadgeClass(string status) => status switch
    {
        "InProgress" => "hcs-status-badge hcs-status-badge--info",
        "Waiting" => "hcs-status-badge hcs-status-badge--warning",
        "Completed" => "hcs-status-badge hcs-status-badge--success",
        "Cancelled" => "hcs-status-badge hcs-status-badge--danger",
        _ => "hcs-status-badge hcs-status-badge--todo"
    };

    public static string PriorityBadgeClass(string priority) => priority switch
    {
        "High" or "Urgent" => "hcs-priority-badge hcs-priority-badge--high",
        "Low" => "hcs-priority-badge hcs-priority-badge--low",
        _ => "hcs-priority-badge hcs-priority-badge--normal"
    };

    public static string ProgressBadgeClass(int percent) => percent >= 100
        ? "hcs-progress-badge hcs-progress-badge--done"
        : "hcs-progress-badge";

    public static int ProjectProgress(string status) => status switch
    {
        "Completed" => 100,
        "Active" => 55,
        "Cancelled" => 0,
        _ => 15
    };

    public const int MaxStars = 5;

    /// <summary>Survey scores are persisted on a 0-100 scale but collected as 1-5 stars.</summary>
    public static int ScoreToStars(decimal score) =>
        score <= 0 ? 0 : Math.Clamp((int)Math.Round(score / 20m, MidpointRounding.AwayFromZero), 1, MaxStars);

    public static decimal StarsToScore(int stars) => stars * 20m;

    public static string FormatRange(DateTime start, DateTime end) =>
        $"{start.ToLocalTime():dd/MM/yyyy HH:mm} - {end.ToLocalTime():dd/MM/yyyy HH:mm}";

    public static string FormatDay(DateTime value) => value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string EventHref(string relatedType, string? relatedId, Guid eventId) =>
        RelatedEntityHref(relatedType, relatedId) ?? $"/calendar-event-detail/{eventId}";

    public static string? RelatedEntityHref(string relatedType, string? relatedId)
    {
        if (!Guid.TryParse(relatedId, out var id)) return null;
        if (string.Equals(relatedType, "PROJECT", StringComparison.OrdinalIgnoreCase))
            return $"/project-detail/{id}";
        if (string.Equals(relatedType, "TASK", StringComparison.OrdinalIgnoreCase))
            return $"/project-task-detail/{id}";
        return null;
    }
}
