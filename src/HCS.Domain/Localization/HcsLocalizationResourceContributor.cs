using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Volo.Abp.Localization;
using Volo.Abp.Threading;

namespace HCS.Localization;

public class HcsLocalizationResourceContributor : ILocalizationResourceContributor
{
    private ILocalizationStore _store = null!;
    private string _resourceName = null!;

    public bool IsDynamic => true;

    public void Initialize(LocalizationResourceInitializationContext context)
    {
        _store = context.ServiceProvider.GetRequiredService<ILocalizationStore>();
        _resourceName = context.Resource.ResourceName;
    }

    public LocalizedString? GetOrNull(string cultureName, string name)
    {
        var texts = AsyncHelper.RunSync(() => _store.GetTextsAsync(_resourceName, cultureName));
        return texts.TryGetValue(name, out var value) ? new LocalizedString(name, value) : null;
    }

    public void Fill(string cultureName, Dictionary<string, LocalizedString> dictionary)
    {
        AsyncHelper.RunSync(() => FillAsync(cultureName, dictionary));
    }

    public async Task FillAsync(string cultureName, Dictionary<string, LocalizedString> dictionary)
    {
        foreach (var pair in await _store.GetTextsAsync(_resourceName, cultureName))
        {
            dictionary[pair.Key] = new LocalizedString(pair.Key, pair.Value);
        }
    }

    public async Task<IEnumerable<string>> GetSupportedCulturesAsync()
    {
        var languages = await _store.GetLanguagesAsync();
        var cultures = new List<string>(languages.Count);
        foreach (var language in languages)
        {
            cultures.Add(language.CultureName);
        }

        return cultures;
    }
}
