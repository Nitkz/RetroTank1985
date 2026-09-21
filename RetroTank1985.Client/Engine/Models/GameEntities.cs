using RetroTank1985.Shared.Enums;

namespace RetroTank1985.Client.Engine.Models;

public class Bullet
{
    public const float BulletSize = 4f;
    public const float NormalSpeed = 3.0f; // NES bullet px/frame
    public const float FastSpeed = 4.0f;

    public int Id { get; set; }
    public int OwnerId { get; set; }
    public int OwnerPlayer { get; set; } = 1; // 1 = P1, 2 = P2, 0 = Enemy
    public float X { get; set; }
    public float Y { get; set; }
    public Direction Direction { get; set; }
    public float Speed { get; set; } = NormalSpeed;
    public bool IsPlayerBullet { get; set; } = true;
    public bool CanBreakSteel { get; set; } = false;
    public bool IsActive { get; set; } = true;
}

public class Explosion
{
    public float X { get; set; }
    public float Y { get; set; }
    public int Frame { get; set; } = 0;
    public int MaxFrames { get; set; } = 3; // 3-frame explosion sprite (0xA0, 0xA2, 0xA4)
    public int FrameCounter { get; set; } = 0;
    public int FrameDelay { get; set; } = 4; // 4 ticks per frame (~66ms)
    public bool IsBig { get; set; } = false;
    public bool IsActive { get; set; } = true;
}

public class TelemetryData
{
    public int Fps { get; set; } = 60;
    public int X { get; set; } = 64;
    public int Y { get; set; } = 192;
    public string Direction { get; set; } = "UP";
    public bool Shield { get; set; } = true;
    public int Lives { get; set; } = 3;
    public int Hp { get; set; } = 1;
    public int MaxHp { get; set; } = 1;
    public int Score { get; set; } = 0;
    public bool IsTwoPlayer { get; set; } = false;
    public int P2X { get; set; } = 128;
    public int P2Y { get; set; } = 192;
    public int P2Lives { get; set; } = 3;
    public int P2Hp { get; set; } = 1;
    public int P2MaxHp { get; set; } = 1;
    public int P2Score { get; set; } = 0;
    public int HighScore { get; set; } = 20000;
    public int EnemiesLeft { get; set; } = 20;
    public int EnemiesActive { get; set; } = 0;
    public bool IsGameOver { get; set; } = false;
}

