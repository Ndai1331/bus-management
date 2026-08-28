using System;
using System.Collections.Generic;

namespace HCS.Blazor.Client.Work;

public sealed record PagedWorkResponse<T>(long TotalCount, List<T> Items);

public sealed record ProjectDto(
    Guid Id, string Code, string Name, string? Description, DateTime StartDate, DateTime EndDate,
    string Status, Guid? OwnerDepartmentId, Guid OwnerUserId, int MemberCount = 0, int TaskCount = 0);

public sealed record ProjectMemberDto(Guid Id, Guid ProjectId, Guid UserId, string Role, bool IsActive);
public sealed record ProjectTaskDto(
    Guid Id, Guid ProjectId, Guid? ParentTaskId, string Code, string Title, string? Description,
    DateTime StartDate, DateTime DueDate, string Priority, string Status, int ProgressPercent);
public sealed record ProjectDetailDto(ProjectDto Project, List<ProjectMemberDto> Members, List<ProjectTaskDto> Tasks);
public sealed record TaskAssignmentDto(Guid Id, Guid ProjectTaskId, Guid UserId, string AssignmentType);
public sealed record TaskDocumentReferenceDto(Guid Id, Guid ProjectTaskId, Guid DocumentId, string? DocumentCode);
public sealed record ProjectTaskDetailDto(
    ProjectTaskDto Task, List<TaskAssignmentDto> Assignments, List<TaskDocumentReferenceDto> Documents);

public sealed record CreateProjectRequest(
    string Code, string Name, string? Description, DateTime StartDate, DateTime EndDate, string Status, Guid? OwnerDepartmentId);
public sealed record UpdateProjectRequest(
    string Name, string? Description, DateTime StartDate, DateTime EndDate, string Status);
public sealed record AddProjectMemberRequest(Guid UserId, string Role);
public sealed record CreateProjectTaskRequest(
    Guid ProjectId, Guid? ParentTaskId, string Code, string Title, string? Description,
    DateTime StartDate, DateTime DueDate, string Priority, string Status, int ProgressPercent);
public sealed record UpdateProjectTaskRequest(
    string Title, string? Description, DateTime StartDate, DateTime DueDate, string Priority, string Status, int ProgressPercent);
public sealed record AddTaskAssignmentRequest(Guid UserId, string AssignmentType);
public sealed record AddTaskDocumentRequest(Guid DocumentId, string? DocumentCode);

public sealed record CalendarEventDto(
    Guid Id, string Title, string? Description, DateTime StartTime, DateTime EndTime, bool AllDay,
    string EventType, string? Location, string RelatedType, string? RelatedId, string Visibility,
    List<Guid> ParticipantUserIds);
public sealed record UpsertCalendarEventRequest(
    string Title, string? Description, DateTime StartTime, DateTime EndTime, bool AllDay,
    string EventType, string? Location, string RelatedType, string? RelatedId, string Visibility,
    IReadOnlyList<Guid>? ParticipantUserIds);

public sealed record SurveyCriteriaDto(Guid Id, string Code, string Name, int SortOrder, bool IsActive,
    Guid? LocationId = null, string? Image = null);
public sealed record SurveyLocationDto(Guid Id, string Code, string Name, Guid? OrganizationUnitId, bool IsActive,
    string? Description = null);
public sealed record SurveySessionDto(Guid Id, string Code, string Name, DateTime StartsAt, DateTime EndsAt, string Status,
    Guid? LocationId, string? FullName = null, string? PhoneNumber = null, string? PatientCode = null,
    DateTime? SurveyTime = null, string? DeviceType = null, string? Note = null, string? SessionDisplay = null);
public sealed record SurveyResultDto(Guid Id, Guid SessionId, Guid CriteriaId, Guid? RespondentUserId, decimal Score, string? Comment);
public sealed record SurveyFileDto(Guid Id, Guid SessionId, string FileName, string ContentType, long Size);
public sealed record CreateSurveyCriteriaRequest(string Code, string Name, int SortOrder, Guid? LocationId = null, string? Image = null);
public sealed record UpdateSurveyCriteriaRequest(string Name, int SortOrder, bool IsActive, Guid? LocationId = null, string? Image = null);
public sealed record CreateSurveyLocationRequest(string Code, string Name, Guid? OrganizationUnitId, string? Description = null);
public sealed record UpdateSurveyLocationRequest(string Name, Guid? OrganizationUnitId, bool IsActive, string? Description = null);
public sealed record CreateSurveySessionRequest(string Code, string Name, DateTime StartsAt, DateTime EndsAt, Guid? LocationId);
public sealed record UpdateSurveySessionRequest(string Name, DateTime StartsAt, DateTime EndsAt, Guid? LocationId);
public sealed record ChangeSurveySessionStatusRequest(string Status);
public sealed record SubmitSurveyResultRequest(Guid CriteriaId, Guid? RespondentUserId, decimal Score, string? Comment);
public sealed record CreatePublicSurveySessionRequest(Guid LocationId, string FullName, string PhoneNumber,
    string? PatientCode, DateTime SurveyTime, string? DeviceType, string? Note, string? SessionDisplay = null);
public sealed record SurveyResultSessionSummaryDto(Guid SurveyResultId, Guid SurveySessionId, Guid CriteriaId,
    string SurveyCriteriaName, decimal Score, string? FullName, string? PhoneNumber, string? PatientCode,
    string? Note, DateTime SurveyTime);
public sealed record SurveyResultSessionDetailDto(Guid SurveyResultId, Guid SurveySessionId, Guid CriteriaId,
    string SurveyCriteriaName, decimal Score, string? Comment);
public sealed record SurveyResultStatisticsDto(int TotalReviews, Dictionary<int, int> ScoreDistribution,
    Dictionary<string, decimal> CriteriaAverageScores);

public sealed record DashboardDto(
    int ActiveProjects, int OpenTasks, int OverdueTasks, int ActiveSurveys, DateTime CalculatedAt);
public sealed record ReportRowDto(string Dimension, string Key, string Label, decimal Value, DateTime RefreshedAt);

public sealed record WorkListQuery(string? Filter, string? Status, int SkipCount, int MaxResultCount);
