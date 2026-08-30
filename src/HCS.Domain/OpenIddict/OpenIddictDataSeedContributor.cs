using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.OpenIddict;
using Volo.Abp.OpenIddict.Applications;
using Volo.Abp.OpenIddict.Scopes;
using Volo.Abp.Uow;

namespace HCS.OpenIddict;

/* Creates initial data that is needed to property run the application
 * and make client-to-server communication possible.
 */
public class OpenIddictDataSeedContributor : OpenIddictDataSeedContributorBase, IDataSeedContributor, ITransientDependency
{
    private readonly IOpenIddictScopeManager scopeManager;

    internal const string GatewayScope = "HCS";
    internal const string GatewayAudience = "HCS";
    internal const string BusManagementAudience = "HCS.BusManagementService";
    internal const string DefaultGatewayRootUrl = "https://localhost:44402";
    internal const string DefaultBlazorRootUrl = "https://localhost:44403";
    public OpenIddictDataSeedContributor(
        IConfiguration configuration,
        IOpenIddictApplicationRepository openIddictApplicationRepository,
        IAbpApplicationManager applicationManager,
        IOpenIddictScopeRepository openIddictScopeRepository,
        IOpenIddictScopeManager scopeManager)
        : base(configuration, openIddictApplicationRepository, applicationManager, openIddictScopeRepository, scopeManager)
    {
        this.scopeManager = scopeManager;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        await CreateScopesAsync();
        await CreateApplicationsAsync();
    }

    private async Task CreateScopesAsync()
    {
        var descriptor = CreateGatewayScopeDescriptor();
        var existingScope = await scopeManager.FindByNameAsync(GatewayScope);
        if (existingScope is null)
        {
            await CreateScopesAsync(descriptor);
            return;
        }

        // The ABP seed helper is create-only for an existing scope. Update the
        // resource list explicitly so existing installations receive newly added
        // downstream audiences on the next idempotent DbMigrator run.
        await scopeManager.UpdateAsync(existingScope, descriptor);
    }

    internal static OpenIddictScopeDescriptor CreateGatewayScopeDescriptor()
    {
        return new OpenIddictScopeDescriptor
        {
            Name = GatewayScope,
            DisplayName = "HCS Web Gateway API",
            Resources = { GatewayAudience, BusManagementAudience }
        };
    }

    private async Task CreateApplicationsAsync()
    {
        var commonScopes = new List<string> {
            OpenIddictConstants.Permissions.Scopes.Address,
            OpenIddictConstants.Permissions.Scopes.Email,
            OpenIddictConstants.Permissions.Scopes.Phone,
            OpenIddictConstants.Permissions.Scopes.Profile,
            OpenIddictConstants.Permissions.Scopes.Roles,
            GatewayScope
        };

        var configurationSection = Configuration.GetSection("OpenIddict:Applications");


        // Console Test / Angular Client
        
        var app = GetHcsAppRegistration(configurationSection);
        if (app is not null)
        {
            await CreateOrUpdateApplicationAsync(
                applicationType: OpenIddictConstants.ApplicationTypes.Web,
                name: app.ClientId,
                type: OpenIddictConstants.ClientTypes.Confidential,
                consentType: OpenIddictConstants.ConsentTypes.Implicit,
                displayName: "HCS Blazor Application",
                secret: app.ClientSecret,
                grantTypes: new List<string> {
                    OpenIddictConstants.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.GrantTypes.RefreshToken,
                    "LinkLogin"
                },
                scopes: commonScopes,
                redirectUris: new List<string> { app.CallbackUrl },
                postLogoutRedirectUris: new List<string>
                {
                    app.LogoutCallbackUrl,
                    app.BlazorRootUrl,
                    $"{app.BlazorRootUrl}/login"
                }.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                clientUri: app.BlazorRootUrl,
                logoUri: "/images/clients/hcs.svg"
            );
        }

        
        




        // Swagger Client
        var swaggerClientId = configurationSection["HCS_Swagger:ClientId"];
        var swaggerRootUrl = configurationSection["HCS_Swagger:RootUrl"]?.TrimEnd('/');
        if (!swaggerClientId.IsNullOrWhiteSpace() && !swaggerRootUrl.IsNullOrWhiteSpace())
        {
            await CreateOrUpdateApplicationAsync(
                applicationType: OpenIddictConstants.ApplicationTypes.Web,
                name: swaggerClientId!,
                type: OpenIddictConstants.ClientTypes.Public,
                consentType: OpenIddictConstants.ConsentTypes.Implicit,
                displayName: "Swagger Application",
                secret: null,
                grantTypes: new List<string> { OpenIddictConstants.GrantTypes.AuthorizationCode, },
                scopes: commonScopes,
                redirectUris: new List<string> { $"{swaggerRootUrl}/swagger/oauth2-redirect.html" },
                clientUri: swaggerRootUrl!.EnsureEndsWith('/') + "swagger",
                logoUri: "/images/clients/swagger.svg"
            );
        }

        foreach (var serviceName in new[]
                 {
                     "PlatformService", "OrganizationService", "DocumentService",
                     "WorkManagementService", "CollaborationService"
                 })
        {
            var serviceSection = configurationSection.GetSection(serviceName);
            var clientId = serviceSection["ClientId"]?.Trim();
            var clientSecret = serviceSection["ClientSecret"];
            if (clientId.IsNullOrWhiteSpace() || clientSecret.IsNullOrWhiteSpace())
            {
                continue;
            }

            await CreateOrUpdateApplicationAsync(
                applicationType: OpenIddictConstants.ApplicationTypes.Web,
                name: clientId!,
                type: OpenIddictConstants.ClientTypes.Confidential,
                consentType: OpenIddictConstants.ConsentTypes.Implicit,
                displayName: $"HCS {serviceName}",
                secret: clientSecret,
                grantTypes: new List<string> { OpenIddictConstants.GrantTypes.ClientCredentials },
                scopes: new List<string> { GatewayScope },
                clientUri: null,
                logoUri: null);
        }


    }

    internal static HcsAppRegistration? GetHcsAppRegistration(IConfigurationSection applications)
    {
        var clientId = applications["HCS_App:ClientId"]?.Trim();
        if (clientId.IsNullOrWhiteSpace()) return null;
        var clientSecret = applications["HCS_App:ClientSecret"];
        if (clientSecret.IsNullOrWhiteSpace())
            throw new InvalidOperationException("OpenIddict HCS_App ClientSecret must come from environment variables or User Secrets.");
        var gatewayRootUrl = ValidateHttpsOrigin(
            applications["HCS_App:RootUrl"] ?? DefaultGatewayRootUrl, "HCS_App RootUrl");
        var blazorRootUrl = ValidateHttpsOrigin(
            applications["HCS_App:PostLogoutRootUrl"] ?? DefaultBlazorRootUrl, "HCS_App PostLogoutRootUrl");
        return new HcsAppRegistration(clientId!, clientSecret!, gatewayRootUrl, blazorRootUrl,
            $"{gatewayRootUrl}/signin-oidc", $"{gatewayRootUrl}/signout-callback-oidc");
    }

    private static string ValidateHttpsOrigin(string value, string setting)
    {
        var origin = value.Trim().TrimEnd('/');
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException($"OpenIddict {setting} must be an HTTPS origin without path, query, or fragment.");
        return origin;
    }
}

internal sealed record HcsAppRegistration(string ClientId, string ClientSecret, string GatewayRootUrl,
    string BlazorRootUrl, string CallbackUrl, string LogoutCallbackUrl);
