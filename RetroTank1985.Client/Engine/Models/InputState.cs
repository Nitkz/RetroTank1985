namespace RetroTank1985.Client.Engine.Models;

public struct InputState
{
    // Player 1 (WASD + Space/J)
    public bool Up { get; set; }
    public bool Down { get; set; }
    public bool Left { get; set; }
    public bool Right { get; set; }
    public bool Fire { get; set; }

    // Player 2 (Arrows + Enter/K/L/Numpad0)
    public bool P2Up { get; set; }
    public bool P2Down { get; set; }
    public bool P2Left { get; set; }
    public bool P2Right { get; set; }
    public bool P2Fire { get; set; }

    // System
    public bool Pause { get; set; }

    public bool HasP1Direction => Up || Down || Left || Right;
    public bool HasP2Direction => P2Up || P2Down || P2Left || P2Right;
}

