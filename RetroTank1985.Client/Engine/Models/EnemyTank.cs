using RetroTank1985.Client.Engine.Enums;
using RetroTank1985.Client.Models;

namespace RetroTank1985.Client.Engine.Models;

/// <summary>
/// Represents an enemy tank on the battlefield.
/// Implements 4 authentic NES archetypes, HP damage tracking, and flashing power-up carrier status.
/// </summary>
public class EnemyTank
{
    public const float TankSize = 16f;

    // Speeds in NES px/frame (at 60 FPS)
    public const float BasicSpeed = 1.0f;  // ~60 px/s
    public const float FastSpeed = 2.0f;   // ~120 px/s (Balanced from 2.5f)
    public const float PowerSpeed = 1.0f;  // ~60 px/s
    public const float ArmorSpeed = 1.0f;  // ~60 px/s

    public int Id { get; set; }
    public EnemyType Type { get; set; } = EnemyType.Basic;
    public float X { get; set; }
    public float Y { get; set; }
    public Direction Direction { get; set; } = Direction.Down;

    public int Hp { get; set; } = 1;
    public int MaxHp { get; set; } = 1;
    public bool IsFlashing { get; set; } = false;
    public float Speed { get; set; } = BasicSpeed;

    public bool IsActive { get; set; } = false;
    public int FreezeTimer { get; set; } = 0;
    public bool IsSpawning { get; set; } = false;
    public int SpawnTimer { get; set; } = 30; // 30 frames star sparkle before emergence

    public int AnimFrame { get; set; } = 0;
    public int AnimCounter { get; set; } = 0;

    // AI movement & decision counters
    public int MoveDecisionTimer { get; set; } = 0;
    public int TurnCooldown { get; set; } = 0;

    public int PointValue => Type switch
    {
        EnemyType.Basic => 100,
        EnemyType.Fast => 200,
        EnemyType.Power => 300,
        EnemyType.Armor => 400,
        _ => 100
    };

    public void Initialize(int id, EnemyType type, float spawnX, float spawnY, bool isFlashing = false)
    {
        Id = id;
        Type = type;
        X = spawnX;
        Y = spawnY;
        Direction = Direction.Down;
        IsFlashing = isFlashing;
        IsActive = true;
        IsSpawning = true;
        SpawnTimer = 30;
        AnimFrame = 0;
        AnimCounter = 0;
        MoveDecisionTimer = 0;
        TurnCooldown = 0;

        switch (type)
        {
            case EnemyType.Basic:
                Speed = BasicSpeed;
                Hp = 1;
                MaxHp = 1;
                break;
            case EnemyType.Fast:
                Speed = FastSpeed;
                Hp = 1;
                MaxHp = 1;
                break;
            case EnemyType.Power:
                Speed = PowerSpeed;
                Hp = 1;
                MaxHp = 1;
                break;
            case EnemyType.Armor:
                Speed = ArmorSpeed;
                Hp = 4;
                MaxHp = 4;
                break;
        }
    }

    public void Reset()
    {
        IsActive = false;
        IsSpawning = false;
        Hp = 0;
    }
}
