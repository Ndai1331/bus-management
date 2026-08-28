using System;
using System.Collections.Generic;
using System.Linq;
using HCS.Permissions;

namespace HCS.Blazor.Client.Pages.Organization;

public partial class OrganizationCatalog
{
    private static IReadOnlyList<MasterDataRouteDefinition> MasterTypeOptions =>
        OrganizationCatalogRouteMap.MasterDataTypes;

    private OrganizationCatalogFormModel NewForm() => new()
    {
        Type = Definition.MasterType ?? string.Empty,
        IsActive = true
    };

    private CatalogPageDefinition ResolveDefinition(OrganizationCatalogKind kind, string? masterType)
    {
        if (kind == OrganizationCatalogKind.Department)
        {
            return new(kind, L["Catalog:DepartmentTitle"].Value, L["Catalog:DepartmentDescription"].Value, "fa fa-sitemap", HCSOrganizationPermissions.Departments, OrganizationCatalogClient.Endpoint(kind), null, false);
        }

        if (kind == OrganizationCatalogKind.Unit)
        {
            return new(kind, L["Catalog:UnitTitle"].Value, L["Catalog:UnitDescription"].Value, "fa fa-building", HCSOrganizationPermissions.Units, OrganizationCatalogClient.Endpoint(kind), null, false);
        }

        if (kind == OrganizationCatalogKind.Position)
        {
            return new(kind, L["Catalog:PositionTitle"].Value, L["Catalog:PositionDescription"].Value, "fa fa-id-badge", HCSOrganizationPermissions.Positions, OrganizationCatalogClient.Endpoint(kind), null, false);
        }

        var option = MasterTypeOptions.FirstOrDefault(item => string.Equals(item.Type, masterType, StringComparison.Ordinal));
        var title = option is null ? L["Catalog:MasterDataTitle"].Value : L[option.TitleKey].Value;
        var description = option is null
            ? L["Catalog:MasterDataDescription"].Value
            : string.Format(L["Catalog:MasterDataTypedDescription"].Value, title.ToLowerInvariant());
        var permission = HCSCatalogPermissions.ForMasterType(option?.Type);
        return new(kind, title, description, "fa fa-tags", permission, OrganizationCatalogClient.Endpoint(kind), option?.Type, option is not null);
    }
}
