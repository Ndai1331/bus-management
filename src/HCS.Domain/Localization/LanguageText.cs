using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace HCS.Localization;

public class LanguageText : FullAuditedAggregateRoot<Guid>
{
    public string ResourceName { get; private set; } = null!;
    public string CultureName { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Value { get; private set; } = null!;

    protected LanguageText()
    {
    }

    public LanguageText(Guid id, string resourceName, string cultureName, string name, string value)
        : base(id)
    {
        ResourceName = Check.NotNullOrWhiteSpace(resourceName, nameof(resourceName), LanguageConsts.MaxResourceNameLength);
        CultureName = Check.NotNullOrWhiteSpace(cultureName, nameof(cultureName), LanguageConsts.MaxCultureNameLength);
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), LanguageConsts.MaxTextNameLength);
        SetValue(value);
    }

    public void SetValue(string value)
    {
        Value = Check.NotNull(value, nameof(value));
        Check.Length(Value, nameof(value), LanguageConsts.MaxTextValueLength);
    }
}
