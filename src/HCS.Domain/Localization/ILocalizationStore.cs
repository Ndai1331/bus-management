using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Localization;

namespace HCS.Localization;

public interface ILocalizationStore
{
    Task<IReadOnlyList<LanguageInfo>> GetLanguagesAsync();
    Task<IReadOnlyDictionary<string, string>> GetTextsAsync(string resourceName, string cultureName);
    Task InvalidateAsync(LocalizationChangedEto change);
}
