using RetroTank1985.Client.Engine.Enums;
using RetroTank1985.Client.Engine.Models;
using RetroTank1985.Client.Models;

namespace RetroTank1985.Client.Engine.Core;


public interface IBattleCityEngine
{
    PlayerTank Player { get; }
    IDestructibleMap Map { get; }
    IBulletSystem Bullets { get; }
    IEnemySystem Enemies { get; }
    IAudioEventQueue Audio { get; }
    GameState State { get; }
    int CurrentStage { get; }
    int Score { get; }
    bool IsPaused { get; }

    void InitializeStage(StageModel? stage, int stageNumber);
    void ResetPlayer();
    void TogglePause();
    void SetPause(bool paused);
    void SetInput(InputState input);
    RenderFrameDto Tick(double timestampMs);
    TelemetryData GetTelemetry(int fps);

    // Sandbox & Debug Tools
    bool SpawnEnemyDebug(EnemyType type, int spawnPoint = -1, bool isFlashing = false);
    void NukeAllEnemies();
    void ClearEnemies();
    void SetPlayerStarPower(int starLevel);
    void TogglePlayerShield();
    void ToggleEagleSteel(bool fortified);
}

public class BattleCityEngine : IBattleCityEngine
{
    private const double MsPerFrame = 1000.0 / 60.0; // 16.6667 ms
    private double _lastTimestamp = 0;
    private double _accumulator = 0;

    private InputState _currentInput;
    private bool _previousFire = false;
    private bool _previousPause = false;

    private readonly RenderFrameDto _cachedFrame = new();
    private readonly List<BulletRenderDto> _bulletDtoPool = new(16);
    private readonly List<EnemyRenderDto> _enemyDtoPool = new(8);
    private readonly List<ExplosionRenderDto> _explosionDtoPool = new(16);

    private int _playerRespawnTimer = 0;

    public PlayerTank Player { get; }
    public IDestructibleMap Map { get; }
    public ITankPhysics Physics { get; }
    public IBulletSystem Bullets { get; }
    public IEnemySystem Enemies { get; }
    public IAudioEventQueue Audio { get; }

    public GameState State { get; private set; } = GameState.Playing;
    public int CurrentStage { get; private set; } = 1;
    public int Score { get; private set; } = 0;
    public bool IsPaused => State == GameState.Paused;

    public BattleCityEngine(
        IDestructibleMap map,
        ITankPhysics physics,
        IBulletSystem bullets,
        IEnemySystem enemies,
        IAudioEventQueue audio)
    {
        Map = map;
        Physics = physics;
        Bullets = bullets;
        Enemies = enemies;
        Audio = audio;
        Player = new PlayerTank();

        // Pre-allocate pool objects for zero heap allocations
        for (int i = 0; i < 16; i++)
        {
            _bulletDtoPool.Add(new BulletRenderDto());
            _explosionDtoPool.Add(new ExplosionRenderDto());
        }
        for (int i = 0; i < 8; i++)
        {
            _enemyDtoPool.Add(new EnemyRenderDto());
        }
    }

    public void InitializeStage(StageModel? stage, int stageNumber)
    {
        CurrentStage = stageNumber;
        Map.LoadStage(stage?.Grid);
        Bullets.Clear();
        Audio.Clear();
        Enemies.InitializeWave(stage);
        
        _playerRespawnTimer = 0;
        Player.Lives = 3;
        Player.StarPower = 0;
        ResetPlayer();

        State = GameState.Playing;
        _accumulator = 0;
        _lastTimestamp = 0;

        Audio.Enqueue(AudioSoundEffect.IntroBgm);
    }

    public void ResetPlayer()
    {
        if (Player.Lives <= 0)
        {
            Player.Lives = 3;
        }
        _playerRespawnTimer = 0;
        Player.Reset(4 * 16f, 12 * 16f);
        Bullets.Clear();
    }

    public void TogglePause()
    {
        if (State == GameState.Playing)
        {
            State = GameState.Paused;
            Audio.Enqueue(AudioSoundEffect.Pause);
            Audio.Enqueue(AudioSoundEffect.EngineStop);
        }
        else if (State == GameState.Paused)
        {
            State = GameState.Playing;
            Audio.Enqueue(AudioSoundEffect.Pause);
        }
    }

    public void SetPause(bool paused)
    {
        if (paused && State == GameState.Playing)
        {
            TogglePause();
        }
        else if (!paused && State == GameState.Paused)
        {
            TogglePause();
        }
    }

    public void SetInput(InputState input)
    {
        _currentInput = input;
    }

    public bool SpawnEnemyDebug(EnemyType type, int spawnPoint = -1, bool isFlashing = false)
    {
        return Enemies.TrySpawnEnemyDebug(type, spawnPoint, isFlashing);
    }

    public void NukeAllEnemies()
    {
        Enemies.NukeAllEnemies(Bullets, Audio, pts => Score += pts);
    }

    public void ClearEnemies()
    {
        Enemies.Clear();
    }

    public void SetPlayerStarPower(int starLevel)
    {
        Player.StarPower = Math.Clamp(starLevel, 0, 3);
    }

    public void TogglePlayerShield()
    {
        Player.ShieldActive = !Player.ShieldActive;
        if (Player.ShieldActive) Player.ShieldTimer = 600; // 10s
    }

    public void ToggleEagleSteel(bool fortified)
    {
        Map.FortifyEagleWithSteel(fortified);
    }

    public RenderFrameDto Tick(double timestampMs)
    {
        if (_lastTimestamp <= 0)
        {
            _lastTimestamp = timestampMs;
        }

        double delta = timestampMs - _lastTimestamp;
        _lastTimestamp = timestampMs;

        // Clamp delta to avoid spiral of death if tab was backgrounded
        _accumulator += Math.Min(delta, 100.0);

        // Check single-shot trigger keys
        if (_currentInput.Pause && !_previousPause)
        {
            TogglePause();
        }
        _previousPause = _currentInput.Pause;

        if (_currentInput.Fire && !_previousFire && State == GameState.Playing)
        {
            Bullets.TryFirePlayerBullet(Player, Audio);
        }
        _previousFire = _currentInput.Fire;

        // Fixed timestep 60Hz physics and game logic simulation
        while (_accumulator >= MsPerFrame)
        {
            if (State == GameState.Playing)
            {
                // 1. Tank Physics (Movement & Collision)
                Physics.UpdatePlayer(Player, _currentInput, Map, Enemies.ActiveEnemies, Audio);

                // 2. Enemy AI & Movement
                Enemies.Update(Player, Map, Bullets, Audio, pts => Score += pts);

                // 3. Bullets & Explosions Update
                Bullets.Update(Player, Enemies.ActiveEnemies, Map, Audio, pts => Score += pts);

                // 4. Player respawn countdown if player died but still has lives remaining
                if (!Player.IsActive && Player.Lives > 0)
                {
                    _playerRespawnTimer++;
                    if (_playerRespawnTimer >= 60) // 1 second delay
                    {
                        _playerRespawnTimer = 0;
                        Player.Reset(4 * 16f, 12 * 16f);
                    }
                }

                // 5. Check Game Over Conditions (Eagle destroyed OR out of lives & inactive)
                if ((Map.IsEagleDestroyed || (Player.Lives <= 0 && !Player.IsActive)) && State != GameState.GameOver)
                {
                    State = GameState.GameOver;
                    Audio.Enqueue(AudioSoundEffect.Explosion);
                }
            }

            _accumulator -= MsPerFrame;
        }


        // Snapshot DTO Population (Zero-Allocation 60 FPS)
        _cachedFrame.PlayerX = Player.X;
        _cachedFrame.PlayerY = Player.Y;
        _cachedFrame.PlayerDir = (byte)Player.Direction;
        _cachedFrame.PlayerAnimFrame = Player.AnimFrame;
        _cachedFrame.PlayerShield = Player.ShieldActive;
        _cachedFrame.PlayerShieldFrame = Player.ShieldFrame;
        _cachedFrame.PlayerActive = Player.IsActive;
        _cachedFrame.PlayerStarPower = Player.StarPower;
        _cachedFrame.EagleDestroyed = Map.IsEagleDestroyed;
        _cachedFrame.IsPaused = IsPaused;
        _cachedFrame.Score = Score;
        _cachedFrame.Lives = Player.Lives;
        _cachedFrame.MapDirty = Map.IsDirty;
        _cachedFrame.EnemiesRemaining = Enemies.EnemiesRemaining;
        _cachedFrame.EnemiesActive = Enemies.ActiveEnemyCount;
        _cachedFrame.IsGameOver = State == GameState.GameOver;

        if (Map.IsDirty)
        {
            _cachedFrame.SubTiles = Map.GetSubTileBytes();
            Map.IsDirty = false;
        }
        else
        {
            _cachedFrame.SubTiles = null;
        }

        // Populate Active Enemies using pool
        _cachedFrame.Enemies.Clear();
        int enemyIdx = 0;
        foreach (var e in Enemies.ActiveEnemies)
        {
            if (e.IsActive)
            {
                if (enemyIdx >= _enemyDtoPool.Count)
                {
                    _enemyDtoPool.Add(new EnemyRenderDto());
                }
                var eDto = _enemyDtoPool[enemyIdx++];
                eDto.Id = e.Id;
                eDto.X = e.X;
                eDto.Y = e.Y;
                eDto.Dir = (byte)e.Direction;
                eDto.Type = (byte)e.Type;
                eDto.Hp = e.Hp;
                eDto.IsFlashing = e.IsFlashing;
                eDto.IsSpawning = e.IsSpawning;
                eDto.SpawnTimer = e.SpawnTimer;
                eDto.AnimFrame = e.AnimFrame;
                _cachedFrame.Enemies.Add(eDto);
            }
        }

        // Populate Bullets using pool
        _cachedFrame.Bullets.Clear();
        int bulletIdx = 0;
        foreach (var b in Bullets.ActiveBullets)
        {
            if (b.IsActive)
            {
                if (bulletIdx >= _bulletDtoPool.Count)
                {
                    _bulletDtoPool.Add(new BulletRenderDto());
                }
                var bDto = _bulletDtoPool[bulletIdx++];
                bDto.X = b.X;
                bDto.Y = b.Y;
                bDto.Dir = (byte)b.Direction;
                _cachedFrame.Bullets.Add(bDto);
            }
        }

        // Populate Explosions using pool
        _cachedFrame.Explosions.Clear();
        int explosionIdx = 0;
        foreach (var ex in Bullets.ActiveExplosions)
        {
            if (ex.IsActive)
            {
                if (explosionIdx >= _explosionDtoPool.Count)
                {
                    _explosionDtoPool.Add(new ExplosionRenderDto());
                }
                var exDto = _explosionDtoPool[explosionIdx++];
                exDto.X = ex.X;
                exDto.Y = ex.Y;
                exDto.Frame = ex.Frame;
                exDto.Big = ex.IsBig;
                _cachedFrame.Explosions.Add(exDto);
            }
        }

        _cachedFrame.AudioQueue = Audio.Flush();
        return _cachedFrame;
    }

    private readonly TelemetryData _cachedTelemetry = new();

    public TelemetryData GetTelemetry(int fps)
    {
        _cachedTelemetry.Fps = fps;
        _cachedTelemetry.X = (int)MathF.Round(Player.X);
        _cachedTelemetry.Y = (int)MathF.Round(Player.Y);
        _cachedTelemetry.Direction = Player.Direction.ToString().ToUpperInvariant();
        _cachedTelemetry.Shield = Player.ShieldActive;
        _cachedTelemetry.Lives = Player.Lives;
        _cachedTelemetry.Score = Score;
        _cachedTelemetry.EnemiesLeft = Enemies.EnemiesRemaining;
        _cachedTelemetry.EnemiesActive = Enemies.ActiveEnemyCount;
        _cachedTelemetry.IsGameOver = State == GameState.GameOver;
        return _cachedTelemetry;
    }

}

