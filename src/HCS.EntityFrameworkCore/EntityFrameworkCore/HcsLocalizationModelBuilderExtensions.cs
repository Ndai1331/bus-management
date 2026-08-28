using HCS.Localization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace HCS.EntityFrameworkCore;

public static class HcsLocalizationModelBuilderExtensions
{
    public static void ConfigureHcsLocalization(this ModelBuilder builder)
    {
        builder.Entity<Language>(b =>
        {
            b.ToTable("HcsLanguages", HCSConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.CultureName).IsRequired().HasMaxLength(LanguageConsts.MaxCultureNameLength);
            b.Property(x => x.DisplayName).IsRequired().HasMaxLength(LanguageConsts.MaxDisplayNameLength);
            b.HasIndex(x => x.CultureName).IsUnique();
            b.HasIndex(x => x.IsDefault).IsUnique().HasFilter("\"IsDefault\" = TRUE");
        });

        builder.Entity<LanguageText>(b =>
        {
            b.ToTable("HcsLanguageTexts", HCSConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ResourceName).IsRequired().HasMaxLength(LanguageConsts.MaxResourceNameLength);
            b.Property(x => x.CultureName).IsRequired().HasMaxLength(LanguageConsts.MaxCultureNameLength);
            b.Property(x => x.Name).IsRequired().HasMaxLength(LanguageConsts.MaxTextNameLength);
            b.Property(x => x.Value).IsRequired().HasMaxLength(LanguageConsts.MaxTextValueLength);
            b.HasIndex(x => new { x.ResourceName, x.CultureName, x.Name }).IsUnique();
        });
    }
}
