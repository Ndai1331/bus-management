using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
using HCS.Bff;
using HCS.Blazor.Client;
using HCS.Blazor.Client.Authentication;
using HCS.Blazor.Client.Navigation;
using HCS.Blazor.Components;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Localization;
using StackExchange.Redis;
using System;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Components.Server.LeptonXLiteTheme;
using Volo.Abp.AspNetCore.Components.Server.LeptonXLiteTheme.Bundling;
using Volo.Abp.AspNetCore.Components.Web;
using Volo.Abp.AspNetCore.Components.Web.Theming.Routing;
using Volo.Abp.AspNetCore.Components.WebAssembly.LeptonXLiteTheme.Bundling;
using Volo.Abp.AspNetCore.Components.WebAssembly.Theming.Bundling;
using Volo.Abp.AspNetCore.Mvc.Client;
using Volo.Abp.AspNetCore.Mvc.Libs;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Http.Client.Web;
using Volo.Abp.Modularity;
using Volo.Abp.Security.Claims;
using Volo.Abp.UI.Navigation;
using Volo.Abp.UI.Navigation.Urls;

namespace HCS.Blazor;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreMvcClientModule),
    typeof(AbpHttpClientWebModule),
    typeof(AbpAspNetCoreComponentsServerLeptonXLiteThemeModule),
    typeof(AbpAspNetCoreComponentsWebAssemblyLeptonXLiteThemeBundlingModule),
    typeof(AbpAspNetCoreMvcUiLeptonXLiteThemeModule),
    typeof(AbpAspNetCoreSerilogModule)
)]
public sealed class HCSBlazorModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<AbpAspNetCoreComponentsWebOptions>(options => options.IsBlazorWebApp = true);
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var environment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        // The Community Blazor host ships client assets through ABP bundles and
        // embedded static assets, not a LibMan-generated wwwroot/libs folder.
        Configure<AbpMvcLibsOptions>(options => options.CheckLibs = false);
        Configure<RequestLocalizationOptions>(options => options
            .SetDefaultCulture("en")
            .AddSupportedCultures("en", "vi")
            .AddSupportedUICultures("en", "vi"));

        context.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        if (environment.IsDevelopment() && configuration.GetValue("App:EnablePII", false))
        {
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
        }

        ConfigureAuthentication(context, configuration, environment);
        context.Services.Replace(
            ServiceDescriptor.Singleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>());
        ConfigureUrls(configuration);
        ConfigureBundles();
        ConfigureBlazorise(context);
        ConfigureRouter();
        ConfigureMenu(configuration);
    }

    private static void ConfigureAuthentication(
        ServiceConfigurationContext context,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var cookieDomain = ValidateAndGetCookieDomain(configuration);
        context.Services.AddSingleton(TimeProvider.System);
        context.Services.AddSingleton<ITicketStore, BffAuthTicketStore>();
        context.Services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Cookie.Name = ".HCS.Bff";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
                // Must match WebGateway: OIDC form_post is cross-site and requires SameSite=None.
                options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.None;
                options.Cookie.Path = "/";
                options.Cookie.Domain = cookieDomain;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = cookieContext =>
                {
                    var gatewayUrl = GetRequiredHttpsOrigin(configuration, "Bff:PublicOrigin");
                    var returnUrl = Uri.EscapeDataString(cookieContext.Request.GetEncodedUrl());
                    cookieContext.Response.Redirect($"{gatewayUrl}/bff/login?returnUrl={returnUrl}");
                    return Task.CompletedTask;
                };
            });
        context.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<ITicketStore>((options, store) => options.SessionStore = store);

        var redisConnectionString = GetRequiredConfiguration(configuration, "DataProtection:Redis");
        var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
        var connectTimeoutSeconds = Math.Clamp(configuration.GetValue("DataProtection:ConnectTimeoutSeconds", 5), 1, 15);
        redisOptions.ConnectTimeout = checked(connectTimeoutSeconds * 1000);
        redisOptions.SyncTimeout = checked(connectTimeoutSeconds * 1000);
        redisOptions.ConnectRetry = 1;
        redisOptions.AbortOnConnectFail = true;
        var redis = ConnectionMultiplexer.Connect(redisOptions);
        context.Services.AddSingleton<IConnectionMultiplexer>(redis);
        var dataProtection = context.Services.AddDataProtection()
            .SetApplicationName("HCS.Bff")
            .PersistKeysToStackExchangeRedis(redis, "HCS-DataProtection-Keys");

        var certificatePath = configuration["DataProtection:Certificate:Path"];
        var certificatePassword = configuration["DataProtection:Certificate:Password"];
        if (string.IsNullOrWhiteSpace(certificatePath) || string.IsNullOrWhiteSpace(certificatePassword))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Production requires DataProtection:Certificate:Path and Password from environment/User Secrets to protect Redis keys at rest.");
            }

            if (!string.IsNullOrWhiteSpace(certificatePath) || !string.IsNullOrWhiteSpace(certificatePassword))
            {
                throw new InvalidOperationException("Data Protection certificate path and password must be supplied together.");
            }
        }
        else
        {
            if (!Path.IsPathFullyQualified(certificatePath) || !File.Exists(certificatePath))
            {
                throw new InvalidOperationException("Data Protection certificate path must be an existing absolute path.");
            }

            var certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath, certificatePassword);
            if (!certificate.HasPrivateKey)
            {
                throw new InvalidOperationException("Data Protection certificate must contain a private key.");
            }

            dataProtection.ProtectKeysWithCertificate(certificate);
        }

        context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
            options.IsDynamicClaimsEnabled = true);
    }

    private static string GetRequiredConfiguration(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Required configuration '{key}' is missing. Supply it through environment variables or user secrets.");
        }

        return value;
    }

    private static string GetRequiredHttpsOrigin(IConfiguration configuration, string key)
    {
        var value = GetRequiredConfiguration(configuration, key).TrimEnd('/');
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"Configuration '{key}' must use HTTPS.");
        }

        return value;
    }

    private static string? ValidateAndGetCookieDomain(IConfiguration configuration)
    {
        var ui = new Uri(GetRequiredHttpsOrigin(configuration, "App:SelfUrl"));
        var gateway = new Uri(GetRequiredHttpsOrigin(configuration, "Bff:PublicOrigin"));
        var domain = configuration["Bff:CookieDomain"]?.Trim().TrimStart('.');
        if (string.IsNullOrWhiteSpace(domain))
        {
            if (!ui.Host.Equals(gateway.Host, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Blazor and BFF Gateway must use the same host unless Bff:CookieDomain covers both hosts.");
            }

            return null;
        }

        if (ui.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || IPAddress.TryParse(ui.Host, out _) || !domain.Contains('.') ||
            !HostMatchesDomain(ui.Host, domain) || !HostMatchesDomain(gateway.Host, domain))
        {
            throw new InvalidOperationException("Bff:CookieDomain must be a valid parent domain of both Blazor and Gateway and cannot be used for localhost/IP.");
        }

        return $".{domain}";
    }

    private static bool HostMatchesDomain(string host, string domain) =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase);

    private void ConfigureUrls(IConfiguration configuration)
    {
        Configure<AppUrlOptions>(options =>
        {
            options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"]!;
            options.RedirectAllowedUrls.AddRange(
                configuration["App:RedirectAllowedUrls"]?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                ?? []);
        });
    }

    private void ConfigureBundles()
    {
        Configure<AbpBundlingOptions>(options =>
        {
            options.Parameters.InteractiveAuto = true;
            options.Parameters["LeptonXTheme.Layout"] = "side-menu";

            options.ScriptBundles.Configure(LeptonXLiteThemeBundles.Scripts.Global,
                bundle => bundle.AddFiles("/global-scripts.js"));
            options.ScriptBundles.Get(BlazorWebAssemblyStandardBundles.Scripts.Global)
                .AddContributors(typeof(HCSScriptBundleContributor));
        });
    }

    private static void ConfigureBlazorise(ServiceConfigurationContext context)
    {
        context.Services.AddBlazorise(options =>
        {
            options.Immediate = true;
        })
            .AddBootstrap5Providers()
            .AddFontAwesomeIcons();
    }

    private void ConfigureRouter()
    {
        Configure<AbpRouterOptions>(options =>
        {
            options.AppAssembly = typeof(HCSBlazorModule).Assembly;
            options.AdditionalAssemblies.Add(typeof(HCSBlazorClientModule).Assembly);
        });
    }

    private void ConfigureMenu(IConfiguration configuration)
    {
        Configure<AbpNavigationOptions>(options =>
            options.MenuContributors.Add(new HCSMenuContributor()));
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
        app.Use(async (httpContext, next) =>
        {
            var cookie = httpContext.RequestServices
                .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(CookieAuthenticationDefaults.AuthenticationScheme)
                .Cookie
                .Build(httpContext);
            BffCookieChunkCleanup.ExpireLegacyChunks(httpContext, cookie);
            await next();
        });
        app.MapAbpStaticAssets();
        app.UseAbpSecurityHeaders();
        app.UseAuthentication();
        app.UseUnitOfWork();
        app.UseDynamicClaims();
        app.UseAntiforgery();
        app.UseAuthorization();
        app.UseAuditing();
        app.UseAbpSerilogEnrichers();

        app.UseConfiguredEndpoints(endpoints =>
        {
            endpoints.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode()
                .AddInteractiveWebAssemblyRenderMode()
                .AddAdditionalAssemblies(
                    endpoints.ServiceProvider.GetRequiredService<IOptions<AbpRouterOptions>>()
                        .Value.AdditionalAssemblies.ToArray());
        });
    }
}
