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
    IPowerUpSystem PowerUps { get; }
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
    void SimulateClearStage();
    void ClearEnemies();
    void SetPlayerStarPower(int starLevel);
    void TogglePlayerShield();
    void ToggleEagleSteel(bool fortified);
    void SpawnPowerUpDebug(PowerUpType type);
    void TriggerStageCurtainDebug();
    void TriggerGameOverDebug();
    bool CheckNextStageReady(out int nextStageNum);
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
    private readonly List<PowerUpRenderDto> _powerUpDtoPool = new(4);
    private readonly List<ScorePopupRenderDto> _scorePopupDtoPool = new(8);

    private int _playerRespawnTimer = 0;

    // Stage Curtain & Tally State Variables
    private int _curtainTimer = 0;
    private const int CurtainDurationFrames = 75; // ~1.25s shutter wipe

    private int _tallyTimer = 0;
    private int _tallyStep = 0; // 0: init/delay, 1: counting Basic, 2: counting Fast, 3: counting Power, 4: counting Armor, 5: total & delay, 6: done
    private int _tallyCountBasic = 0;
    private int _tallyCountFast = 0;
    private int _tallyCountPower = 0;
    private int _tallyCountArmor = 0;
    private int _tallyPostDelay = 0;
    private int _waveClearDelayTimer = 0;
    private int _gameOverDelayTimer = 0;
    private bool _gameOverSoundTriggered = false;
    private bool _nextStagePending = false;

    public PlayerTank Player { get; }
    public IDestructibleMap Map { get; }
    public ITankPhysics Physics { get; }
    public IBulletSystem Bullets { get; }
    public IEnemySystem Enemies { get; }
    public IPowerUpSystem PowerUps { get; }
    public IAudioEventQueue Audio { get; }

    public GameState State { get; private set; } = GameState.StageCurtain;
    public int CurrentStage { get; private set; } = 1;
    public int Score { get; private set; } = 0;
    public bool IsPaused => State == GameState.Paused;

    public BattleCityEngine(
        IDestructibleMap map,
        ITankPhysics physics,
        IBulletSystem bullets,
        IEnemySystem enemies,
        IPowerUpSystem powerUps,
        IAudioEventQueue audio)
    {
        Map = map;
        Physics = physics;
        Bullets = bullets;
        Enemies = enemies;
        PowerUps = powerUps;
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
        for (int i = 0; i < 4; i++)
        {
            _powerUpDtoPool.Add(new PowerUpRenderDto());
        }
        for (int i = 0; i < 8; i++)
        {
            _scorePopupDtoPool.Add(new ScorePopupRenderDto());
        }
    }

    public void InitializeStage(StageModel? stage, int stageNumber)
    {
        CurrentStage = stageNumber;
        Map.LoadStage(stage?.Grid);
        Bullets.Clear();
        Audio.Clear();
        Enemies.InitializeWave(stage);
        PowerUps.Clear();
        
        _playerRespawnTimer = 0;
        Player.Lives = 3;
        Player.StarPower = 0;
        ResetPlayer();

        _curtainTimer = CurtainDurationFrames;
        State = GameState.StageCurtain;
        _accumulator = 0;
        _lastTimestamp = 0;
        _nextStagePending = false;
        _waveClearDelayTimer = 0;
        _gameOverDelayTimer = 0;
        _gameOverSoundTriggered = false;

        Audio.Enqueue(AudioSoundEffect.IntroBgm);
    }

    public void ResetPlayer()
    {
        if (Player.Lives <= 0)
        {
            Player.Lives = 3;
        }
        _playerRespawnTimer = 0;
        _gameOverDelayTimer = 0;
        _gameOverSoundTriggered = false;
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

    public void SimulateClearStage()
    {
        Enemies.SimulateClearAllEnemies(Bullets, Audio, pts => Score += pts);
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

    public void SpawnPowerUpDebug(PowerUpType type)
    {
        PowerUps.SpawnPowerUpDebug(type, audioQueue: Audio);
    }

    public void TriggerStageCurtainDebug()
    {
        _curtainTimer = CurtainDurationFrames;
        State = GameState.StageCurtain;
        Audio.Enqueue(AudioSoundEffect.IntroBgm);
    }

    public void TriggerGameOverDebug()
    {
        State = GameState.GameOver;
        Audio.Enqueue(AudioSoundEffect.EagleHit);
        Audio.Enqueue(AudioSoundEffect.GameOver);
    }

    public bool CheckNextStageReady(out int nextStageNum)
    {
        if (_nextStagePending)
        {
            _nextStagePending = false;
            nextStageNum = (CurrentStage % 35) + 1;
            return true;
        }
        nextStageNum = CurrentStage;
        return false;
    }

    private void StartStageTally()
    {
        State = GameState.StageTally;
        _tallyTimer = 0;
        _tallyStep = 0;
        _tallyCountBasic = 0;
        _tallyCountFast = 0;
        _tallyCountPower = 0;
        _tallyCountArmor = 0;
        _tallyPostDelay = 0;
        _nextStagePending = false;

        Audio.Enqueue(AudioSoundEffect.StageClear);
    }

    private void UpdateStageTally()
    {
        _tallyTimer++;

        // Step 0: Initial delay (60 frames)
        if (_tallyStep == 0)
        {
            if (_tallyTimer >= 45)
            {
                _tallyStep = 1;
                _tallyTimer = 0;
            }
            return;
        }

        // Step 1: Count Basic Tanks (Every 10 frames increment by 1)
        if (_tallyStep == 1)
        {
            int target = Enemies.KillsByType[(int)EnemyType.Basic];
            if (_tallyCountBasic < target)
            {
                if (_tallyTimer >= 8)
                {
                    _tallyTimer = 0;
                    _tallyCountBasic++;
                    Audio.Enqueue(AudioSoundEffect.TallyTick);
                }
            }
            else
            {
                if (_tallyTimer >= 15)
                {
                    _tallyStep = 2;
                    _tallyTimer = 0;
                }
            }
            return;
        }

        // Step 2: Count Fast Tanks
        if (_tallyStep == 2)
        {
            int target = Enemies.KillsByType[(int)EnemyType.Fast];
            if (_tallyCountFast < target)
            {
                if (_tallyTimer >= 8)
                {
                    _tallyTimer = 0;
                    _tallyCountFast++;
                    Audio.Enqueue(AudioSoundEffect.TallyTick);
                }
            }
            else
            {
                if (_tallyTimer >= 15)
                {
                    _tallyStep = 3;
                    _tallyTimer = 0;
                }
            }
            return;
        }

        // Step 3: Count Power Tanks
        if (_tallyStep == 3)
        {
            int target = Enemies.KillsByType[(int)EnemyType.Power];
            if (_tallyCountPower < target)
            {
                if (_tallyTimer >= 8)
                {
                    _tallyTimer = 0;
                    _tallyCountPower++;
                    Audio.Enqueue(AudioSoundEffect.TallyTick);
                }
            }
            else
            {
                if (_tallyTimer >= 15)
                {
                    _tallyStep = 4;
                    _tallyTimer = 0;
                }
            }
            return;
        }

        // Step 4: Count Armor Tanks
        if (_tallyStep == 4)
        {
            int target = Enemies.KillsByType[(int)EnemyType.Armor];
            if (_tallyCountArmor < target)
            {
                if (_tallyTimer >= 8)
                {
                    _tallyTimer = 0;
                    _tallyCountArmor++;
                    Audio.Enqueue(AudioSoundEffect.TallyTick);
                }
            }
            else
            {
                if (_tallyTimer >= 15)
                {
                    _tallyStep = 5;
                    _tallyTimer = 0;
                    Audio.Enqueue(AudioSoundEffect.TallyDone);
                }
            }
            return;
        }

        // Step 5: Total Summary & Finish Delay
        if (_tallyStep == 5)
        {
            _tallyPostDelay++;
            // Press Space/Fire to skip post delay or auto-advance after 3.5s (~210 frames)
            if (_tallyPostDelay >= 210 || (_currentInput.Fire && _tallyPostDelay >= 45))
            {
                _tallyStep = 6;
                _nextStagePending = true;
            }
        }
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
            if (State == GameState.StageCurtain)
            {
                if (_curtainTimer > 0)
                {
                    _curtainTimer--;
                }
                else
                {
                    State = GameState.Playing;
                }
            }
            else if (State == GameState.StageTally)
            {
                UpdateStageTally();
            }
            else if (State == GameState.Playing)
            {
                // 1. Tank Physics (Movement & Collision)
                Physics.UpdatePlayer(Player, _currentInput, Map, Enemies.ActiveEnemies, Audio);

                // 2. Enemy AI & Movement
                Enemies.Update(Player, Map, Bullets, Audio, enemy => 
                {
                    Score += enemy.PointValue;
                    Enemies.RecordKill(enemy.Type);
                    if (enemy.IsFlashing)
                    {
                        PowerUps.DropRandomPowerUp(enemy.X, enemy.Y, Audio);
                    }
                });

                // 3. Bullets & Explosions Update
                Bullets.Update(Player, Enemies.ActiveEnemies, Map, Audio, enemy =>
                {
                    Score += enemy.PointValue;
                    Enemies.RecordKill(enemy.Type);
                    if (enemy.IsFlashing)
                    {
                        PowerUps.DropRandomPowerUp(enemy.X, enemy.Y, Audio);
                    }
                });

                // 4. Power-Up System Update (Pickup collisions, effects, score popups)
                PowerUps.Update(Player, Enemies, Map, Bullets, Audio, pts => Score += pts);

                // 5. Destructible Map Shovel countdown update
                Map.UpdateShovelTimer();

                // 6. Player respawn countdown if player died but still has lives remaining
                if (!Player.IsActive && Player.Lives > 0)
                {
                    _playerRespawnTimer++;
                    if (_playerRespawnTimer >= 60) // 1 second delay
                    {
                        _playerRespawnTimer = 0;
                        Player.Reset(4 * 16f, 12 * 16f);
                    }
                }

                // 7. Check Stage Cleared Condition (All 20 enemies spawned & destroyed)
                if (Enemies.IsWaveCleared)
                {
                    _waveClearDelayTimer++;
                    // Authentic NES Delay: Allow last explosion animation (~30 frames) to finish 
                    // and give player ~1.5 seconds (90 frames) of cleared arena before transitioning
                    if (_waveClearDelayTimer >= 90)
                    {
                        _waveClearDelayTimer = 0;
                        StartStageTally();
                    }
                }
                else
                {
                    _waveClearDelayTimer = 0;
                }

                // 8. Check Game Over Conditions (Eagle destroyed OR out of lives & inactive)
                bool isGameOverCondition = Map.IsEagleDestroyed || (Player.Lives <= 0 && !Player.IsActive);
                if (isGameOverCondition)
                {
                    if (!_gameOverSoundTriggered)
                    {
                        _gameOverSoundTriggered = true;
                        if (Map.IsEagleDestroyed)
                        {
                            Audio.Enqueue(AudioSoundEffect.EagleHit);
                        }
                        Audio.Enqueue(AudioSoundEffect.GameOver);
                    }

                    _gameOverDelayTimer++;
                    // Authentic NES Delay (~120 frames / 2.0 seconds):
                    // Allows explosion animation to finish, phoenix destroyed state to show,
                    // before freezing gameplay or showing Game Over screen
                    if (_gameOverDelayTimer >= 120 && State != GameState.GameOver)
                    {
                        State = GameState.GameOver;
                    }
                }
                else
                {
                    _gameOverDelayTimer = 0;
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
        _cachedFrame.GameState = (byte)State;
        _cachedFrame.StageNumber = CurrentStage;
        _cachedFrame.CurtainProgress = Math.Clamp(1.0f - ((float)_curtainTimer / CurtainDurationFrames), 0f, 1f);

        // Kills & Tally info
        _cachedFrame.KillsBasic = Enemies.KillsByType[(int)EnemyType.Basic];
        _cachedFrame.KillsFast = Enemies.KillsByType[(int)EnemyType.Fast];
        _cachedFrame.KillsPower = Enemies.KillsByType[(int)EnemyType.Power];
        _cachedFrame.KillsArmor = Enemies.KillsByType[(int)EnemyType.Armor];
        _cachedFrame.TallyStep = _tallyStep;
        _cachedFrame.TallyCountBasic = _tallyCountBasic;
        _cachedFrame.TallyCountFast = _tallyCountFast;
        _cachedFrame.TallyCountPower = _tallyCountPower;
        _cachedFrame.TallyCountArmor = _tallyCountArmor;

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

        // Populate Active Power-Ups using pool
        _cachedFrame.PowerUps.Clear();
        int powerUpIdx = 0;
        foreach (var p in PowerUps.ActivePowerUps)
        {
            if (p.IsActive)
            {
                if (powerUpIdx >= _powerUpDtoPool.Count)
                {
                    _powerUpDtoPool.Add(new PowerUpRenderDto());
                }
                var pDto = _powerUpDtoPool[powerUpIdx++];
                pDto.X = p.X;
                pDto.Y = p.Y;
                pDto.Type = (byte)p.Type;
                pDto.Visible = p.IsVisible;
                _cachedFrame.PowerUps.Add(pDto);
            }
        }

        // Populate Floating Score Popups using pool
        _cachedFrame.ScorePopups.Clear();
        int scorePopupIdx = 0;
        foreach (var sp in PowerUps.ActiveScorePopups)
        {
            if (sp.IsActive)
            {
                if (scorePopupIdx >= _scorePopupDtoPool.Count)
                {
                    _scorePopupDtoPool.Add(new ScorePopupRenderDto());
                }
                var spDto = _scorePopupDtoPool[scorePopupIdx++];
                spDto.X = sp.X;
                spDto.Y = sp.Y;
                spDto.Score = sp.Score;
                _cachedFrame.ScorePopups.Add(spDto);
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

