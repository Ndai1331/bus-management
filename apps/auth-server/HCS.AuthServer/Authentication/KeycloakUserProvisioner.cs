using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Uow;
using AbpIdentityUser = Volo.Abp.Identity.IdentityUser;

namespace HCS.AuthServer;

public interface IKeycloakUserProvisioner
{
    Task<Guid> ProvisionAsync(
        ClaimsPrincipal principal,
        IReadOnlyList<string> roles,
        CancellationToken cancellationToken = default);
}

public interface IKeycloakIdentityStore
{
    Task<KeycloakIdentityUser?> FindByExternalLoginAsync(string subject, CancellationToken cancellationToken);
    Task<KeycloakIdentityUser?> FindByVerifiedEmailAsync(string email, CancellationToken cancellationToken);
    Task<KeycloakIdentityUser?> FindByUserNameAsync(string userName, CancellationToken cancellationToken);
    Task<KeycloakIdentityUser> CreateAsync(string userName, string verifiedEmail, CancellationToken cancellationToken);
    Task LinkAsync(KeycloakIdentityUser user, string subject, CancellationToken cancellationToken);
    Task UpdateProfileNamesAsync(KeycloakIdentityUser user, string? givenName, string? surname, CancellationToken cancellationToken);
}

public sealed record KeycloakIdentityUser(Guid Id, string UserName, string? Email, object NativeUser);

public class KeycloakUserProvisioner(IKeycloakIdentityStore store) : IKeycloakUserProvisioner, ITransientDependency
{
    [UnitOfWork]
    public virtual async Task<Guid> ProvisionAsync(
        ClaimsPrincipal principal,
        IReadOnlyList<string> roles,
        CancellationToken cancellationToken = default)
    {
        var profile = KeycloakUserProfile.FromPrincipal(principal, roles);
        var linked = await store.FindByExternalLoginAsync(profile.Subject, cancellationToken);
        var byEmail = profile.VerifiedEmail is null
            ? null
            : await store.FindByVerifiedEmailAsync(profile.VerifiedEmail, cancellationToken);
        var byName = await store.FindByUserNameAsync(profile.UserName, cancellationToken);

        EnsureSameAccount(linked, byEmail, byName);

        var user = linked ?? byEmail ?? byName;
        if (user is null)
        {
            if (profile.VerifiedEmail is null)
            {
                throw new KeycloakProvisioningException(
                    "A verified Keycloak email is required when provisioning a new HCS user.");
            }

            user = await store.CreateAsync(profile.UserName, profile.VerifiedEmail, cancellationToken);
        }

        if (linked is null)
        {
            await store.LinkAsync(user, profile.Subject, cancellationToken);
        }

        var givenName = profile.GivenName;
        var surname = profile.FamilyName;
        if (string.IsNullOrWhiteSpace(givenName)
            && string.IsNullOrWhiteSpace(surname)
            && !string.IsNullOrWhiteSpace(profile.FullName))
        {
            givenName = profile.FullName;
        }

        await store.UpdateProfileNamesAsync(user, givenName, surname, cancellationToken);
        return user.Id;
    }

    private static void EnsureSameAccount(params KeycloakIdentityUser?[] candidates)
    {
        var ids = candidates.Where(candidate => candidate is not null).Select(candidate => candidate!.Id).Distinct().ToArray();
        if (ids.Length > 1)
        {
            throw new KeycloakProvisioningException(
                "Keycloak login matches conflicting HCS accounts. Automatic linking was refused.");
        }
    }
}

public sealed class AbpKeycloakIdentityStore(
    IdentityUserManager userManager,
    IGuidGenerator guidGenerator) : IKeycloakIdentityStore, ITransientDependency
{
    public const string LoginProvider = "Keycloak";

    public async Task<KeycloakIdentityUser?> FindByExternalLoginAsync(
        string subject,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Wrap(await userManager.FindByLoginAsync(LoginProvider, subject));
    }

    public async Task<KeycloakIdentityUser?> FindByVerifiedEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Wrap(await userManager.FindByEmailAsync(email));
    }

    public async Task<KeycloakIdentityUser?> FindByUserNameAsync(
        string userName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Wrap(await userManager.FindByNameAsync(userName));
    }

    public async Task<KeycloakIdentityUser> CreateAsync(
        string userName,
        string verifiedEmail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = new AbpIdentityUser(guidGenerator.Create(), userName, verifiedEmail);
        Check(await userManager.CreateAsync(user), "create user");
        Check(await userManager.AddToRoleAsync(user, "nhanvien"), "assign default role");
        return Wrap(user)!;
    }

    public async Task LinkAsync(
        KeycloakIdentityUser user,
        string subject,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Check(
            await userManager.AddLoginAsync(
                GetNativeUser(user),
                new UserLoginInfo(LoginProvider, subject, LoginProvider)),
            "link Keycloak login");
    }

    public async Task UpdateProfileNamesAsync(
        KeycloakIdentityUser user,
        string? givenName,
        string? surname,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var native = GetNativeUser(user);
        if (string.IsNullOrWhiteSpace(givenName) && string.IsNullOrWhiteSpace(surname))
        {
            return;
        }

        native.Name = string.IsNullOrWhiteSpace(givenName) ? native.Name : givenName.Trim();
        native.Surname = string.IsNullOrWhiteSpace(surname) ? native.Surname : surname.Trim();
        Check(await userManager.UpdateAsync(native), "update profile names");
    }

    private static AbpIdentityUser GetNativeUser(KeycloakIdentityUser user) =>
        user.NativeUser as AbpIdentityUser
        ?? throw new AbpException("The Keycloak identity store received an invalid native user instance.");

    private static KeycloakIdentityUser? Wrap(AbpIdentityUser? user) =>
        user is null ? null : new KeycloakIdentityUser(user.Id, user.UserName, user.Email, user);

    private static void Check(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        throw new KeycloakProvisioningException(
            $"Unable to {operation}: {string.Join("; ", result.Errors.Select(error => error.Description))}");
    }
}
