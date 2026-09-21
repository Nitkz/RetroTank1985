namespace RetroTank1985.Shared.Enums;

/// <summary>
/// 6 Droppable Power-Up items in NES Battle City.
/// </summary>
public enum PowerUpType : byte
{
    Helmet = 0,   // Forcefield invulnerability (10s)
    Timer = 1,    // Freeze all enemies (10s)
    Shovel = 2,   // Fortify base with steel (20s)
    Star = 3,     // Upgrade tank firepower / speed / steel destruction
    Grenade = 4,  // Destroy all active enemies on screen immediately
    TankLife = 5  // Extra 1-UP life for player
}
