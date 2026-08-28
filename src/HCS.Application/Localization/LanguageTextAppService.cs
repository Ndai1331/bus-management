using System;
using System.Linq;
using System.Threading.Tasks;
using HCS.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.EventBus.Distributed;

namespace HCS.Localization;

[Authorize(HCSPermissions.Languages.ManageTexts)]
public class LanguageTextAppService : HCSAppService, ILanguageTextAppService
{
    private readonly ILanguageTextRepository _repository;
    private readonly ILanguageRepository _languageRepository;
    private readonly IDistributedEventBus _eventBus;
    private readonly ILocalizationStore _localizationStore;

    public LanguageTextAppService(
        ILanguageTextRepository repository,
        ILanguageRepository languageRepository,
        IDistributedEventBus eventBus,
        ILocalizationStore localizationStore)
    {
        _repository = repository;
        _languageRepository = languageRepository;
        _eventBus = eventBus;
        _localizationStore = localizationStore;
    }

    public async Task<PagedResultDto<LanguageTextDto>> GetListAsync(GetLanguageTextsInput input)
    {
        var sorting = input.Sorting.IsNullOrWhiteSpace() ? nameof(LanguageText.Name) : input.Sorting;
        var entities = await _repository.GetFilteredListAsync(input.ResourceName, input.CultureName, input.Filter, input.SkipCount, input.MaxResultCount, sorting);
        var count = await _repository.GetFilteredCountAsync(input.ResourceName, input.CultureName, input.Filter);
        return new PagedResultDto<LanguageTextDto>(count, entities.Select(Map).ToList());
    }

    public async Task<LanguageTextDto> GetAsync(Guid id) => Map(await _repository.GetAsync(id));

    public async Task<LanguageTextDto> CreateAsync(CreateLanguageTextDto input)
    {
        if (await _languageRepository.FindByCultureNameAsync(input.CultureName) == null)
        {
            throw new BusinessException("HCS:LanguageNotFound").WithData("CultureName", input.CultureName);
        }

        if (await _repository.FindByKeyAsync(input.ResourceName, input.CultureName, input.Name) != null)
        {
            throw new BusinessException(HCSDomainErrorCodes.LanguageTextAlreadyExists);
        }

        var entity = new LanguageText(GuidGenerator.Create(), input.ResourceName, input.CultureName, input.Name, input.Value);
        await _repository.InsertAsync(entity, autoSave: true);
        await PublishChangeAsync(entity);
        return Map(entity);
    }

    public async Task<LanguageTextDto> UpdateAsync(Guid id, UpdateLanguageTextDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.SetValue(input.Value);
        await _repository.UpdateAsync(entity, autoSave: true);
        await PublishChangeAsync(entity);
        return Map(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        await _repository.DeleteAsync(entity, autoSave: true);
        await PublishChangeAsync(entity);
    }

    private async Task PublishChangeAsync(LanguageText entity)
    {
        var change = new LocalizationChangedEto
        {
            ResourceName = entity.ResourceName,
            CultureName = entity.CultureName
        };
        await _localizationStore.InvalidateAsync(change);
        await _eventBus.PublishAsync(change);
    }

    private static LanguageTextDto Map(LanguageText entity) => new()
    {
        Id = entity.Id,
        ResourceName = entity.ResourceName,
        CultureName = entity.CultureName,
        Name = entity.Name,
        Value = entity.Value,
        CreationTime = entity.CreationTime,
        CreatorId = entity.CreatorId,
        LastModificationTime = entity.LastModificationTime,
        LastModifierId = entity.LastModifierId
    };
}
