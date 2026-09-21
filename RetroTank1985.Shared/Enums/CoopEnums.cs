namespace RetroTank1985.Shared.Enums;

/// <summary>
/// สถานะของห้องเล่นมัลติเพลเยอร์
/// </summary>
public enum CoopRoomState
{
    WaitingForGuest = 0,
    InLobbyReady = 1,
    Starting = 2,
    InGame = 3,
    StageCompleted = 4,
    GameOver = 5,
    Closed = 6
}

/// <summary>
/// ตำแหน่งผู้เล่นในห้อง (Slot)
/// </summary>
public enum CoopPlayerSlotIndex
{
    Player1 = 0, // Host (Yellow Tank)
    Player2 = 1  // Guest (Green Tank)
}

/// <summary>
/// ชนิดของสัญญาณ WebRTC Signaling
/// </summary>
public enum WebRtcSignalType
{
    Offer = 0,
    Answer = 1,
    IceCandidate = 2,
    Ping = 3,
    Pong = 4
}

/// <summary>
/// รหัส Emote ข้อความสื่อสารด่วน 8-บิต
/// </summary>
public enum RetroEmoteType : byte
{
    None = 0,
    DefendEagle = 1,   // 🛡️ "DEFEND HQ!"
    TakeStar = 2,      // ⭐ "TAKE STAR!"
    AttackFlank = 3,   // 🚀 "ATTACK FLANK!"
    NukeBomb = 4,      // 💣 "NUKE BOMB!"
    NiceShot = 5,      // 🤝 "NICE SHOT!"
    Sorry = 6          // 😅 "SORRY!"
}
