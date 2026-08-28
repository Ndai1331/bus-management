using HCS.DocumentService.Conversion;
using HCS.DocumentService.Documents;
using HCS.DocumentService.Integration;
using HCS.DocumentService.Signing;
using HCS.DocumentService.Storage;
using HCS.DocumentService.Workflows;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using OpenIddict.Validation.AspNetCore;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.AntiForgery;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.Minio;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.EventBus.RabbitMq;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;
using Volo.Abp.Swashbuckle;

namespace HCS.DocumentService;

[DependsOn(typeof(AbpAutofacModule), typeof(AbpAspNetCoreMvcModule), typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule), typeof(AbpBlobStoringMinioModule),
    typeof(AbpEventBusRabbitMqModule), typeof(AbpSwashbuckleModule),
    typeof(AbpOpenIddictAspNetCoreModule))]
public sealed class HcsDocumentServiceModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PdfSharpFontResolverRegistration.EnsureRegistered();

        var configuration = context.Services.GetConfiguration();
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
        var configuration = context.Services.GetConfiguration();
        var environment = context.Services.GetHostingEnvironment();
        context.Services.AddHttpContextAccessor();
        ConfigureDataProtection(context.Services, configuration, environment);
        context.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultForbidScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        });
        context.Services.AddAuthorization(options =>
        {
            foreach (var permission in GetDocumentPermissions())
            {
                options.AddPolicy(permission, policy => policy.RequireAssertion(auth =>
                    DocumentAccess.HasPermission(auth.User, permission)));
            }
        });
        Configure<AbpAntiForgeryOptions>(BearerApiAntiforgery.DisableCookieValidation);
        context.Services.AddDbContext<DocumentServiceDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DocumentService")));
        Configure<AbpBlobStoringOptions>(options =>
        {
            ConfigureContainer<DocumentBlobContainer>(options, configuration);
            ConfigureContainer<SigningBlobContainer>(options, configuration);
        });
        context.Services.AddSingleton<IDocxToPdfConverter, LibreOfficeDocxToPdfConverter>();
        context.Services.AddScoped<IDocumentAppService, DocumentAppService>();
        context.Services.AddScoped<IWorkflowAppService, WorkflowAppService>();
        context.Services.AddScoped<IWorkflowAssigneeResolver, HttpWorkflowAssigneeResolver>();
        context.Services.AddScoped<IWorkflowUserProfileResolver, HttpWorkflowUserProfileResolver>();
        context.Services.AddScoped<WorkflowSubmissionPreparationService>();
        context.Services.AddHttpClient("HCS.Platform", client =>
        {
            var baseUrl = configuration["Services:Platform:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        });
        context.Services.AddHttpClient("HCS.Organization", client =>
        {
            var baseUrl = configuration["Services:Organization:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
                client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        });
        context.Services.AddScoped<ISigningAppService, SigningAppService>();
        context.Services.AddScoped<DocumentFileService>();
        context.Services.AddScoped<DocumentPdfWatermarkService>();
        context.Services.AddScoped<ISigningSecretProtector, DataProtectionSigningSecretProtector>();
        context.Services.AddScoped<IDigitalSigningAdapter, LicensedElectronicSigningAdapter>();
        context.Services.AddScoped<IDigitalSigningAdapter, LicensedRemoteCaSigningAdapter>();
        context.Services.AddScoped<IDigitalSigningAdapter>(sp => new LicensedBnnSigningAdapter(
            SigningKind.Hsm, sp.GetRequiredService<ILoggerFactory>()));
        context.Services.AddScoped<IDigitalSigningAdapter>(sp => new LicensedBnnSigningAdapter(
            SigningKind.UsbToken, sp.GetRequiredService<ILoggerFactory>()));
        context.Services.AddScoped<ISigningProviderFactory, SigningProviderFactory>();
        context.Services.AddScoped<IInboxExecutor, EfInboxExecutor>();
        context.Services.AddScoped<ITypedDistributedEventPublisher, AbpTypedDistributedEventPublisher>();
        context.Services.AddScoped<IOutboxEventPublisher, AbpOutboxEventPublisher>();
        context.Services.AddScoped<OutboxDispatcher>();
        context.Services.AddHostedService<OutboxWorker>();
        context.Services.AddHealthChecks().AddDbContextCheck<DocumentServiceDbContext>("hcs_document");
        context.Services.AddAbpSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "HCS Document Service API", Version = "v1" });
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
        // The audit middleware owns the explicit EF transaction shared by direct DbContext services.
        app.UseMiddleware<HttpAuditOutboxMiddleware>();
        app.UseSwagger();
        app.UseAbpSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "HCS Document Service API"));
        app.UseAuditing();
        app.UseConfiguredEndpoints(endpoints => endpoints.MapHealthChecks("/health"));
    }

    private static void ConfigureContainer<T>(AbpBlobStoringOptions options, IConfiguration configuration)
    {
        options.Containers.Configure<T>(container => container.UseMinio(minio =>
        {
            minio.EndPoint = configuration["Minio:EndPoint"] ?? "localhost:9000";
            minio.AccessKey = configuration["Minio:AccessKey"] ?? string.Empty;
            minio.SecretKey = configuration["Minio:SecretKey"] ?? string.Empty;
            minio.WithSSL = configuration.GetValue("Minio:WithSSL", false);
            minio.CreateBucketIfNotExists = configuration.GetValue("Minio:CreateBucketIfNotExists", true);
        }));
    }

    private static string[] GetDocumentPermissions() =>
    [
        DocumentPermissions.View, DocumentPermissions.Create, DocumentPermissions.Update,
        DocumentPermissions.Assign, DocumentPermissions.ManageFiles, DocumentPermissions.WorkflowView,
        DocumentPermissions.WorkflowManage, DocumentPermissions.WorkflowStart,
        DocumentPermissions.WorkflowDecide, DocumentPermissions.SigningConfigure,
        DocumentPermissions.SigningExecute, DocumentPermissions.SigningReport
    ];

    private static void ConfigureDataProtection(IServiceCollection services, IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var builder = services.AddDataProtection().SetApplicationName("HCS.DocumentService");
        var keysPath = configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            builder.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }
        else if (!environment.IsDevelopment())
        {
            throw new AbpException("Production requires DataProtection:KeysPath from runtime configuration.");
        }

        var certificatePath = configuration["DataProtection:CertificatePath"];
        var certificatePassword = configuration["DataProtection:CertificatePassword"];
        if (!string.IsNullOrWhiteSpace(certificatePath) && !string.IsNullOrWhiteSpace(certificatePassword))
        {
            builder.ProtectKeysWithCertificate(X509CertificateLoader.LoadPkcs12FromFile(
                certificatePath, certificatePassword));
        }
        else if (!environment.IsDevelopment())
        {
            throw new AbpException("Production requires a Data Protection certificate from runtime configuration.");
        }
    }
}
