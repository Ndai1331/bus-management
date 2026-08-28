namespace HCS.Permissions;

public static class HCSOrganizationPermissions
{
    public const string Group = "HCS.Organization";
    public const string Departments = Group + ".Departments";
    public const string Units = Group + ".Units";
    public const string Positions = Group + ".Positions";
    public const string MasterData = Group + ".MasterData";
    public const string UserMappings = Group + ".UserMappings";

    public static readonly string[] AdministrationPermissions =
    [
        Departments,
        Units,
        Positions,
        MasterData,
        UserMappings
    ];
}
