using RetroTank1985.Shared.Enums;
using RetroTank1985.Shared.Models;
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
    Task OnReceiveGameSnapshot(CoopSyncSnapshotDto snapshot);
    Task OnReceivePlayerInput(PlayerInputPacket input);
    Task OnStageReloadRequested(int stageNumber);
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
    Task<RoomActionResult> ChangeGameSettings(string roomCode, GameSettings settings);
    Task<RoomActionResult> StartGame(string roomCode);
    Task<RoomActionResult> RestartMatch(string roomCode, int stageNumber);
    Task<RoomActionResult> UpdateRoomState(string roomCode, CoopRoomState newState);
    Task SendSignal(WebRtcSignalMessage signal);
    Task SendGameSnapshot(string roomCode, CoopSyncSnapshotDto snapshot);
    Task SendPlayerInput(string roomCode, PlayerInputPacket input);
    Task SendHeartbeat(string roomCode, int pingMs);
}
