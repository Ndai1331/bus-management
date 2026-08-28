using HCS.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Server;
using OpenIddict.Validation.AspNetCore;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Mvc.UI.Theming;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;
using Volo.Abp.Security.Claims;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.Ui.LayoutHooks;

namespace HCS.AuthServer;

[DependsOn(
    typeof(HCSEntityFrameworkCoreModule),
    typeof(HCSApplicationContractsModule),
    typeof(AbpAccountApplicationModule),
    typeof(AbpAccountWebOpenIddictModule),
    typeof(AbpAspNetCoreMvcUiLeptonXLiteThemeModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpAutofacModule)
)]
public sealed class HCSAuthServerModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var environment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        PreConfigure<OpenIddictBuilder>(builder =>
        {
            builder.AddValidation(options =>
            {
                options.AddAudiences("HCS");
                options.UseLocalServer();
                options.UseAspNetCore();
            });
        });

        PreConfigure<OpenIddictServerBuilder>(ConfigureAccessTokenFormat);
        PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
            serverBuilder.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(builder =>
                builder.UseScopedHandler<PermissionClaimsHandler>()
                    .SetOrder(OpenIddictServerHandlers.PrepareAccessTokenPrincipal.Descriptor.Order - 500)));

        if (!environment.IsDevelopment())
        {
            var certificatePath = configuration["AuthServer:CertificatePath"];
            var certificatePassword = configuration["AuthServer:CertificatePassword"];
            if (string.IsNullOrWhiteSpace(certificatePath) || string.IsNullOrWhiteSpace(certificatePassword))
            {
                throw new AbpException(
                    "Production requires AuthServer:CertificatePath and AuthServer:CertificatePassword from runtime configuration.");
            }

            PreConfigure<AbpOpenIddictAspNetCoreOptions>(options =>
                options.AddDevelopmentEncryptionAndSigningCertificate = false);
            PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
                serverBuilder.AddProductionEncryptionAndSigningCertificate(certificatePath, certificatePassword));
        }
    }

    public static void ConfigureAccessTokenFormat(OpenIddictServerBuilder serverBuilder)
    {
        // APIs validate tokens in separate processes through issuer discovery.
        // Keep access tokens signed but unencrypted so standard JWT validation can read them.
        serverBuilder.DisableAccessTokenEncryption();
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        Configure<AbpLayoutHookOptions>(options =>
            options.Add(
                LayoutHooks.Head.First,
                typeof(AuthServerFaviconViewComponent),
                layout: StandardLayouts.Account));

        Configure<AbpBundlingOptions>(options =>
            options.StyleBundles.Configure(
                LeptonXLiteThemeBundles.Styles.Global,
                bundle => bundle.AddFiles("/auth-login.css")));

        Configure<AppUrlOptions>(options =>
        {
            options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"]!;
            options.RedirectAllowedUrls.AddRange(
                configuration["App:RedirectAllowedUrls"]?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                ?? []);

            var clientUrl = configuration["App:ClientUrl"];
            if (!string.IsNullOrWhiteSpace(clientUrl))
            {
                options.RedirectAllowedUrls.AddIfNotContains(clientUrl.TrimEnd('/'));
                options.RedirectAllowedUrls.AddIfNotContains(clientUrl.TrimEnd('/') + "/");
            }
        });

        if (!configuration.GetValue("AuthServer:RequireHttpsMetadata", true))
        {
            Configure<OpenIddictServerAspNetCoreOptions>(options =>
                options.DisableTransportSecurityRequirement = true);
        }

        context.Services.ForwardIdentityAuthenticationForBearer(
            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
            options.IsDynamicClaimsEnabled = true);
        context.Services.AddTransient<PermissionClaimsHandler>();

        ConfigureKeycloak(context);
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var environment = context.GetEnvironment();

        if (environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseErrorPage();
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseAbpRequestLocalization();
        app.UseCorrelationId();
        app.UseRouting();
        app.MapAbpStaticAssets();
        app.UseAbpSecurityHeaders();
        app.UseAuthentication();
        app.UseAbpOpenIddictValidation();
        app.UseUnitOfWork();
        app.UseDynamicClaims();
        app.UseAuthorization();
        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints();
    }

    private static void ConfigureKeycloak(ServiceConfigurationContext context)
    {
        var section = context.Services.GetConfiguration().GetSection(KeycloakOptions.SectionName);
        var settings = section.Get<KeycloakOptions>() ?? new KeycloakOptions();

        context.Services.AddOptions<KeycloakOptions>()
            .Bind(section);

        if (!settings.Enabled)
        {
            return;
        }

        context.Services.AddAuthentication()
            .AddOpenIdConnect(KeycloakOptions.Scheme, "Login với SSO", options =>
            {
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.Authority = settings.Authority;
                if (!string.IsNullOrWhiteSpace(settings.MetadataAddress))
                {
                    options.MetadataAddress = settings.MetadataAddress;
                }
                options.RequireHttpsMetadata = settings.RequireHttpsMetadata;
                options.ClientId = settings.ClientId;
                options.ClientSecret = settings.ClientSecret;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;

                // options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;

                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.CallbackPath = KeycloakOptions.CallbackPath;
                options.MapInboundClaims = false;
                options.Scope.Add("email");
                options.ClaimActions.MapJsonKey(KeycloakGroupRoleMapper.GroupsClaim, "groups");
                options.TokenValidationParameters.NameClaimType = "preferred_username";
                options.TokenValidationParameters.RoleClaimType = AbpClaimTypes.Role;
                options.Events = KeycloakOpenIdConnectEvents.Create();
            });
    }
}

public sealed class AuthServerFaviconViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        var link = new TagBuilder("link");
        link.Attributes["rel"] = "icon";
        link.Attributes["href"] = "/favicon.ico";
        link.Attributes["type"] = "image/x-icon";

        return new HtmlContentViewComponentResult(link);
    }
}
