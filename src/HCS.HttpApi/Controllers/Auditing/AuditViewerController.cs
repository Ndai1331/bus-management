using System;
using System.Threading.Tasks;
using HCS.Auditing;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace HCS.Controllers.Auditing;

[Route("api/hcs/audit-logs")]
[Route("api/audit-logs")]
public class AuditViewerController : HCSController, IAuditViewerAppService
{
    private readonly IAuditViewerAppService _service;

    public AuditViewerController(IAuditViewerAppService service) => _service = service;

    [HttpGet]
    public Task<PagedResultDto<AuditLogDto>> GetListAsync([FromQuery] GetAuditLogsInput input) => _service.GetListAsync(input);

    [HttpGet("{id:guid}")]
    public Task<AuditLogDetailDto> GetAsync(Guid id) => _service.GetAsync(id);
}
