using System;
using System.Threading.Tasks;
using HCS.Localization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace HCS.Controllers.Localization;

[Route("api/hcs/languages")]
[Route("api/language-management/languages")]
public class LanguagesController : HCSController, ILanguageAppService
{
    private readonly ILanguageAppService _service;

    public LanguagesController(ILanguageAppService service) => _service = service;

    [HttpGet]
    public Task<PagedResultDto<LanguageDto>> GetListAsync(GetLanguagesInput input) => _service.GetListAsync(input);

    [HttpGet("{id:guid}")]
    public Task<LanguageDto> GetAsync(Guid id) => _service.GetAsync(id);

    [HttpPost]
    public Task<LanguageDto> CreateAsync(CreateLanguageDto input) => _service.CreateAsync(input);

    [HttpPut("{id:guid}")]
    public Task<LanguageDto> UpdateAsync(Guid id, UpdateLanguageDto input) => _service.UpdateAsync(id, input);

    [HttpDelete("{id:guid}")]
    public Task DeleteAsync(Guid id) => _service.DeleteAsync(id);
}
