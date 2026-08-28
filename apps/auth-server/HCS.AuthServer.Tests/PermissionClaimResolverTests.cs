using System.Security.Claims;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HCS.AuthServer.Tests;

public sealed class PermissionClaimResolverTests
{
    [Fact]
    public async Task ResolveAsync_ReturnsTheDistinctUnionOfLocalRoleGrants()
    {
        var manager = new FakePermissionManager(
            ("admin", [Granted("HCS.Organization.Departments"), Granted("AbpIdentity.Roles.ManagePermissions")]),
            ("nhanvien", [Granted("HCS.Organization.Departments"), Denied("HCS.Organization.Units")]));
        var principal = CreatePrincipal("admin", "nhanvien");

        var permissions = await new PermissionClaimResolver(manager).ResolveAsync(principal);

        Assert.Equal(
            ["HCS.Organization.Departments", "AbpIdentity.Roles.ManagePermissions"],
            permissions);
        Assert.All(manager.Calls, call => Assert.Equal("R", call.ProviderName));
    }

    [Fact]
    public async Task ResolveAsync_PrioritizesModulePermissions_AndDoesNotDropWorkManagementForLargeGrantSets()
    {
        var many = Enumerable.Range(0, 300)
            .Select(i => Granted($"FeatureManagement.Extra{i}"))
            .Append(Granted("WorkManagement.Dashboard"))
            .Append(Granted("Documents.View"))
            .ToArray();
        var manager = new FakePermissionManager(("nhanvien", many));

        var permissions = await new PermissionClaimResolver(manager).ResolveAsync(CreatePrincipal("nhanvien"));

        Assert.Contains("WorkManagement.Dashboard", permissions);
        Assert.Contains("Documents.View", permissions);
        Assert.Equal("WorkManagement.Dashboard", permissions[0]);
    }

    [Fact]
    public async Task ResolveAsync_DoesNotTruncateAdminGrants()
    {
        var many = Enumerable.Range(0, 400)
            .Select(i => Granted($"FeatureManagement.Extra{i}"))
            .Append(Granted("WorkManagement.Surveys"))
            .ToArray();
        var manager = new FakePermissionManager(("admin", many));

        var permissions = await new PermissionClaimResolver(manager).ResolveAsync(CreatePrincipal("admin"));

        Assert.Equal(401, permissions.Count);
        Assert.Contains("WorkManagement.Surveys", permissions);
    }

    [Fact]
    public async Task Handler_ReplacesExternalPermissionClaimsWithResolvedAccessTokenClaims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("permission", "untrusted-keycloak-permission")], "test"));
        var context = new OpenIddictServerEvents.ProcessSignInContext(new OpenIddictServerTransaction())
        {
            Principal = principal
        };

        await new PermissionClaimsHandler(new StaticPermissionClaimResolver("HCS.Organization.Departments"))
            .HandleAsync(context);

        var claim = Assert.Single(principal.FindAll("permission"));
        Assert.Equal("HCS.Organization.Departments", claim.Value);
        Assert.Equal(
            [OpenIddictConstants.Destinations.AccessToken],
            claim.GetDestinations().OrderBy(destination => destination));
    }

    [Fact]
    public async Task Handler_AddsLocalRolesToAccessToken()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(AbpClaimTypes.Role, "admin")], "test"));
        var context = new OpenIddictServerEvents.ProcessSignInContext(new OpenIddictServerTransaction())
        {
            Principal = principal
        };

        await new PermissionClaimsHandler(new StaticPermissionClaimResolver("HCS.Organization.Departments")
        {
            Roles = ["admin", "bacsi"]
        }).HandleAsync(context);

        var roles = principal.FindAll(AbpClaimTypes.Role).Select(claim => claim.Value).OrderBy(role => role).ToArray();
        Assert.Equal(["admin", "bacsi"], roles);
        Assert.All(principal.FindAll(AbpClaimTypes.Role), claim =>
            Assert.Contains(OpenIddictConstants.Destinations.AccessToken, claim.GetDestinations()));
    }

    [Fact]
    public async Task Handler_SendsProfileNameClaimsToAccessToken()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("preferred_username", "doctor"),
            new Claim("name", "Nguyễn Văn A"),
            new Claim("given_name", "Văn A"),
            new Claim("family_name", "Nguyễn")
        ], "test"));
        var context = new OpenIddictServerEvents.ProcessSignInContext(new OpenIddictServerTransaction())
        {
            Principal = principal
        };

        await new PermissionClaimsHandler(new StaticPermissionClaimResolver())
            .HandleAsync(context);

        Assert.Contains(OpenIddictConstants.Destinations.AccessToken,
            principal.FindFirst("name")!.GetDestinations());
        Assert.Contains(OpenIddictConstants.Destinations.AccessToken,
            principal.FindFirst("given_name")!.GetDestinations());
        Assert.Contains(OpenIddictConstants.Destinations.AccessToken,
            principal.FindFirst("family_name")!.GetDestinations());
        Assert.Equal("Nguyễn Văn A", principal.FindFirst("name")?.Value);
    }

    private static ClaimsPrincipal CreatePrincipal(params string[] roles) =>
        new(new ClaimsIdentity(
            roles.Select(role => new Claim(AbpClaimTypes.Role, role)),
            "test"));

    private static PermissionWithGrantedProviders Granted(string name) => new(name, true);

    private static PermissionWithGrantedProviders Denied(string name) => new(name, false);

    private sealed class StaticPermissionClaimResolver(params string[] permissions) : IPermissionClaimResolver
    {
        public IReadOnlyList<string> Roles { get; init; } = [];

        public Task<IReadOnlyList<string>> ResolveAsync(ClaimsPrincipal principal) =>
            Task.FromResult<IReadOnlyList<string>>(permissions);

        public Task<IReadOnlyList<string>> ResolveRolesAsync(ClaimsPrincipal principal) =>
            Task.FromResult(Roles);
    }

    private sealed class FakePermissionManager(
        params (string Role, PermissionWithGrantedProviders[] Permissions)[] grants) : IPermissionManager
    {
        private readonly Dictionary<string, PermissionWithGrantedProviders[]> _grants = grants.ToDictionary(
            grant => grant.Role,
            grant => grant.Permissions,
            StringComparer.OrdinalIgnoreCase);

        public List<(string ProviderName, string ProviderKey)> Calls { get; } = [];

        public Task<List<PermissionWithGrantedProviders>> GetAllAsync(string providerName, string providerKey)
        {
            Calls.Add((providerName, providerKey));
            return Task.FromResult(_grants.TryGetValue(providerKey, out var permissions)
                ? permissions.ToList()
                : []);
        }

        public Task<PermissionWithGrantedProviders> GetAsync(string permissionName, string providerName, string providerKey) =>
            throw new NotSupportedException();

        public Task<MultiplePermissionWithGrantedProviders> GetAsync(string[] permissionNames, string provideName, string providerKey) =>
            throw new NotSupportedException();

        public Task SetAsync(string permissionName, string providerName, string providerKey, bool isGranted) =>
            throw new NotSupportedException();

        public Task<PermissionGrant> UpdateProviderKeyAsync(PermissionGrant permissionGrant, string providerKey) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string providerName, string providerKey) => throw new NotSupportedException();
    }
}
