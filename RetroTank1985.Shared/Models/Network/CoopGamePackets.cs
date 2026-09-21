using RetroTank1985.Shared.Enums;

namespace RetroTank1985.Shared.Models.Network;

/// <summary>
/// แพ็กเก็ตอินพุตของผู้เล่น (Client -> Host, 60Hz)
/// </summary>
public struct PlayerInputPacket
{
    public uint Sequence { get; set; }
    public byte Direction { get; set; } // 0=None, 1=Up, 2=Right, 3=Down, 4=Left
    public bool IsFiring { get; set; }
    public bool BorrowLifeReq { get; set; }
    public RetroEmoteType Emote { get; set; }
    public ushort ClientTimestamp { get; set; }
}

/// <summary>
/// Snapshot ของรถถังผู้เล่นในเฟรมนั้น
/// </summary>
public struct TankNetworkSnapshot
{
    public float X { get; set; }
    public float Y { get; set; }
    public byte Direction { get; set; }
    public bool IsMoving { get; set; }
    public bool IsActive { get; set; }
    public int Lives { get; set; }
    public int Hp { get; set; }
    public int StarTier { get; set; }
    public float ShieldTimeRemaining { get; set; }
    public int InvulnerableTimer { get; set; }
    public bool IsDestroyed { get; set; }
    public RetroEmoteType ActiveEmote { get; set; }
}

/// <summary>
/// Snapshot ของรถถังศัตรู
/// </summary>
public struct EnemyNetworkSnapshot
{
    public int Id { get; set; }
    public byte TankType { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public byte Direction { get; set; }
    public int Health { get; set; }
    public bool IsFlashing { get; set; }
    public bool IsSpawning { get; set; }
    public float SpawnAnimProgress { get; set; }
}

/// <summary>
/// Snapshot ของกระสุนปืน
/// </summary>
public struct BulletNetworkSnapshot
{
    public int Id { get; set; }
    public byte Owner { get; set; } // 0=P1, 1=P2, 2=Enemy
    public float X { get; set; }
    public float Y { get; set; }
    public byte Direction { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// เหตุการณ์การเปลี่ยนแปลงก้อนอิฐในแผนที่ (Delta Event)
/// </summary>
public struct MapDeltaMutation
{
    public int SubTileIndex { get; set; }
    public byte NewTileType { get; set; }
}

/// <summary>
/// ภาพรวม Game State Snapshot (Host -> Client, 30-60Hz)
/// </summary>
public class CoopSyncSnapshotDto
{
    public uint FrameIndex { get; set; }
    public uint AckP2Sequence { get; set; }

    public TankNetworkSnapshot Player1 { get; set; }
    public TankNetworkSnapshot Player2 { get; set; }

    public EnemyNetworkSnapshot[] Enemies { get; set; } = [];
    public BulletNetworkSnapshot[] Bullets { get; set; } = [];
    public MapDeltaMutation[]? MapMutations { get; set; }
    public byte[]? SubTiles { get; set; }

    public byte? ActivePowerUpType { get; set; }
    public float PowerUpX { get; set; }
    public float PowerUpY { get; set; }

    public bool IsEagleDestroyed { get; set; }
    public int RemainingEnemyWaveCount { get; set; }

    public byte[]? AudioEvents { get; set; }

    // Synchronized GameState & Stage Tally (FEAT-03)
    public byte GameState { get; set; }
    public int StageNumber { get; set; }
    public int TallyStep { get; set; }
    public int TallyCountBasicP1 { get; set; }
    public int TallyCountFastP1 { get; set; }
    public int TallyCountPowerP1 { get; set; }
    public int TallyCountArmorP1 { get; set; }
    public int TallyCountBasicP2 { get; set; }
    public int TallyCountFastP2 { get; set; }
    public int TallyCountPowerP2 { get; set; }
    public int TallyCountArmorP2 { get; set; }
    public int ScoreP1 { get; set; }
    public int ScoreP2 { get; set; }
}
