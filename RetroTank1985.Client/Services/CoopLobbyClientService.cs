using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using RetroTank1985.Shared.Contracts;
using RetroTank1985.Shared.Enums;
using RetroTank1985.Shared.Models.Network;

namespace RetroTank1985.Client.Services;

/// <summary>
/// Service จัดการการเชื่อมต่อ SignalR และสถานะห้อง Co-Op สำหรับฝั่ง Blazor Client
/// </summary>
public class CoopLobbyClientService : IAsyncDisposable
{
    private readonly NavigationManager _navigationManager;
    private readonly ILogger<CoopLobbyClientService> _logger;
    private HubConnection? _hubConnection;
    private System.Threading.Timer? _heartbeatTimer;

    // สถานะปัจจุบัน
    public CoopRoomInfo? CurrentRoom { get; private set; }
    public string? CurrentConnectionId => _hubConnection?.ConnectionId;
    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
    public bool IsHost => CurrentRoom?.HostPlayer?.ConnectionId == CurrentConnectionId;
    public bool IsGuest => CurrentRoom?.GuestPlayer?.ConnectionId == CurrentConnectionId;
    public CoopPlayerSlot? MySlot => IsHost ? CurrentRoom?.HostPlayer : (IsGuest ? CurrentRoom?.GuestPlayer : null);
    public CoopPlayerSlot? OpponentSlot => IsHost ? CurrentRoom?.GuestPlayer : (IsGuest ? CurrentRoom?.HostPlayer : null);

    // Events สำหรับ UI
    public event Action<CoopRoomInfo>? OnRoomStateChanged;
    public event Action<CoopPlayerSlot>? OnPlayerJoinedEvent;
    public event Action<string>? OnPlayerLeftEvent;
    public event Action<CoopRoomInfo>? OnGameStartingEvent;
    public event Action<WebRtcSignalMessage>? OnSignalReceivedEvent;
    public event Action<string>? OnErrorOccurred;
    public event Action<HubConnectionState>? OnConnectionStateChanged;

    public CoopLobbyClientService(NavigationManager navigationManager, ILogger<CoopLobbyClientService> logger)
    {
        _navigationManager = navigationManager;
        _logger = logger;
    }

    /// <summary>
    /// เชื่อมต่อ SignalR Hub
    /// </summary>
    public async Task EnsureConnectedAsync()
    {
        if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
        {
            return;
        }

        if (_hubConnection == null)
        {
            var hubUrl = _navigationManager.ToAbsoluteUri("/hubs/coop-lobby");
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
                .Build();

            RegisterHubHandlers();

            _hubConnection.Reconnecting += error =>
            {
                _logger.LogWarning(error, "SignalR reconnecting...");
                OnConnectionStateChanged?.Invoke(HubConnectionState.Reconnecting);
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += connectionId =>
            {
                _logger.LogInformation("SignalR reconnected: {ConnectionId}", connectionId);
                OnConnectionStateChanged?.Invoke(HubConnectionState.Connected);
                return Task.CompletedTask;
            };

            _hubConnection.Closed += error =>
            {
                _logger.LogWarning(error, "SignalR connection closed");
                OnConnectionStateChanged?.Invoke(HubConnectionState.Disconnected);
                return Task.CompletedTask;
            };
        }

        try
        {
            await _hubConnection.StartAsync();
            _logger.LogInformation("SignalR connected successfully: {ConnectionId}", _hubConnection.ConnectionId);
            OnConnectionStateChanged?.Invoke(HubConnectionState.Connected);

            // Start heartbeat ping
            StartHeartbeat();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to SignalR hub");
            OnErrorOccurred?.Invoke($"ไม่สามารถเชื่อมต่อเซิร์ฟเวอร์ได้: {ex.Message}");
            throw;
        }
    }

    private void RegisterHubHandlers()
    {
        if (_hubConnection == null) return;

        _hubConnection.On<CoopRoomInfo>(nameof(ICoopLobbyClient.OnRoomUpdated), room =>
        {
            CurrentRoom = room;
            OnRoomStateChanged?.Invoke(room);
        });

        _hubConnection.On<CoopPlayerSlot>(nameof(ICoopLobbyClient.OnPlayerJoined), player =>
        {
            OnPlayerJoinedEvent?.Invoke(player);
        });

        _hubConnection.On<string>(nameof(ICoopLobbyClient.OnPlayerLeft), playerId =>
        {
            OnPlayerLeftEvent?.Invoke(playerId);
        });

        _hubConnection.On<CoopRoomInfo>(nameof(ICoopLobbyClient.OnGameStarting), room =>
        {
            CurrentRoom = room;
            OnGameStartingEvent?.Invoke(room);
        });

        _hubConnection.On<WebRtcSignalMessage>(nameof(ICoopLobbyClient.OnReceiveSignal), signal =>
        {
            OnSignalReceivedEvent?.Invoke(signal);
        });

        _hubConnection.On<string>(nameof(ICoopLobbyClient.OnErrorMessage), msg =>
        {
            OnErrorOccurred?.Invoke(msg);
        });
    }

    /// <summary>
    /// ขอสร้างห้องใหม่
    /// </summary>
    public async Task<RoomActionResult> CreateRoomAsync(string playerName, int stage = 1, string difficulty = "Classic1985")
    {
        await EnsureConnectedAsync();
        if (_hubConnection == null) return RoomActionResult.Fail("Not connected");

        var request = new CreateRoomRequest
        {
            PlayerName = playerName,
            InitialStage = stage,
            DifficultyMode = difficulty
        };

        var result = await _hubConnection.InvokeAsync<RoomActionResult>(nameof(ICoopLobbyHub.CreateRoom), request);
        if (result.Success && result.Room != null)
        {
            CurrentRoom = result.Room;
            OnRoomStateChanged?.Invoke(result.Room);
        }
        return result;
    }

    /// <summary>
    /// ขอเข้าร่วมห้อง
    /// </summary>
    public async Task<RoomActionResult> JoinRoomAsync(string roomCode, string playerName)
    {
        await EnsureConnectedAsync();
        if (_hubConnection == null) return RoomActionResult.Fail("Not connected");

        var request = new JoinRoomRequest
        {
            RoomCode = roomCode.Trim().ToUpperInvariant(),
            PlayerName = playerName
        };

        var result = await _hubConnection.InvokeAsync<RoomActionResult>(nameof(ICoopLobbyHub.JoinRoom), request);
        if (result.Success && result.Room != null)
        {
            CurrentRoom = result.Room;
            OnRoomStateChanged?.Invoke(result.Room);
        }
        return result;
    }

    /// <summary>
    /// ออกจากห้องปัจจุบัน
    /// </summary>
    public async Task<RoomActionResult> LeaveRoomAsync()
    {
        if (_hubConnection == null || CurrentRoom == null) return RoomActionResult.Ok(new CoopRoomInfo());

        var roomCode = CurrentRoom.RoomCode;
        var result = await _hubConnection.InvokeAsync<RoomActionResult>(nameof(ICoopLobbyHub.LeaveRoom), roomCode);
        CurrentRoom = null;
        return result;
    }

    /// <summary>
    /// เปลี่ยนสถานะความพร้อม (Ready)
    /// </summary>
    public async Task<RoomActionResult> SetReadyAsync(bool isReady)
    {
        if (_hubConnection == null || CurrentRoom == null) return RoomActionResult.Fail("Not in room");
        return await _hubConnection.InvokeAsync<RoomActionResult>(nameof(ICoopLobbyHub.SetReady), CurrentRoom.RoomCode, isReady);
    }

    /// <summary>
    /// เปลี่ยน Stage (สำหรับ Host)
    /// </summary>
    public async Task<RoomActionResult> ChangeStageAsync(int stageNumber)
    {
        if (_hubConnection == null || CurrentRoom == null) return RoomActionResult.Fail("Not in room");
        return await _hubConnection.InvokeAsync<RoomActionResult>(nameof(ICoopLobbyHub.ChangeStage), CurrentRoom.RoomCode, stageNumber);
    }

    /// <summary>
    /// สั่งเริ่มเกม (สำหรับ Host)
    /// </summary>
    public async Task<RoomActionResult> StartGameAsync()
    {
        if (_hubConnection == null || CurrentRoom == null) return RoomActionResult.Fail("Not in room");
        return await _hubConnection.InvokeAsync<RoomActionResult>(nameof(ICoopLobbyHub.StartGame), CurrentRoom.RoomCode);
    }

    /// <summary>
    /// ส่ง WebRTC Signal Message (SDP Offer/Answer หรือ ICE Candidate)
    /// </summary>
    public async Task SendSignalAsync(WebRtcSignalMessage signal)
    {
        if (_hubConnection == null) return;
        signal.RoomCode = CurrentRoom?.RoomCode ?? signal.RoomCode;
        await _hubConnection.InvokeAsync(nameof(ICoopLobbyHub.SendSignal), signal);
    }

    private void StartHeartbeat()
    {
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = new System.Threading.Timer(async _ =>
        {
            if (_hubConnection?.State == HubConnectionState.Connected && CurrentRoom != null)
            {
                try
                {
                    var start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    await _hubConnection.InvokeAsync(nameof(ICoopLobbyHub.SendHeartbeat), CurrentRoom.RoomCode, 15);
                }
                catch
                {
                    // Ignore ping error
                }
            }
        }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public async ValueTask DisposeAsync()
    {
        _heartbeatTimer?.Dispose();
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
        GC.SuppressFinalize(this);
    }
}
