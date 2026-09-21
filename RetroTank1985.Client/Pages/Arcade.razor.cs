using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using RetroTank1985.Client.Components.Arcade;
using RetroTank1985.Client.Components.Play;
using RetroTank1985.Client.Engine.Models;
using RetroTank1985.Shared.Enums;
using RetroTank1985.Shared.Models;
using RetroTank1985.Shared.Models.Network;
using RetroTank1985.Client.Services;

namespace RetroTank1985.Client.Pages;

public partial class Arcade : ComponentBase, IAsyncDisposable
{
    [Inject] private StageService StageService { get; set; } = default!;
    [Inject] private GameEngineService EngineService { get; set; } = default!;
    [Inject] private GameStorageService StorageService { get; set; } = default!;
    [Inject] private CoopLobbyClientService LobbyService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
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
    private int _highScore = 20000;
    private bool _engineStarted = false;
    private TelemetryData _telemetry = new();
    private GameSettings _settings = new();

    // Co-op state
    private bool _isCoopSession = false;
    private bool _isCoopHost = false;
    private string _roomCode = string.Empty;

    // Milestone M5: In-Game Emote Wheel & Disconnection Grace Period
    private bool _isEmoteWheelOpen = false;
    private bool _isDisconnectGraceActive = false;
    private int _disconnectGraceSeconds = 15;
    private System.Threading.Timer? _gracePeriodTimer;
    private DotNetObjectReference<Arcade>? _arcadeDotNetRef;

    // Game Over Overlay state
    private bool _isGameOverOverlayVisible = false;

    protected override async Task OnInitializedAsync()
    {
        _highScore = await StorageService.GetHighScoreAsync();
        var audioPrefs = await StorageService.GetAudioPreferencesAsync();
        _isMuted = audioPrefs.IsMuted;
        _settings = await StorageService.GetGameSettingsAsync();

        if (QueryCoop == 1)
        {
            _isCoopSession = true;
            _isCoopHost = string.Equals(QueryRole, "p1", StringComparison.OrdinalIgnoreCase);
            _roomCode = QueryRoom ?? LobbyService.CurrentRoom?.RoomCode ?? string.Empty;
            _selectedStage = QueryStage ?? 1;

            EngineService.IsCoopSession = true;
            EngineService.IsCoopHost = _isCoopHost;

            // Subscribe to disconnection events for Grace Period handling
            LobbyService.OnPlayerLeftEvent += HandlePlayerLeftSession;
            LobbyService.OnConnectionStateChanged += HandleConnectionStateChanged;
            LobbyService.OnPlayerJoinedEvent += HandlePlayerReconnected;

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
        EngineService.OnStageChanged += HandleStageChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _arcadeDotNetRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("GameBridge.registerPlayComponent", _arcadeDotNetRef);
        }

        if (!_engineStarted)
        {
            if (_currentStage == null)
            {
                _currentStage = await StageService.GetStageAsync(_selectedStage);
            }

            if (_currentStage != null)
            {
                await Task.Delay(50); // Ensure Canvas DOM is rendered
                // Set 2-Player mode if Co-Op
                EngineService.SetTwoPlayerMode(_isCoopSession);

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

    [JSInvokable]
    public void ToggleEmoteWheel()
    {
        if (!_isCoopSession) return;
        _isEmoteWheelOpen = !_isEmoteWheelOpen;
        StateHasChanged();
    }

    private void HandleEmoteSelected(RetroEmoteType emote)
    {
        EngineService.TriggerEmote(emote);
    }

    private void HandleTelemetry(TelemetryData telemetry)
    {
        _telemetry = telemetry;
        if (telemetry.HighScore > _highScore)
        {
            _highScore = telemetry.HighScore;
        }

        // Detect Game Over state transition
        if (telemetry.IsGameOver && !_isGameOverOverlayVisible)
        {
            _isGameOverOverlayVisible = true;
        }
        else if (!telemetry.IsGameOver && _isGameOverOverlayVisible)
        {
            _isGameOverOverlayVisible = false;
        }

        StateHasChanged();
    }

    private string GetGameOverReason()
    {
        if (EngineService.Engine.Map.IsEagleDestroyed)
        {
            return "💥 EAGLE HEADQUARTERS DESTROYED";
        }
        return "💀 ALL TANKS DESTROYED (0 LIVES)";
    }

    private async Task HandleCoopRetryStage()
    {
        if (!_isCoopSession || !_isCoopHost) return;
        _isGameOverOverlayVisible = false;
        await EngineService.SetStageAsync(_selectedStage);
    }

    private void HandleReturnToLobby()
    {
        _isGameOverOverlayVisible = false;
        if (_isCoopSession && !string.IsNullOrWhiteSpace(_roomCode))
        {
            NavigationManager.NavigateTo($"/coop?room={_roomCode}");
        }
        else
        {
            NavigationManager.NavigateTo("/coop");
        }
    }

    private async Task HandleRestartFromStage1()
    {
        _isGameOverOverlayVisible = false;
        _selectedStage = 1;
        await EngineService.SetStageAsync(1);
    }

    private async Task HandleExitAsync()
    {
        if (_isCoopSession)
        {
            await HandleLeaveSession();
        }
        else
        {
            HandleReturnToMenu();
        }
    }

    private void HandleReturnToMenu()
    {
        NavigationManager.NavigateTo("/");
    }

    private void HandleStageChanged(int newStage)
    {
        _selectedStage = newStage;
        _isGameOverOverlayVisible = false;
        StateHasChanged();
    }

    private async Task OnStageSelected(int stageNum)
    {
        if (_isCoopSession) return;
        if (stageNum < 1 || stageNum > 35) return;
        _isGameOverOverlayVisible = false;
        _selectedStage = stageNum;
        _currentStage = await StageService.GetStageAsync(stageNum);
        if (_currentStage != null)
        {
            await EngineService.SetStageAsync(stageNum);
        }
    }

    private async Task OnPrevClicked() => await OnStageSelected(_selectedStage - 1);
    private async Task OnNextClicked() => await OnStageSelected(_selectedStage + 1);

    private async Task RestartStage()
    {
        if (_isCoopSession) return;
        _isGameOverOverlayVisible = false;
        await EngineService.RestartCurrentStage();
    }

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

    private void HandlePlayerLeftSession(string playerId)
    {
        if (!_isCoopSession || _isDisconnectGraceActive) return;
        StartDisconnectionGracePeriod();
    }

    private void HandleConnectionStateChanged(Microsoft.AspNetCore.SignalR.Client.HubConnectionState state)
    {
        if (!_isCoopSession) return;
        if (state == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Reconnecting ||
            state == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Disconnected)
        {
            if (!_isDisconnectGraceActive)
            {
                StartDisconnectionGracePeriod();
            }
        }
        else if (state == Microsoft.AspNetCore.SignalR.Client.HubConnectionState.Connected)
        {
            if (_isDisconnectGraceActive)
            {
                CancelDisconnectionGracePeriod();
            }
        }
    }

    private void HandlePlayerReconnected(CoopPlayerSlot slot)
    {
        if (_isDisconnectGraceActive)
        {
            CancelDisconnectionGracePeriod();
        }
    }

    private void StartDisconnectionGracePeriod()
    {
        _isDisconnectGraceActive = true;
        _disconnectGraceSeconds = 15;
        EngineService.Engine.SetDisconnectGracePeriod(true, 15f, _isCoopHost ? "P2" : "P1");
        InvokeAsync(StateHasChanged);

        _gracePeriodTimer?.Dispose();
        _gracePeriodTimer = new System.Threading.Timer(async _ =>
        {
            _disconnectGraceSeconds--;
            EngineService.Engine.SetDisconnectGracePeriod(true, Math.Max(0, _disconnectGraceSeconds), _isCoopHost ? "P2" : "P1");

            if (_disconnectGraceSeconds <= 0)
            {
                _gracePeriodTimer?.Dispose();
                _gracePeriodTimer = null;
                await InvokeAsync(async () =>
                {
                    if (_isCoopHost)
                    {
                        // Host default action upon timeout: convert to single player
                        HandleConvertToSinglePlayer();
                    }
                    else
                    {
                        // Guest cannot continue alone without host: return to lobby
                        await HandleLeaveSession();
                    }
                });
            }
            else
            {
                await InvokeAsync(StateHasChanged);
            }
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private void CancelDisconnectionGracePeriod()
    {
        _gracePeriodTimer?.Dispose();
        _gracePeriodTimer = null;
        _isDisconnectGraceActive = false;
        EngineService.Engine.SetDisconnectGracePeriod(false);
        InvokeAsync(StateHasChanged);
    }

    private void HandleConvertToSinglePlayer()
    {
        _gracePeriodTimer?.Dispose();
        _gracePeriodTimer = null;
        _isDisconnectGraceActive = false;
        EngineService.Engine.SetDisconnectGracePeriod(false);

        // Convert Host to Solo 1P Mode
        _isCoopSession = false;
        EngineService.IsCoopSession = false;
        EngineService.SetTwoPlayerMode(false);
        InvokeAsync(StateHasChanged);
    }

    private async Task HandleLeaveSession()
    {
        _gracePeriodTimer?.Dispose();
        _gracePeriodTimer = null;
        _isDisconnectGraceActive = false;
        EngineService.Engine.SetDisconnectGracePeriod(false);

        try
        {
            await LobbyService.LeaveRoomAsync();
        }
        catch {}

        await JS.InvokeVoidAsync("eval", "window.location.href = '/coop';");
    }

    public async ValueTask DisposeAsync()
    {
        _gracePeriodTimer?.Dispose();
        _gracePeriodTimer = null;

        if (_isCoopSession)
        {
            LobbyService.OnPlayerLeftEvent -= HandlePlayerLeftSession;
            LobbyService.OnConnectionStateChanged -= HandleConnectionStateChanged;
            LobbyService.OnPlayerJoinedEvent -= HandlePlayerReconnected;
        }

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

        try
        {
            await JS.InvokeVoidAsync("GameBridge.unregisterPlayComponent");
        }
        catch {}

        _arcadeDotNetRef?.Dispose();
        EngineService.OnTelemetryUpdated -= HandleTelemetry;
        EngineService.OnStageChanged -= HandleStageChanged;
        await EngineService.DisposeAsync();
    }
}
