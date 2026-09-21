using RetroTank1985.Shared.Enums;
using RetroTank1985.Client.Engine.Models;

namespace RetroTank1985.Client.Engine.Core;

public interface ITankPhysics
{
    void UpdatePlayer(
        PlayerTank player,
        bool up,
        bool down,
        bool left,
        bool right,
        IDestructibleMap map,
        IReadOnlyList<EnemyTank> enemies,
        PlayerTank? otherPlayer,
        IAudioEventQueue audioQueue);
}

public class TankPhysics : ITankPhysics
{
    private const float SnappingThreshold = 6.0f;
    private const float TankCollisionThreshold = 14.0f; // 14px threshold allows turning in 16px alleys

    private int _iceSlideTicksP1 = 0;
    private int _iceSlideTicksP2 = 0;
    private bool _engineSoundActive = false;

    public void UpdatePlayer(
        PlayerTank player,
        bool up,
        bool down,
        bool left,
        bool right,
        IDestructibleMap map,
        IReadOnlyList<EnemyTank> enemies,
        PlayerTank? otherPlayer,
        IAudioEventQueue audioQueue)
    {
        if (!player.IsActive) return;

        Direction? requestedDir = null;
        if (up) requestedDir = Direction.Up;
        else if (down) requestedDir = Direction.Down;
        else if (left) requestedDir = Direction.Left;
        else if (right) requestedDir = Direction.Right;

        bool onIce = map.IsOnIce(player.X, player.Y, PlayerTank.TankSize);

        ref int iceSlideTicks = ref (player.PlayerIndex == 2 ? ref _iceSlideTicksP2 : ref _iceSlideTicksP1);

        if (requestedDir.HasValue)
        {
            iceSlideTicks = onIce ? 14 : 0; // On ice, buffer ~14 ticks of slide momentum upon release
        }
        else if (iceSlideTicks > 0)
        {
            iceSlideTicks--;
        }

        bool isSliding = !requestedDir.HasValue && iceSlideTicks > 0 && onIce;
        bool shouldMove = requestedDir.HasValue || isSliding;

        if (shouldMove)
        {
            var newDir = requestedDir ?? player.Direction;

            // Handle Turn & Famicom 8px Grid Alignment Snapping
            if (player.Direction != newDir)
            {
                player.Direction = newDir;

                // On normal ground snap threshold is 6px, on ice it's reduced to 2px
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
            var (dx, dy) = player.Direction.ToVector(player.Speed);
            float nextX = player.X + dx;
            float nextY = player.Y + dy;

            bool canMove = map.CanTankMoveTo(nextX, nextY, PlayerTank.TankSize) 
                           && !CollidesWithAnyEnemy(player, nextX, nextY, enemies)
                           && !CollidesWithOtherPlayer(player, nextX, nextY, otherPlayer);

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
                    if (MathF.Abs(player.X - snappedX) > 0.01f 
                        && map.CanTankMoveTo(snappedX, nextY, PlayerTank.TankSize) 
                        && !CollidesWithAnyEnemy(player, snappedX, nextY, enemies)
                        && !CollidesWithOtherPlayer(player, snappedX, nextY, otherPlayer))
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
                    if (MathF.Abs(player.Y - snappedY) > 0.01f 
                        && map.CanTankMoveTo(nextX, snappedY, PlayerTank.TankSize) 
                        && !CollidesWithAnyEnemy(player, nextX, snappedY, enemies)
                        && !CollidesWithOtherPlayer(player, nextX, snappedY, otherPlayer))
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

                // Audio Engine Sound Hum (Player 1 or Player 2 moving)
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

        // Armor Hit Invulnerability Countdown (i-Frames)
        if (player.InvulnerableTimer > 0)
        {
            player.InvulnerableTimer--;
        }
    }

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

    private static bool CollidesWithOtherPlayer(PlayerTank player, float nextX, float nextY, PlayerTank? otherPlayer)
    {
        if (otherPlayer == null || !otherPlayer.IsActive) return false;

        float currentDist = MathF.Max(MathF.Abs(player.X - otherPlayer.X), MathF.Abs(player.Y - otherPlayer.Y));
        float nextDist = MathF.Max(MathF.Abs(nextX - otherPlayer.X), MathF.Abs(nextY - otherPlayer.Y));
        if (currentDist < TankCollisionThreshold && nextDist > currentDist)
        {
            return false; // Moving away, allowed
        }

        return MathF.Abs(nextX - otherPlayer.X) < TankCollisionThreshold && MathF.Abs(nextY - otherPlayer.Y) < TankCollisionThreshold;
    }
}

