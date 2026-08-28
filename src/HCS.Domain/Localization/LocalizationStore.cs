using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Localization;

namespace HCS.Localization;

public class LocalizationStore : ILocalizationStore, ITransientDependency
{
    public const string LanguageCacheKey = "HcsLocalization:Languages";

    private readonly ILanguageRepository _languageRepository;
    private readonly ILanguageTextRepository _textRepository;
    private readonly IDistributedCache<LanguageCacheItem> _languageCache;
    private readonly IDistributedCache<LanguageTextCacheItem> _textCache;

    public LocalizationStore(
        ILanguageRepository languageRepository,
        ILanguageTextRepository textRepository,
        IDistributedCache<LanguageCacheItem> languageCache,
        IDistributedCache<LanguageTextCacheItem> textCache)
    {
        _languageRepository = languageRepository;
        _textRepository = textRepository;
        _languageCache = languageCache;
        _textCache = textCache;
    }

    public async Task<IReadOnlyList<LanguageInfo>> GetLanguagesAsync()
    {
        var item = await _languageCache.GetOrAddAsync(LanguageCacheKey, async () =>
        {
            var entities = await _languageRepository.GetListAsync(x => x.IsEnabled);
            return new LanguageCacheItem
            {
                Languages = entities.OrderByDescending(x => x.IsDefault).ThenBy(x => x.DisplayName)
                    .Select(x => new LanguageCacheEntry { CultureName = x.CultureName, DisplayName = x.DisplayName })
                    .ToList()
            };
        });

        return item!.Languages.Select(x => new LanguageInfo(x.CultureName, x.CultureName, x.DisplayName)).ToList();
    }

    public async Task<IReadOnlyDictionary<string, string>> GetTextsAsync(string resourceName, string cultureName)
    {
        var key = GetTextCacheKey(resourceName, cultureName);
        var item = await _textCache.GetOrAddAsync(key, async () =>
        {
            var entities = await _textRepository.GetByResourceCultureAsync(resourceName, cultureName);
            return new LanguageTextCacheItem { Texts = entities.ToDictionary(x => x.Name, x => x.Value) };
        });

        return item!.Texts;
    }

    public async Task InvalidateAsync(LocalizationChangedEto change)
    {
        if (change.LanguagesChanged)
        {
            await _languageCache.RemoveAsync(LanguageCacheKey);
        }

        if (!change.ResourceName.IsNullOrWhiteSpace() && !change.CultureName.IsNullOrWhiteSpace())
        {
            await _textCache.RemoveAsync(GetTextCacheKey(change.ResourceName!, change.CultureName!));
        }
    }

    public static string GetTextCacheKey(string resourceName, string cultureName) =>
        $"HcsLocalization:Texts:{resourceName}:{cultureName}";
}
