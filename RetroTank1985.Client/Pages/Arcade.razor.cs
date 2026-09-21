using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using RetroTank1985.Client.Components.Arcade;
using RetroTank1985.Client.Components.Play;
using RetroTank1985.Client.Engine.Models;
using RetroTank1985.Shared.Models;
using RetroTank1985.Client.Services;

namespace RetroTank1985.Client.Pages;

public partial class Arcade : ComponentBase, IAsyncDisposable
{
    [Inject] private StageService StageService { get; set; } = default!;
    [Inject] private GameEngineService EngineService { get; set; } = default!;
    [Inject] private GameStorageService StorageService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private int _selectedStage = 1;
    private StageModel? _currentStage;
    private bool _isMuted = false;
    private int _highScore = 20000;
    private bool _engineStarted = false;
    private TelemetryData _telemetry = new();
    private GameSettings _settings = new();

    protected override async Task OnInitializedAsync()
    {
        _highScore = await StorageService.GetHighScoreAsync();
        var audioPrefs = await StorageService.GetAudioPreferencesAsync();
        _isMuted = audioPrefs.IsMuted;
        _settings = await StorageService.GetGameSettingsAsync();

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

    private async Task OpenOptionsDialog()
    {
        var parameters = new DialogParameters<GameOptionsDialog>
        {
            { x => x.InitialSettings, _settings }
        };

        var options = new DialogOptions
        {
            CloseButton = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await DialogService.ShowAsync<GameOptionsDialog>("GAME OPTIONS", parameters, options);
        var result = await dialog.Result;

        if (result != null && !result.Canceled && result.Data is GameSettings newSettings)
        {
            _settings = newSettings;
            await EngineService.ApplySettingsAsync(newSettings);
            await EngineService.RestartCurrentStage();
            StateHasChanged();
        }
    }

    private string GetDifficultyLabel(GameDifficultyPreset preset) => preset switch
    {
        GameDifficultyPreset.KidsFriendly => "🐣 KIDS EASY",
        GameDifficultyPreset.Classic1985 => "🕹️ CLASSIC 1985",
        GameDifficultyPreset.Veteran => "⚔️ VETERAN",
        GameDifficultyPreset.Custom => "⚙️ CUSTOM",
        _ => "NORMAL"
    };

    private Color GetDifficultyColor(GameDifficultyPreset preset) => preset switch
    {
        GameDifficultyPreset.KidsFriendly => Color.Success,
        GameDifficultyPreset.Classic1985 => Color.Warning,
        GameDifficultyPreset.Veteran => Color.Error,
        GameDifficultyPreset.Custom => Color.Info,
        _ => Color.Default
    };

    private string GetHpBorderColor(int hp) => hp switch
    {
        <= 1 => "#ef4444",
        2 => "#f59e0b",
        3 => "#10b981",
        _ => "#06b6d4"
    };

    public async ValueTask DisposeAsync()
    {
        EngineService.OnTelemetryUpdated -= HandleTelemetry;
        EngineService.OnStageChanged -= HandleStageChanged;
        await EngineService.DisposeAsync();
    }
}
