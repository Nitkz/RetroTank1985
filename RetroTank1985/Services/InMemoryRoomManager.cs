using System.Collections.Concurrent;
using System.Security.Cryptography;
using RetroTank1985.Shared.Enums;
using RetroTank1985.Shared.Models.Network;

namespace RetroTank1985.Services;

/// <summary>
/// ตัวจัดการห้อง Co-Op ในหน่วยความจำ (Thread-safe In-Memory Room Manager)
/// รองรับการสร้างรหัสห้อง, จับคู่ผู้เล่น 2P, ตรวจจับ Heartbeat และ Auto-cleanup
/// </summary>
public class InMemoryRoomManager : IRoomManager, IDisposable
{
    private readonly ConcurrentDictionary<string, CoopRoomInfo> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _connectionToRoomMap = new();
    private readonly ILogger<InMemoryRoomManager> _logger;
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _roomInactivityTimeout = TimeSpan.FromMinutes(30);
    private readonly TimeSpan _playerHeartbeatTimeout = TimeSpan.FromSeconds(30);
    private bool _disposed;

    private static readonly char[] RoomCodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray(); // ตัด 0, O, 1, I ออกเพื่อลดความสับสน

    public InMemoryRoomManager(ILogger<InMemoryRoomManager> logger)
    {
        _logger = logger;
        // Run cleanup every 1 minute
        _cleanupTimer = new Timer(CleanupInactiveRooms, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public Task<RoomActionResult> CreateRoomAsync(string connectionId, string playerId, CreateRoomRequest request)
    {
        // Check if player already in a room
        if (_connectionToRoomMap.TryGetValue(connectionId, out var existingRoomCode))
        {
            LeaveRoomAsync(connectionId, existingRoomCode);
        }

        var roomCode = GenerateUniqueRoomCode();
        var hostPlayer = new CoopPlayerSlot
        {
            ConnectionId = connectionId,
            PlayerId = string.IsNullOrWhiteSpace(playerId) ? Guid.NewGuid().ToString("N")[..8] : playerId,
            PlayerName = string.IsNullOrWhiteSpace(request.PlayerName) ? "Player 1" : request.PlayerName.Trim(),
            SlotIndex = CoopPlayerSlotIndex.Player1,
            IsHost = true,
            IsReady = true, // Host is ready by default
            LastHeartbeat = DateTime.UtcNow
        };

        var room = new CoopRoomInfo
        {
            RoomCode = roomCode,
            State = CoopRoomState.WaitingForGuest,
            SelectedStage = Math.Clamp(request.InitialStage, 1, 35),
            DifficultyMode = request.DifficultyMode ?? "Classic1985",
            FriendlyFireStun = true,
            CreatedAt = DateTime.UtcNow,
            HostPlayer = hostPlayer,
            GuestPlayer = null
        };

        if (_rooms.TryAdd(roomCode, room))
        {
            _connectionToRoomMap[connectionId] = roomCode;
            _logger.LogInformation("Co-op room {RoomCode} created by host {HostName} ({ConnectionId})", roomCode, hostPlayer.PlayerName, connectionId);
            return Task.FromResult(RoomActionResult.Ok(room, "Room created successfully"));
        }

        return Task.FromResult(RoomActionResult.Fail("Failed to generate room. Please try again."));
    }

    public Task<RoomActionResult> JoinRoomAsync(string connectionId, string playerId, JoinRoomRequest request)
    {
        var roomCode = request.RoomCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(roomCode) || !_rooms.TryGetValue(roomCode, out var room))
        {
            return Task.FromResult(RoomActionResult.Fail("Room not found or invalid room code."));
        }

        lock (room)
        {
            if (room.State != CoopRoomState.WaitingForGuest && room.State != CoopRoomState.InLobbyReady)
            {
                return Task.FromResult(RoomActionResult.Fail("Game has already started or room is unavailable."));
            }

            if (room.IsFull)
            {
                return Task.FromResult(RoomActionResult.Fail("Room is full (2/2 players)."));
            }

            // If connection already has a room mapped, remove old mapping
            if (_connectionToRoomMap.TryGetValue(connectionId, out var prevRoomCode) && prevRoomCode != roomCode)
            {
                LeaveRoomAsync(connectionId, prevRoomCode);
            }

            var guestPlayer = new CoopPlayerSlot
            {
                ConnectionId = connectionId,
                PlayerId = string.IsNullOrWhiteSpace(playerId) ? Guid.NewGuid().ToString("N")[..8] : playerId,
                PlayerName = string.IsNullOrWhiteSpace(request.PlayerName) ? "Player 2" : request.PlayerName.Trim(),
                SlotIndex = CoopPlayerSlotIndex.Player2,
                IsHost = false,
                IsReady = false,
                LastHeartbeat = DateTime.UtcNow
            };

            room.GuestPlayer = guestPlayer;
            room.State = CoopRoomState.InLobbyReady;
            _connectionToRoomMap[connectionId] = roomCode;

            _logger.LogInformation("Player {GuestName} joined room {RoomCode} as Guest ({ConnectionId})", guestPlayer.PlayerName, roomCode, connectionId);
            return Task.FromResult(RoomActionResult.Ok(room, "Joined room successfully"));
        }
    }

    public Task<RoomActionResult> LeaveRoomAsync(string connectionId, string? roomCode = null)
    {
        if (string.IsNullOrEmpty(roomCode))
        {
            _connectionToRoomMap.TryGetValue(connectionId, out roomCode);
        }

        if (string.IsNullOrEmpty(roomCode) || !_rooms.TryGetValue(roomCode, out var room))
        {
            _connectionToRoomMap.TryRemove(connectionId, out _);
            return Task.FromResult(RoomActionResult.Fail("Room not found."));
        }

        lock (room)
        {
            _connectionToRoomMap.TryRemove(connectionId, out _);

            bool isHost = room.HostPlayer?.ConnectionId == connectionId;
            bool isGuest = room.GuestPlayer?.ConnectionId == connectionId;

            if (isHost)
            {
                _logger.LogInformation("Host left room {RoomCode}. Closing room.", roomCode);
                room.State = CoopRoomState.Closed;
                room.HostPlayer = null;

                // Close the room and remove guest mapping if exists
                if (room.GuestPlayer != null)
                {
                    _connectionToRoomMap.TryRemove(room.GuestPlayer.ConnectionId, out _);
                }
                _rooms.TryRemove(roomCode, out _);
                return Task.FromResult(RoomActionResult.Ok(room, "Host left, room closed"));
            }
            else if (isGuest)
            {
                _logger.LogInformation("Guest left room {RoomCode}.", roomCode);
                room.GuestPlayer = null;
                room.State = CoopRoomState.WaitingForGuest;
                return Task.FromResult(RoomActionResult.Ok(room, "Guest left room"));
            }

            return Task.FromResult(RoomActionResult.Ok(room));
        }
    }

    public Task<RoomActionResult> SetReadyAsync(string connectionId, string roomCode, bool isReady)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
        {
            return Task.FromResult(RoomActionResult.Fail("Room not found."));
        }

        lock (room)
        {
            if (room.HostPlayer?.ConnectionId == connectionId)
            {
                room.HostPlayer.IsReady = isReady;
            }
            else if (room.GuestPlayer?.ConnectionId == connectionId)
            {
                room.GuestPlayer.IsReady = isReady;
            }
            else
            {
                return Task.FromResult(RoomActionResult.Fail("Player not found in room."));
            }

            return Task.FromResult(RoomActionResult.Ok(room));
        }
    }

    public Task<RoomActionResult> ChangeStageAsync(string connectionId, string roomCode, int stageNumber)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
        {
            return Task.FromResult(RoomActionResult.Fail("Room not found."));
        }

        lock (room)
        {
            if (room.HostPlayer?.ConnectionId != connectionId)
            {
                return Task.FromResult(RoomActionResult.Fail("Only host can change stage."));
            }

            room.SelectedStage = Math.Clamp(stageNumber, 1, 35);
            return Task.FromResult(RoomActionResult.Ok(room));
        }
    }

    public Task<RoomActionResult> StartGameAsync(string connectionId, string roomCode)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
        {
            return Task.FromResult(RoomActionResult.Fail("Room not found."));
        }

        lock (room)
        {
            if (room.HostPlayer?.ConnectionId != connectionId)
            {
                return Task.FromResult(RoomActionResult.Fail("Only host can start the game."));
            }

            if (!room.CanStartGame)
            {
                return Task.FromResult(RoomActionResult.Fail("Cannot start game: Both players must be ready and in room."));
            }

            room.State = CoopRoomState.Starting;
            return Task.FromResult(RoomActionResult.Ok(room, "Game starting..."));
        }
    }

    public Task<RoomActionResult> UpdateRoomStateAsync(string connectionId, string roomCode, CoopRoomState newState)
    {
        if (!_rooms.TryGetValue(roomCode, out var room))
        {
            return Task.FromResult(RoomActionResult.Fail("Room not found."));
        }

        lock (room)
        {
            if (room.HostPlayer?.ConnectionId != connectionId && room.GuestPlayer?.ConnectionId != connectionId)
            {
                return Task.FromResult(RoomActionResult.Fail("Unauthorized to update room state."));
            }

            room.State = newState;
            return Task.FromResult(RoomActionResult.Ok(room));
        }
    }

    public Task<bool> UpdateHeartbeatAsync(string connectionId, string roomCode, int pingMs)
    {
        if (!_rooms.TryGetValue(roomCode, out var room)) return Task.FromResult(false);

        lock (room)
        {
            if (room.HostPlayer?.ConnectionId == connectionId)
            {
                room.HostPlayer.LastHeartbeat = DateTime.UtcNow;
                room.HostPlayer.PingMs = pingMs;
                return Task.FromResult(true);
            }
            else if (room.GuestPlayer?.ConnectionId == connectionId)
            {
                room.GuestPlayer.LastHeartbeat = DateTime.UtcNow;
                room.GuestPlayer.PingMs = pingMs;
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    public CoopRoomInfo? GetRoom(string roomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode)) return null;
        _rooms.TryGetValue(roomCode.Trim().ToUpperInvariant(), out var room);
        return room;
    }

    public CoopRoomInfo? GetRoomByConnectionId(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId)) return null;
        if (_connectionToRoomMap.TryGetValue(connectionId, out var roomCode))
        {
            return GetRoom(roomCode);
        }
        return null;
    }

    public string? GetOpponentConnectionId(string connectionId, string roomCode)
    {
        var room = GetRoom(roomCode);
        if (room == null) return null;

        if (room.HostPlayer?.ConnectionId == connectionId)
        {
            return room.GuestPlayer?.ConnectionId;
        }
        else if (room.GuestPlayer?.ConnectionId == connectionId)
        {
            return room.HostPlayer?.ConnectionId;
        }

        return null;
    }

    public bool RemoveRoom(string roomCode)
    {
        if (string.IsNullOrWhiteSpace(roomCode)) return false;
        var normalized = roomCode.Trim().ToUpperInvariant();
        if (_rooms.TryRemove(normalized, out var room))
        {
            if (room.HostPlayer != null) _connectionToRoomMap.TryRemove(room.HostPlayer.ConnectionId, out _);
            if (room.GuestPlayer != null) _connectionToRoomMap.TryRemove(room.GuestPlayer.ConnectionId, out _);
            return true;
        }
        return false;
    }

    private string GenerateUniqueRoomCode()
    {
        const int codeLength = 6;
        for (int attempt = 0; attempt < 100; attempt++)
        {
            var bytes = new byte[codeLength];
            RandomNumberGenerator.Fill(bytes);
            var code = new char[codeLength];
            for (int i = 0; i < codeLength; i++)
            {
                code[i] = RoomCodeChars[bytes[i] % RoomCodeChars.Length];
            }
            var roomCode = new string(code);
            if (!_rooms.ContainsKey(roomCode))
            {
                return roomCode;
            }
        }

        // Fallback
        return Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    }

    private void CleanupInactiveRooms(object? state)
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _rooms)
        {
            var room = kvp.Value;
            var isExpired = (now - room.CreatedAt) > _roomInactivityTimeout;

            // Check if both players missed heartbeats
            bool hostTimedOut = room.HostPlayer == null || (now - room.HostPlayer.LastHeartbeat) > _playerHeartbeatTimeout;
            bool guestTimedOut = room.GuestPlayer == null || (now - room.GuestPlayer.LastHeartbeat) > _playerHeartbeatTimeout;

            if (isExpired || (hostTimedOut && guestTimedOut))
            {
                _logger.LogInformation("Cleaning up inactive room {RoomCode}", room.RoomCode);
                RemoveRoom(room.RoomCode);
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cleanupTimer.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
