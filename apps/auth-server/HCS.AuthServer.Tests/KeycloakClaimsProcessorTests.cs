using System.Security.Claims;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HCS.AuthServer.Tests;

public sealed class KeycloakClaimsProcessorTests
{
    [Fact]
    public void Apply_FailsGateWithoutAddingDefaultRole()
    {
        var principal = CreatePrincipal(new Claim("groups", "[\"bd-bacsi\"]"));

        var result = KeycloakClaimsProcessor.Apply(principal);

        Assert.False(result.IsAllowed);
        Assert.Empty(result.Roles);
        Assert.Empty(principal.FindAll(AbpClaimTypes.Role));
    }

    [Fact]
    public void Apply_ParsesJsonGroupsAndAddsMappedRolesAndEmail()
    {
        var principal = CreatePrincipal(
            new Claim("groups", "[\"bd-app-hcs\",\"bd-bacsi\"]"),
            new Claim("email", "doctor@benhvien.vn"));

        var result = KeycloakClaimsProcessor.Apply(principal);

        Assert.True(result.IsAllowed);
        Assert.Equal(["bacsi"], result.Roles);
        Assert.Equal("bacsi", principal.FindFirst(AbpClaimTypes.Role)?.Value);
        Assert.Equal("doctor@benhvien.vn", principal.FindFirst(AbpClaimTypes.Email)?.Value);
    }

    [Fact]
    public void Apply_DoesNotDuplicateRoleClaims()
    {
        var principal = CreatePrincipal(
            new Claim("groups", "bd-app-hcs"),
            new Claim("group", "bd-admin"),
            new Claim(AbpClaimTypes.Role, "admin"));

        var result = KeycloakClaimsProcessor.Apply(principal);

        Assert.True(result.IsAllowed);
        Assert.Single(principal.FindAll(AbpClaimTypes.Role));
        Assert.Single(principal.FindAll(ClaimTypes.Role));
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Keycloak"));
}
