using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using RetroTank1985.Client.Engine.Models;
using RetroTank1985.Client.Models;
using RetroTank1985.Client.Services;

namespace RetroTank1985.Client.Pages;

public partial class Play : ComponentBase, IAsyncDisposable
{
    [Inject] private StageService StageService { get; set; } = default!;
    [Inject] private GameEngineService EngineService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private int _selectedStage = 1;
    private StageModel? _currentStage;
    private bool _isMuted = false;
    private bool _engineStarted = false;
    private TelemetryData _telemetry = new();

    protected override async Task OnInitializedAsync()
    {
        _currentStage = await StageService.GetStageAsync(_selectedStage);
        EngineService.OnTelemetryUpdated += HandleTelemetry;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_engineStarted)
        {
            if (_currentStage == null)
            {
                _currentStage = await StageService.GetStageAsync(_selectedStage);
            }

            if (_currentStage != null)
            {
                await Task.Delay(50); // Ensure Canvas DOM is rendered
                var success = await EngineService.InitializeAsync("gameArenaCanvas", _selectedStage);
                if (success)
                {
                    _engineStarted = true;
                    await EngineService.StartAsync();
                }
            }
        }
    }

    private void HandleTelemetry(TelemetryData telemetry)
    {
        _telemetry = telemetry;
        StateHasChanged();
    }

    private async Task OnStageSelected(int stageNum)
    {
        if (stageNum < 1 || stageNum > 35) return;
        _selectedStage = stageNum;
        _currentStage = await StageService.GetStageAsync(stageNum);
        if (_currentStage != null)
        {
            await EngineService.SetStageAsync(stageNum);
        }
    }

    private async Task ResetPlayer() => await EngineService.ResetPlayerAsync();

    private async Task RestartStage() => await EngineService.RestartCurrentStage();

    private async Task ToggleMute() => _isMuted = await JS.InvokeAsync<bool>("nesSynth.toggleMute");

    private async Task TogglePause() => await EngineService.TogglePauseAsync();

    private async Task HandleVirtualTouch((string control, bool isPressed) args) =>
        await EngineService.SetVirtualInputAsync(args.control, args.isPressed);


    private void HandleDebugSpawn(Components.Play.GameDebugSandboxPanel.EnemyTypeSpawnArgs args)
    {
        EngineService.Engine.SpawnEnemyDebug(args.Type, -1, args.IsFlashing);
    }


    private void HandleDebugNuke()
    {
        EngineService.Engine.NukeAllEnemies();
    }

    private void HandleDebugClear()
    {
        EngineService.Engine.ClearEnemies();
    }

    private void HandleDebugStarPower(int starLevel)
    {
        EngineService.Engine.SetPlayerStarPower(starLevel);
    }

    private void HandleDebugShieldToggle()
    {
        EngineService.Engine.TogglePlayerShield();
    }

    private void HandleDebugFortifyEagle(bool fortified)
    {
        EngineService.Engine.ToggleEagleSteel(fortified);
    }

    private void HandleDebugSpawnPowerUp(Engine.Enums.PowerUpType type)
    {
        EngineService.Engine.SpawnPowerUpDebug(type);
    }

    public async ValueTask DisposeAsync()
    {
        EngineService.OnTelemetryUpdated -= HandleTelemetry;
        await EngineService.DisposeAsync();
    }
}

