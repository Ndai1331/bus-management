using System;
using System.Threading.Tasks;
using HCS.Localization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace HCS.Controllers.Localization;

[Route("api/hcs/language-texts")]
[Route("api/language-management/language-texts")]
public class LanguageTextsController : HCSController, ILanguageTextAppService
{
    private readonly ILanguageTextAppService _service;

    public LanguageTextsController(ILanguageTextAppService service) => _service = service;

    [HttpGet]
    public Task<PagedResultDto<LanguageTextDto>> GetListAsync(GetLanguageTextsInput input) => _service.GetListAsync(input);

    [HttpGet("{id:guid}")]
    public Task<LanguageTextDto> GetAsync(Guid id) => _service.GetAsync(id);

    [HttpPost]
    public Task<LanguageTextDto> CreateAsync(CreateLanguageTextDto input) => _service.CreateAsync(input);

    [HttpPut("{id:guid}")]
    public Task<LanguageTextDto> UpdateAsync(Guid id, UpdateLanguageTextDto input) => _service.UpdateAsync(id, input);

    [HttpDelete("{id:guid}")]
    public Task DeleteAsync(Guid id) => _service.DeleteAsync(id);
}
