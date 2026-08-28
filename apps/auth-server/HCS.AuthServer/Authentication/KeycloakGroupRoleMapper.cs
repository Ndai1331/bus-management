namespace HCS.AuthServer;

public static class KeycloakGroupRoleMapper
{
    public const string GroupsClaim = "groups";
    public const string AppAccessGroup = "bd-app-hcs";

    private static readonly (string Group, string Role)[] Mappings =
    [
        ("bd-admin", "admin"),
        ("bd-lanhdao", "lanhdao"),
        ("bd-bacsi", "bacsi"),
        ("bd-nhanvien", "nhanvien")
    ];

    public static bool HasAppAccess(IEnumerable<string> groups) =>
        Normalize(groups).Contains(AppAccessGroup);

    public static IReadOnlyList<string> ResolveRoles(IEnumerable<string> groups)
    {
        var normalizedGroups = Normalize(groups);
        if (!normalizedGroups.Contains(AppAccessGroup))
        {
            return [];
        }

        var roles = Mappings
            .Where(mapping => normalizedGroups.Contains(mapping.Group))
            .Select(mapping => mapping.Role)
            .ToList();

        return roles.Count == 0 ? ["nhanvien"] : roles;
    }

    private static HashSet<string> Normalize(IEnumerable<string> groups) =>
        groups
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Select(group => group.Trim().TrimStart('/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
