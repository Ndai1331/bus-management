using Serilog;
using Volo.Abp;
using HCS.CollaborationService.Data;
using Microsoft.EntityFrameworkCore;

namespace HCS.CollaborationService;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
        try
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Host.UseAutofac().UseSerilog((context, services, logger) => logger
                .ReadFrom.Configuration(context.Configuration).ReadFrom.Services(services).Enrich.FromLogContext());
            await builder.AddApplicationAsync<HCSCollaborationServiceModule>();
            var app = builder.Build();
            await using (var scope = app.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<CollaborationDbContext>().Database.MigrateAsync();
            await app.InitializeApplicationAsync();
            await app.RunAsync();
            return 0;
        }
        catch (HostAbortedException) { return 2; }
        catch (Exception exception)
        {
            Log.Fatal(exception, "HCS Collaboration Service terminated unexpectedly");
            return 1;
        }
        finally { await Log.CloseAndFlushAsync(); }
    }
}
