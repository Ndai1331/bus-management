using HCS.DocumentService;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.AddAppSettingsSecretsJson().UseAutofac().UseSerilog();
    await builder.AddApplicationAsync<HcsDocumentServiceModule>();
    var app = builder.Build();
    await using (var scope = app.Services.CreateAsyncScope())
        await scope.ServiceProvider.GetRequiredService<DocumentServiceDbContext>().Database.MigrateAsync();
    await app.InitializeApplicationAsync();
    await app.RunAsync();
}
catch (Exception exception) when (exception is not HostAbortedException)
{
    Log.Fatal(exception, "HCS DocumentService terminated unexpectedly");
}
finally { await Log.CloseAndFlushAsync(); }
