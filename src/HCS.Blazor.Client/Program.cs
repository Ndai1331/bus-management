using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace HCS.Blazor.Client;

public class Program
{
    public async static Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        var application = await builder.AddApplicationAsync<HCSBlazorClientModule>(options =>
        {
            options.UseAutofac();
        });

        var host = builder.Build();
        await ApplySavedCultureAsync(host.Services);
        await application.InitializeApplicationAsync(host.Services);

        await host.RunAsync();
    }

    private static async Task ApplySavedCultureAsync(System.IServiceProvider services)
    {
        try
        {
            var js = services.GetRequiredService<IJSRuntime>();
            var cultureName = await js.InvokeAsync<string>("hcsGetCulture");
            if (string.IsNullOrWhiteSpace(cultureName))
            {
                return;
            }

            var culture = new CultureInfo(cultureName);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
        catch (Exception)
        {
            // Keep the runtime default when the host script is not available yet.
        }
    }
}
