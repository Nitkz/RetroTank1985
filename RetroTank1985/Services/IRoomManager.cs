using RetroTank1985.Shared.Enums;
using RetroTank1985.Shared.Models;
using RetroTank1985.Shared.Models.Network;

namespace RetroTank1985.Services;

/// <summary>
/// Interface จัดการสถานะห้องเล่น Co-Op ในหน่วยความจำ Server
/// </summary>
public interface IRoomManager
{
    /// <summary>
    /// สร้างห้องใหม่โดย Host
    /// </summary>
    Task<RoomActionResult> CreateRoomAsync(string connectionId, string playerId, CreateRoomRequest request);

    /// <summary>
    /// ผู้เล่น Guest เข้าร่วมห้องด้วย RoomCode
    /// </summary>
    Task<RoomActionResult> JoinRoomAsync(string connectionId, string playerId, JoinRoomRequest request);

    /// <summary>
    /// ผู้เล่นออกจากห้อง หรือหลุดการเชื่อมต่อ
    /// </summary>
    Task<RoomActionResult> LeaveRoomAsync(string connectionId, string? roomCode = null);

    /// <summary>
    /// เปลี่ยนสถานะ Ready ของผู้เล่นในห้อง
    /// </summary>
    Task<RoomActionResult> SetReadyAsync(string connectionId, string roomCode, bool isReady);

    /// <summary>
    /// Host เปลี่ยน Stage ที่เลือกเล่น
    /// </summary>
    Task<RoomActionResult> ChangeStageAsync(string connectionId, string roomCode, int stageNumber);

    /// <summary>
    /// Host ปรับเปลี่ยน Game Settings (Difficulty, Lives, Speed, etc.)
    /// </summary>
    Task<RoomActionResult> ChangeGameSettingsAsync(string connectionId, string roomCode, GameSettings settings);

    /// <summary>
    /// Host สั่งเริ่มเล่นเกม (เมื่อทั้งสองฝ่าย Ready)
    /// </summary>
    Task<RoomActionResult> StartGameAsync(string connectionId, string roomCode);

    /// <summary>
    /// อัปเดตสถานะห้อง เช่น InGame, StageCompleted, GameOver
    /// </summary>
    Task<RoomActionResult> UpdateRoomStateAsync(string connectionId, string roomCode, CoopRoomState newState);

    /// <summary>
    /// อัปเดต Heartbeat และ Ping ของผู้เล่น
    /// </summary>
    Task<bool> UpdateHeartbeatAsync(string connectionId, string roomCode, int pingMs);

    /// <summary>
    /// ดึงข้อมูลห้องจากรหัสห้อง
    /// </summary>
    CoopRoomInfo? GetRoom(string roomCode);

    /// <summary>
    /// ดึงข้อมูลห้องจาก ConnectionId ของผู้เล่น
    /// </summary>
    CoopRoomInfo? GetRoomByConnectionId(string connectionId);

    /// <summary>
    /// ดึง ConnectionId ของเพื่อนร่วมห้อง (อีกฝ่าย)
    /// </summary>
    string? GetOpponentConnectionId(string connectionId, string roomCode);

    /// <summary>
    /// ลบห้องออกจากระบบ
    /// </summary>
    bool RemoveRoom(string roomCode);
}
