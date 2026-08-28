using System.Collections.Generic;

namespace HCS.Localization;

public class LanguageCacheItem
{
    public List<LanguageCacheEntry> Languages { get; set; } = [];
}

public class LanguageCacheEntry
{
    public string CultureName { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
}

public class LanguageTextCacheItem
{
    public Dictionary<string, string> Texts { get; set; } = [];
}
