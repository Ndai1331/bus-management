using Volo.Abp.Application;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;
using HCS.OrganizationService.Integration;
using Microsoft.Extensions.DependencyInjection;

namespace HCS.OrganizationService;

[DependsOn(typeof(AbpDddApplicationModule), typeof(AbpEntityFrameworkCoreModule))]
public sealed class HCSOrganizationServiceModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddScoped<IInboxExecutor, EfInboxExecutor>();
        context.Services.AddScoped<IOrganizationOutboxEventPublisher, OrganizationOutboxEventPublisher>();
        context.Services.AddScoped<OrganizationOutboxDispatcher>();
    }
}
