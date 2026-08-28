using System;
using System.Globalization;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace HCS.Localization;

public class Language : FullAuditedAggregateRoot<Guid>
{
    public string CultureName { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public bool IsEnabled { get; private set; }
    public bool IsDefault { get; private set; }

    protected Language()
    {
    }

    public Language(Guid id, string cultureName, string displayName, bool isEnabled = true, bool isDefault = false)
        : base(id)
    {
        SetCultureName(cultureName);
        SetDisplayName(displayName);
        IsEnabled = isEnabled;
        IsDefault = isDefault;
    }

    public void Update(string displayName, bool isEnabled)
    {
        SetDisplayName(displayName);
        IsEnabled = isEnabled;
    }

    public void SetDefault(bool isDefault) => IsDefault = isDefault;

    private void SetCultureName(string cultureName)
    {
        cultureName = Check.NotNullOrWhiteSpace(cultureName, nameof(cultureName), LanguageConsts.MaxCultureNameLength);
        _ = CultureInfo.GetCultureInfo(cultureName);
        CultureName = cultureName;
    }

    private void SetDisplayName(string displayName)
    {
        DisplayName = Check.NotNullOrWhiteSpace(displayName, nameof(displayName), LanguageConsts.MaxDisplayNameLength);
    }
}
