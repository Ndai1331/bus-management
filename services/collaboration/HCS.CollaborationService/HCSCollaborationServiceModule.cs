using HCS.CollaborationService.Application;
using HCS.CollaborationService.Contracts;
using HCS.CollaborationService.Data;
using HCS.CollaborationService.Hubs;
using HCS.CollaborationService.Integration;
using HCS.CollaborationService.Push;
using HCS.CollaborationService.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
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
using Volo.Abp.Swashbuckle;

namespace HCS.CollaborationService;

[DependsOn(typeof(AbpAutofacModule), typeof(AbpAspNetCoreMvcModule), typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule), typeof(AbpBlobStoringMinioModule),
    typeof(AbpEventBusRabbitMqModule), typeof(AbpSwashbuckleModule))]
public sealed class HCSCollaborationServiceModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        CollaborationJwtBearer.AlignAbpClaimTypes();
        context.Services.AddAbpDbContext<CollaborationDbContext>();
        Configure<AbpDbContextOptions>(options => options.Configure<CollaborationDbContext>(db => db.UseNpgsql()));

        context.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => CollaborationJwtBearer.Configure(options, configuration));
        Configure<AbpAntiForgeryOptions>(BearerApiAntiforgery.DisableCookieValidation);
        context.Services.AddAuthorization(options =>
        {
            options.AddPolicy(CollaborationPermissions.Chat, p => p.RequireClaim("permission", CollaborationPermissions.Chat));
            options.AddPolicy(CollaborationPermissions.Notifications, p => p.RequireClaim("permission", CollaborationPermissions.Notifications));
            options.AddPolicy(CollaborationPermissions.Administration, p => p.RequireClaim("permission", CollaborationPermissions.Administration));
        });
        context.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins((configuration["App:CorsOrigins"] ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        var signalR = context.Services.AddSignalR();
        var redisConnection = configuration["Redis:Configuration"];
        if (!string.IsNullOrWhiteSpace(redisConnection))
            signalR.AddStackExchangeRedis(redisConnection, options =>
                options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal(
                    configuration["Redis:SignalRChannel"] ?? "hcs-collaboration"));
        context.Services.AddHttpClient<IPushSender, FirebasePushSender>();
        context.Services.AddTransient<IChatRealtimeNotifier, SignalRChatRealtimeNotifier>();
        context.Services.AddHostedService<CollaborationOutboxDispatcher>();
        context.Services.AddHostedService<PushDeliveryWorker>();
        Configure<AbpBlobStoringOptions>(options => options.Containers.Configure<CollaborationAttachmentContainer>(container =>
            container.UseMinio(minio =>
            {
                minio.EndPoint = configuration["Minio:EndPoint"] ?? "localhost:9000";
                minio.AccessKey = configuration["Minio:AccessKey"] ?? string.Empty;
                minio.SecretKey = configuration["Minio:SecretKey"] ?? string.Empty;
                minio.WithSSL = configuration.GetValue("Minio:WithSSL", false);
                minio.CreateBucketIfNotExists = configuration.GetValue("Minio:CreateBucketIfNotExists", true);
                minio.PresignedGetExpirySeconds = configuration.GetValue("AttachmentPolicy:PresignedLifetimeSeconds", 300);
            })));
        context.Services.AddHealthChecks().AddDbContextCheck<CollaborationDbContext>("hcs_collaboration");
        context.Services.AddAbpSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "HCS Collaboration API", Version = "v1" });
            options.CustomSchemaIds(type => type.FullName);
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        if (context.GetEnvironment().IsDevelopment()) app.UseDeveloperExceptionPage();
        app.UseCorrelationId(); app.UseRouting(); app.UseCors(); app.UseAuthentication(); app.UseAuthorization();
        app.UseSwagger();
        app.UseAbpSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "HCS Collaboration API"));
        app.UseAuditing(); app.UseUnitOfWork(); app.UseMiddleware<HttpAuditOutboxMiddleware>(); app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints(endpoints =>
        {
            endpoints.MapHub<ChatHub>("/hubs/chat");
            endpoints.MapHealthChecks("/health");
        });
    }
}
