using System.Security.Claims;
using System.Text.Json;
using Volo.Abp.Security.Claims;

namespace HCS.AuthServer;

public static class KeycloakClaimsProcessor
{
    public static KeycloakClaimsResult Apply(ClaimsPrincipal principal)
    {
        var groups = ExtractGroups(principal).ToList();
        if (!KeycloakGroupRoleMapper.HasAppAccess(groups))
        {
            return KeycloakClaimsResult.Denied(
                $"Missing required Keycloak group '{KeycloakGroupRoleMapper.AppAccessGroup}'.");
        }

        var identity = principal.Identity as ClaimsIdentity;
        if (identity is null)
        {
            return KeycloakClaimsResult.Denied("The external identity is unavailable.");
        }

        var roles = KeycloakGroupRoleMapper.ResolveRoles(groups);
        foreach (var claim in identity.Claims.Where(claim => claim.Type == AbpClaimTypes.Role || claim.Type == ClaimTypes.Role).ToArray())
        {
            identity.RemoveClaim(claim);
        }

        foreach (var role in roles)
        {
            AddIfMissing(identity, AbpClaimTypes.Role, role);
            AddIfMissing(identity, ClaimTypes.Role, role);
        }

        var email = principal.FindFirst(ClaimTypes.Email)?.Value
                    ?? principal.FindFirst("email")?.Value;
        if (!string.IsNullOrWhiteSpace(email))
        {
            AddIfMissing(identity, AbpClaimTypes.Email, email);
        }

        return KeycloakClaimsResult.Allowed(roles);
    }

    public static IEnumerable<string> ExtractGroups(ClaimsPrincipal principal)
    {
        return principal.Claims
            .Where(claim => claim.Type is KeycloakGroupRoleMapper.GroupsClaim or "group")
            .SelectMany(claim => ParseClaimValue(claim.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ParseClaimValue(string value)
    {
        if (!value.TrimStart().StartsWith('['))
        {
            return [value];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(value) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void AddIfMissing(ClaimsIdentity identity, string type, string value)
    {
        if (!identity.HasClaim(claim =>
                claim.Type == type && string.Equals(claim.Value, value, StringComparison.OrdinalIgnoreCase)))
        {
            identity.AddClaim(new Claim(type, value));
        }
    }
}

public sealed record KeycloakClaimsResult(bool IsAllowed, IReadOnlyList<string> Roles, string? FailureReason)
{
    public static KeycloakClaimsResult Allowed(IReadOnlyList<string> roles) =>
        new(true, roles, null);

    public static KeycloakClaimsResult Denied(string reason) =>
        new(false, [], reason);
}
