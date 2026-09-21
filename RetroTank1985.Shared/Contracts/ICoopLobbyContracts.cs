using RetroTank1985.Shared.Models.Network;

namespace RetroTank1985.Shared.Contracts;

/// <summary>
/// Interface ฝั่ง Client สำหรับรับ Event จาก SignalR Lobby Hub
/// </summary>
public interface ICoopLobbyClient
{
    Task OnRoomUpdated(CoopRoomInfo room);
    Task OnPlayerJoined(CoopPlayerSlot player);
    Task OnPlayerLeft(string playerId);
    Task OnGameStarting(CoopRoomInfo room);
    Task OnReceiveSignal(WebRtcSignalMessage signal);
    Task OnErrorMessage(string message);
}

/// <summary>
/// Interface ฝั่ง Server SignalR Lobby Hub (สำหรับ Strongly-Typed Client Calling)
/// </summary>
public interface ICoopLobbyHub
{
    Task<RoomActionResult> CreateRoom(CreateRoomRequest request);
    Task<RoomActionResult> JoinRoom(JoinRoomRequest request);
    Task<RoomActionResult> LeaveRoom(string roomCode);
    Task<RoomActionResult> SetReady(string roomCode, bool isReady);
    Task<RoomActionResult> ChangeStage(string roomCode, int stageNumber);
    Task<RoomActionResult> StartGame(string roomCode);
    Task SendSignal(WebRtcSignalMessage signal);
    Task SendHeartbeat(string roomCode, int pingMs);
}
