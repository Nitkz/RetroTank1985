using RetroTank1985.Client.Engine.Enums;

namespace RetroTank1985.Client.Engine.Models;

public class PlayerTank
{
    public const float TankSize = 16f;
    public const float NormalSpeed = 1.25f; // ~75 NES px/sec at 60Hz

    public float X { get; set; } = 4 * 16f;  // Col 4 (64 px)
    public float Y { get; set; } = 12 * 16f; // Row 12 (192 px)
    public Direction Direction { get; set; } = Direction.Up;

    public bool IsMoving { get; set; }
    public int AnimFrame { get; set; }
    public int AnimCounter { get; set; }

    public bool ShieldActive { get; set; } = true;
    public int ShieldTimer { get; set; } = 180; // ~3 seconds at 60Hz
    public int ShieldFrame { get; set; }

    public int Lives { get; set; } = 3;
    public int StarPower { get; set; } = 0; // 0=Normal, 1=Fast bullet, 2=Dual bullet, 3=Break steel
    public bool IsActive { get; set; } = true;

    public void Reset(float spawnX = 4 * 16f, float spawnY = 12 * 16f)
    {
        X = spawnX;
        Y = spawnY;
        Direction = Direction.Up;
        IsMoving = false;
        AnimFrame = 0;
        AnimCounter = 0;
        ShieldActive = true;
        ShieldTimer = 180;
        ShieldFrame = 0;
        IsActive = true;
    }
}
