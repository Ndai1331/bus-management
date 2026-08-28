using Serilog;
using Volo.Abp;

namespace HCS.WebGateway;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Async(sink => sink.Console())
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting HCS Web Gateway");

            var builder = WebApplication.CreateBuilder(args);
            builder.WebHost.ConfigureKestrel(options =>
            {
                // Default 32 KB rejects chunked BFF cookies (431) before they can be
                // replaced with the Redis session-id cookie. 128 KB is a safety net.
                options.Limits.MaxRequestHeadersTotalSize = 128 * 1024;
                options.Limits.MaxRequestHeaderCount = 200;
            });
            builder.Host
                .UseAutofac()
                .UseSerilog((context, services, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services));

            await builder.AddApplicationAsync<HCSWebGatewayModule>();

            var app = builder.Build();
            await app.InitializeApplicationAsync();
            await app.RunAsync();

            return 0;
        }
        catch (HostAbortedException)
        {
            return 2;
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "HCS Web Gateway terminated unexpectedly");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
