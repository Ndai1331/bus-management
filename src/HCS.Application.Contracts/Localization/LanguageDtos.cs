using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace HCS.Localization;

public class LanguageDto : FullAuditedEntityDto<Guid>
{
    public string CultureName { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public bool IsEnabled { get; set; }
    public bool IsDefault { get; set; }
}

public class GetLanguagesInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public bool? IsEnabled { get; set; }
}

public class CreateLanguageDto
{
    [Required, StringLength(LanguageConsts.MaxCultureNameLength)]
    public string CultureName { get; set; } = null!;

    [Required, StringLength(LanguageConsts.MaxDisplayNameLength)]
    public string DisplayName { get; set; } = null!;

    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; }
}

public class UpdateLanguageDto
{
    [Required, StringLength(LanguageConsts.MaxDisplayNameLength)]
    public string DisplayName { get; set; } = null!;

    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; }
}
