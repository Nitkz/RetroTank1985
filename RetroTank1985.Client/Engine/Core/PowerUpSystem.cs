using RetroTank1985.Client.Engine.Enums;
using RetroTank1985.Client.Engine.Models;

namespace RetroTank1985.Client.Engine.Core;

public interface IPowerUpSystem
{
    IReadOnlyList<PowerUp> ActivePowerUps { get; }
    IReadOnlyList<ScorePopup> ActiveScorePopups { get; }

    void DropRandomPowerUp(float x, float y, IAudioEventQueue audioQueue);
    void SpawnPowerUpDebug(PowerUpType type, float? x = null, float? y = null, IAudioEventQueue? audioQueue = null);
    void Update(
        PlayerTank player,
        IEnemySystem enemies,
        IDestructibleMap map,
        IBulletSystem bullets,
        IAudioEventQueue audioQueue,
        Action<int> onAddScore);
    void Clear();
}

public class PowerUpSystem : IPowerUpSystem
{
    private const int MaxPowerUps = 4;
    private const int MaxScorePopups = 8;

    private readonly List<PowerUp> _powerUps = new(MaxPowerUps);
    private readonly List<ScorePopup> _scorePopups = new(MaxScorePopups);
    private readonly PowerUp[] _powerUpPool = new PowerUp[MaxPowerUps];
    private readonly ScorePopup[] _scorePopupPool = new ScorePopup[MaxScorePopups];
    private readonly Random _rand = new();
    private int _nextPowerUpId = 1;

    public IReadOnlyList<PowerUp> ActivePowerUps => _powerUps;
    public IReadOnlyList<ScorePopup> ActiveScorePopups => _scorePopups;

    public PowerUpSystem()
    {
        for (int i = 0; i < MaxPowerUps; i++)
        {
            _powerUpPool[i] = new PowerUp { IsActive = false };
        }
        for (int i = 0; i < MaxScorePopups; i++)
        {
            _scorePopupPool[i] = new ScorePopup { IsActive = false };
        }
    }

    private PowerUp? AcquirePowerUp()
    {
        for (int i = 0; i < MaxPowerUps; i++)
        {
            if (!_powerUpPool[i].IsActive)
            {
                return _powerUpPool[i];
            }
        }
        return null;
    }

    private ScorePopup? AcquireScorePopup()
    {
        for (int i = 0; i < MaxScorePopups; i++)
        {
            if (!_scorePopupPool[i].IsActive)
            {
                return _scorePopupPool[i];
            }
        }
        return null;
    }

    public void DropRandomPowerUp(float x, float y, IAudioEventQueue audioQueue)
    {
        // Randomly pick one of 6 powerups
        var type = (PowerUpType)_rand.Next(0, 6);
        SpawnPowerUp(type, x, y, audioQueue);
    }

    public void SpawnPowerUpDebug(PowerUpType type, float? x = null, float? y = null, IAudioEventQueue? audioQueue = null)
    {
        // Place in arena (grid aligned to 8px/16px)
        float spawnX = x ?? (_rand.Next(2, 11) * 16f);
        float spawnY = y ?? (_rand.Next(2, 10) * 16f);
        SpawnPowerUp(type, spawnX, spawnY, audioQueue);
    }

    private void SpawnPowerUp(PowerUpType type, float x, float y, IAudioEventQueue? audioQueue)
    {
        // Snap inside boundaries (16..176 NES px) aligned to 8px grid
        float clampedX = Math.Clamp(MathF.Round(x / 8f) * 8f, 16f, 176f);
        float clampedY = Math.Clamp(MathF.Round(y / 8f) * 8f, 16f, 176f);

        var p = AcquirePowerUp();
        if (p == null)
        {
            // Evict oldest powerup if pool is full
            if (_powerUps.Count > 0)
            {
                p = _powerUps[0];
                p.IsActive = false;
                _powerUps.RemoveAt(0);
            }
            else return;
        }

        p.Id = _nextPowerUpId++;
        p.Type = type;
        p.X = clampedX;
        p.Y = clampedY;
        p.IsActive = true;
        p.Lifetime = 900; // 15 seconds
        p.BlinkCounter = 0;
        p.IsVisible = true;

        if (!_powerUps.Contains(p))
        {
            _powerUps.Add(p);
        }

        audioQueue?.Enqueue(AudioSoundEffect.BonusAppear);
    }

    public void Update(
        PlayerTank player,
        IEnemySystem enemies,
        IDestructibleMap map,
        IBulletSystem bullets,
        IAudioEventQueue audioQueue,
        Action<int> onAddScore)
    {
        // 1. Update Active Power-Ups & Check Player Pickup Collision
        for (int i = _powerUps.Count - 1; i >= 0; i--)
        {
            var p = _powerUps[i];
            if (!p.IsActive)
            {
                _powerUps.RemoveAt(i);
                continue;
            }

            p.Lifetime--;
            p.BlinkCounter++;
            // Rapid blinking when about to expire (< 180 frames)
            if (p.Lifetime < 180)
            {
                p.IsVisible = (p.BlinkCounter / 8) % 2 == 0;
            }
            else
            {
                p.IsVisible = true;
            }

            if (p.Lifetime <= 0)
            {
                p.IsActive = false;
                _powerUps.RemoveAt(i);
                continue;
            }

            // Pickup Collision with Player Tank (AABB 16x16 intersection: threshold < 16f)
            if (player.IsActive && MathF.Abs(p.X - player.X) < 16f && MathF.Abs(p.Y - player.Y) < 16f)
            {
                ApplyPowerUpEffect(p.Type, player, enemies, map, bullets, audioQueue, onAddScore);

                // Spawn floating +500 PTS popup
                SpawnScorePopup(p.X, p.Y, 500);
                onAddScore(500);

                p.IsActive = false;
                _powerUps.RemoveAt(i);
            }
        }

        // 2. Update Floating Score Popups
        for (int i = _scorePopups.Count - 1; i >= 0; i--)
        {
            var sp = _scorePopups[i];
            if (!sp.IsActive)
            {
                _scorePopups.RemoveAt(i);
                continue;
            }

            sp.Lifetime--;
            if (sp.Lifetime <= 0)
            {
                sp.IsActive = false;
                _scorePopups.RemoveAt(i);
            }
        }
    }

    private void ApplyPowerUpEffect(
        PowerUpType type,
        PlayerTank player,
        IEnemySystem enemies,
        IDestructibleMap map,
        IBulletSystem bullets,
        IAudioEventQueue audioQueue,
        Action<int> onAddScore)
    {
        switch (type)
        {
            case PowerUpType.Star:
                // Star: Upgrades player gun (1: fast, 2: dual, 3: break steel)
                player.StarPower = Math.Min(3, player.StarPower + 1);
                audioQueue.Enqueue(AudioSoundEffect.Bonus);
                break;

            case PowerUpType.Helmet:
                // Helmet: 10 seconds of invulnerability shield
                player.ShieldActive = true;
                player.ShieldTimer = 600; // 10s at 60Hz
                audioQueue.Enqueue(AudioSoundEffect.Bonus);
                break;

            case PowerUpType.Timer:
                // Timer: Freeze all active enemies on screen for 10s
                enemies.FreezeEnemies(600);
                audioQueue.Enqueue(AudioSoundEffect.Bonus);
                break;

            case PowerUpType.Grenade:
                // Grenade (Bomb): Instantly nuke all active enemies on the screen
                enemies.NukeAllEnemies(bullets, audioQueue, onAddScore);
                audioQueue.Enqueue(AudioSoundEffect.Bonus);
                break;

            case PowerUpType.Shovel:
                // Shovel: Fortify Eagle HQ base wall with Steel for 20 seconds (1200 frames)
                map.FortifyEagleWithSteel(true, 1200);
                audioQueue.Enqueue(AudioSoundEffect.Bonus);
                break;

            case PowerUpType.TankLife:
                // 1-UP: Awards an extra life to the player
                player.Lives++;
                audioQueue.Enqueue(AudioSoundEffect.Life);
                break;
        }
    }

    private void SpawnScorePopup(float x, float y, int score)
    {
        var sp = AcquireScorePopup();
        if (sp == null) return;

        sp.X = x;
        sp.Y = y;
        sp.Score = score;
        sp.Lifetime = 40; // ~0.66 seconds
        sp.IsActive = true;

        if (!_scorePopups.Contains(sp))
        {
            _scorePopups.Add(sp);
        }
    }

    public void Clear()
    {
        for (int i = 0; i < MaxPowerUps; i++)
        {
            _powerUpPool[i].IsActive = false;
        }
        _powerUps.Clear();

        for (int i = 0; i < MaxScorePopups; i++)
        {
            _scorePopupPool[i].IsActive = false;
        }
        _scorePopups.Clear();
    }
}
