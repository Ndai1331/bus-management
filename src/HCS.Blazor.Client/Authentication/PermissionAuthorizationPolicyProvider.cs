using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace HCS.Blazor.Client.Authentication;

/// <summary>
/// Lets ABP Blazor pages use their existing permission names as authorization policies.
/// The BFF only exposes server-issued permission claims, while APIs remain the authority
/// for every write operation.
/// </summary>
public sealed class PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    private static readonly string[] PermissionPrefixes =
    [
        "AbpIdentity.",
        "FeatureManagement.",
        "SettingManagement.",
        "PermissionManagement.",
        "HCS.",
        "Collaboration.",
        "WorkManagement.",
        "Documents."
    ];

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var configuredPolicy = await base.GetPolicyAsync(policyName);
        if (configuredPolicy is not null || !IsPermissionPolicy(policyName))
        {
            return configuredPolicy;
        }

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim("permission", policyName)
            .Build();
    }

    private static bool IsPermissionPolicy(string policyName) =>
        !string.IsNullOrWhiteSpace(policyName) &&
        PermissionPrefixes.Any(prefix => policyName.StartsWith(prefix, StringComparison.Ordinal));
}
