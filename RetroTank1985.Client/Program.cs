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
builder.Services.AddScoped<RetroTank1985.Client.Services.GameStorageService>();

builder.Services.AddTransient<RetroTank1985.Client.Engine.Core.IDestructibleMap, RetroTank1985.Client.Engine.Core.DestructibleMap>();
builder.Services.AddTransient<RetroTank1985.Client.Engine.Core.ITankPhysics, RetroTank1985.Client.Engine.Core.TankPhysics>();
builder.Services.AddTransient<RetroTank1985.Client.Engine.Core.IBulletSystem, RetroTank1985.Client.Engine.Core.BulletSystem>();
builder.Services.AddTransient<RetroTank1985.Client.Engine.Core.IEnemySystem, RetroTank1985.Client.Engine.Core.EnemySystem>();
builder.Services.AddTransient<RetroTank1985.Client.Engine.Core.IPowerUpSystem, RetroTank1985.Client.Engine.Core.PowerUpSystem>();
builder.Services.AddTransient<RetroTank1985.Client.Engine.Core.IAudioEventQueue, RetroTank1985.Client.Engine.Core.AudioEventQueue>();
builder.Services.AddTransient<RetroTank1985.Client.Engine.Core.IBattleCityEngine, RetroTank1985.Client.Engine.Core.BattleCityEngine>();
builder.Services.AddScoped<RetroTank1985.Client.Services.GameEngineService>();


await builder.Build().RunAsync();

public partial class NativeInterop
{
    [JSImport("isStandalone", "interop")]
    public static partial bool IsStandalone();
}


