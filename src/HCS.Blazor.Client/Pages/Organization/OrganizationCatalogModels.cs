using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HCS.Blazor.Client.Pages.Organization;

public enum OrganizationCatalogKind
{
    Department,
    Unit,
    Position,
    MasterData
}

public sealed record CatalogPageDefinition(
    OrganizationCatalogKind Kind,
    string Title,
    string Description,
    string Icon,
    string Permission,
    string Endpoint,
    string? MasterType,
    bool IsTypedMasterData);

public sealed record OrganizationCatalogQuery(
    string? Filter,
    bool? IsActive,
    int SkipCount,
    int MaxResultCount);

public sealed record OrganizationPagedResponse<T>(long TotalCount, List<T> Items);

public sealed record DepartmentCatalogDto(
    Guid Id,
    string Code,
    string Name,
    Guid? ParentId,
    int SortOrder,
    bool IsActive);

public sealed record UnitCatalogDto(
    Guid Id,
    Guid DepartmentId,
    string Code,
    string Name,
    int SortOrder,
    bool IsActive);

public sealed record PositionCatalogDto(
    Guid Id,
    string Code,
    string Name,
    int SignOrder,
    int SortOrder,
    bool IsActive);

public sealed record MasterDataCatalogDto(
    Guid Id,
    string Type,
    string Code,
    string Name,
    int SortOrder,
    bool IsActive);

public sealed record DepartmentUpsertRequest(
    string Code,
    string Name,
    Guid? ParentId,
    int SortOrder,
    bool IsActive);

public sealed record UnitUpsertRequest(
    Guid DepartmentId,
    string Code,
    string Name,
    int SortOrder,
    bool IsActive);

public sealed record PositionUpsertRequest(
    string Code,
    string Name,
    int SignOrder,
    int SortOrder,
    bool IsActive);

public sealed record MasterDataUpsertRequest(
    string Type,
    string Code,
    string Name,
    int SortOrder,
    bool IsActive);

public sealed class OrganizationCatalogFormModel
{
    [Required, StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(256)]
    public string Name { get; set; } = string.Empty;

    public string ParentId { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;

    [Range(0, 100)]
    public int SignOrder { get; set; }

    [Range(0, 10_000)]
    public int SortOrder { get; set; }

    public string Type { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public OrganizationCatalogFormModel Clone() => new()
    {
        Code = Code,
        Name = Name,
        ParentId = ParentId,
        DepartmentId = DepartmentId,
        SignOrder = SignOrder,
        SortOrder = SortOrder,
        Type = Type,
        IsActive = IsActive
    };
}

public sealed record OrganizationCatalogRow(
    Guid Id,
    string Type,
    string Code,
    string Name,
    Guid? RelationId,
    int SignOrder,
    int SortOrder,
    bool IsActive);
