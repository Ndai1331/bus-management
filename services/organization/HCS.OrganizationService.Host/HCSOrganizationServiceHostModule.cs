using HCS.OrganizationService.Data;
using HCS.OrganizationService.Host.Integration;
using HCS.OrganizationService.Integration;
using OpenIddict.Validation.AspNetCore;
using Volo.Abp.OpenIddict;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using System.Linq;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.Modularity;
using Volo.Abp.EventBus.RabbitMq;

namespace HCS.OrganizationService.Host;

[DependsOn(
    typeof(HCSOrganizationServiceModule),
    typeof(AbpAspNetCoreMvcModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpAutofacModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule),
    typeof(AbpEventBusRabbitMqModule),
    typeof(AbpOpenIddictAspNetCoreModule))]
public sealed class HCSOrganizationServiceHostModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        context.Services.AddAbpDbContext<OrganizationDbContext>();
        var authority = configuration["AuthServer:Authority"]
            ?? throw new InvalidOperationException("AuthServer:Authority is required.");
        PreConfigure<OpenIddictBuilder>(builder => builder.AddValidation(options =>
        {
            options.SetIssuer(new Uri(authority));
            options.AddAudiences(configuration["AuthServer:Audience"] ?? "HCS");
            options.UseSystemNetHttp(http =>
            {
                if (configuration.GetValue("AuthServer:AllowUntrustedBackchannelCertificate", false))
                    http.ConfigureHttpClientHandler(handler => handler.ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator);
            });
            options.UseAspNetCore();
        }));
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<OrganizationDbContext>();
        Configure<AbpDbContextOptions>(options =>
            options.Configure<OrganizationDbContext>(db => db.UseNpgsql()));
        context.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultForbidScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        });

        context.Services.AddAuthorization(options =>
        {
            foreach (var permission in new[]
                     {
                         Contracts.OrganizationPermissions.Departments,
                         Contracts.OrganizationPermissions.Units,
                         Contracts.OrganizationPermissions.Positions,
                         Contracts.OrganizationPermissions.UserMappings
                     })
            {
                options.AddPolicy(permission, policy => policy.RequireClaim("permission", permission));
            }

            var masterDataAccess = Contracts.OrganizationPermissions.MasterDataAccess;
            options.AddPolicy(Contracts.OrganizationPermissions.MasterData, policy =>
                policy.RequireAssertion(context =>
                    masterDataAccess.Any(permission => context.User.HasClaim("permission", permission))));
            foreach (var permission in masterDataAccess.Where(item => item != Contracts.OrganizationPermissions.MasterData))
            {
                options.AddPolicy(permission, policy => policy.RequireClaim("permission", permission));
            }
        });

        context.Services.AddHealthChecks().AddDbContextCheck<OrganizationDbContext>();
        context.Services.AddHostedService<OrganizationOutboxWorker>();
        context.Services.AddAbpSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "HCS Organization API", Version = "v1" });
            options.DocInclusionPredicate((_, _) => true);
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();
        if (env.IsDevelopment()) app.UseDeveloperExceptionPage();

        app.UseRouting();
        app.UseCorrelationId();
        app.UseAuthentication();
        app.UseUnitOfWork();
        app.UseAuthorization();
        // The audit middleware owns the explicit EF transaction shared by direct DbContext services.
        app.UseMiddleware<HttpAuditOutboxMiddleware>();
        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseSwagger();
        app.UseAbpSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "HCS Organization API"));
        app.UseConfiguredEndpoints(endpoints => endpoints.MapHealthChecks("/health"));
    }
}
