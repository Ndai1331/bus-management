using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.PermissionManagement.Identity;
using Volo.Abp.Authorization.Permissions;

namespace HCS.Data;

public sealed class HCSRoleDataSeedContributor(
    IIdentityRoleRepository roleRepository,
    IGuidGenerator guidGenerator,
    IPermissionDataSeeder permissionDataSeeder,
    IPermissionDefinitionManager permissionDefinitionManager) : IDataSeedContributor, ITransientDependency
{
    private static readonly string[] RoleNames =
    [
        "admin", "bacsi", "lanhdao", "nhanvien", "station-manager", "operations-staff",
        "accountant-business", "control-security", "office"
    ];

    public async Task SeedAsync(DataSeedContext context)
    {
        foreach (var roleName in RoleNames)
        {
            var existingRole = await roleRepository.FindByNormalizedNameAsync(
                roleName.ToUpperInvariant());
            if (existingRole is not null)
            {
                continue;
            }

            await roleRepository.InsertAsync(new IdentityRole(guidGenerator.Create(), roleName));
        }

        var permissions = (await permissionDefinitionManager.GetPermissionsAsync())
            .Where(permission => permission.IsEnabled)
            .Select(permission => permission.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // Admin is the local break-glass administrator. Seed every enabled permission
        // registered by the current application, but never revoke existing grants.
        // This keeps policies and grants configured through the Roles UI intact.
        await permissionDataSeeder.SeedAsync(
            RolePermissionValueProvider.ProviderName,
            "admin",
            permissions);

        // Operational roles need the Work/Document/Chat pages after Community port.
        // Grants are additive so later Role UI changes are not revoked.
        var operationalPermissions = permissions
            .Where(permission =>
                permission.StartsWith("WorkManagement.", StringComparison.Ordinal) ||
                permission.StartsWith("Documents.", StringComparison.Ordinal) ||
                permission.StartsWith("Collaboration.", StringComparison.Ordinal))
            .ToArray();

        foreach (var roleName in RoleNames.Where(role => !string.Equals(role, "admin", StringComparison.Ordinal)))
        {
            await permissionDataSeeder.SeedAsync(
                RolePermissionValueProvider.ProviderName,
                roleName,
                operationalPermissions);
        }

        var busPermissions = permissions
            .Where(permission => permission.StartsWith("HCS.BusManagement", StringComparison.Ordinal))
            .ToArray();
        // A manager approves adjustment records created by accounting/operations; keeping
        // creation out of this seeded role preserves maker-checker at role level as well.
        var stationManagerPermissions = busPermissions
            .Where(permission => !string.Equals(permission, "HCS.BusManagement.Reconciliation.Adjust", StringComparison.Ordinal))
            .ToArray();
        var operationsPermissions = busPermissions.Where(permission =>
            permission.StartsWith("HCS.BusManagement.Departures", StringComparison.Ordinal) ||
            permission.StartsWith("HCS.BusManagement.Revenue", StringComparison.Ordinal)).ToArray();
        var accountantPermissions = busPermissions.Where(permission =>
            permission.StartsWith("HCS.BusManagement.Revenue", StringComparison.Ordinal) ||
            permission.StartsWith("HCS.BusManagement.Expenses", StringComparison.Ordinal) ||
            permission.StartsWith("HCS.BusManagement.Premises", StringComparison.Ordinal) ||
            permission.StartsWith("HCS.BusManagement.Reconciliation", StringComparison.Ordinal) ||
            permission.StartsWith("HCS.BusManagement.Reports", StringComparison.Ordinal)).ToArray();
        accountantPermissions = accountantPermissions
            .Where(permission => !string.Equals(permission, "HCS.BusManagement.Reconciliation.AdjustApprove", StringComparison.Ordinal) &&
                !string.Equals(permission, "HCS.BusManagement.Revenue.Parking.Create", StringComparison.Ordinal) &&
                !string.Equals(permission, "HCS.BusManagement.Revenue.Parking.Update", StringComparison.Ordinal))
            .ToArray();
        var controlPermissions = busPermissions.Where(permission =>
            permission.StartsWith("HCS.BusManagement.Departures", StringComparison.Ordinal) ||
            permission.StartsWith("HCS.BusManagement.MasterData", StringComparison.Ordinal)).ToArray();
        var leadershipPermissions = busPermissions.Where(permission =>
            !permission.EndsWith(".Create", StringComparison.Ordinal) &&
            !permission.EndsWith(".Update", StringComparison.Ordinal) &&
            !permission.EndsWith(".Delete", StringComparison.Ordinal) &&
            !permission.EndsWith(".Approve", StringComparison.Ordinal) &&
            !permission.EndsWith(".AdjustApprove", StringComparison.Ordinal) &&
            !permission.EndsWith(".Check", StringComparison.Ordinal) &&
            !permission.EndsWith(".Close", StringComparison.Ordinal) &&
            !permission.EndsWith(".Adjust", StringComparison.Ordinal) &&
            !permission.EndsWith(".Export", StringComparison.Ordinal)).ToArray();

        await permissionDataSeeder.SeedAsync(RolePermissionValueProvider.ProviderName, "lanhdao", leadershipPermissions);
        await permissionDataSeeder.SeedAsync(RolePermissionValueProvider.ProviderName, "station-manager", stationManagerPermissions);
        await permissionDataSeeder.SeedAsync(RolePermissionValueProvider.ProviderName, "operations-staff", operationsPermissions);
        await permissionDataSeeder.SeedAsync(RolePermissionValueProvider.ProviderName, "accountant-business", accountantPermissions);
        await permissionDataSeeder.SeedAsync(RolePermissionValueProvider.ProviderName, "control-security", controlPermissions);
    }
}
