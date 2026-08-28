using System.Security.Claims;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;

namespace HCS.AuthServer;

public interface IPermissionClaimResolver
{
    Task<IReadOnlyList<string>> ResolveAsync(ClaimsPrincipal principal);
    Task<IReadOnlyList<string>> ResolveRolesAsync(ClaimsPrincipal principal);
}

/// <summary>
/// Resolves the current HCS roles against ABP's local permission store. External claims are
/// deliberately not trusted for permissions: Keycloak supplies identity and role membership only.
/// </summary>
public sealed class PermissionClaimResolver(
    IPermissionManager permissionManager,
    IIdentityUserRepository? userRepository = null)
    : IPermissionClaimResolver, ITransientDependency
{
    // Admin grants every ABP + HCS permission; the previous 256 cap dropped
    // WorkManagement.* / Documents.* (sorted late) and caused AccessDenied after login.
    private const int MaxPermissionClaims = 1024;
    private const int MaxPermissionNameLength = 256;

    private static readonly string[] PriorityPrefixes =
    [
        "WorkManagement.",
        "Documents.",
        "Collaboration.",
        "HCS.",
        "AbpIdentity.",
        "FeatureManagement.",
        "SettingManagement.",
        "PermissionManagement."
    ];

    public Task<IReadOnlyList<string>> ResolveRolesAsync(ClaimsPrincipal principal) =>
        ResolveLocalRolesAsync(principal);

    public async Task<IReadOnlyList<string>> ResolveAsync(ClaimsPrincipal principal)
    {
        var roles = await ResolveRolesAsync(principal);
        var isAdmin = roles.Any(role => string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase));

        var grants = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            var permissions = await permissionManager.GetAllForRoleAsync(role);
            foreach (var permission in permissions)
            {
                if (permission.IsGranted && IsValidPermissionName(permission.Name))
                {
                    grants.Add(permission.Name);
                }
            }
        }

        var ordered = grants
            .OrderBy(PriorityRank)
            .ThenBy(permission => permission, StringComparer.Ordinal);

        // Break-glass admin must keep the full grant set in the access token.
        return isAdmin
            ? ordered.ToArray()
            : ordered.Take(MaxPermissionClaims).ToArray();
    }

    private static int PriorityRank(string permission)
    {
        for (var index = 0; index < PriorityPrefixes.Length; index++)
        {
            if (permission.StartsWith(PriorityPrefixes[index], StringComparison.Ordinal))
            {
                return index;
            }
        }

        return PriorityPrefixes.Length;
    }

    private static bool IsValidPermissionName(string permission) =>
        !string.IsNullOrWhiteSpace(permission) && permission.Length <= MaxPermissionNameLength;

    private async Task<IReadOnlyList<string>> ResolveLocalRolesAsync(ClaimsPrincipal principal)
    {
        var subject = principal.FindFirst("sub")?.Value;
        if (userRepository is not null && Guid.TryParse(subject, out var userId))
        {
            return await userRepository.GetRoleNamesAsync(userId);
        }

        // The fallback keeps unit tests and non-Identity callers usable. Normal sign-in
        // principals always carry the local Identity user id in `sub` and take the branch above.
        return principal.FindAll(AbpClaimTypes.Role)
            .Concat(principal.FindAll(ClaimTypes.Role))
            .Select(claim => claim.Value)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

/// <summary>
/// Adds local role, permission, and profile claims to the access-token principal before OpenIddict clones it.
/// Role claims must be on the access token so Platform Identity can assign roles to other users.
/// </summary>
public sealed class PermissionClaimsHandler(
    IPermissionClaimResolver resolver,
    IIdentityUserRepository? users = null)
    : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignInContext>
{
    private static readonly string[] ProfileClaimTypes =
    [
        OpenIddictConstants.Claims.PreferredUsername,
        OpenIddictConstants.Claims.Name,
        OpenIddictConstants.Claims.GivenName,
        OpenIddictConstants.Claims.FamilyName
    ];

    public async ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        var principal = context.Principal
            ?? throw new InvalidOperationException("The OpenIddict sign-in principal is unavailable.");
        var identity = principal.Identity as ClaimsIdentity
            ?? throw new InvalidOperationException("The OpenIddict sign-in principal has no claims identity.");

        foreach (var claim in principal.FindAll("permission").ToArray())
        {
            identity.RemoveClaim(claim);
        }

        var roles = await resolver.ResolveRolesAsync(principal);
        foreach (var claim in identity.Claims
            .Where(claim => claim.Type == AbpClaimTypes.Role || claim.Type == ClaimTypes.Role)
            .ToArray())
        {
            identity.RemoveClaim(claim);
        }

        foreach (var role in roles)
        {
            var claim = new Claim(AbpClaimTypes.Role, role);
            claim.SetDestinations(
                OpenIddictConstants.Destinations.AccessToken,
                OpenIddictConstants.Destinations.IdentityToken);
            identity.AddClaim(claim);
        }

        var permissions = await resolver.ResolveAsync(principal);
        foreach (var permission in permissions)
        {
            var claim = new Claim("permission", permission);
            claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
            identity.AddClaim(claim);
        }

        foreach (var type in ProfileClaimTypes)
        {
            foreach (var claim in identity.FindAll(type))
            {
                claim.SetDestinations(
                    OpenIddictConstants.Destinations.AccessToken,
                    OpenIddictConstants.Destinations.IdentityToken);
            }
        }

        await AddIdentityProfileClaimsAsync(identity, principal);
    }

    private async Task AddIdentityProfileClaimsAsync(ClaimsIdentity identity, ClaimsPrincipal principal)
    {
        if (users is null)
        {
            return;
        }

        var subject = principal.FindFirst("sub")?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            return;
        }

        var user = await users.FindAsync(userId);
        if (user is null)
        {
            return;
        }

        UpsertProfileClaim(identity, OpenIddictConstants.Claims.PreferredUsername, user.UserName);
        UpsertProfileClaim(identity, OpenIddictConstants.Claims.GivenName, user.Name);
        UpsertProfileClaim(identity, OpenIddictConstants.Claims.FamilyName, user.Surname);
        var displayName = string.Join(' ', new[] { user.Surname, user.Name }
            .Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = user.UserName;
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            UpsertProfileClaim(identity, OpenIddictConstants.Claims.Name, displayName);
        }
    }

    private static void UpsertProfileClaim(ClaimsIdentity identity, string type, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var existing in identity.FindAll(type).ToArray())
        {
            identity.RemoveClaim(existing);
        }

        var claim = new Claim(type, value.Trim());
        claim.SetDestinations(
            OpenIddictConstants.Destinations.AccessToken,
            OpenIddictConstants.Destinations.IdentityToken);
        identity.AddClaim(claim);
    }
}
