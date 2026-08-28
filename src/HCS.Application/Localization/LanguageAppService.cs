using System;
using System.Linq;
using System.Threading.Tasks;
using HCS.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.EventBus.Distributed;

namespace HCS.Localization;

[Authorize(HCSPermissions.Languages.Default)]
public class LanguageAppService : HCSAppService, ILanguageAppService
{
    private readonly ILanguageRepository _repository;
    private readonly LanguageManager _manager;
    private readonly IDistributedEventBus _eventBus;
    private readonly ILocalizationStore _localizationStore;

    public LanguageAppService(
        ILanguageRepository repository,
        LanguageManager manager,
        IDistributedEventBus eventBus,
        ILocalizationStore localizationStore)
    {
        _repository = repository;
        _manager = manager;
        _eventBus = eventBus;
        _localizationStore = localizationStore;
    }

    public async Task<PagedResultDto<LanguageDto>> GetListAsync(GetLanguagesInput input)
    {
        var sorting = input.Sorting.IsNullOrWhiteSpace() ? nameof(Language.DisplayName) : input.Sorting;
        var entities = await _repository.GetFilteredListAsync(input.Filter, input.IsEnabled, input.SkipCount, input.MaxResultCount, sorting);
        var count = await _repository.GetFilteredCountAsync(input.Filter, input.IsEnabled);
        return new PagedResultDto<LanguageDto>(count, entities.Select(Map).ToList());
    }

    public async Task<LanguageDto> GetAsync(Guid id) => Map(await _repository.GetAsync(id));

    [Authorize(HCSPermissions.Languages.Create)]
    public async Task<LanguageDto> CreateAsync(CreateLanguageDto input)
    {
        var entity = await _manager.CreateAsync(input.CultureName, input.DisplayName, input.IsEnabled, input.IsDefault);
        await _repository.InsertAsync(entity, autoSave: true);
        await PublishChangeAsync();
        return Map(entity);
    }

    [Authorize(HCSPermissions.Languages.Update)]
    public async Task<LanguageDto> UpdateAsync(Guid id, UpdateLanguageDto input)
    {
        var entity = await _repository.GetAsync(id);
        if (entity.IsDefault && !input.IsDefault)
        {
            throw new Volo.Abp.BusinessException(HCSDomainErrorCodes.DefaultLanguageRequired);
        }

        entity.Update(input.DisplayName, input.IsEnabled);
        if (input.IsDefault)
        {
            await _manager.SetDefaultAsync(entity);
        }

        await _repository.UpdateAsync(entity, autoSave: true);
        await PublishChangeAsync();
        return Map(entity);
    }

    [Authorize(HCSPermissions.Languages.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        await _manager.EnsureCanDeleteAsync(entity);
        await _repository.DeleteAsync(entity, autoSave: true);
        await PublishChangeAsync();
    }

    private async Task PublishChangeAsync()
    {
        var change = new LocalizationChangedEto { LanguagesChanged = true };
        await _localizationStore.InvalidateAsync(change);
        await _eventBus.PublishAsync(change);
    }

    private static LanguageDto Map(Language entity) => new()
    {
        Id = entity.Id,
        CultureName = entity.CultureName,
        DisplayName = entity.DisplayName,
        IsEnabled = entity.IsEnabled,
        IsDefault = entity.IsDefault,
        CreationTime = entity.CreationTime,
        CreatorId = entity.CreatorId,
        LastModificationTime = entity.LastModificationTime,
        LastModifierId = entity.LastModifierId
    };
}
