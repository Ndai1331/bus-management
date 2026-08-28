using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Localization;

namespace HCS.Localization;

[Dependency(ReplaceServices = true)]
public class HcsLanguageProvider : ILanguageProvider, ITransientDependency
{
    private readonly ILocalizationStore _store;
    private readonly AbpLocalizationOptions _options;

    public HcsLanguageProvider(ILocalizationStore store, IOptions<AbpLocalizationOptions> options)
    {
        _store = store;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<LanguageInfo>> GetLanguagesAsync()
    {
        var languages = await _store.GetLanguagesAsync();
        return languages.Count == 0 ? _options.Languages : languages;
    }
}
