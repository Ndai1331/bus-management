using HCS.OrganizationService.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;
using Volo.Abp.Testing;
using Volo.Abp.Uow;

namespace HCS.OrganizationService.Tests;

[DependsOn(
    typeof(HCSOrganizationServiceModule),
    typeof(AbpAutofacModule),
    typeof(AbpEntityFrameworkCoreSqliteModule))]
public sealed class OrganizationTestModule : AbpModule
{
    private SqliteConnection? _connection;

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        context.Services.AddAlwaysDisableUnitOfWorkTransaction();
        context.Services.AddAbpDbContext<OrganizationDbContext>();
        context.Services.Configure<AbpDbContextOptions>(options =>
            options.Configure<OrganizationDbContext>(db => db.DbContextOptions.UseSqlite(_connection)));
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        using var scope = context.ServiceProvider.CreateScope();
        scope.ServiceProvider.GetRequiredService<OrganizationDbContext>().Database.EnsureCreated();
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context) => _connection?.Dispose();
}

public abstract class OrganizationTestBase : AbpIntegratedTest<OrganizationTestModule>
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options) => options.UseAutofac();
}
