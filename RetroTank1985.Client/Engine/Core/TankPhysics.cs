using RetroTank1985.Client.Engine.Enums;
using RetroTank1985.Client.Engine.Models;

namespace RetroTank1985.Client.Engine.Core;

public interface ITankPhysics
{
    void UpdatePlayer(PlayerTank player, InputState input, IDestructibleMap map, IReadOnlyList<EnemyTank> enemies, IAudioEventQueue audioQueue);
}

public class TankPhysics : ITankPhysics
{
    private const float SnappingThreshold = 6.0f;
    private const float TankCollisionSize = 15.0f; // 15px bounding box to prevent overlap
    private int _iceSlideTicks = 0;
    private bool _engineSoundActive = false;

    public void UpdatePlayer(PlayerTank player, InputState input, IDestructibleMap map, IReadOnlyList<EnemyTank> enemies, IAudioEventQueue audioQueue)
    {
        if (!player.IsActive) return;

        Direction? requestedDir = null;
        if (input.Up) requestedDir = Direction.Up;
        else if (input.Down) requestedDir = Direction.Down;
        else if (input.Left) requestedDir = Direction.Left;
        else if (input.Right) requestedDir = Direction.Right;

        bool onIce = map.IsOnIce(player.X, player.Y, PlayerTank.TankSize);

        if (requestedDir.HasValue)
        {
            _iceSlideTicks = onIce ? 14 : 0; // On ice, buffer ~14 ticks (~16px / 1 full tile) of slide momentum upon release
        }
        else if (_iceSlideTicks > 0)
        {
            _iceSlideTicks--;
        }

        bool isSliding = !requestedDir.HasValue && _iceSlideTicks > 0 && onIce;
        bool shouldMove = requestedDir.HasValue || isSliding;

        if (shouldMove)
        {
            var newDir = requestedDir ?? player.Direction;

            // Handle Turn & Famicom 8px Grid Alignment Snapping
            if (player.Direction != newDir)
            {
                player.Direction = newDir;

                // On normal ground snap threshold is 6px, on ice it's reduced to 2px (harder to align corridors)
                float currentThreshold = onIce ? 2.0f : SnappingThreshold;

                if (newDir.IsVertical())
                {
                    // Snap X to nearest 8px grid line
                    float snappedX = MathF.Round(player.X / 8f) * 8f;
                    if (MathF.Abs(player.X - snappedX) <= currentThreshold)
                    {
                        player.X = snappedX;
                    }
                }
                else // Horizontal
                {
                    // Snap Y to nearest 8px grid line
                    float snappedY = MathF.Round(player.Y / 8f) * 8f;
                    if (MathF.Abs(player.Y - snappedY) <= currentThreshold)
                    {
                        player.Y = snappedY;
                    }
                }
            }

            // Calculate next target position
            var (dx, dy) = player.Direction.ToVector(PlayerTank.NormalSpeed);
            float nextX = player.X + dx;
            float nextY = player.Y + dy;

            bool canMove = map.CanTankMoveTo(nextX, nextY, PlayerTank.TankSize) && !CollidesWithAnyEnemy(player, nextX, nextY, enemies);

            if (canMove)
            {
                player.X = nextX;
                player.Y = nextY;
                player.IsMoving = true;
            }
            else
            {
                // Try gentle slide / nudge if close to grid corridor alignment
                bool nudged = false;
                if (player.Direction.IsVertical())
                {
                    float snappedX = MathF.Round(player.X / 8f) * 8f;
                    if (MathF.Abs(player.X - snappedX) > 0.01f && map.CanTankMoveTo(snappedX, nextY, PlayerTank.TankSize) && !CollidesWithAnyEnemy(player, snappedX, nextY, enemies))
                    {
                        player.X = snappedX;
                        player.Y = nextY;
                        player.IsMoving = true;
                        nudged = true;
                    }
                }
                else // Horizontal
                {
                    float snappedY = MathF.Round(player.Y / 8f) * 8f;
                    if (MathF.Abs(player.Y - snappedY) > 0.01f && map.CanTankMoveTo(nextX, snappedY, PlayerTank.TankSize) && !CollidesWithAnyEnemy(player, nextX, snappedY, enemies))
                    {
                        player.X = nextX;
                        player.Y = snappedY;
                        player.IsMoving = true;
                        nudged = true;
                    }
                }

                if (!nudged)
                {
                    player.IsMoving = requestedDir.HasValue;
                }
            }

            if (player.IsMoving)
            {
                // Tread animation (toggles every 4 frames while moving)
                player.AnimCounter++;
                if (player.AnimCounter >= 4)
                {
                    player.AnimCounter = 0;
                    player.AnimFrame = (player.AnimFrame + 1) % 2;
                }

                // Audio Engine Sound Hum
                if (!_engineSoundActive)
                {
                    audioQueue.Enqueue(AudioSoundEffect.EngineStart);
                    _engineSoundActive = true;
                }
            }
            else if (_engineSoundActive && !requestedDir.HasValue)
            {
                audioQueue.Enqueue(AudioSoundEffect.EngineStop);
                _engineSoundActive = false;
            }
        }
        else
        {
            player.IsMoving = false;
            if (_engineSoundActive)
            {
                audioQueue.Enqueue(AudioSoundEffect.EngineStop);
                _engineSoundActive = false;
            }
        }

        // Force Shield timer & frame oscillation
        if (player.ShieldActive)
        {
            player.ShieldTimer--;
            player.ShieldFrame = (player.ShieldTimer / 4) % 2;
            if (player.ShieldTimer <= 0)
            {
                player.ShieldActive = false;
            }
        }
    }

    private const float TankCollisionThreshold = 14.0f;

    private static bool CollidesWithAnyEnemy(PlayerTank player, float nextX, float nextY, IReadOnlyList<EnemyTank> enemies)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (!e.IsActive || e.IsSpawning) continue;

            // If already overlapping, allow moving away (increasing distance)
            float currentDist = MathF.Max(MathF.Abs(player.X - e.X), MathF.Abs(player.Y - e.Y));
            float nextDist = MathF.Max(MathF.Abs(nextX - e.X), MathF.Abs(nextY - e.Y));
            if (currentDist < TankCollisionThreshold && nextDist > currentDist)
            {
                continue; // Moving away, allowed
            }

            // AABB Box intersection check with 14px threshold to allow tight corridor turning
            if (MathF.Abs(nextX - e.X) < TankCollisionThreshold && MathF.Abs(nextY - e.Y) < TankCollisionThreshold)
            {
                return true;
            }
        }
        return false;
    }
}
