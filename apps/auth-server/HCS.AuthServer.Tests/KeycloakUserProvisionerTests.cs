using System.Security.Claims;
using Xunit;

namespace HCS.AuthServer.Tests;

public sealed class KeycloakUserProvisionerTests
{
    [Fact]
    public async Task First_Login_Creates_Links_And_Uses_Local_Default_Role()
    {
        var store = new InMemoryIdentityStore();
        var provisioner = new KeycloakUserProvisioner(store);
        var principal = CreatePrincipal("kc-1", "doctor", "doctor@benhvien.vn", emailVerified: true);

        var firstId = await provisioner.ProvisionAsync(principal, ["bacsi"]);
        var secondId = await provisioner.ProvisionAsync(principal, ["bacsi"]);

        Assert.Equal(firstId, secondId);
        Assert.Single(store.Users);
        Assert.Single(store.Links);
        Assert.Equal(["nhanvien"], store.Roles[firstId]);
    }

    [Fact]
    public async Task Existing_Verified_Email_Is_Linked_Without_Replacing_Local_Roles()
    {
        var existing = InMemoryIdentityStore.User("local-name", "doctor@benhvien.vn");
        var store = new InMemoryIdentityStore(existing);
        store.Roles[existing.Id] = ["station-manager"];
        var provisioner = new KeycloakUserProvisioner(store);
        var principal = CreatePrincipal("kc-2", "doctor", "doctor@benhvien.vn", emailVerified: true);

        await provisioner.ProvisionAsync(principal, ["bacsi", "nhanvien"]);
        await provisioner.ProvisionAsync(principal, ["lanhdao"]);

        Assert.Single(store.Users);
        Assert.Equal(existing.Id, store.Links["kc-2"]);
        Assert.Equal(["station-manager"], store.Roles[existing.Id]);
    }

    [Fact]
    public async Task Keycloak_Name_Claims_Are_Copied_Onto_The_Local_User()
    {
        var store = new InMemoryIdentityStore();
        var provisioner = new KeycloakUserProvisioner(store);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "kc-name"),
            new Claim("preferred_username", "doctor"),
            new Claim("email", "doctor@benhvien.vn"),
            new Claim("email_verified", "true"),
            new Claim("given_name", "Văn A"),
            new Claim("family_name", "Nguyễn")
        ], "Keycloak"));

        var userId = await provisioner.ProvisionAsync(principal, ["bacsi"]);

        Assert.Equal(("Văn A", "Nguyễn"), store.Names[userId]);
    }

    [Fact]
    public async Task New_User_Requires_A_Verified_Email()
    {
        var provisioner = new KeycloakUserProvisioner(new InMemoryIdentityStore());
        var principal = CreatePrincipal("kc-3", "employee", "employee@benhvien.vn", emailVerified: false);

        var exception = await Assert.ThrowsAsync<KeycloakProvisioningException>(
            () => provisioner.ProvisionAsync(principal, ["nhanvien"]));

        Assert.Contains("verified Keycloak email", exception.Message);
    }

    [Fact]
    public async Task Conflicting_Email_And_Username_Are_Not_Automatically_Linked()
    {
        var byName = InMemoryIdentityStore.User("employee", "one@benhvien.vn");
        var byEmail = InMemoryIdentityStore.User("other", "employee@benhvien.vn");
        var provisioner = new KeycloakUserProvisioner(new InMemoryIdentityStore(byName, byEmail));
        var principal = CreatePrincipal("kc-4", "employee", "employee@benhvien.vn", emailVerified: true);

        await Assert.ThrowsAsync<KeycloakProvisioningException>(
            () => provisioner.ProvisionAsync(principal, ["nhanvien"]));
    }

    private static ClaimsPrincipal CreatePrincipal(
        string subject,
        string userName,
        string email,
        bool emailVerified) =>
        new(new ClaimsIdentity(
        [
            new Claim("sub", subject),
            new Claim("preferred_username", userName),
            new Claim("email", email),
            new Claim("email_verified", emailVerified.ToString().ToLowerInvariant())
        ], "Keycloak"));

    private sealed class InMemoryIdentityStore(params KeycloakIdentityUser[] seed) : IKeycloakIdentityStore
    {
        public List<KeycloakIdentityUser> Users { get; } = [.. seed];
        public Dictionary<string, Guid> Links { get; } = new(StringComparer.Ordinal);
        public Dictionary<Guid, IReadOnlyList<string>> Roles { get; } = [];
        public Dictionary<Guid, (string? GivenName, string? Surname)> Names { get; } = [];

        public static KeycloakIdentityUser User(string userName, string email) =>
            new(Guid.NewGuid(), userName, email, new object());

        public Task<KeycloakIdentityUser?> FindByExternalLoginAsync(string subject, CancellationToken cancellationToken) =>
            Task.FromResult(
                Links.TryGetValue(subject, out var id) ? Users.Single(user => user.Id == id) : null);

        public Task<KeycloakIdentityUser?> FindByVerifiedEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(Users.SingleOrDefault(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)));

        public Task<KeycloakIdentityUser?> FindByUserNameAsync(string userName, CancellationToken cancellationToken) =>
            Task.FromResult(Users.SingleOrDefault(user => string.Equals(user.UserName, userName, StringComparison.OrdinalIgnoreCase)));

        public Task<KeycloakIdentityUser> CreateAsync(string userName, string verifiedEmail, CancellationToken cancellationToken)
        {
            var user = User(userName, verifiedEmail);
            Users.Add(user);
            Roles[user.Id] = ["nhanvien"];
            return Task.FromResult(user);
        }

        public Task LinkAsync(KeycloakIdentityUser user, string subject, CancellationToken cancellationToken)
        {
            Links.TryAdd(subject, user.Id);
            return Task.CompletedTask;
        }

        public Task UpdateProfileNamesAsync(
            KeycloakIdentityUser user,
            string? givenName,
            string? surname,
            CancellationToken cancellationToken)
        {
            Names[user.Id] = (givenName, surname);
            return Task.CompletedTask;
        }
    }
}
