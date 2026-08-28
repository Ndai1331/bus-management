namespace HCS.WorkManagementService.Contracts;

public static class WorkPermissions
{
    public const string Projects = "WorkManagement.Projects";
    public const string Tasks = "WorkManagement.ProjectTasks";
    public const string Calendar = "WorkManagement.Calendar";
    public const string Surveys = "WorkManagement.Surveys";
    public const string SurveyManagement = "WorkManagement.SurveyManagement";
    public const string Reports = "WorkManagement.Reports";
    public const string Dashboard = "WorkManagement.Dashboard";
}

public sealed record PagedWorkDto<T>(long TotalCount, IReadOnlyList<T> Items);

public sealed record ProjectDto(Guid Id, string Code, string Name, string? Description, DateTime StartDate,
    DateTime EndDate, string Status, Guid? OwnerDepartmentId, Guid OwnerUserId, int MemberCount = 0, int TaskCount = 0);
public sealed record ProjectDetailDto(ProjectDto Project, IReadOnlyList<ProjectMemberDto> Members,
    IReadOnlyList<ProjectTaskDto> Tasks);
public sealed record CreateProjectDto(string Code, string Name, string? Description, DateTime StartDate,
    DateTime EndDate, string Status, Guid? OwnerDepartmentId);
public sealed record UpdateProjectDto(string Name, string? Description, DateTime StartDate, DateTime EndDate, string Status);
public sealed record ProjectMemberDto(Guid Id, Guid ProjectId, Guid UserId, string Role, bool IsActive);
public sealed record AddProjectMemberDto(Guid UserId, string Role);

public sealed record ProjectTaskDto(Guid Id, Guid ProjectId, Guid? ParentTaskId, string Code, string Title,
    string? Description, DateTime StartDate, DateTime DueDate, string Priority, string Status, int ProgressPercent);
public sealed record CreateProjectTaskDto(Guid ProjectId, Guid? ParentTaskId, string Code, string Title,
    string? Description, DateTime StartDate, DateTime DueDate, string Priority, string Status, int ProgressPercent);
public sealed record UpdateProjectTaskDto(string Title, string? Description, DateTime StartDate, DateTime DueDate,
    string Priority, string Status, int ProgressPercent);
public sealed record TaskAssignmentDto(Guid Id, Guid ProjectTaskId, Guid UserId, string AssignmentType);
public sealed record AddTaskAssignmentDto(Guid UserId, string AssignmentType);
public sealed record TaskDocumentReferenceDto(Guid Id, Guid ProjectTaskId, Guid DocumentId, string? DocumentCode);
public sealed record AddTaskDocumentReferenceDto(Guid DocumentId, string? DocumentCode);
public sealed record ProjectTaskDetailDto(ProjectTaskDto Task, IReadOnlyList<TaskAssignmentDto> Assignments,
    IReadOnlyList<TaskDocumentReferenceDto> Documents);

public sealed record CalendarEventDto(Guid Id, string Title, string? Description, DateTime StartTime, DateTime EndTime,
    bool AllDay, string EventType, string? Location, string RelatedType, string? RelatedId, string Visibility,
    IReadOnlyList<Guid> ParticipantUserIds);
public sealed record CreateCalendarEventDto(string Title, string? Description, DateTime StartTime, DateTime EndTime,
    bool AllDay, string EventType, string? Location, string RelatedType, string? RelatedId, string Visibility,
    IReadOnlyList<Guid>? ParticipantUserIds);
public sealed record UpdateCalendarEventDto(string Title, string? Description, DateTime StartTime, DateTime EndTime,
    bool AllDay, string EventType, string? Location, string RelatedType, string? RelatedId, string Visibility,
    IReadOnlyList<Guid>? ParticipantUserIds);

public sealed record SurveyCriteriaDto(Guid Id, string Code, string Name, int SortOrder, bool IsActive,
    Guid? LocationId = null, string? Image = null);
public sealed record SurveyLocationDto(Guid Id, string Code, string Name, Guid? OrganizationUnitId, bool IsActive,
    string? Description = null);
public sealed record SurveySessionDto(Guid Id, string Code, string Name, DateTime StartsAt, DateTime EndsAt,
    string Status, Guid? LocationId, string? FullName = null, string? PhoneNumber = null, string? PatientCode = null,
    DateTime? SurveyTime = null, string? DeviceType = null, string? Note = null, string? SessionDisplay = null);
public sealed record SurveyResultDto(Guid Id, Guid SessionId, Guid CriteriaId, Guid? RespondentUserId,
    decimal Score, string? Comment);
public sealed record CreateSurveyCriteriaDto(string Code, string Name, int SortOrder,
    Guid? LocationId = null, string? Image = null);
public sealed record UpdateSurveyCriteriaDto(string Name, int SortOrder, bool IsActive,
    Guid? LocationId = null, string? Image = null);
public sealed record CreateSurveyLocationDto(string Code, string Name, Guid? OrganizationUnitId,
    string? Description = null);
public sealed record UpdateSurveyLocationDto(string Name, Guid? OrganizationUnitId, bool IsActive,
    string? Description = null);
public sealed record CreateSurveySessionDto(string Code, string Name, DateTime StartsAt, DateTime EndsAt, Guid? LocationId);
public sealed record UpdateSurveySessionDto(string Name, DateTime StartsAt, DateTime EndsAt, Guid? LocationId);
public sealed record ChangeSurveySessionStatusDto(string Status);
public sealed record SubmitSurveyResultDto(Guid CriteriaId, Guid? RespondentUserId, decimal Score, string? Comment);
public sealed record CreatePublicSurveySessionDto(Guid LocationId, string FullName, string PhoneNumber,
    string? PatientCode, DateTime SurveyTime, string? DeviceType, string? Note, string? SessionDisplay = null);
public sealed record SurveyResultSessionSummaryDto(Guid SurveyResultId, Guid SurveySessionId, Guid CriteriaId,
    string SurveyCriteriaName, decimal Score, string? FullName, string? PhoneNumber, string? PatientCode,
    string? Note, DateTime SurveyTime);
public sealed record SurveyResultSessionDetailDto(Guid SurveyResultId, Guid SurveySessionId, Guid CriteriaId,
    string SurveyCriteriaName, decimal Score, string? Comment);
public sealed record SurveyResultStatisticsDto(int TotalReviews, IReadOnlyDictionary<int, int> ScoreDistribution,
    IReadOnlyDictionary<string, decimal> CriteriaAverageScores);
public sealed record SurveyFileReferenceDto(Guid Id, Guid SessionId, string FileName, string ContentType, long Size);

public sealed record DashboardDto(int ActiveProjects, int OpenTasks, int OverdueTasks, int ActiveSurveys,
    DateTime CalculatedAt);
public sealed record ReportRowDto(string Dimension, string Key, string Label, decimal Value, DateTime RefreshedAt);
public sealed record WorkAssetDto(string BlobName, string FileName, string ContentType, long Size);
