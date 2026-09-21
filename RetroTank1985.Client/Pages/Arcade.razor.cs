using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using RetroTank1985.Client.Engine.Models;
using RetroTank1985.Client.Models;
using RetroTank1985.Client.Services;

namespace RetroTank1985.Client.Pages;

public partial class Arcade : ComponentBase, IAsyncDisposable
{
    [Inject] private StageService StageService { get; set; } = default!;
    [Inject] private GameEngineService EngineService { get; set; } = default!;
    [Inject] private GameStorageService StorageService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private int _selectedStage = 1;
    private StageModel? _currentStage;
    private bool _isMuted = false;
    private int _highScore = 20000;
    private bool _engineStarted = false;
    private TelemetryData _telemetry = new();

    protected override async Task OnInitializedAsync()
    {
        _highScore = await StorageService.GetHighScoreAsync();
        var audioPrefs = await StorageService.GetAudioPreferencesAsync();
        _isMuted = audioPrefs.IsMuted;

        _currentStage = await StageService.GetStageAsync(_selectedStage);
        EngineService.OnTelemetryUpdated += HandleTelemetry;
        EngineService.OnStageChanged += HandleStageChanged;
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
                // Strict 1-Player Campaign Mode
                EngineService.SetTwoPlayerMode(false);

                var success = await EngineService.InitializeAsync("arcadeGameCanvas", _selectedStage);
                if (success)
                {
                    _engineStarted = true;
                    if (_isMuted)
                    {
                        await JS.InvokeVoidAsync("nesSynth.setMute", true);
                    }
                    await EngineService.StartAsync();
                }
            }
        }
    }

    private void HandleTelemetry(TelemetryData telemetry)
    {
        _telemetry = telemetry;
        if (telemetry.HighScore > _highScore)
        {
            _highScore = telemetry.HighScore;
        }
        StateHasChanged();
    }

    private void HandleStageChanged(int newStage)
    {
        _selectedStage = newStage;
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

    private async Task OnPrevClicked() => await OnStageSelected(_selectedStage - 1);
    private async Task OnNextClicked() => await OnStageSelected(_selectedStage + 1);

    private async Task RestartStage() => await EngineService.RestartCurrentStage();

    private async Task ToggleMute()
    {
        _isMuted = await JS.InvokeAsync<bool>("nesSynth.toggleMute");
        await StorageService.SaveAudioPreferencesAsync(new AudioPreferences { IsMuted = _isMuted });
    }

    private async Task ToggleFullscreen()
    {
        await JS.InvokeVoidAsync("GameBridge.toggleFullscreen", "arcadeBezelContainer");
    }

    public async ValueTask DisposeAsync()
    {
        EngineService.OnTelemetryUpdated -= HandleTelemetry;
        EngineService.OnStageChanged -= HandleStageChanged;
        await EngineService.DisposeAsync();
    }
}
