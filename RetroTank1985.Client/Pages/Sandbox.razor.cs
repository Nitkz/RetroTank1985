using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using RetroTank1985.Client.Engine.Models;
using RetroTank1985.Shared.Models;
using RetroTank1985.Client.Services;

namespace RetroTank1985.Client.Pages;

public partial class Sandbox : ComponentBase, IAsyncDisposable
{
    [Inject] private StageService StageService { get; set; } = default!;
    [Inject] private GameEngineService EngineService { get; set; } = default!;
    [Inject] private GameStorageService StorageService { get; set; } = default!;
    [Inject] private CoopLobbyClientService LobbyService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "coop")]
    public int? QueryCoop { get; set; }

    [SupplyParameterFromQuery(Name = "role")]
    public string? QueryRole { get; set; }

    [SupplyParameterFromQuery(Name = "stage")]
    public int? QueryStage { get; set; }

    [SupplyParameterFromQuery(Name = "room")]
    public string? QueryRoom { get; set; }

    private int _selectedStage = 1;
    private StageModel? _currentStage;
    private bool _isMuted = false;
    private bool _isTwoPlayer = false;
    private int _highScore = 20000;
    private bool _engineStarted = false;
    private TelemetryData _telemetry = new();
    private bool _isCoopSession = false;
    private bool _isCoopHost = false;
    private string _roomCode = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        _highScore = await StorageService.GetHighScoreAsync();
        var audioPrefs = await StorageService.GetAudioPreferencesAsync();
        _isMuted = audioPrefs.IsMuted;

        if (QueryCoop == 1)
        {
            _isCoopSession = true;
            _isTwoPlayer = true;
            _isCoopHost = string.Equals(QueryRole, "p1", StringComparison.OrdinalIgnoreCase);
            _roomCode = QueryRoom ?? LobbyService.CurrentRoom?.RoomCode ?? string.Empty;
            _selectedStage = QueryStage ?? 1;

            EngineService.IsCoopSession = true;
            EngineService.IsCoopHost = _isCoopHost;

            if (_isCoopHost)
            {
                // Host broadcasts snapshots
                EngineService.OnCoopSnapshotGenerated += HandleBroadcastSnapshot;
                // Host receives Guest input
                LobbyService.OnPlayerInputReceivedEvent += HandleGuestInputReceived;
            }
            else
            {
                // Guest sends local input to Host
                EngineService.OnGuestInputGenerated += HandleGuestInputGeneratedLocally;
                // Guest receives Host snapshots
                LobbyService.OnGameSnapshotReceivedEvent += HandleSnapshotReceivedFromHost;
            }
        }
        else if (QueryStage.HasValue && QueryStage.Value >= 1 && QueryStage.Value <= 35)
        {
            _selectedStage = QueryStage.Value;
        }

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
                    if (_isCoopSession || _isTwoPlayer)
                    {
                        EngineService.SetTwoPlayerMode(true);
                    }
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

    private void ToggleTwoPlayerMode()
    {
        _isTwoPlayer = !_isTwoPlayer;
        EngineService.SetTwoPlayerMode(_isTwoPlayer);
        StateHasChanged();
    }

    private async Task ResetPlayer() => await EngineService.ResetPlayerAsync();

    private async Task RestartStage() => await EngineService.RestartCurrentStage();

    private async Task ToggleMute()
    {
        _isMuted = await JS.InvokeAsync<bool>("nesSynth.toggleMute");
        await StorageService.SaveAudioPreferencesAsync(new AudioPreferences { IsMuted = _isMuted });
    }

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
        EngineService.Engine.SetPlayerStarPower(starLevel, 1);
        if (_isTwoPlayer)
        {
            EngineService.Engine.SetPlayerStarPower(starLevel, 2);
        }
    }

    private void HandleDebugShieldToggle()
    {
        EngineService.Engine.TogglePlayerShield(1);
        if (_isTwoPlayer)
        {
            EngineService.Engine.TogglePlayerShield(2);
        }
    }

    private void HandleDebugFortifyEagle(bool fortified)
    {
        EngineService.Engine.ToggleEagleSteel(fortified);
    }

    private void HandleDebugSpawnPowerUp(PowerUpType type)
    {
        EngineService.Engine.SpawnPowerUpDebug(type);
    }

    private void HandleDebugTriggerCurtain()
    {
        EngineService.Engine.TriggerStageCurtainDebug();
    }

    private void HandleDebugSimulateClearStage()
    {
        EngineService.Engine.SimulateClearStage();
    }

    private void HandleDebugTriggerGameOver()
    {
        EngineService.Engine.TriggerGameOverDebug();
    }

    private async Task HandleBroadcastSnapshot(CoopSyncSnapshotDto snapshot)
    {
        if (!_isCoopSession || !_isCoopHost || string.IsNullOrWhiteSpace(_roomCode)) return;
        await LobbyService.SendGameSnapshotAsync(_roomCode, snapshot);
    }

    private async Task HandleGuestInputGeneratedLocally(PlayerInputPacket input)
    {
        if (!_isCoopSession || _isCoopHost || string.IsNullOrWhiteSpace(_roomCode)) return;
        await LobbyService.SendPlayerInputAsync(_roomCode, input);
    }

    private void HandleGuestInputReceived(PlayerInputPacket input)
    {
        if (!_isCoopSession || !_isCoopHost) return;
        EngineService.Engine.ApplyRemoteP2Input(input);
    }

    private void HandleSnapshotReceivedFromHost(CoopSyncSnapshotDto snapshot)
    {
        if (!_isCoopSession || _isCoopHost) return;
        EngineService.Engine.ApplyNetworkSnapshot(snapshot);
    }

    public async ValueTask DisposeAsync()
    {
        if (_isCoopHost)
        {
            EngineService.OnCoopSnapshotGenerated -= HandleBroadcastSnapshot;
            LobbyService.OnPlayerInputReceivedEvent -= HandleGuestInputReceived;
        }
        else
        {
            EngineService.OnGuestInputGenerated -= HandleGuestInputGeneratedLocally;
            LobbyService.OnGameSnapshotReceivedEvent -= HandleSnapshotReceivedFromHost;
        }

        EngineService.OnTelemetryUpdated -= HandleTelemetry;
        await EngineService.DisposeAsync();
    }
}
