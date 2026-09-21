using Microsoft.AspNetCore.Components;
using RetroTank1985.Client.Pages;
using RetroTank1985.Components;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddMudServices();
builder.Services.AddScoped(sp =>
{
    var nav = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(nav.BaseUri) };
});
builder.Services.AddScoped<RetroTank1985.Client.Services.StageService>();
builder.Services.AddScoped<RetroTank1985.Client.Services.GameStorageService>();

// Battle City C# Game Engine Services (Hybrid Architecture)
builder.Services.AddTransient<RetroTank1985.Client.Engine.Core.IDestructibleMap, RetroTank1985.Client.Engine.Core.DestructibleMap>();
builder.Services.AddTransient<RetroTank1985.Client.Engine.Core.ITankPhysics, RetroTank1985.Client.Engine.Core.TankPhysics>();
builder.Services.AddTransient<RetroTank1985.Client.Engine.Core.IBulletSystem, RetroTank1985.Client.Engine.Core.BulletSystem>();
builder.Services.AddTransient<RetroTank1985.Client.Engine.Core.IEnemySystem, RetroTank1985.Client.Engine.Core.EnemySystem>();
builder.Services.AddTransient<RetroTank1985.Client.Engine.Core.IPowerUpSystem, RetroTank1985.Client.Engine.Core.PowerUpSystem>();
builder.Services.AddTransient<RetroTank1985.Client.Engine.Core.IAudioEventQueue, RetroTank1985.Client.Engine.Core.AudioEventQueue>();
builder.Services.AddTransient<RetroTank1985.Client.Engine.Core.IBattleCityEngine, RetroTank1985.Client.Engine.Core.BattleCityEngine>();
builder.Services.AddScoped<RetroTank1985.Client.Services.GameEngineService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}


app.UseAntiforgery();

app.UseStaticFiles();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(RetroTank1985.Client._Imports).Assembly);

app.Run();
