using HCS.BusManagementService.Application;
using HCS.BusManagementService.Contracts;
using HCS.BusManagementService.Data;
using HCS.BusManagementService.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using OpenIddict.Validation.AspNetCore;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.AntiForgery;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;
using Volo.Abp.Swashbuckle;

namespace HCS.BusManagementService;

[DependsOn(typeof(AbpAutofacModule), typeof(AbpAspNetCoreMvcModule), typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule), typeof(AbpEventBusRabbitMqModule),
    typeof(AbpSwashbuckleModule), typeof(AbpOpenIddictAspNetCoreModule))]
public sealed class HcsBusManagementServiceModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var authority = configuration["AuthServer:Authority"]
            ?? throw new InvalidOperationException("AuthServer:Authority is required.");

        PreConfigure<OpenIddictBuilder>(builder => builder.AddValidation(options =>
        {
            options.SetIssuer(new Uri(authority));
            options.AddAudiences(configuration["AuthServer:Audience"] ?? "HCS.BusManagementService");
            options.UseSystemNetHttp(http =>
            {
                if (configuration.GetValue("AuthServer:AllowUntrustedBackchannelCertificate", false))
                {
                    http.ConfigureHttpClientHandler(handler => handler.ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator);
                }
            });
            options.UseAspNetCore();
        }));
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        context.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultForbidScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        });
        context.Services.AddAuthorization(options =>
        {
            foreach (var permission in BusPermissions.All)
            {
                options.AddPolicy(permission, policy => policy.RequireClaim("permission", permission));
            }
        });
        Configure<AbpAntiForgeryOptions>(BearerApiAntiforgery.DisableCookieValidation);
        context.Services.AddDbContext<BusManagementDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString(BusManagementDbContext.ConnectionStringName)));
        context.Services.AddScoped<BusAccessScope>();
        context.Services.AddScoped<BusManagementAppService>();
        context.Services.AddScoped<OutboxDispatcher>();
        context.Services.AddHostedService<OutboxWorker>();
        context.Services.AddHealthChecks().AddDbContextCheck<BusManagementDbContext>("hcs_bus_management");
        context.Services.AddAbpSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "HCS Bus Management API", Version = "v1" });
            options.CustomSchemaIds(type => type.FullName);
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        if (context.GetEnvironment().IsDevelopment()) app.UseDeveloperExceptionPage();
        app.UseCorrelationId();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseSwagger();
        app.UseAbpSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "HCS Bus Management API"));
        app.UseAuditing();
        app.UseUnitOfWork();
        app.UseMiddleware<HttpAuditOutboxMiddleware>();
        app.UseConfiguredEndpoints(endpoints => endpoints.MapHealthChecks("/health"));
    }
}
