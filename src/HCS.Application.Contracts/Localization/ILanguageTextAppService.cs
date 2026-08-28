using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HCS.Localization;

public interface ILanguageTextAppService : IApplicationService
{
    Task<PagedResultDto<LanguageTextDto>> GetListAsync(GetLanguageTextsInput input);
    Task<LanguageTextDto> GetAsync(Guid id);
    Task<LanguageTextDto> CreateAsync(CreateLanguageTextDto input);
    Task<LanguageTextDto> UpdateAsync(Guid id, UpdateLanguageTextDto input);
    Task DeleteAsync(Guid id);
}
