using RetroTank1985.Shared.Enums;

namespace RetroTank1985.Shared.Models.Network;

/// <summary>
/// ข้อมูลผู้เล่นใน Slot ห้อง
/// </summary>
public class CoopPlayerSlot
{
    public string ConnectionId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public CoopPlayerSlotIndex SlotIndex { get; set; } = CoopPlayerSlotIndex.Player1;
    public bool IsHost { get; set; }
    public bool IsReady { get; set; }
    public int PingMs { get; set; }
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// ข้อมูลภาพรวมของห้อง Co-Op
/// </summary>
public class CoopRoomInfo
{
    public string RoomCode { get; set; } = string.Empty;
    public CoopRoomState State { get; set; } = CoopRoomState.WaitingForGuest;
    public int SelectedStage { get; set; } = 1;
    public string DifficultyMode { get; set; } = "Classic1985";
    public bool FriendlyFireStun { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CoopPlayerSlot? HostPlayer { get; set; }
    public CoopPlayerSlot? GuestPlayer { get; set; }

    public bool IsFull => HostPlayer != null && GuestPlayer != null;
    public bool CanStartGame => IsFull && HostPlayer?.IsReady == true && GuestPlayer?.IsReady == true;
}

/// <summary>
/// Request ขอสร้างห้องใหม่
/// </summary>
public class CreateRoomRequest
{
    public string PlayerName { get; set; } = "Player 1";
    public int InitialStage { get; set; } = 1;
    public string DifficultyMode { get; set; } = "Classic1985";
}

/// <summary>
/// Request ขอเข้าร่วมห้อง
/// </summary>
public class JoinRoomRequest
{
    public string RoomCode { get; set; } = string.Empty;
    public string PlayerName { get; set; } = "Player 2";
}

/// <summary>
/// ผลลัพธ์การกระทำในห้อง
/// </summary>
public class RoomActionResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public CoopRoomInfo? Room { get; set; }

    public static RoomActionResult Ok(CoopRoomInfo room, string? message = null) 
        => new() { Success = true, Room = room, Message = message };

    public static RoomActionResult Fail(string message) 
        => new() { Success = false, Message = message };
}
