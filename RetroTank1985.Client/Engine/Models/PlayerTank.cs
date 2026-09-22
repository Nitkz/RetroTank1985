using RetroTank1985.Shared.Enums;

namespace RetroTank1985.Client.Engine.Models;

public class PlayerTank
{
    public const float TileSize = 16f;
    public const float TankSize = 16f;
    public const float NormalSpeed = 1.25f; // ~75 NES px/sec at 60Hz

    public const float SpawnP1X = 4 * TileSize;
    public const float SpawnP2X = 8 * TileSize;
    public const float SpawnY = 12 * TileSize;

    public int PlayerIndex { get; set; } = 1; // 1 = P1 (Yellow), 2 = P2 (Green)
    public float X { get; set; } = SpawnP1X;  // Default for P1
    public float Y { get; set; } = SpawnY;
    public Direction Direction { get; set; } = Direction.Up;

    public bool IsMoving { get; set; }
    public int AnimFrame { get; set; }
    public int AnimCounter { get; set; }

    public bool ShieldActive { get; set; } = true;
    public int ShieldTimer { get; set; } = 180; // ~3 seconds at 60Hz
    public int ShieldFrame { get; set; }

    public int Lives { get; set; } = 3;
    public int Hp { get; set; } = 1;
    public int MaxHp { get; set; } = 1;
    public int InvulnerableTimer { get; set; } = 0; // i-Frames countdown after losing 1 HP armor

    public float Speed { get; set; } = NormalSpeed;
    public int StarPower { get; set; } = 0; // 0=Normal, 1=Fast bullet, 2=Dual bullet, 3=Break steel
    public int Score { get; set; } = 0;
    public bool IsActive { get; set; } = true;

    // Active Emote Balloon (2.5s = 150 frames at 60Hz)
    public RetroEmoteType ActiveEmote { get; set; } = RetroEmoteType.None;
    public int EmoteTimer { get; set; } = 0;

    public void TriggerEmote(RetroEmoteType emote)
    {
        ActiveEmote = emote;
        EmoteTimer = 150; // 2.5 seconds at 60Hz
    }

    /// <summary>
    /// Resets the tank's state (health, shield, invulnerability) and positions it at the spawn point.
    /// </summary>
    /// <param name="spawnX">Optional specific X coordinate to spawn at. If null, defaults to the standard P1 or P2 spawn location.</param>
    /// <param name="spawnY">Optional specific Y coordinate to spawn at. If null, defaults to the standard spawn row.</param>
    public void Reset(float? spawnX = null, float? spawnY = null)
    {
        X = spawnX ?? (PlayerIndex == 2 ? SpawnP2X : SpawnP1X);
        Y = spawnY ?? SpawnY;
        Direction = Direction.Up;
        IsMoving = false;
        AnimFrame = 0;
        AnimCounter = 0;
        ShieldActive = true;
        ShieldTimer = 180;
        ShieldFrame = 0;
        Hp = MaxHp;
        InvulnerableTimer = 0;
        IsActive = true;
    }

    public void InitializeForStage(int defaultLives, int maxHp, bool preserveState, float spawnX, float spawnY)
    {
        Lives = preserveState ? Math.Max(1, Lives) : defaultLives;
        StarPower = preserveState ? StarPower : 0;
        MaxHp = maxHp;
        Reset(spawnX, spawnY);
    }

    public void ApplySettings(float gameSpeedMultiplier, int playerArmorHp)
    {
        Speed = NormalSpeed * gameSpeedMultiplier;
        MaxHp = playerArmorHp;
        Hp = playerArmorHp;
    }

    public void ReviveIfNeeded(int defaultLives, int armorHp)
    {
        if (Lives <= 0)
        {
            Lives = defaultLives;
        }
        MaxHp = armorHp;
        Hp = armorHp;
        Reset();
    }
}

