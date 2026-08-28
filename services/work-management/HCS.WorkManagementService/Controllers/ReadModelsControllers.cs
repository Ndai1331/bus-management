using HCS.WorkManagementService.Application;
using HCS.WorkManagementService.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCS.WorkManagementService.Controllers;

[ApiController, Authorize(Policy = WorkPermissions.Reports), Route("api/reports")]
public sealed class ReportsController(ReadModelAppService service) : ControllerBase
{
    [HttpGet] public Task<List<ReportRowDto>> Get(string? dimension, CancellationToken ct) => service.GetReportAsync(dimension, ct);
}

[ApiController, Authorize(Policy = WorkPermissions.Dashboard), Route("api/dashboard")]
public sealed class DashboardController(ReadModelAppService service) : ControllerBase
{
    [HttpGet] public Task<DashboardDto> Get(CancellationToken ct) => service.GetDashboardAsync(ct);
}
