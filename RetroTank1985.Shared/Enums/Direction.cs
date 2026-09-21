namespace RetroTank1985.Shared.Enums;

/// <summary>
/// 4-Directional NES Battle City orientation.
/// Ordered matching NES ROM sprite tables: 0=UP, 1=LEFT, 2=DOWN, 3=RIGHT.
/// </summary>
public enum Direction : byte
{
    Up = 0,
    Left = 1,
    Down = 2,
    Right = 3
}

public static class DirectionExtensions
{
    public static (float dx, float dy) ToVector(this Direction dir, float speed) => dir switch
    {
        Direction.Up => (0, -speed),
        Direction.Down => (0, speed),
        Direction.Left => (-speed, 0),
        Direction.Right => (speed, 0),
        _ => (0, 0)
    };

    public static bool IsVertical(this Direction dir) =>
        dir == Direction.Up || dir == Direction.Down;

    public static bool IsHorizontal(this Direction dir) =>
        dir == Direction.Left || dir == Direction.Right;

    public static Direction Opposite(this Direction dir) => dir switch
    {
        Direction.Up => Direction.Down,
        Direction.Down => Direction.Up,
        Direction.Left => Direction.Right,
        Direction.Right => Direction.Left,
        _ => Direction.Up
    };
}
