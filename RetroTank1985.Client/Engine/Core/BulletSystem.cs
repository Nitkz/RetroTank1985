using RetroTank1985.Client.Engine.Enums;
using RetroTank1985.Client.Engine.Models;

namespace RetroTank1985.Client.Engine.Core;

public interface IBulletSystem
{
    IReadOnlyList<Bullet> ActiveBullets { get; }
    IReadOnlyList<Explosion> ActiveExplosions { get; }

    bool TryFirePlayerBullet(PlayerTank player, IAudioEventQueue audioQueue);
    void Update(IDestructibleMap map, IAudioEventQueue audioQueue);
    void Clear();
    void SpawnExplosion(float x, float y, bool isBig = false);
}

public class BulletSystem : IBulletSystem
{
    private const int MaxBullets = 16;
    private const int MaxExplosions = 16;

    private readonly List<Bullet> _bullets = new(MaxBullets);
    private readonly List<Explosion> _explosions = new(MaxExplosions);
    private readonly Bullet[] _bulletPool = new Bullet[MaxBullets];
    private readonly Explosion[] _explosionPool = new Explosion[MaxExplosions];
    private int _nextBulletId = 1;

    public IReadOnlyList<Bullet> ActiveBullets => _bullets;
    public IReadOnlyList<Explosion> ActiveExplosions => _explosions;

    public BulletSystem()
    {
        for (int i = 0; i < MaxBullets; i++)
        {
            _bulletPool[i] = new Bullet { IsActive = false };
        }
        for (int i = 0; i < MaxExplosions; i++)
        {
            _explosionPool[i] = new Explosion { IsActive = false };
        }
    }

    private Bullet? AcquireBullet()
    {
        for (int i = 0; i < MaxBullets; i++)
        {
            if (!_bulletPool[i].IsActive)
            {
                return _bulletPool[i];
            }
        }
        return null;
    }

    private Explosion? AcquireExplosion()
    {
        for (int i = 0; i < MaxExplosions; i++)
        {
            if (!_explosionPool[i].IsActive)
            {
                return _explosionPool[i];
            }
        }
        return null;
    }

    public bool TryFirePlayerBullet(PlayerTank player, IAudioEventQueue audioQueue)
    {
        if (!player.IsActive) return false;

        // Player starts with 1 bullet capacity (upgrades to 2 with StarPower >= 2)
        int maxAllowed = player.StarPower >= 2 ? 2 : 1;
        int activePlayerBullets = 0;
        for (int i = 0; i < _bullets.Count; i++)
        {
            if (_bullets[i].IsPlayerBullet && _bullets[i].IsActive)
                activePlayerBullets++;
        }

        if (activePlayerBullets >= maxAllowed) return false;

        var bullet = AcquireBullet();
        if (bullet == null) return false;

        float bx = player.X + 6f; // Centered on 16x16 tank
        float by = player.Y + 6f;

        switch (player.Direction)
        {
            case Direction.Up:
                by = player.Y - 4f;
                break;
            case Direction.Down:
                by = player.Y + 16f;
                break;
            case Direction.Left:
                bx = player.X - 4f;
                break;
            case Direction.Right:
                bx = player.X + 16f;
                break;
        }

        bullet.Id = _nextBulletId++;
        bullet.X = bx;
        bullet.Y = by;
        bullet.Direction = player.Direction;
        bullet.Speed = player.StarPower >= 1 ? Bullet.FastSpeed : Bullet.NormalSpeed;
        bullet.IsPlayerBullet = true;
        bullet.CanBreakSteel = player.StarPower >= 3;
        bullet.IsActive = true;

        if (!_bullets.Contains(bullet))
        {
            _bullets.Add(bullet);
        }

        audioQueue.Enqueue(AudioSoundEffect.Shot);
        return true;
    }

    public void Update(IDestructibleMap map, IAudioEventQueue audioQueue)
    {
        // 1. Update Bullets
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            var b = _bullets[i];
            if (!b.IsActive)
            {
                _bullets.RemoveAt(i);
                continue;
            }

            var (dx, dy) = b.Direction.ToVector(b.Speed);
            b.X += dx;
            b.Y += dy;

            // Playfield boundary collision (0..208 NES px)
            if (b.X < 0 || b.X > IDestructibleMap.PlayfieldSize - Bullet.BulletSize ||
                b.Y < 0 || b.Y > IDestructibleMap.PlayfieldSize - Bullet.BulletSize)
            {
                b.IsActive = false;
                SpawnExplosion(b.X, b.Y, false);
                audioQueue.Enqueue(AudioSoundEffect.HitSteel);
                _bullets.RemoveAt(i);
                continue;
            }

            // Sub-tile Destructible map collision
            if (map.HandleBulletHit(b, out var sfx, out var hitEagle))
            {
                b.IsActive = false;
                SpawnExplosion(b.X, b.Y, hitEagle);
                if (sfx != AudioSoundEffect.None)
                {
                    audioQueue.Enqueue(sfx);
                }
                _bullets.RemoveAt(i);
                continue;
            }
        }

        // 2. Update Bullet-vs-Bullet collisions
        for (int i = 0; i < _bullets.Count; i++)
        {
            for (int j = i + 1; j < _bullets.Count; j++)
            {
                var b1 = _bullets[i];
                var b2 = _bullets[j];
                if (b1.IsPlayerBullet != b2.IsPlayerBullet && b1.IsActive && b2.IsActive)
                {
                    if (MathF.Abs(b1.X - b2.X) < 6f && MathF.Abs(b1.Y - b2.Y) < 6f)
                    {
                        b1.IsActive = false;
                        b2.IsActive = false;
                        SpawnExplosion((b1.X + b2.X) / 2f, (b1.Y + b2.Y) / 2f, false);
                    }
                }
            }
        }

        // 3. Update Explosions
        for (int i = _explosions.Count - 1; i >= 0; i--)
        {
            var ex = _explosions[i];
            ex.FrameCounter++;
            if (ex.FrameCounter >= ex.FrameDelay)
            {
                ex.FrameCounter = 0;
                ex.Frame++;
                if (ex.Frame >= ex.MaxFrames)
                {
                    ex.IsActive = false;
                    _explosions.RemoveAt(i);
                }
            }
        }
    }

    public void SpawnExplosion(float x, float y, bool isBig = false)
    {
        var ex = AcquireExplosion();
        if (ex == null) return;

        ex.X = x - 4f;
        ex.Y = y - 4f;
        ex.Frame = 0;
        ex.FrameCounter = 0;
        ex.MaxFrames = isBig ? 5 : 3;
        ex.IsBig = isBig;
        ex.IsActive = true;

        if (!_explosions.Contains(ex))
        {
            _explosions.Add(ex);
        }
    }

    public void Clear()
    {
        for (int i = 0; i < MaxBullets; i++) _bulletPool[i].IsActive = false;
        for (int i = 0; i < MaxExplosions; i++) _explosionPool[i].IsActive = false;
        _bullets.Clear();
        _explosions.Clear();
    }
}
