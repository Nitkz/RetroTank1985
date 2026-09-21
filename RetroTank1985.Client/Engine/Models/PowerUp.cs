using RetroTank1985.Shared.Enums;

namespace RetroTank1985.Client.Engine.Models;

public class PowerUp
{
    public const float Size = 16f;

    public int Id { get; set; }
    public PowerUpType Type { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public bool IsActive { get; set; }
    public int Lifetime { get; set; } // Auto-despawn timer if needed (~900 frames / 15s)
    public int BlinkCounter { get; set; }
    public bool IsVisible { get; set; } = true;
}

public class ScorePopup
{
    public float X { get; set; }
    public float Y { get; set; }
    public int Score { get; set; } = 500;
    public int Lifetime { get; set; } = 40; // ~0.66s at 60Hz
    public bool IsActive { get; set; }
}
