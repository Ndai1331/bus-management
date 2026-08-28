using Serilog;
using Volo.Abp;
using HCS.OrganizationService.Data;
using Microsoft.EntityFrameworkCore;

namespace HCS.OrganizationService.Host;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
        try
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Host.UseAutofac().UseSerilog((context, services, logger) => logger
                .ReadFrom.Configuration(context.Configuration).ReadFrom.Services(services).Enrich.FromLogContext()
                .WriteTo.Console());
            await builder.AddApplicationAsync<HCSOrganizationServiceHostModule>();
            var app = builder.Build();
            await using (var scope = app.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<OrganizationDbContext>().Database.MigrateAsync();
            await app.InitializeApplicationAsync();
            await app.RunAsync();
            return 0;
        }
        catch (HostAbortedException) { return 2; }
        catch (Exception exception)
        {
            Log.Fatal(exception, "HCS Organization Service terminated unexpectedly");
            return 1;
        }
        finally { await Log.CloseAndFlushAsync(); }
    }
}
