using HCS.DocumentService.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCS.DocumentService.Controllers;

[ApiController, Authorize(Policy = Documents.DocumentPermissions.WorkflowView), Route("api/workflows")]
public sealed class WorkflowsController(IWorkflowAppService workflows) : ControllerBase
{
    [HttpGet("kinds")]
    public Task<IReadOnlyList<WorkflowKindDto>> GetKinds(CancellationToken cancellationToken) =>
        workflows.GetKindsAsync(cancellationToken);
    [HttpGet("kinds/{id:guid}")]
    public async Task<ActionResult<WorkflowKindDto>> GetKind(Guid id, CancellationToken cancellationToken) =>
        await workflows.GetKindAsync(id, cancellationToken) is { } result ? Ok(result) : NotFound();
    [HttpPost("kinds"), Authorize(Policy = Documents.DocumentPermissions.WorkflowManage)]
    public Task<Guid> CreateKind(CreateWorkflowKindRequest input, CancellationToken cancellationToken) =>
        workflows.CreateKindAsync(input, cancellationToken);
    [HttpPut("kinds/{id:guid}"), Authorize(Policy = Documents.DocumentPermissions.WorkflowManage)]
    public async Task<IActionResult> UpdateKind(Guid id, UpdateWorkflowKindRequest input, CancellationToken cancellationToken)
    {
        await workflows.UpdateKindAsync(id, input, cancellationToken);
        return NoContent();
    }
    [HttpDelete("kinds/{id:guid}"), Authorize(Policy = Documents.DocumentPermissions.WorkflowManage)]
    public async Task<IActionResult> DeleteKind(Guid id, CancellationToken cancellationToken)
    {
        await workflows.DeleteKindAsync(id, cancellationToken);
        return NoContent();
    }
    [HttpGet("definitions")]
    public Task<IReadOnlyList<WorkflowDefinitionDto>> GetDefinitions(CancellationToken cancellationToken) =>
        workflows.GetDefinitionsAsync(cancellationToken);
    [HttpGet("definitions/{id:guid}")]
    public async Task<ActionResult<WorkflowDefinitionDto>> GetDefinition(Guid id, CancellationToken cancellationToken) =>
        await workflows.GetDefinitionAsync(id, cancellationToken) is { } result ? Ok(result) : NotFound();
    [HttpGet("templates")]
    public Task<IReadOnlyList<WorkflowTemplateDto>> GetTemplates(CancellationToken cancellationToken) =>
        workflows.GetTemplatesAsync(cancellationToken);
    [HttpGet("templates/{id:guid}")]
    public async Task<ActionResult<WorkflowTemplateDto>> GetTemplate(Guid id, CancellationToken cancellationToken) =>
        await workflows.GetTemplateAsync(id, cancellationToken) is { } result ? Ok(result) : NotFound();
    [HttpGet("instances")]
    public Task<IReadOnlyList<WorkflowInstanceDto>> GetInstances([FromQuery] Guid? documentId,
        [FromQuery] WorkflowInstanceStatus? status, CancellationToken cancellationToken) =>
        workflows.GetInstancesAsync(documentId, status, cancellationToken);
    [HttpGet("instances/{id:guid}")]
    public async Task<ActionResult<WorkflowInstanceDto>> GetInstance(Guid id, CancellationToken cancellationToken) =>
        await workflows.GetInstanceAsync(id, cancellationToken) is { } result ? Ok(result) : NotFound();
    [HttpPost("definitions"), Authorize(Policy = Documents.DocumentPermissions.WorkflowManage)]
    public Task<Guid> CreateDefinition(CreateWorkflowDefinitionRequest input, CancellationToken cancellationToken) => workflows.CreateDefinitionAsync(input, cancellationToken);
    [HttpPut("definitions/{id:guid}"), Authorize(Policy = Documents.DocumentPermissions.WorkflowManage)]
    public async Task<IActionResult> UpdateDefinition(Guid id, UpdateWorkflowDefinitionRequest input, CancellationToken cancellationToken)
    {
        await workflows.UpdateDefinitionAsync(id, input, cancellationToken);
        return NoContent();
    }
    [HttpDelete("definitions/{id:guid}"), Authorize(Policy = Documents.DocumentPermissions.WorkflowManage)]
    public async Task<IActionResult> DeleteDefinition(Guid id, CancellationToken cancellationToken)
    {
        await workflows.DeleteDefinitionAsync(id, cancellationToken);
        return NoContent();
    }
    [HttpPost("templates"), Authorize(Policy = Documents.DocumentPermissions.WorkflowManage)]
    public Task<WorkflowTemplateDto> CreateTemplate(CreateWorkflowTemplateRequest input, CancellationToken cancellationToken) => workflows.CreateTemplateAsync(input, cancellationToken);
    [HttpPut("templates/{id:guid}"), Authorize(Policy = Documents.DocumentPermissions.WorkflowManage)]
    public Task<WorkflowTemplateDto> UpdateTemplate(Guid id, UpdateWorkflowTemplateRequest input, CancellationToken cancellationToken) =>
        workflows.UpdateTemplateAsync(id, input, cancellationToken);
    [HttpPost("templates/{id:guid}/active"), Authorize(Policy = Documents.DocumentPermissions.WorkflowManage)]
    public Task<WorkflowTemplateDto> SetTemplateActive(Guid id, [FromBody] bool isActive, CancellationToken cancellationToken) =>
        workflows.SetTemplateActiveAsync(id, isActive, cancellationToken);
    [HttpPost("templates/{id:guid}/files"), Authorize(Policy = Documents.DocumentPermissions.WorkflowManage)]
    [RequestSizeLimit(Documents.DocumentFileService.MaxFileSize)]
    public async Task<ActionResult<WorkflowTemplateDto>> UploadTemplateFile(Guid id, [FromQuery] string kind, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0) return BadRequest();
        await using var stream = file.OpenReadStream();
        return await workflows.UploadTemplateFileAsync(id, kind, file.FileName, file.ContentType, stream, file.Length, cancellationToken);
    }
    [HttpGet("templates/{id:guid}/files/{kind}/content")]
    public async Task<IActionResult> DownloadTemplateFile(Guid id, string kind, CancellationToken cancellationToken)
    {
        var result = await workflows.OpenTemplateFileAsync(id, kind, cancellationToken);
        var fileName = Path.GetFileName(result.FileName);
        if (string.IsNullOrWhiteSpace(fileName)) fileName = "template";
        return File(result.Content, result.ContentType, fileName, enableRangeProcessing: true);
    }
    [HttpPost("instances"), Authorize(Policy = Documents.DocumentPermissions.WorkflowStart)]
    public Task<WorkflowInstanceDto> Start(StartWorkflowRequest input, CancellationToken cancellationToken) => workflows.StartAsync(input, cancellationToken);
    [HttpGet("definitions/{id:guid}/assignee-candidates"), Authorize(Policy = Documents.DocumentPermissions.WorkflowStart)]
    public Task<IReadOnlyList<WorkflowStepCandidateGroupDto>> GetAssigneeCandidates(Guid id, CancellationToken cancellationToken) =>
        workflows.GetAssigneeCandidatesAsync(id, cancellationToken);
    [HttpPost("tasks/{taskId:guid}/decision"), Authorize(Policy = Documents.DocumentPermissions.WorkflowDecide)]
    public Task<WorkflowInstanceDto> Decide(Guid taskId, DecideApprovalTaskRequest input, CancellationToken cancellationToken) => workflows.DecideAsync(taskId, input, cancellationToken);
    [HttpPost("tasks/{taskId:guid}/extend"), Authorize(Policy = Documents.DocumentPermissions.WorkflowDecide)]
    public Task<WorkflowInstanceDto> ExtendDueDate(Guid taskId, ExtendWorkflowDueDateRequest input, CancellationToken cancellationToken) => workflows.ExtendDueDateAsync(taskId, input, cancellationToken);
    [HttpPost("instances/{id:guid}/resubmit"), Authorize(Policy = Documents.DocumentPermissions.WorkflowStart)]
    public Task<WorkflowInstanceDto> Resubmit(Guid id, [FromBody] string idempotencyKey, CancellationToken cancellationToken) =>
        workflows.ResubmitAsync(id, idempotencyKey, cancellationToken);
}
