using System.Runtime.InteropServices.JavaScript;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using RetroTank1985.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

if (OperatingSystem.IsBrowser())
{
    try
    {
        var interopUrl = $"{builder.HostEnvironment.BaseAddress.TrimEnd('/')}/js/interop.js";
        await JSHost.ImportAsync("interop", interopUrl);
        if (NativeInterop.IsStandalone())
        {
            builder.RootComponents.Add<Routes>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");
        }
    }
    catch
    {
        // In Blazor Web App mode (Server Host), root components are handled by App.razor
    }
}

builder.Services.AddMudServices();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<RetroTank1985.Client.Services.StageService>();

await builder.Build().RunAsync();

public partial class NativeInterop
{
    [JSImport("isStandalone", "interop")]
    public static partial bool IsStandalone();
}


