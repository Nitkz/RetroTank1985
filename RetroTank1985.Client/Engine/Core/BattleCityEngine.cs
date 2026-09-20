using RetroTank1985.Client.Engine.Enums;
using RetroTank1985.Client.Engine.Models;

namespace RetroTank1985.Client.Engine.Core;

public interface IBattleCityEngine
{
    PlayerTank Player { get; }
    IDestructibleMap Map { get; }
    IBulletSystem Bullets { get; }
    IAudioEventQueue Audio { get; }
    GameState State { get; }
    int CurrentStage { get; }
    int Score { get; }
    bool IsPaused { get; }

    void InitializeStage(List<List<int>>? stageGrid, int stageNumber);
    void ResetPlayer();
    void TogglePause();
    void SetPause(bool paused);
    void SetInput(InputState input);
    RenderFrameDto Tick(double timestampMs);
    TelemetryData GetTelemetry(int fps);
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
    private readonly List<BulletRenderDto> _bulletDtoPool = new(8);
    private readonly List<ExplosionRenderDto> _explosionDtoPool = new(8);

    public PlayerTank Player { get; }
    public IDestructibleMap Map { get; }
    public ITankPhysics Physics { get; }
    public IBulletSystem Bullets { get; }
    public IAudioEventQueue Audio { get; }

    public GameState State { get; private set; } = GameState.Playing;
    public int CurrentStage { get; private set; } = 1;
    public int Score { get; private set; } = 0;
    public bool IsPaused => State == GameState.Paused;

    public BattleCityEngine(
        IDestructibleMap map,
        ITankPhysics physics,
        IBulletSystem bullets,
        IAudioEventQueue audio)
    {
        Map = map;
        Physics = physics;
        Bullets = bullets;
        Audio = audio;
        Player = new PlayerTank();

        // Pre-allocate pool objects
        for (int i = 0; i < 8; i++)
        {
            _bulletDtoPool.Add(new BulletRenderDto());
            _explosionDtoPool.Add(new ExplosionRenderDto());
        }
    }

    public void InitializeStage(List<List<int>>? stageGrid, int stageNumber)
    {
        CurrentStage = stageNumber;
        Map.LoadStage(stageGrid);
        Bullets.Clear();
        Audio.Clear();
        ResetPlayer();
        State = GameState.Playing;
        _accumulator = 0;
        _lastTimestamp = 0;

        Audio.Enqueue(AudioSoundEffect.IntroBgm);
    }

    public void ResetPlayer()
    {
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

    public RenderFrameDto Tick(double timestampMs)
    {
        if (_lastTimestamp <= 0)
        {
            _lastTimestamp = timestampMs;
        }

        double delta = timestampMs - _lastTimestamp;
        _lastTimestamp = timestampMs;

        // Clamp delta to avoid large spiral of death jumps if tab was backgrounded
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

        // Fixed 60 Hz Physics update steps
        while (_accumulator >= MsPerFrame)
        {
            if (State == GameState.Playing)
            {
                Physics.UpdatePlayer(Player, _currentInput, Map, Audio);
                Bullets.Update(Map, Audio);

                if (Map.IsEagleDestroyed && State != GameState.GameOver)
                {
                    State = GameState.GameOver;
                    Audio.Enqueue(AudioSoundEffect.Explosion);
                }
            }

            _accumulator -= MsPerFrame;
        }

        // Reuse cached Render Frame Snapshot DTO (Zero 60 FPS Heap Allocations)
        _cachedFrame.PlayerX = Player.X;
        _cachedFrame.PlayerY = Player.Y;
        _cachedFrame.PlayerDir = (byte)Player.Direction;
        _cachedFrame.PlayerAnimFrame = Player.AnimFrame;
        _cachedFrame.PlayerShield = Player.ShieldActive;
        _cachedFrame.PlayerShieldFrame = Player.ShieldFrame;
        _cachedFrame.PlayerActive = Player.IsActive;
        _cachedFrame.EagleDestroyed = Map.IsEagleDestroyed;
        _cachedFrame.IsPaused = IsPaused;
        _cachedFrame.Score = Score;
        _cachedFrame.Lives = Player.Lives;
        _cachedFrame.MapDirty = Map.IsDirty;

        if (Map.IsDirty)
        {
            _cachedFrame.SubTiles = Map.GetSubTileBytes();
            Map.IsDirty = false;
        }
        else
        {
            _cachedFrame.SubTiles = null;
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
        return _cachedTelemetry;
    }
}
