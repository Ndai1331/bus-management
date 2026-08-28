using HCS.WorkManagementService.Application;
using HCS.WorkManagementService.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCS.WorkManagementService.Controllers;

[ApiController, Authorize(Policy = WorkPermissions.Surveys), Route("api/surveys")]
public sealed class SurveysController(SurveyAppService service, WorkAssetService assets) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("public/locations/{locationId:guid}")]
    public Task<SurveyLocationDto> GetPublicLocation(Guid locationId, CancellationToken ct) => service.GetPublicLocationAsync(locationId, ct);

    [AllowAnonymous]
    [HttpGet("public/criteria")]
    public Task<List<SurveyCriteriaDto>> GetPublicCriteria(Guid locationId, CancellationToken ct) => service.GetPublicCriteriaAsync(locationId, ct);

    [AllowAnonymous]
    [HttpPost("public/sessions")]
    public Task<SurveySessionDto> CreatePublicSession(CreatePublicSurveySessionDto input, CancellationToken ct) => service.CreatePublicSessionAsync(input, ct);

    [AllowAnonymous]
    [HttpPost("public/sessions/{sessionId:guid}/results")]
    public Task<List<SurveyResultDto>> SubmitPublicResults(Guid sessionId, List<SubmitSurveyResultDto> input, CancellationToken ct) =>
        service.SubmitPublicResultsAsync(sessionId, input, ct);

    [AllowAnonymous]
    [HttpPost("public/sessions/{sessionId:guid}/files"), RequestSizeLimit(WorkAssetService.MaxFileSize)]
    public async Task<SurveyFileReferenceDto> UploadPublic(Guid sessionId, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await assets.SavePublicSurveyFileAsync(sessionId, stream, file.FileName, file.ContentType, file.Length, ct);
    }

    [Authorize(Policy = WorkPermissions.Surveys)]
    [HttpGet("results/statistics")]
    public Task<SurveyResultStatisticsDto> GetStatistics(Guid? locationId, CancellationToken ct) => service.GetStatisticsAsync(locationId, ct);

    [Authorize(Policy = WorkPermissions.Surveys)]
    [HttpGet("results/summaries")]
    public Task<PagedWorkDto<SurveyResultSessionSummaryDto>> GetResultSummaries(Guid? locationId, int skip = 0, int take = 20, CancellationToken ct = default) =>
        service.GetResultSummariesAsync(locationId, skip, take, ct);

    [Authorize(Policy = WorkPermissions.Surveys)]
    [HttpGet("results/{sessionId:guid}/details")]
    public Task<List<SurveyResultSessionDetailDto>> GetResultDetails(Guid sessionId, Guid? locationId, CancellationToken ct) =>
        service.GetResultDetailsAsync(sessionId, locationId, ct);

    [HttpGet("criteria")]
    public Task<List<SurveyCriteriaDto>> GetCriteria(CancellationToken ct) => service.GetCriteriaAsync(ct);
    [HttpPost("criteria"), Authorize(Policy = WorkPermissions.SurveyManagement)]
    public Task<SurveyCriteriaDto> CreateCriteria(CreateSurveyCriteriaDto input, CancellationToken ct) => service.CreateCriteriaAsync(input, ct);
    [HttpPut("criteria/{id:guid}"), Authorize(Policy = WorkPermissions.SurveyManagement)]
    public Task<SurveyCriteriaDto> UpdateCriteria(Guid id, UpdateSurveyCriteriaDto input, CancellationToken ct) => service.UpdateCriteriaAsync(id, input, ct);
    [HttpDelete("criteria/{id:guid}"), Authorize(Policy = WorkPermissions.SurveyManagement)]
    public async Task<IActionResult> DeleteCriteria(Guid id, CancellationToken ct)
    {
        await service.DeleteCriteriaAsync(id, ct);
        return NoContent();
    }

    [HttpGet("locations")]
    public Task<List<SurveyLocationDto>> GetLocations(CancellationToken ct) => service.GetLocationsAsync(ct);
    [HttpPost("locations"), Authorize(Policy = WorkPermissions.SurveyManagement)]
    public Task<SurveyLocationDto> CreateLocation(CreateSurveyLocationDto input, CancellationToken ct) => service.CreateLocationAsync(input, ct);
    [HttpPut("locations/{id:guid}"), Authorize(Policy = WorkPermissions.SurveyManagement)]
    public Task<SurveyLocationDto> UpdateLocation(Guid id, UpdateSurveyLocationDto input, CancellationToken ct) => service.UpdateLocationAsync(id, input, ct);
    [HttpDelete("locations/{id:guid}"), Authorize(Policy = WorkPermissions.SurveyManagement)]
    public async Task<IActionResult> DeleteLocation(Guid id, CancellationToken ct)
    {
        await service.DeleteLocationAsync(id, ct);
        return NoContent();
    }

    [HttpGet("sessions")]
    public Task<List<SurveySessionDto>> GetSessions(Guid? locationId, CancellationToken ct) => service.GetSessionsAsync(locationId, ct);
    [HttpPost("sessions"), Authorize(Policy = WorkPermissions.SurveyManagement)]
    public Task<SurveySessionDto> CreateSession(CreateSurveySessionDto input, CancellationToken ct) => service.CreateSessionAsync(input, ct);
    [HttpPut("sessions/{sessionId:guid}"), Authorize(Policy = WorkPermissions.SurveyManagement)]
    public Task<SurveySessionDto> UpdateSession(Guid sessionId, UpdateSurveySessionDto input, CancellationToken ct) =>
        service.UpdateSessionAsync(sessionId, input, ct);
    [HttpPost("sessions/{sessionId:guid}/status"), Authorize(Policy = WorkPermissions.SurveyManagement)]
    public Task<SurveySessionDto> ChangeStatus(Guid sessionId, ChangeSurveySessionStatusDto input, CancellationToken ct) =>
        service.ChangeSessionStatusAsync(sessionId, input, ct);
    [HttpDelete("sessions/{sessionId:guid}"), Authorize(Policy = WorkPermissions.SurveyManagement)]
    public async Task<IActionResult> DeleteSession(Guid sessionId, CancellationToken ct)
    {
        await service.DeleteSessionAsync(sessionId, ct);
        return NoContent();
    }

    [HttpGet("sessions/{sessionId:guid}/results")]
    public Task<List<SurveyResultDto>> GetResults(Guid sessionId, CancellationToken ct) => service.GetResultsAsync(sessionId, ct);
    [HttpPost("sessions/{sessionId:guid}/results")]
    public Task<SurveyResultDto> Submit(Guid sessionId, SubmitSurveyResultDto input, CancellationToken ct) =>
        service.SubmitAsync(sessionId, input, ct);
    [HttpGet("sessions/{sessionId:guid}/files")]
    public Task<List<SurveyFileReferenceDto>> GetFiles(Guid sessionId, CancellationToken ct) =>
        service.GetSessionFilesAsync(sessionId, ct);
    [HttpPost("sessions/{sessionId:guid}/files"), RequestSizeLimit(WorkAssetService.MaxFileSize)]
    public async Task<SurveyFileReferenceDto> Upload(Guid sessionId, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await assets.SaveSurveyFileAsync(sessionId, stream, file.FileName, file.ContentType, file.Length, ct);
    }
    [HttpGet("files/{fileId:guid}/content")]
    public async Task<IActionResult> Download(Guid fileId, CancellationToken ct)
    {
        var result = await assets.GetSurveyFileAsync(fileId, ct);
        return File(result.Stream, result.File.ContentType, result.File.FileName, enableRangeProcessing: true);
    }
}
