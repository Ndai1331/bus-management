using System;
using System.Net.Http;
using Blazorise;
using Blazorise.Bootstrap5;
using Blazorise.Icons.FontAwesome;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using HCS.Blazor.Client.Navigation;
using HCS.Blazor.Client.Authentication;
using HCS.Blazor.Client.Auditing;
using HCS.Blazor.Client.Collaboration;
using HCS.Blazor.Client.Pages.Organization;
using HCS.Blazor.Client.Pages.BusManagement;
using HCS.Blazor.Client.Pages;
using Localization.Resources.AbpUi;
using OpenIddict.Abstractions;
using Volo.Abp.AspNetCore.Components.Web;
using Volo.Abp.AspNetCore.Components.Web.Theming.Routing;
using Volo.Abp.Autofac.WebAssembly;
using Volo.Abp.Http.Client;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;
using Volo.Abp.Localization;
using Volo.Abp.UI.Navigation;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Components.WebAssembly.Theming.Bundling;
using Volo.Abp.AspNetCore.Components.WebAssembly.LeptonXLiteTheme;
using Volo.Abp.SettingManagement.Blazor.WebAssembly;
using Volo.Abp.FeatureManagement.Blazor.WebAssembly;
using Volo.Abp.Identity.Blazor;
using Volo.Abp.Identity.Blazor.WebAssembly;


namespace HCS.Blazor.Client;

[DependsOn(
    typeof(AbpSettingManagementBlazorWebAssemblyModule),
    typeof(AbpFeatureManagementBlazorWebAssemblyModule),
    typeof(AbpIdentityBlazorWebAssemblyModule),
    typeof(AbpAspNetCoreComponentsWebAssemblyLeptonXLiteThemeModule),
    typeof(AbpAutofacWebAssemblyModule),
    typeof(HCSHttpApiClientModule)
)]
public class HCSBlazorClientModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<AbpAspNetCoreComponentsWebOptions>(options =>
        {
            options.IsBlazorWebApp = true;
        });
        PreConfigure<AbpHttpClientBuilderOptions>(options =>
        {
            // ABP dynamic proxies (application-configuration, Identity, …) must send BFF cookies.
            options.ProxyClientBuildActions.Add((_, clientBuilder) =>
            {
                clientBuilder.AddHttpMessageHandler<BffHttpMessageHandler>();
            });
        });
    }
    
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var environment = context.Services.GetSingletonInstance<IWebAssemblyHostEnvironment>();
        var builder = context.Services.GetSingletonInstance<WebAssemblyHostBuilder>();

        ConfigureAuthentication(builder);
        ConfigureHttpClient(context, context.Services.GetConfiguration(), environment);
        ConfigureBlazorise(context);
        ConfigureMessageLocalization();
        context.Services.AddScoped<OrganizationCatalogClient>();
        context.Services.AddScoped<IdentityAdminClient>();
        context.Services.AddScoped<AuditLogClient>();
        context.Services.AddScoped<CollaborationClient>();
        context.Services.AddScoped<SurveyCatalogCache>();
        context.Services.AddScoped<Account.AccountProfileClient>();
        context.Services.AddScoped<Work.WorkManagementClient>();
        context.Services.AddScoped<Documents.DocumentClient>();
        context.Services.AddScoped<BusManagementClient>();
        ConfigureRouter(context);
        ConfigureMenu(context);
    }


    private void ConfigureRouter(ServiceConfigurationContext context)
    {
        Configure<AbpRouterOptions>(options =>
        {
            options.AppAssembly = typeof(HCSBlazorClientModule).Assembly;
            options.AdditionalAssemblies.Add(typeof(HCSBlazorClientModule).Assembly);
        });
    }

    private void ConfigureMenu(ServiceConfigurationContext context)
    {
        Configure<AbpNavigationOptions>(options =>
        {
            options.MenuContributors.Add(new HCSMenuContributor());
        });
    }

    private void ConfigureMessageLocalization()
    {
        Configure<AbpLocalizationOptions>(options =>
            options.Resources.Get<AbpUiResource>().AddVirtualJson("/Localization/HCS"));
    }

    private void ConfigureBlazorise(ServiceConfigurationContext context)
    {
        context.Services
            .AddBlazorise(options =>
            {
                options.Immediate = true;
            })
            .AddBootstrap5Providers()
            .AddFontAwesomeIcons();
    }

    private static void ConfigureAuthentication(WebAssemblyHostBuilder builder)
    {
        builder.Services.AddBlazorWebAppServices();
        builder.Services.Replace(ServiceDescriptor.Singleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>());
    }
    

    private static void ConfigureHttpClient(
        ServiceConfigurationContext context,
        IConfiguration configuration,
        IWebAssemblyHostEnvironment environment)
    {
        var configuredGatewayUrl = configuration["RemoteServices:Default:BaseUrl"] ?? environment.BaseAddress;
        var gatewayBaseAddress = Uri.TryCreate(configuredGatewayUrl, UriKind.Absolute, out var absoluteGatewayUrl)
            ? absoluteGatewayUrl
            : new Uri(new Uri(environment.BaseAddress), configuredGatewayUrl.EnsureEndsWith('/'));
        context.Services.AddSingleton(new ChatRealtimeConnection(gatewayBaseAddress));
        context.Services.AddTransient(_ => new BffHttpMessageHandler(gatewayBaseAddress));
        context.Services.AddHttpClient("HCS.Bff", client => client.BaseAddress = gatewayBaseAddress)
            .AddHttpMessageHandler<BffHttpMessageHandler>();
        context.Services.AddScoped<BffAuthenticationStateProvider>();
        context.Services.AddScoped<AuthenticationStateProvider>(provider =>
            provider.GetRequiredService<BffAuthenticationStateProvider>());
        context.Services.AddTransient(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("HCS.Bff"));
    }
}
