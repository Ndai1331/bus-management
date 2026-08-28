using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HCS.Blazor.Client.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace HCS.Blazor.Client.Work;

public sealed class WorkManagementClient(IHttpClientFactory httpClientFactory)
{
    public const int MaxPageSize = 100;

    public Task<PagedWorkResponse<ProjectDto>> GetProjectsAsync(WorkListQuery query, CancellationToken cancellationToken = default) =>
        GetAsync<PagedWorkResponse<ProjectDto>>(BuildListUri("/api/projects", query), cancellationToken);

    public Task<ProjectDetailDto> GetProjectAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<ProjectDetailDto>($"/api/projects/{id:D}", cancellationToken);

    public Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<ProjectDto>(HttpMethod.Post, "/api/projects", request, cancellationToken);

    public Task<ProjectDto> UpdateProjectAsync(Guid id, UpdateProjectRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<ProjectDto>(HttpMethod.Put, $"/api/projects/{id:D}", request, cancellationToken);

    public Task DeleteProjectAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"/api/projects/{id:D}", cancellationToken);

    public Task<ProjectMemberDto> AddMemberAsync(Guid projectId, AddProjectMemberRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<ProjectMemberDto>(HttpMethod.Post, $"/api/projects/{projectId:D}/members", request, cancellationToken);

    public Task RemoveMemberAsync(Guid projectId, Guid memberId, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"/api/projects/{projectId:D}/members/{memberId:D}", cancellationToken);

    public Task SyncProjectChatAccessAsync(Guid projectId, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Post, $"/api/projects/{projectId:D}/chat-access", cancellationToken);

    public Task<PagedWorkResponse<ProjectTaskDto>> GetTasksAsync(Guid? projectId, WorkListQuery query, CancellationToken cancellationToken = default)
    {
        var uri = BuildListUri("/api/project-tasks", query);
        if (projectId.HasValue) uri += $"&projectId={projectId.Value:D}";
        return GetAsync<PagedWorkResponse<ProjectTaskDto>>(uri, cancellationToken);
    }

    public Task<ProjectTaskDetailDto> GetTaskAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<ProjectTaskDetailDto>($"/api/project-tasks/{id:D}", cancellationToken);

    public Task<ProjectTaskDto> CreateTaskAsync(CreateProjectTaskRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<ProjectTaskDto>(HttpMethod.Post, "/api/project-tasks", request, cancellationToken);

    public Task<ProjectTaskDto> UpdateTaskAsync(Guid id, UpdateProjectTaskRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<ProjectTaskDto>(HttpMethod.Put, $"/api/project-tasks/{id:D}", request, cancellationToken);

    public Task DeleteTaskAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"/api/project-tasks/{id:D}", cancellationToken);

    public Task<TaskAssignmentDto> AddAssignmentAsync(Guid taskId, AddTaskAssignmentRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<TaskAssignmentDto>(HttpMethod.Post, $"/api/project-tasks/{taskId:D}/assignments", request, cancellationToken);

    public Task RemoveAssignmentAsync(Guid taskId, Guid assignmentId, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"/api/project-tasks/{taskId:D}/assignments/{assignmentId:D}", cancellationToken);

    public Task<TaskDocumentReferenceDto> AddTaskDocumentAsync(Guid taskId, AddTaskDocumentRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<TaskDocumentReferenceDto>(HttpMethod.Post, $"/api/project-tasks/{taskId:D}/documents", request, cancellationToken);

    public Task RemoveTaskDocumentAsync(Guid taskId, Guid referenceId, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"/api/project-tasks/{taskId:D}/documents/{referenceId:D}", cancellationToken);

    public Task<List<CalendarEventDto>> GetCalendarAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder("/api/calendar?");
        if (from.HasValue) builder.Append($"from={Uri.EscapeDataString(from.Value.ToUniversalTime().ToString("O"))}&");
        if (to.HasValue) builder.Append($"to={Uri.EscapeDataString(to.Value.ToUniversalTime().ToString("O"))}");
        return GetAsync<List<CalendarEventDto>>(builder.ToString().TrimEnd('?', '&'), cancellationToken);
    }

    public Task<CalendarEventDto> GetCalendarEventAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<CalendarEventDto>($"/api/calendar/{id:D}", cancellationToken);

    public Task<CalendarEventDto> CreateCalendarEventAsync(UpsertCalendarEventRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CalendarEventDto>(HttpMethod.Post, "/api/calendar", request, cancellationToken);

    public Task<CalendarEventDto> UpdateCalendarEventAsync(Guid id, UpsertCalendarEventRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CalendarEventDto>(HttpMethod.Put, $"/api/calendar/{id:D}", request, cancellationToken);

    public Task DeleteCalendarEventAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"/api/calendar/{id:D}", cancellationToken);

    public Task<List<SurveyCriteriaDto>> GetCriteriaAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<SurveyCriteriaDto>>("/api/surveys/criteria", cancellationToken);
    public Task<SurveyLocationDto> GetPublicLocationAsync(Guid locationId, CancellationToken cancellationToken = default) =>
        GetAsync<SurveyLocationDto>($"/api/surveys/public/locations/{locationId:D}", cancellationToken);
    public Task<List<SurveyCriteriaDto>> GetPublicCriteriaAsync(Guid locationId, CancellationToken cancellationToken = default) =>
        GetAsync<List<SurveyCriteriaDto>>($"/api/surveys/public/criteria?locationId={locationId:D}", cancellationToken);
    public Task<SurveySessionDto> CreatePublicSessionAsync(CreatePublicSurveySessionRequest request,
        CancellationToken cancellationToken = default) =>
        SendAsync<SurveySessionDto>(HttpMethod.Post, "/api/surveys/public/sessions", request, cancellationToken);
    public Task<List<SurveyResultDto>> SubmitPublicResultsAsync(Guid sessionId,
        IReadOnlyList<SubmitSurveyResultRequest> request, CancellationToken cancellationToken = default) =>
        SendAsync<List<SurveyResultDto>>(HttpMethod.Post, $"/api/surveys/public/sessions/{sessionId:D}/results", request, cancellationToken);
    public Task<SurveyCriteriaDto> CreateCriteriaAsync(CreateSurveyCriteriaRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SurveyCriteriaDto>(HttpMethod.Post, "/api/surveys/criteria", request, cancellationToken);
    public Task<SurveyCriteriaDto> UpdateCriteriaAsync(Guid id, UpdateSurveyCriteriaRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SurveyCriteriaDto>(HttpMethod.Put, $"/api/surveys/criteria/{id:D}", request, cancellationToken);
    public Task DeleteCriteriaAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"/api/surveys/criteria/{id:D}", cancellationToken);

    public Task<List<SurveyLocationDto>> GetLocationsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<List<SurveyLocationDto>>("/api/surveys/locations", cancellationToken);
    public Task<SurveyLocationDto> CreateLocationAsync(CreateSurveyLocationRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SurveyLocationDto>(HttpMethod.Post, "/api/surveys/locations", request, cancellationToken);
    public Task<SurveyLocationDto> UpdateLocationAsync(Guid id, UpdateSurveyLocationRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SurveyLocationDto>(HttpMethod.Put, $"/api/surveys/locations/{id:D}", request, cancellationToken);
    public Task DeleteLocationAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"/api/surveys/locations/{id:D}", cancellationToken);

    public Task<List<SurveySessionDto>> GetSessionsAsync(Guid? locationId = null, CancellationToken cancellationToken = default) =>
        GetAsync<List<SurveySessionDto>>(
            locationId.HasValue ? $"/api/surveys/sessions?locationId={locationId.Value:D}" : "/api/surveys/sessions",
            cancellationToken);
    public Task<SurveySessionDto> CreateSessionAsync(CreateSurveySessionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SurveySessionDto>(HttpMethod.Post, "/api/surveys/sessions", request, cancellationToken);
    public Task<SurveySessionDto> UpdateSessionAsync(Guid id, UpdateSurveySessionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SurveySessionDto>(HttpMethod.Put, $"/api/surveys/sessions/{id:D}", request, cancellationToken);
    public Task<SurveySessionDto> ChangeSessionStatusAsync(Guid id, string status, CancellationToken cancellationToken = default) =>
        SendAsync<SurveySessionDto>(HttpMethod.Post, $"/api/surveys/sessions/{id:D}/status", new ChangeSurveySessionStatusRequest(status), cancellationToken);
    public Task DeleteSessionAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Delete, $"/api/surveys/sessions/{id:D}", cancellationToken);
    public Task<List<SurveyResultDto>> GetResultsAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        GetAsync<List<SurveyResultDto>>($"/api/surveys/sessions/{sessionId:D}/results", cancellationToken);
    public Task<SurveyResultDto> SubmitResultAsync(Guid sessionId, SubmitSurveyResultRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<SurveyResultDto>(HttpMethod.Post, $"/api/surveys/sessions/{sessionId:D}/results", request, cancellationToken);
    public Task<List<SurveyFileDto>> GetSessionFilesAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        GetAsync<List<SurveyFileDto>>($"/api/surveys/sessions/{sessionId:D}/files", cancellationToken);

    public async Task<SurveyFileDto> UploadSurveyFileAsync(Guid sessionId, IBrowserFile file, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream(25 * 1024 * 1024, cancellationToken);
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        content.Add(fileContent, "file", file.Name);
        using var response = await CreateClient().PostAsync($"/api/surveys/sessions/{sessionId:D}/files", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<SurveyFileDto>(cancellationToken: cancellationToken)
            ?? throw new BffApiException(HttpStatusCode.NoContent, "Gateway returned an empty response.");
    }

    public async Task<SurveyFileDto> UploadPublicSurveyFileAsync(Guid sessionId, IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream(25 * 1024 * 1024, cancellationToken);
        using var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        content.Add(fileContent, "file", file.Name);
        using var response = await CreateClient().PostAsync($"/api/surveys/public/sessions/{sessionId:D}/files", content, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<SurveyFileDto>(cancellationToken: cancellationToken)
            ?? throw new BffApiException(HttpStatusCode.NoContent, "Gateway returned an empty response.");
    }

    public Task<SurveyResultStatisticsDto> GetSurveyStatisticsAsync(Guid? locationId = null,
        CancellationToken cancellationToken = default) =>
        GetAsync<SurveyResultStatisticsDto>(
            locationId.HasValue ? $"/api/surveys/results/statistics?locationId={locationId.Value:D}" : "/api/surveys/results/statistics",
            cancellationToken);

    public Task<PagedWorkResponse<SurveyResultSessionSummaryDto>> GetSurveyResultSummariesAsync(Guid? locationId = null,
        int skip = 0, int take = 20, CancellationToken cancellationToken = default)
    {
        var uri = $"/api/surveys/results/summaries?skip={Math.Max(0, skip)}&take={Math.Clamp(take, 1, MaxPageSize)}";
        if (locationId.HasValue) uri += $"&locationId={locationId.Value:D}";
        return GetAsync<PagedWorkResponse<SurveyResultSessionSummaryDto>>(uri, cancellationToken);
    }

    public Task<List<SurveyResultSessionDetailDto>> GetSurveyResultDetailsAsync(Guid sessionId, Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        var uri = $"/api/surveys/results/{sessionId:D}/details";
        if (locationId.HasValue) uri += $"?locationId={locationId.Value:D}";
        return GetAsync<List<SurveyResultSessionDetailDto>>(uri, cancellationToken);
    }

    public Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default) =>
        GetAsync<DashboardDto>("/api/dashboard", cancellationToken);

    public Task<List<ReportRowDto>> GetReportsAsync(string? dimension = null, CancellationToken cancellationToken = default) =>
        GetAsync<List<ReportRowDto>>(
            string.IsNullOrWhiteSpace(dimension) ? "/api/reports" : $"/api/reports?dimension={Uri.EscapeDataString(dimension)}",
            cancellationToken);

    internal static string BuildListUri(string endpoint, WorkListQuery query)
    {
        var parameters = new List<string>
        {
            $"skip={Math.Max(0, query.SkipCount)}",
            $"take={Math.Clamp(query.MaxResultCount, 1, MaxPageSize)}"
        };
        if (!string.IsNullOrWhiteSpace(query.Filter))
            parameters.Add($"filter={Uri.EscapeDataString(SearchText.Normalize(query.Filter))}");
        if (!string.IsNullOrWhiteSpace(query.Status))
            parameters.Add($"status={Uri.EscapeDataString(query.Status.Trim())}");
        return $"{endpoint}?{string.Join('&', parameters)}";
    }

    private async Task<T> GetAsync<T>(string uri, CancellationToken cancellationToken)
    {
        using var response = await CreateClient().GetAsync(uri, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new BffApiException(HttpStatusCode.NoContent, "Gateway returned an empty response.");
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string uri, object payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = JsonContent.Create(payload) };
        using var response = await CreateClient().SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
            ?? throw new BffApiException(HttpStatusCode.NoContent, "Gateway returned an empty response.");
    }

    private async Task SendNoContentAsync(HttpMethod method, string uri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri);
        using var response = await CreateClient().SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new BffApiException(response.StatusCode, body);
    }

    private HttpClient CreateClient() => httpClientFactory.CreateClient("HCS.Bff");
}
