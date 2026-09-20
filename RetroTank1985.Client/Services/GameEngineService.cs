using Microsoft.JSInterop;
using RetroTank1985.Client.Engine.Core;
using RetroTank1985.Client.Engine.Models;

namespace RetroTank1985.Client.Services;

public class GameEngineService : IAsyncDisposable
{
    private readonly IBattleCityEngine _engine;
    private readonly StageService _stageService;
    private readonly IJSRuntime _js;
    private DotNetObjectReference<GameEngineService>? _dotNetRef;
    private bool _isInitialized = false;

    public IBattleCityEngine Engine => _engine;
    public event Action<TelemetryData>? OnTelemetryUpdated;
    public event Action<int>? OnStageChanged;

    public GameEngineService(
        IBattleCityEngine engine,
        StageService stageService,
        IJSRuntime js)
    {
        _engine = engine;
        _stageService = stageService;
        _js = js;
    }

    public async Task<bool> InitializeAsync(string canvasId, int stageNumber = 1)
    {
        _dotNetRef ??= DotNetObjectReference.Create(this);

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

    public async Task SetStageAsync(int stageNumber)
    {
        var stage = await _stageService.GetStageAsync(stageNumber);
        if (stage == null) return;

        _engine.InitializeStage(stage, stageNumber);
        await _js.InvokeVoidAsync("GameBridge.setStage", stage.Grid, stageNumber);
        OnStageChanged?.Invoke(stageNumber);
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

    /// <summary>
    /// Invoked every frame from JS requestAnimationFrame loop.
    /// Executes C# Game Brain physics, collisions, and state update.
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
            Pause = pause
        });

        var frame = _engine.Tick(timestamp);
        frame.Fps = fps;

        // Throttle Blazor UI telemetry updates to ~2 times per second (500ms)
        // to avoid saturating Blazor Virtual DOM render cycles at 60 FPS
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
