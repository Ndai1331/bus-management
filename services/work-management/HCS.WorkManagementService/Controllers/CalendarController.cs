using HCS.WorkManagementService.Application;
using HCS.WorkManagementService.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HCS.WorkManagementService.Controllers;

[ApiController, Authorize(Policy = WorkPermissions.Calendar), Route("api/calendar")]
public sealed class CalendarController(CalendarAppService service) : ControllerBase
{
    [HttpGet]
    public Task<List<CalendarEventDto>> GetList(DateTime? from, DateTime? to, CancellationToken ct) =>
        service.GetListAsync(from, to, ct);

    [HttpGet("{id:guid}")]
    public Task<CalendarEventDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, ct);

    [HttpPost]
    public Task<CalendarEventDto> Create(CreateCalendarEventDto input, CancellationToken ct) => service.CreateAsync(input, ct);

    [HttpPut("{id:guid}")]
    public Task<CalendarEventDto> Update(Guid id, UpdateCalendarEventDto input, CancellationToken ct) =>
        service.UpdateAsync(id, input, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
