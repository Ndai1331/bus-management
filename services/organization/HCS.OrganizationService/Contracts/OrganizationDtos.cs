using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace HCS.OrganizationService.Contracts;

public sealed class OrganizationListInput : PagedAndSortedResultRequestDto
{
    [StringLength(200)]
    public string? Filter { get; set; }
    public bool? IsActive { get; set; }
}

public sealed record DepartmentDto(Guid Id, string Code, string Name, Guid? ParentId, int SortOrder, bool IsActive);
public sealed record UnitDto(Guid Id, Guid DepartmentId, string Code, string Name, int SortOrder, bool IsActive);
public sealed record PositionDto(Guid Id, string Code, string Name, int SignOrder, int SortOrder, bool IsActive);
public sealed record MasterDataItemDto(Guid Id, string Type, string Code, string Name, int SortOrder, bool IsActive);
public sealed record UserOrganizationMappingDto(
    Guid Id, Guid UserId, Guid DepartmentId, Guid? UnitId, Guid? PositionId, bool IsPrimary);

public sealed record UserDepartmentLookupDto(Guid UserId, Guid? DepartmentId, string? DepartmentName = null,
    Guid? PositionId = null, string? PositionName = null);

public sealed class UpsertDepartmentDto
{
    [Required, StringLength(50)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(256)] public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    [Range(0, 10_000)] public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpsertUnitDto
{
    public Guid DepartmentId { get; set; }
    [Required, StringLength(50)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(256)] public string Name { get; set; } = string.Empty;
    [Range(0, 10_000)] public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpsertPositionDto
{
    [Required, StringLength(50)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(256)] public string Name { get; set; } = string.Empty;
    [Range(0, 100)] public int SignOrder { get; set; }
    [Range(0, 10_000)] public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpsertMasterDataItemDto
{
    [Required, StringLength(50)] public string Type { get; set; } = string.Empty;
    [Required, StringLength(50)] public string Code { get; set; } = string.Empty;
    [Required, StringLength(256)] public string Name { get; set; } = string.Empty;
    [Range(0, 10_000)] public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpsertUserOrganizationMappingDto
{
    public Guid UserId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid? UnitId { get; set; }
    public Guid? PositionId { get; set; }
    public bool IsPrimary { get; set; }
}
