namespace HCS.OrganizationService.Contracts;

public static class OrganizationPermissions
{
    public const string Group = HCS.Permissions.HCSOrganizationPermissions.Group;
    public const string Departments = HCS.Permissions.HCSOrganizationPermissions.Departments;
    public const string Units = HCS.Permissions.HCSOrganizationPermissions.Units;
    public const string Positions = HCS.Permissions.HCSOrganizationPermissions.Positions;
    public const string MasterData = HCS.Permissions.HCSOrganizationPermissions.MasterData;
    public const string UserMappings = HCS.Permissions.HCSOrganizationPermissions.UserMappings;

    public static readonly string[] MasterDataAccess =
    [
        MasterData,
        ..HCS.Permissions.HCSCatalogPermissions.AllWithCrud
    ];
}
