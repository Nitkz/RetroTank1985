using RetroTank1985.Shared.Enums;
using RetroTank1985.Client.Engine.Models;
using RetroTank1985.Shared.Models;
using RetroTank1985.Shared.Models.Network;

namespace RetroTank1985.Client.Engine.Core;

public interface IEnemySystem
{
    IReadOnlyList<EnemyTank> ActiveEnemies { get; }
    int EnemiesRemaining { get; }
    int ActiveEnemyCount { get; }
    int[] KillsByType { get; }
    int[] KillsByTypeP1 { get; }
    int[] KillsByTypeP2 { get; }
    int TotalKills { get; }
    bool IsWaveCleared { get; }

    float SpeedMultiplier { get; set; }
    int TotalWaveEnemies { get; set; }
    int FireIntervalFrames { get; set; }

    void InitializeWave(StageModel? stage);
    void SyncFromNetwork(EnemyNetworkSnapshot[] snapshots, int remainingWaveCount);
    void Update(IReadOnlyList<PlayerTank> players, IDestructibleMap map, IBulletSystem bullets, IAudioEventQueue audioQueue, Action<EnemyTank, int>? onEnemyKilled = null);
    void Clear();
    void RecordKill(EnemyType type, int playerIndex = 1);
    bool TrySpawnEnemyDebug(EnemyType type, int spawnPointIndex = -1, bool isFlashing = false);
    void NukeAllEnemies(IBulletSystem bullets, IAudioEventQueue audioQueue, Action<int> onEnemyKilled);
    void SimulateClearAllEnemies(IBulletSystem bullets, IAudioEventQueue audioQueue, Action<int> onEnemyKilled);
    void FreezeEnemies(int durationFrames = 600);
}

public class EnemySystem : IEnemySystem
{
    public const int MaxConcurrentEnemies = 4;
    public int TotalWaveEnemies { get; set; } = 20;
    public float SpeedMultiplier { get; set; } = 1.0f;
    public int FireIntervalFrames { get; set; } = 35;

    // 3 NES Spawner positions (X, Y in NES pixels)
    // E1: (0, 0), E2: (6*16, 0) = (96, 0), E3: (12*16, 0) = (192, 0)
    private static readonly (float X, float Y)[] SpawnPoints =
    {
        (0f, 0f),
        (96f, 0f),
        (192f, 0f)
    };

    private readonly List<EnemyTank> _enemies = new(MaxConcurrentEnemies);
    private readonly EnemyTank[] _enemyPool = new EnemyTank[MaxConcurrentEnemies];
    private readonly List<EnemyType> _waveQueue = new(20);
    private readonly HashSet<int> _flashingIndices = new() { 3, 10, 17 }; // Standard NES 4th, 11th, 18th enemies flash
    private readonly int[] _killsByTypeP1 = new int[4]; // 0: Basic, 1: Fast, 2: Power, 3: Armor
    private readonly int[] _killsByTypeP2 = new int[4];
    private readonly int[] _killsByTypeCombined = new int[4];

    private readonly Random _rand = new();
    private int _spawnPointRotator = 0;
    private int _spawnDelayTimer = 0;
    private int _spawnedCount = 0;
    private int _nextEnemyId = 1;
    private int? _syncedRemainingEnemies = null;

    public IReadOnlyList<EnemyTank> ActiveEnemies => _enemies;
    public int EnemiesRemaining => _syncedRemainingEnemies ?? (Math.Max(0, TotalWaveEnemies - _spawnedCount) + ActiveEnemyCount);
    public int ActiveEnemyCount => _enemies.Count(e => e.IsActive);
    public int[] KillsByType
    {
        get
        {
            for (int i = 0; i < 4; i++) _killsByTypeCombined[i] = _killsByTypeP1[i] + _killsByTypeP2[i];
            return _killsByTypeCombined;
        }
    }
    public int[] KillsByTypeP1 => _killsByTypeP1;
    public int[] KillsByTypeP2 => _killsByTypeP2;
    public int TotalKills => KillsByType[0] + KillsByType[1] + KillsByType[2] + KillsByType[3];
    public bool IsWaveCleared => (_syncedRemainingEnemies.HasValue ? _syncedRemainingEnemies.Value == 0 : _spawnedCount >= TotalWaveEnemies) && ActiveEnemyCount == 0;

    public EnemySystem()
    {
        for (int i = 0; i < MaxConcurrentEnemies; i++)
        {
            _enemyPool[i] = new EnemyTank();
        }
    }

    public void InitializeWave(StageModel? stage)
    {
        Clear();
        _syncedRemainingEnemies = null;
        Array.Clear(_killsByTypeP1, 0, _killsByTypeP1.Length);
        Array.Clear(_killsByTypeP2, 0, _killsByTypeP2.Length);
        Array.Clear(_killsByTypeCombined, 0, _killsByTypeCombined.Length);
        _waveQueue.Clear();
        _spawnedCount = 0;
        _spawnDelayTimer = 60; // Initial delay before 1st enemy spawns
        _spawnPointRotator = 0;

        if (stage?.SpawnOrder != null && stage.SpawnOrder.Count > 0)
        {
            foreach (var t in stage.SpawnOrder)
            {
                _waveQueue.Add((EnemyType)Math.Clamp(t, 0, 3));
            }
        }
        else if (stage?.EnemyCounts != null)
        {
            for (int i = 0; i < stage.EnemyCounts.Basic; i++) _waveQueue.Add(EnemyType.Basic);
            for (int i = 0; i < stage.EnemyCounts.Fast; i++) _waveQueue.Add(EnemyType.Fast);
            for (int i = 0; i < stage.EnemyCounts.Power; i++) _waveQueue.Add(EnemyType.Power);
            for (int i = 0; i < stage.EnemyCounts.Armor; i++) _waveQueue.Add(EnemyType.Armor);
        }

        // Guarantee 20 enemies in wave
        while (_waveQueue.Count < TotalWaveEnemies)
        {
            _waveQueue.Add(EnemyType.Basic);
        }
    }

    public void SyncFromNetwork(EnemyNetworkSnapshot[] snapshots, int remainingWaveCount)
    {
        _syncedRemainingEnemies = remainingWaveCount;
        for (int i = 0; i < MaxConcurrentEnemies; i++) _enemyPool[i].IsActive = false;
        _enemies.Clear();

        if (snapshots == null || snapshots.Length == 0) return;

        for (int i = 0; i < snapshots.Length && i < MaxConcurrentEnemies; i++)
        {
            var s = snapshots[i];
            var e = _enemyPool[i];
            e.Id = s.Id;
            e.Type = (EnemyType)s.TankType;
            e.X = s.X;
            e.Y = s.Y;
            e.Direction = (Direction)s.Direction;
            e.Hp = s.Health;
            e.IsFlashing = s.IsFlashing;
            e.IsSpawning = s.IsSpawning;
            e.SpawnTimer = (int)s.SpawnAnimProgress;
            e.IsActive = true;
            _enemies.Add(e);
        }
    }

    private EnemyTank? AcquireEnemySlot()
    {
        for (int i = 0; i < MaxConcurrentEnemies; i++)
        {
            if (!_enemyPool[i].IsActive)
            {
                return _enemyPool[i];
            }
        }
        return null;
    }

    public bool TrySpawnEnemyDebug(EnemyType type, int spawnPointIndex = -1, bool isFlashing = false)
    {
        var slot = AcquireEnemySlot();
        if (slot == null) return false;

        int spIdx = spawnPointIndex >= 0 && spawnPointIndex < 3 
            ? spawnPointIndex 
            : _spawnPointRotator % 3;
        _spawnPointRotator++;

        var (spX, spY) = SpawnPoints[spIdx];
        slot.Initialize(_nextEnemyId++, type, spX, spY, isFlashing);

        if (!_enemies.Contains(slot))
        {
            _enemies.Add(slot);
        }
        return true;
    }

    private void TickSpawner()
    {
        if (_spawnedCount >= TotalWaveEnemies || ActiveEnemyCount >= MaxConcurrentEnemies)
        {
            return;
        }

        if (_spawnDelayTimer > 0)
        {
            _spawnDelayTimer--;
            return;
        }

        // Try spawn from queue - verify spawn point is clear
        int spIdx = _spawnPointRotator % 3;
        var (spX, spY) = SpawnPoints[spIdx];

        // Do not spawn if spawn point is currently occupied by player or another enemy
        if (IsSpawnPointBlocked(spX, spY))
        {
            _spawnDelayTimer = 30; // Retry shortly
            _spawnPointRotator++;  // Try another spawn point next time
            return;
        }

        var slot = AcquireEnemySlot();
        if (slot == null) return;

        _spawnPointRotator++;

        var nextType = _spawnedCount < _waveQueue.Count ? _waveQueue[_spawnedCount] : EnemyType.Basic;
        bool isFlashing = _flashingIndices.Contains(_spawnedCount);

        slot.Initialize(_nextEnemyId++, nextType, spX, spY, isFlashing);
        if (!_enemies.Contains(slot))
        {
            _enemies.Add(slot);
        }

        _spawnedCount++;
        _spawnDelayTimer = 180; // ~3.0s between sequential wave spawns
    }

    private bool IsSpawnPointBlocked(float spX, float spY)
    {
        for (int i = 0; i < _enemies.Count; i++)
        {
            var e = _enemies[i];
            if (e.IsActive && MathF.Abs(spX - e.X) < 14f && MathF.Abs(spY - e.Y) < 14f)
            {
                return true;
            }
        }
        return false;
    }

    public void Update(
        IReadOnlyList<PlayerTank> players, 
        IDestructibleMap map, 
        IBulletSystem bullets, 
        IAudioEventQueue audioQueue, 
        Action<EnemyTank, int>? onEnemyKilled = null)
    {
        // 1. Wave Spawner Tick
        TickSpawner();

        // 2. Active Enemy Updates
        for (int i = _enemies.Count - 1; i >= 0; i--)
        {
            var e = _enemies[i];
            if (!e.IsActive)
            {
                _enemies.RemoveAt(i);
                continue;
            }

            // A. Spawning Star Animation
            if (e.IsSpawning)
            {
                e.SpawnTimer--;
                if (e.SpawnTimer <= 0)
                {
                    e.IsSpawning = false;
                }
                continue; // Cannot move or fire while in spawn star animation
            }

            // B. Freeze Timer check (Timer Power-up)
            if (e.FreezeTimer > 0)
            {
                e.FreezeTimer--;
                continue; // Cannot move, animate, or shoot while frozen
            }

            // C. Tread Animation
            e.AnimCounter++;
            if (e.AnimCounter >= 4)
            {
                e.AnimCounter = 0;
                e.AnimFrame = (e.AnimFrame + 1) % 2;
            }

            // D. AI Direction Decision & 8px Grid Snapping
            e.MoveDecisionTimer++;
            if (e.TurnCooldown > 0) e.TurnCooldown--;

            // Famicom AI: Every ~30-60 frames or upon hitting obstacle, evaluate target & direction
            bool isAlignedToGrid = ((int)MathF.Round(e.X) % 8 == 0) && ((int)MathF.Round(e.Y) % 8 == 0);

            if (isAlignedToGrid && e.MoveDecisionTimer >= 45 && e.TurnCooldown <= 0)
            {
                e.MoveDecisionTimer = 0;
                e.TurnCooldown = 20;

                // 45% chance to hunt eagle base / nearest player, 55% random direction
                if (_rand.NextDouble() < 0.45)
                {
                    e.Direction = ChooseDirectionTowardTarget(e, players, map);
                }
                else
                {
                    e.Direction = (Direction)_rand.Next(0, 4);
                }
            }

            // E. Physics Step with Obstacle & Tank-vs-Tank Collision
            var (dx, dy) = e.Direction.ToVector(e.Speed * SpeedMultiplier);
            float nextX = e.X + dx;
            float nextY = e.Y + dy;

            bool canMove = map.CanTankMoveTo(nextX, nextY, EnemyTank.TankSize) 
                           && !CollidesWithAnyPlayer(e, nextX, nextY, players) 
                           && !CollidesWithOtherEnemy(e, nextX, nextY, _enemies);

            if (canMove)
            {
                e.X = nextX;
                e.Y = nextY;
            }
            else
            {
                // Hit wall, player, or other enemy tank: Snap to nearest 8px grid line and change direction
                if (e.Direction.IsVertical())
                {
                    e.X = MathF.Round(e.X / 8f) * 8f;
                }
                else
                {
                    e.Y = MathF.Round(e.Y / 8f) * 8f;
                }

                // Pick alternative direction that is open (avoid getting permanently stuck)
                e.Direction = PickOpenDirection(e, map, players, _enemies);
                e.TurnCooldown = 15;
            }

            // F. Random Enemy Firing (approx 1 in FireIntervalFrames chance per frame when active)
            int interval = Math.Max(10, FireIntervalFrames);
            if (_rand.Next(0, interval) == 0)
            {
                bullets.TryFireEnemyBullet(e, audioQueue);
            }
        }
    }

    private const float TankCollisionThreshold = 14.0f; // 14px threshold allows turning in 16px alleys

    private static bool CollidesWithAnyPlayer(EnemyTank e, float nextX, float nextY, IReadOnlyList<PlayerTank> players)
    {
        for (int pIdx = 0; pIdx < players.Count; pIdx++)
        {
            var player = players[pIdx];
            if (!player.IsActive) continue;

            // If already overlapping/stuck, allow moving away (if distance is increasing)
            float currentDist = MathF.Max(MathF.Abs(e.X - player.X), MathF.Abs(e.Y - player.Y));
            float nextDist = MathF.Max(MathF.Abs(nextX - player.X), MathF.Abs(nextY - player.Y));
            if (currentDist < TankCollisionThreshold && nextDist > currentDist)
            {
                continue; // Moving away, allow it
            }

            if (MathF.Abs(nextX - player.X) < TankCollisionThreshold && MathF.Abs(nextY - player.Y) < TankCollisionThreshold)
            {
                return true;
            }
        }
        return false;
    }

    private static bool CollidesWithOtherEnemy(EnemyTank e, float nextX, float nextY, List<EnemyTank> enemies)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            var other = enemies[i];
            if (other.Id == e.Id || !other.IsActive || other.IsSpawning) continue;

            float currentDist = MathF.Max(MathF.Abs(e.X - other.X), MathF.Abs(e.Y - other.Y));
            float nextDist = MathF.Max(MathF.Abs(nextX - other.X), MathF.Abs(nextY - other.Y));
            if (currentDist < TankCollisionThreshold && nextDist > currentDist)
            {
                continue; // Moving away, allow it
            }

            if (MathF.Abs(nextX - other.X) < TankCollisionThreshold && MathF.Abs(nextY - other.Y) < TankCollisionThreshold)
            {
                return true;
            }
        }
        return false;
    }

    private Direction PickOpenDirection(EnemyTank e, IDestructibleMap map, IReadOnlyList<PlayerTank> players, List<EnemyTank> enemies)
    {
        // Try all 4 directions in randomized order to find an open path
        var dirs = new Direction[] { Direction.Down, Direction.Left, Direction.Right, Direction.Up };
        for (int i = 0; i < dirs.Length; i++)
        {
            int r = _rand.Next(i, dirs.Length);
            (dirs[i], dirs[r]) = (dirs[r], dirs[i]);
        }

        foreach (var dir in dirs)
        {
            if (dir == e.Direction) continue;
            var (dx, dy) = dir.ToVector(e.Speed * 2f);
            float tx = e.X + dx;
            float ty = e.Y + dy;
            if (map.CanTankMoveTo(tx, ty, EnemyTank.TankSize) 
                && !CollidesWithAnyPlayer(e, tx, ty, players) 
                && !CollidesWithOtherEnemy(e, tx, ty, enemies))
            {
                return dir;
            }
        }

        // Fallback: reverse direction
        return e.Direction.Opposite();
    }

    private Direction ChooseDirectionTowardTarget(EnemyTank enemy, IReadOnlyList<PlayerTank> players, IDestructibleMap map)
    {
        // Target Eagle Base (row 24, col 12 => X: 96-104, Y: 192) with highest priority, then nearest player
        float targetX = 96f;
        float targetY = 192f;

        // Find closest active player
        PlayerTank? closestPlayer = null;
        float minPlayerDist = float.MaxValue;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p.IsActive)
            {
                float dist = MathF.Abs(p.X - enemy.X) + MathF.Abs(p.Y - enemy.Y);
                if (dist < minPlayerDist)
                {
                    minPlayerDist = dist;
                    closestPlayer = p;
                }
            }
        }

        if (_rand.NextDouble() < 0.5 && closestPlayer != null)
        {
            targetX = closestPlayer.X;
            targetY = closestPlayer.Y;
        }

        float diffX = targetX - enemy.X;
        float diffY = targetY - enemy.Y;

        if (MathF.Abs(diffY) > MathF.Abs(diffX) && _rand.NextDouble() < 0.7)
        {
            return diffY > 0 ? Direction.Down : Direction.Up;
        }
        else
        {
            return diffX > 0 ? Direction.Right : Direction.Left;
        }
    }

    public void RecordKill(EnemyType type, int playerIndex = 1)
    {
        int idx = (int)type;
        if (idx >= 0 && idx < 4)
        {
            if (playerIndex == 2)
            {
                _killsByTypeP2[idx]++;
            }
            else
            {
                _killsByTypeP1[idx]++;
            }
        }
    }

    public void NukeAllEnemies(IBulletSystem bullets, IAudioEventQueue audioQueue, Action<int> onEnemyKilled)
    {
        for (int i = 0; i < _enemies.Count; i++)
        {
            var e = _enemies[i];
            if (e.IsActive)
            {
                e.IsActive = false;
                RecordKill(e.Type, 1);
                bullets.SpawnExplosion(e.X, e.Y, true);
                onEnemyKilled(e.PointValue);
            }
        }
        _enemies.Clear();
        audioQueue.Enqueue(AudioSoundEffect.Explosion);
    }

    public void SimulateClearAllEnemies(IBulletSystem bullets, IAudioEventQueue audioQueue, Action<int> onEnemyKilled)
    {
        // 1. Kill all currently active enemies
        NukeAllEnemies(bullets, audioQueue, onEnemyKilled);

        // 2. Count all remaining enemies from queue as destroyed and award points
        while (_spawnedCount < TotalWaveEnemies && _spawnedCount < _waveQueue.Count)
        {
            var type = _waveQueue[_spawnedCount];
            RecordKill(type, 1);
            int pts = type switch
            {
                EnemyType.Basic => 100,
                EnemyType.Fast => 200,
                EnemyType.Power => 300,
                EnemyType.Armor => 400,
                _ => 100
            };
            onEnemyKilled(pts);
            _spawnedCount++;
        }
        _spawnedCount = TotalWaveEnemies;
    }

    public void FreezeEnemies(int durationFrames = 600)
    {
        for (int i = 0; i < _enemies.Count; i++)
        {
            if (_enemies[i].IsActive)
            {
                _enemies[i].FreezeTimer = durationFrames;
            }
        }
    }

    public void Clear()
    {
        for (int i = 0; i < MaxConcurrentEnemies; i++)
        {
            _enemyPool[i].Reset();
        }
        _enemies.Clear();
    }
}

