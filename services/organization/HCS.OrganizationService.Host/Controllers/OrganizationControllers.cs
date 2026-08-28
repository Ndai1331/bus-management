using HCS.OrganizationService.Application;
using HCS.OrganizationService.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace HCS.OrganizationService.Host.Controllers;

[ApiController]
[IgnoreAntiforgeryToken]
public abstract class OrganizationControllerBase : ControllerBase
{
    protected IOrganizationAppService Service { get; }
    protected OrganizationControllerBase(IOrganizationAppService service) => Service = service;
}

[Route("api/organization/departments")]
[Authorize(OrganizationPermissions.Departments)]
public sealed class DepartmentsController(IOrganizationAppService service) : OrganizationControllerBase(service)
{
    [HttpGet] public Task<PagedResultDto<DepartmentDto>> List([FromQuery] OrganizationListInput input, CancellationToken ct) => Service.GetDepartmentsAsync(input, ct);
    [HttpPost] public Task<DepartmentDto> Create([FromBody] UpsertDepartmentDto input, CancellationToken ct) => Service.CreateDepartmentAsync(input, ct);
    [HttpPut("{id:guid}")] public Task<DepartmentDto> Update(Guid id, [FromBody] UpsertDepartmentDto input, CancellationToken ct) => Service.UpdateDepartmentAsync(id, input, ct);
    [HttpDelete("{id:guid}")] public Task Delete(Guid id, CancellationToken ct) => Service.DeleteDepartmentAsync(id, ct);
}

[Route("api/organization/units")]
[Authorize(OrganizationPermissions.Units)]
public sealed class UnitsController(IOrganizationAppService service) : OrganizationControllerBase(service)
{
    [HttpGet] public Task<PagedResultDto<UnitDto>> List([FromQuery] OrganizationListInput input, CancellationToken ct) => Service.GetUnitsAsync(input, ct);
    [HttpPost] public Task<UnitDto> Create([FromBody] UpsertUnitDto input, CancellationToken ct) => Service.CreateUnitAsync(input, ct);
    [HttpPut("{id:guid}")] public Task<UnitDto> Update(Guid id, [FromBody] UpsertUnitDto input, CancellationToken ct) => Service.UpdateUnitAsync(id, input, ct);
    [HttpDelete("{id:guid}")] public Task Delete(Guid id, CancellationToken ct) => Service.DeleteUnitAsync(id, ct);
}

[Route("api/organization/positions")]
[Authorize(OrganizationPermissions.Positions)]
public sealed class PositionsController(IOrganizationAppService service) : OrganizationControllerBase(service)
{
    [HttpGet] public Task<PagedResultDto<PositionDto>> List([FromQuery] OrganizationListInput input, CancellationToken ct) => Service.GetPositionsAsync(input, ct);
    [HttpPost] public Task<PositionDto> Create([FromBody] UpsertPositionDto input, CancellationToken ct) => Service.CreatePositionAsync(input, ct);
    [HttpPut("{id:guid}")] public Task<PositionDto> Update(Guid id, [FromBody] UpsertPositionDto input, CancellationToken ct) => Service.UpdatePositionAsync(id, input, ct);
    [HttpDelete("{id:guid}")] public Task Delete(Guid id, CancellationToken ct) => Service.DeletePositionAsync(id, ct);
}

[Route("api/organization/master-data")]
[Authorize(OrganizationPermissions.MasterData)]
public sealed class MasterDataController(IOrganizationAppService service) : OrganizationControllerBase(service)
{
    [HttpGet] public Task<PagedResultDto<MasterDataItemDto>> List([FromQuery] string? type, [FromQuery] OrganizationListInput input, CancellationToken ct) => Service.GetMasterDataAsync(type, input, ct);
    [HttpPost] public Task<MasterDataItemDto> Create([FromBody] UpsertMasterDataItemDto input, CancellationToken ct) => Service.CreateMasterDataAsync(input, ct);
    [HttpPut("{id:guid}")] public Task<MasterDataItemDto> Update(Guid id, [FromBody] UpsertMasterDataItemDto input, CancellationToken ct) => Service.UpdateMasterDataAsync(id, input, ct);
    [HttpDelete("{id:guid}")] public Task Delete(Guid id, CancellationToken ct) => Service.DeleteMasterDataAsync(id, ct);
}

[Route("api/organization/user-mappings")]
[Authorize(OrganizationPermissions.UserMappings)]
public sealed class UserMappingsController(IOrganizationAppService service) : OrganizationControllerBase(service)
{
    [HttpGet] public Task<PagedResultDto<UserOrganizationMappingDto>> List([FromQuery] Guid? userId, [FromQuery] int skipCount = 0, [FromQuery] int maxResultCount = 20, CancellationToken ct = default) => Service.GetUserMappingsAsync(userId, skipCount, maxResultCount, ct);
    [HttpPost] public Task<UserOrganizationMappingDto> Create([FromBody] UpsertUserOrganizationMappingDto input, CancellationToken ct) => Service.CreateUserMappingAsync(input, ct);
    [HttpPut("{id:guid}")] public Task<UserOrganizationMappingDto> Update(Guid id, [FromBody] UpsertUserOrganizationMappingDto input, CancellationToken ct) => Service.UpdateUserMappingAsync(id, input, ct);
    [HttpDelete("{id:guid}")] public Task Delete(Guid id, CancellationToken ct) => Service.DeleteUserMappingAsync(id, ct);
}

[Route("api/organization/user-departments")]
[Authorize]
public sealed class UserDepartmentLookupController(IOrganizationAppService service) : OrganizationControllerBase(service)
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDepartmentLookupDto>>> List(
        [FromQuery] Guid[] userIds, CancellationToken ct)
    {
        if (!CanReadLookup()) return Forbid();
        return Ok(await Service.GetUserDepartmentsAsync(userIds, ct));
    }

    private bool CanReadLookup() =>
        User.IsInRole("admin")
        || User.IsInRole("lanhdao")
        || User.IsInRole("bacsi")
        || User.HasClaim("permission", "Documents.Signing.Execute")
        || User.HasClaim("permission", "Documents.Workflow.Start")
        || User.HasClaim("permission", "Documents.Workflow.View")
        || User.HasClaim("permission", OrganizationPermissions.UserMappings);
}
