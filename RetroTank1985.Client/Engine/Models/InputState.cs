namespace RetroTank1985.Client.Engine.Models;

public struct InputState
{
    public bool Up { get; set; }
    public bool Down { get; set; }
    public bool Left { get; set; }
    public bool Right { get; set; }
    public bool Fire { get; set; }
    public bool Pause { get; set; }

    public bool HasDirection => Up || Down || Left || Right;
}
