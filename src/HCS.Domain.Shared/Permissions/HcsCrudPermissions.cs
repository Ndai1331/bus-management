using System.Collections.Generic;
using System.Linq;

namespace HCS.Permissions;

public static class HcsCrudPermissions
{
    public const string CreateSuffix = ".Create";
    public const string UpdateSuffix = ".Update";
    public const string DeleteSuffix = ".Delete";

    public static string Create(string permission) => permission + CreateSuffix;
    public static string Update(string permission) => permission + UpdateSuffix;
    public static string Delete(string permission) => permission + DeleteSuffix;

    public static IReadOnlyList<string> WithCrud(string permission) =>
    [
        permission,
        Create(permission),
        Update(permission),
        Delete(permission)
    ];

    public static IReadOnlyList<string> Expand(IEnumerable<string> permissions) =>
        permissions.SelectMany(WithCrud).ToArray();
}
