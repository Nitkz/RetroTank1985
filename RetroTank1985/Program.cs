using RetroTank1985.Client.Pages;
using RetroTank1985.Components;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddMudServices();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5085/") });
builder.Services.AddScoped<RetroTank1985.Client.Services.StageService>();

// Battle City C# Game Engine Services (Hybrid Architecture)
builder.Services.AddTransient<RetroTank1985.Client.Engine.Core.IDestructibleMap, RetroTank1985.Client.Engine.Core.DestructibleMap>();
builder.Services.AddTransient<RetroTank1985.Client.Engine.Core.ITankPhysics, RetroTank1985.Client.Engine.Core.TankPhysics>();
builder.Services.AddTransient<RetroTank1985.Client.Engine.Core.IBulletSystem, RetroTank1985.Client.Engine.Core.BulletSystem>();
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
