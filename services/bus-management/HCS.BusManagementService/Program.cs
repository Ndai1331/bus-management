using HCS.BusManagementService;
using HCS.BusManagementService.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.AddAppSettingsSecretsJson().UseAutofac().UseSerilog();
    await builder.AddApplicationAsync<HcsBusManagementServiceModule>();
    var app = builder.Build();
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<BusManagementDbContext>();
        await db.Database.MigrateAsync();
    }
    await app.InitializeApplicationAsync();
    await app.RunAsync();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "HCS Bus Management Service terminated unexpectedly");
}
finally { await Log.CloseAndFlushAsync(); }
