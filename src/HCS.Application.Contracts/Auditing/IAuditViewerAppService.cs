using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HCS.Auditing;

public interface IAuditViewerAppService : IApplicationService
{
    Task<PagedResultDto<AuditLogDto>> GetListAsync(GetAuditLogsInput input);
    Task<AuditLogDetailDto> GetAsync(Guid id);
}
