using HCS.WorkManagementService;
using HCS.WorkManagementService.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.AddAppSettingsSecretsJson().UseAutofac().UseSerilog();
    await builder.AddApplicationAsync<HcsWorkManagementServiceModule>();
    var app = builder.Build();
    await using (var scope = app.Services.CreateAsyncScope())
        await scope.ServiceProvider.GetRequiredService<WorkManagementDbContext>().Database.MigrateAsync();
    await app.InitializeApplicationAsync();
    await app.RunAsync();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "HCS WorkManagementService terminated unexpectedly");
}
finally { await Log.CloseAndFlushAsync(); }
