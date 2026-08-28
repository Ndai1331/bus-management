using HCS.WorkManagementService.Application;
using HCS.WorkManagementService.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCS.WorkManagementService.Controllers;

[ApiController, Authorize(Policy = WorkPermissions.Tasks), Route("api/project-tasks")]
public sealed class ProjectTasksController(ProjectTaskAppService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedWorkDto<ProjectTaskDto>> GetList(Guid? projectId, string? filter, string? status, int skip = 0, int take = 20, CancellationToken ct = default) =>
        service.GetListAsync(projectId, filter, status, skip, take, ct);

    [HttpGet("{id:guid}")]
    public Task<ProjectTaskDetailDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost]
    public Task<ProjectTaskDto> Create(CreateProjectTaskDto input, CancellationToken ct) => service.CreateAsync(input, ct);

    [HttpPut("{id:guid}")]
    public Task<ProjectTaskDto> Update(Guid id, UpdateProjectTaskDto input, CancellationToken ct) => service.UpdateAsync(id, input, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/assignments")]
    public Task<TaskAssignmentDto> AddAssignment(Guid id, AddTaskAssignmentDto input, CancellationToken ct) =>
        service.AddAssignmentAsync(id, input, ct);

    [HttpDelete("{id:guid}/assignments/{assignmentId:guid}")]
    public async Task<IActionResult> RemoveAssignment(Guid id, Guid assignmentId, CancellationToken ct)
    {
        await service.RemoveAssignmentAsync(id, assignmentId, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/documents")]
    public Task<TaskDocumentReferenceDto> AddDocument(Guid id, AddTaskDocumentReferenceDto input, CancellationToken ct) =>
        service.AddDocumentAsync(id, input, ct);

    [HttpDelete("{id:guid}/documents/{referenceId:guid}")]
    public async Task<IActionResult> RemoveDocument(Guid id, Guid referenceId, CancellationToken ct)
    {
        await service.RemoveDocumentAsync(id, referenceId, ct);
        return NoContent();
    }
}
