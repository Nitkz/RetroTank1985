using Microsoft.AspNetCore.SignalR;
using RetroTank1985.Services;
using RetroTank1985.Shared.Contracts;
using RetroTank1985.Shared.Enums;
using RetroTank1985.Shared.Models.Network;

namespace RetroTank1985.Hubs;

/// <summary>
/// SignalR Hub สำหรับจัดการห้อง Co-Op Lobby และ WebRTC Signaling ระหว่าง P1 (Host) และ P2 (Guest)
/// </summary>
public class CoopLobbyHub : Hub<ICoopLobbyClient>, ICoopLobbyHub
{
    private readonly IRoomManager _roomManager;
    private readonly ILogger<CoopLobbyHub> _logger;

    public CoopLobbyHub(IRoomManager roomManager, ILogger<CoopLobbyHub> logger)
    {
        _roomManager = roomManager;
        _logger = logger;
    }

    public async Task<RoomActionResult> CreateRoom(CreateRoomRequest request)
    {
        var result = await _roomManager.CreateRoomAsync(Context.ConnectionId, Context.ConnectionId, request);
        if (result.Success && result.Room != null)
        {
            var groupName = GetRoomGroupName(result.Room.RoomCode);
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            await Clients.Caller.OnRoomUpdated(result.Room);
        }
        return result;
    }

    public async Task<RoomActionResult> JoinRoom(JoinRoomRequest request)
    {
        var result = await _roomManager.JoinRoomAsync(Context.ConnectionId, Context.ConnectionId, request);
        if (result.Success && result.Room != null)
        {
            var groupName = GetRoomGroupName(result.Room.RoomCode);
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            // แจ้งเตือนทุกคนในห้องถึงการอัปเดต และแจ้ง PlayerJoined
            await Clients.Group(groupName).OnRoomUpdated(result.Room);
            if (result.Room.GuestPlayer != null)
            {
                await Clients.Group(groupName).OnPlayerJoined(result.Room.GuestPlayer);
            }
        }
        return result;
    }

    public async Task<RoomActionResult> LeaveRoom(string roomCode)
    {
        var result = await _roomManager.LeaveRoomAsync(Context.ConnectionId, roomCode);
        var groupName = GetRoomGroupName(roomCode);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        if (result.Room != null)
        {
            if (result.Room.State == CoopRoomState.Closed)
            {
                // หากห้องถูกปิด (Host ออก) แจ้งคนในห้องและปล่อยห้อง
                await Clients.Group(groupName).OnRoomUpdated(result.Room);
                await Clients.Group(groupName).OnPlayerLeft(Context.ConnectionId);
            }
            else
            {
                await Clients.Group(groupName).OnRoomUpdated(result.Room);
                await Clients.Group(groupName).OnPlayerLeft(Context.ConnectionId);
            }
        }
        return result;
    }

    public async Task<RoomActionResult> SetReady(string roomCode, bool isReady)
    {
        var result = await _roomManager.SetReadyAsync(Context.ConnectionId, roomCode, isReady);
        if (result.Success && result.Room != null)
        {
            var groupName = GetRoomGroupName(roomCode);
            await Clients.Group(groupName).OnRoomUpdated(result.Room);
        }
        return result;
    }

    public async Task<RoomActionResult> ChangeStage(string roomCode, int stageNumber)
    {
        var result = await _roomManager.ChangeStageAsync(Context.ConnectionId, roomCode, stageNumber);
        if (result.Success && result.Room != null)
        {
            var groupName = GetRoomGroupName(roomCode);
            await Clients.Group(groupName).OnRoomUpdated(result.Room);
        }
        return result;
    }

    public async Task<RoomActionResult> StartGame(string roomCode)
    {
        var result = await _roomManager.StartGameAsync(Context.ConnectionId, roomCode);
        if (result.Success && result.Room != null)
        {
            var groupName = GetRoomGroupName(roomCode);
            await Clients.Group(groupName).OnGameStarting(result.Room);
            await Clients.Group(groupName).OnRoomUpdated(result.Room);
        }
        return result;
    }

    public async Task SendSignal(WebRtcSignalMessage signal)
    {
        signal.SenderConnectionId = Context.ConnectionId;
        
        // หากระบุ TargetConnectionId ให้ส่งเจาะจงเฉพาะเครื่องนั้น
        if (!string.IsNullOrWhiteSpace(signal.TargetConnectionId))
        {
            await Clients.Client(signal.TargetConnectionId).OnReceiveSignal(signal);
            return;
        }

        // หากไม่ระบุ ให้ค้นหาคู่เล่นอีกฝั่งในห้องเดียวกัน
        if (!string.IsNullOrWhiteSpace(signal.RoomCode))
        {
            var opponentId = _roomManager.GetOpponentConnectionId(Context.ConnectionId, signal.RoomCode);
            if (!string.IsNullOrWhiteSpace(opponentId))
            {
                signal.TargetConnectionId = opponentId;
                await Clients.Client(opponentId).OnReceiveSignal(signal);
            }
            else
            {
                // Fallback: ส่งไปยัง Group โดยไม่รวมผู้ส่ง
                var groupName = GetRoomGroupName(signal.RoomCode);
                await Clients.OthersInGroup(groupName).OnReceiveSignal(signal);
            }
        }
    }

    public async Task SendHeartbeat(string roomCode, int pingMs)
    {
        await _roomManager.UpdateHeartbeatAsync(Context.ConnectionId, roomCode, pingMs);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var room = _roomManager.GetRoomByConnectionId(Context.ConnectionId);
        if (room != null)
        {
            _logger.LogInformation("Connection {ConnectionId} disconnected from room {RoomCode}", Context.ConnectionId, room.RoomCode);
            var groupName = GetRoomGroupName(room.RoomCode);
            var result = await _roomManager.LeaveRoomAsync(Context.ConnectionId, room.RoomCode);
            if (result.Room != null)
            {
                await Clients.Group(groupName).OnPlayerLeft(Context.ConnectionId);
                await Clients.Group(groupName).OnRoomUpdated(result.Room);
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    private static string GetRoomGroupName(string roomCode) => $"coop_room_{roomCode.Trim().ToUpperInvariant()}";
}
