using HCS.WorkManagementService.Domain;
using Volo.Abp;

namespace HCS.WorkManagementService.Tests;

public sealed class DomainInvariantTests
{
    [Fact]
    public void Project_stores_unspecified_dates_as_utc()
    {
        var start = new DateTime(2026, 8, 19);
        Assert.Equal(DateTimeKind.Unspecified, start.Kind);
        var project = new Project(Guid.NewGuid(), "P-1", "Project", start, start.AddDays(1), "Active", null, Guid.NewGuid());
        Assert.Equal(DateTimeKind.Utc, project.StartDate.Kind);
        Assert.Equal(DateTimeKind.Utc, project.EndDate.Kind);
        Assert.Equal(new DateTime(2026, 8, 19, 0, 0, 0, DateTimeKind.Utc), project.StartDate);
    }

    [Fact]
    public void Project_rejects_inverted_date_range()
    {
        Assert.Throws<BusinessException>(() => new Project(Guid.NewGuid(), "P-1", "Project",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(-1), "Active", null, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Task_rejects_progress_outside_percentage(int progress)
    {
        Assert.Throws<BusinessException>(() => new ProjectTask(Guid.NewGuid(), Guid.NewGuid(), null, "T-1", "Task",
            null, DateTime.UtcNow, DateTime.UtcNow.AddDays(1), "Normal", "Open", progress));
    }

    [Fact]
    public void Task_progress_at_100_promotes_status_to_completed()
    {
        var task = new ProjectTask(Guid.NewGuid(), Guid.NewGuid(), null, "T-100", "Task",
            null, DateTime.UtcNow, DateTime.UtcNow.AddDays(1), "Normal", "InProgress", 100);

        Assert.Equal(WorkConsts.CompletedStatus, task.Status);
        Assert.Equal(100, task.ProgressPercent);
    }

    [Fact]
    public void Task_completed_status_promotes_progress_to_100()
    {
        var task = new ProjectTask(Guid.NewGuid(), Guid.NewGuid(), null, "T-done", "Task",
            null, DateTime.UtcNow, DateTime.UtcNow.AddDays(1), "Normal", WorkConsts.CompletedStatus, 40);

        Assert.Equal(WorkConsts.CompletedStatus, task.Status);
        Assert.Equal(100, task.ProgressPercent);
    }

    [Fact]
    public void Project_calendar_event_uses_project_identity()
    {
        var start = DateTime.UtcNow;
        var project = new Project(Guid.NewGuid(), "P-1", "Project", start, start.AddDays(2), "Active", null, Guid.NewGuid());
        var calendar = WorkCalendarSync.CreateProjectEvent(Guid.NewGuid(), project);
        Assert.Equal(WorkCalendarSync.ProjectRelatedType, calendar.RelatedType);
        Assert.Equal(project.Id.ToString(), calendar.RelatedId);
        Assert.Equal(project.Name, calendar.Title);
        Assert.Equal(project.StartDate, calendar.StartTime);
        Assert.Equal(project.EndDate, calendar.EndTime);
        Assert.Equal(WorkCalendarSync.SyncedVisibility, calendar.Visibility);
    }

    [Fact]
    public void Task_calendar_event_uses_task_identity()
    {
        var start = DateTime.UtcNow;
        var owner = Guid.NewGuid();
        var task = new ProjectTask(Guid.NewGuid(), Guid.NewGuid(), null, "T-1", "Task",
            null, start, start.AddDays(1), "Normal", "Open", 0);
        var calendar = WorkCalendarSync.CreateTaskEvent(Guid.NewGuid(), task, owner);
        Assert.Equal(WorkCalendarSync.TaskRelatedType, calendar.RelatedType);
        Assert.Equal(task.Id.ToString(), calendar.RelatedId);
        Assert.Equal(task.Title, calendar.Title);
        Assert.Equal(owner, calendar.OwnerUserId);
    }

    [Fact]
    public void Calendar_rejects_inverted_date_range()
    {
        Assert.Throws<BusinessException>(() => new CalendarEvent(Guid.NewGuid(), "Meeting", null,
            DateTime.UtcNow, DateTime.UtcNow.AddMinutes(-1), false, "Meeting", null, "None", null, "Internal", Guid.NewGuid()));
    }

    [Fact]
    public void Calendar_change_rejects_inverted_date_range()
    {
        var start = DateTime.UtcNow;
        var calendar = new CalendarEvent(Guid.NewGuid(), "Meeting", null, start, start.AddHours(1), false,
            "Meeting", null, "NONE", null, "Internal", Guid.NewGuid());
        Assert.Throws<BusinessException>(() => calendar.Change("Updated", null, start, start.AddMinutes(-1), false,
            "Meeting", null, "PROJECT", null, "Private"));
    }

    [Fact]
    public void Survey_session_change_rejects_inverted_date_range()
    {
        var start = DateTime.UtcNow;
        var session = new SurveySession(Guid.NewGuid(), "S-1", "Survey", start, start.AddDays(1), null, Guid.NewGuid());
        Assert.Throws<BusinessException>(() => session.Change("Updated", start, start.AddDays(-1), null));
        session.Change("Updated", start, start.AddDays(2), null);
        Assert.Equal("Updated", session.Name);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void Survey_rejects_score_outside_range(decimal score)
    {
        Assert.Throws<BusinessException>(() => new SurveyResult(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, score, null));
    }
}
