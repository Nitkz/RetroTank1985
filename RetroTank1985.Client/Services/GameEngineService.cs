using Microsoft.JSInterop;
using RetroTank1985.Client.Engine.Core;
using RetroTank1985.Client.Engine.Models;

namespace RetroTank1985.Client.Services;

public class GameEngineService : IAsyncDisposable
{
    private readonly IBattleCityEngine _engine;
    private readonly StageService _stageService;
    private readonly GameStorageService _storageService;
    private readonly IJSRuntime _js;
    private DotNetObjectReference<GameEngineService>? _dotNetRef;
    private bool _isInitialized = false;

    public IBattleCityEngine Engine => _engine;
    public event Action<TelemetryData>? OnTelemetryUpdated;
    public event Action<int>? OnStageChanged;

    public GameEngineService(
        IBattleCityEngine engine,
        StageService stageService,
        GameStorageService storageService,
        IJSRuntime js)
    {
        _engine = engine;
        _stageService = stageService;
        _storageService = storageService;
        _js = js;
    }

    public async Task<bool> InitializeAsync(string canvasId, int stageNumber = 1)
    {
        _dotNetRef ??= DotNetObjectReference.Create(this);

        // Load persisted high score and game settings
        int savedHighScore = await _storageService.GetHighScoreAsync();
        _engine.HighScore = Math.Max(savedHighScore, _engine.HighScore);

        var savedSettings = await _storageService.GetGameSettingsAsync();
        _engine.ApplySettings(savedSettings);

        var stage = await _stageService.GetStageAsync(stageNumber);
        if (stage == null) return false;

        _engine.InitializeStage(stage, stageNumber);

        var success = await _js.InvokeAsync<bool>(
            "GameBridge.init",
            canvasId,
            _dotNetRef,
            stage.Grid,
            stageNumber);

        _isInitialized = success;
        if (success)
        {
            OnStageChanged?.Invoke(stageNumber);
        }
        return success;
    }

    public async Task ApplySettingsAsync(GameSettings settings)
    {
        _engine.ApplySettings(settings);
        await _storageService.SaveGameSettingsAsync(settings);
    }

    public async Task StartAsync()
    {
        if (!_isInitialized) return;
        await _js.InvokeVoidAsync("GameBridge.start");
    }

    public async Task StopAsync()
    {
        if (!_isInitialized) return;
        await _js.InvokeVoidAsync("GameBridge.stop");
    }

    public void SetTwoPlayerMode(bool enable)
    {
        _engine.SetTwoPlayerMode(enable);
    }

    public async Task SetStageAsync(int stageNumber, bool preservePlayerState = false)
    {
        var stage = await _stageService.GetStageAsync(stageNumber);
        if (stage == null) return;

        _engine.InitializeStage(stage, stageNumber, preservePlayerState);
        await _js.InvokeVoidAsync("GameBridge.setStage", stage.Grid, stageNumber);
        OnStageChanged?.Invoke(stageNumber);

        _ = _storageService.SaveMaxStageAsync(stageNumber);
    }

    public async Task ResetPlayerAsync()
    {
        _engine.ResetPlayer();
        await _js.InvokeVoidAsync("GameBridge.resetPlayer");
    }

    [JSInvokable]
    public async Task RestartCurrentStage()
    {
        await SetStageAsync(_engine.CurrentStage);
    }

    public async Task TogglePauseAsync()
    {
        _engine.TogglePause();
        await _js.InvokeVoidAsync("GameBridge.togglePause");
    }

    public async Task SetVirtualInputAsync(string control, bool isPressed)
    {
        await _js.InvokeVoidAsync("GameBridge.setVirtualInput", control, isPressed);
    }

    private double _lastTelemetryTime = 0;
    private int _lastSavedHighScore = 20000;

    /// <summary>
    /// Invoked every frame from JS requestAnimationFrame loop.
    /// Executes C# Game Brain physics, collisions, and state update with split P1 / P2 inputs.
    /// Returns the RenderFrameDto for JS Fast Canvas blitting.
    /// </summary>
    [JSInvokable]
    public RenderFrameDto OnEngineTick(
        double timestamp,
        bool up,
        bool down,
        bool left,
        bool right,
        bool fire,
        bool p2Up,
        bool p2Down,
        bool p2Left,
        bool p2Right,
        bool p2Fire,
        bool pause,
        int fps)
    {
        _engine.SetInput(new InputState
        {
            Up = up,
            Down = down,
            Left = left,
            Right = right,
            Fire = fire,
            P2Up = p2Up,
            P2Down = p2Down,
            P2Left = p2Left,
            P2Right = p2Right,
            P2Fire = p2Fire,
            Pause = pause
        });

        var frame = _engine.Tick(timestamp);
        frame.Fps = fps;

        // Auto-save High Score when broken
        if (_engine.HighScore > _lastSavedHighScore)
        {
            _lastSavedHighScore = _engine.HighScore;
            _ = _storageService.SaveHighScoreAsync(_lastSavedHighScore);
        }

        // Check if Score Tally finished and requested auto-advancement to next stage
        if (_engine.CheckNextStageReady(out int nextStageNum))
        {
            _ = Task.Run(async () =>
            {
                await SetStageAsync(nextStageNum, preservePlayerState: true);
            });
        }

        // Throttle Blazor UI telemetry updates to ~2 times per second (500ms)
        if (timestamp - _lastTelemetryTime >= 500 || _lastTelemetryTime == 0)
        {
            _lastTelemetryTime = timestamp;
            var telemetry = _engine.GetTelemetry(fps);
            OnTelemetryUpdated?.Invoke(telemetry);
        }

        return frame;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync();
        }
        catch
        {
            // Ignore during page disposal
        }
        finally
        {
            _dotNetRef?.Dispose();
            _dotNetRef = null;
        }
    }
}
