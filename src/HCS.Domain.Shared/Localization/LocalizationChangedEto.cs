using System;

namespace HCS.Localization;

[Serializable]
public class LocalizationChangedEto
{
    public bool LanguagesChanged { get; set; }
    public string? ResourceName { get; set; }
    public string? CultureName { get; set; }
}
