using Xunit;

namespace HCS.AuthServer.Tests;

public sealed class KeycloakGroupRoleMapperTests
{
    [Fact]
    public void ResolveRoles_DeniesGroupsWithoutAppEntitlement()
    {
        var roles = KeycloakGroupRoleMapper.ResolveRoles(["bd-admin"]);

        Assert.Empty(roles);
        Assert.False(KeycloakGroupRoleMapper.HasAppAccess(["bd-admin"]));
    }

    [Fact]
    public void ResolveRoles_DefaultsOnlyEntitledUsersToNhanvien()
    {
        var roles = KeycloakGroupRoleMapper.ResolveRoles(["/bd-app-hcs", "unmapped"]);

        Assert.Equal(["nhanvien"], roles);
    }

    [Fact]
    public void ResolveRoles_MapsKnownGroupsInPriorityOrder()
    {
        var roles = KeycloakGroupRoleMapper.ResolveRoles(
            ["bd-bacsi", "BD-APP-HCS", "bd-admin", "bd-lanhdao", "bd-nhanvien"]);

        Assert.Equal(["admin", "lanhdao", "bacsi", "nhanvien"], roles);
    }
}
