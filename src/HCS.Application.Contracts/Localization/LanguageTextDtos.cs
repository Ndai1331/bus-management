using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace HCS.Localization;

public class LanguageTextDto : FullAuditedEntityDto<Guid>
{
    public string ResourceName { get; set; } = null!;
    public string CultureName { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Value { get; set; } = null!;
}

public class GetLanguageTextsInput : PagedAndSortedResultRequestDto
{
    public string? ResourceName { get; set; }
    public string? CultureName { get; set; }
    public string? Filter { get; set; }
}

public class CreateLanguageTextDto
{
    [Required, StringLength(LanguageConsts.MaxResourceNameLength)]
    public string ResourceName { get; set; } = null!;

    [Required, StringLength(LanguageConsts.MaxCultureNameLength)]
    public string CultureName { get; set; } = null!;

    [Required, StringLength(LanguageConsts.MaxTextNameLength)]
    public string Name { get; set; } = null!;

    [Required(AllowEmptyStrings = true), StringLength(LanguageConsts.MaxTextValueLength)]
    public string Value { get; set; } = null!;
}

public class UpdateLanguageTextDto
{
    [Required(AllowEmptyStrings = true), StringLength(LanguageConsts.MaxTextValueLength)]
    public string Value { get; set; } = null!;
}
