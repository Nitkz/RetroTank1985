namespace RetroTank1985.Client.Engine.Enums;

/// <summary>
/// 8x8 px sub-tile types in the 26x26 Battle City playfield.
/// </summary>
public enum SubTileType : byte
{
    Empty = 0,
    Brick = 1,
    Steel = 2,
    Water = 3,
    Trees = 4,
    Ice = 5,
    Eagle = 6,
    DestroyedEagle = 7
}
