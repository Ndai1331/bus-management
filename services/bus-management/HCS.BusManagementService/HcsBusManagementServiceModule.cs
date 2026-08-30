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
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;
using Volo.Abp.Swashbuckle;
using Volo.Abp.Uow;

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
            foreach (var permission in BusPermissions.All.Where(permission =>
                !string.Equals(permission, BusPermissions.Dashboard, StringComparison.Ordinal)))
            {
                options.AddPolicy(permission, policy => policy.RequireClaim("permission", permission));
            }

            // Dashboard is a read-only entry point. Any scoped Bus Management read
            // permission is sufficient to see the dashboard; reports remain separately
            // protected by BusPermissions.Reports below.
            options.AddPolicy(BusPermissions.Dashboard, policy =>
                policy.RequireAuthenticatedUser().RequireAssertion(context => context.User.Claims.Any(claim =>
                    claim.Type == "permission" && claim.Value.StartsWith("HCS.BusManagement.", StringComparison.Ordinal))));
        });
        Configure<AbpAntiForgeryOptions>(BearerApiAntiforgery.DisableCookieValidation);
        context.Services.AddAbpDbContext<BusManagementDbContext>();
        Configure<AbpDbContextOptions>(options => options.Configure<BusManagementDbContext>(db =>
            db.DbContextOptions.UseNpgsql(configuration.GetConnectionString(BusManagementDbContext.ConnectionStringName))));
        // Business-day advisory locks must remain held through validation and SaveChanges.
        // Enable the ABP transaction for every service request so the lock is never released
        // between the guard query and the financial mutation/close.
        Configure<AbpUnitOfWorkDefaultOptions>(options =>
            options.TransactionBehavior = UnitOfWorkTransactionBehavior.Enabled);
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
