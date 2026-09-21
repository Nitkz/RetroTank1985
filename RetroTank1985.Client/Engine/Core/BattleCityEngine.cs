using RetroTank1985.Shared.Enums;
using RetroTank1985.Client.Engine.Models;
using RetroTank1985.Client.Engine.Network;
using RetroTank1985.Shared.Models;
using RetroTank1985.Shared.Models.Network;

namespace RetroTank1985.Client.Engine.Core;

public interface IBattleCityEngine
{
    PlayerTank Player { get; }
    PlayerTank Player2 { get; }
    bool IsTwoPlayerMode { get; set; }
    int HighScore { get; set; }
    GameSettings Settings { get; }
    IDestructibleMap Map { get; }
    IBulletSystem Bullets { get; }
    IEnemySystem Enemies { get; }
    IPowerUpSystem PowerUps { get; }
    IAudioEventQueue Audio { get; }
    GameState State { get; }
    int CurrentStage { get; }
    int Score { get; }
    bool IsPaused { get; }
    bool IsNetworkGuest { get; set; }

    void ApplySettings(GameSettings settings);
    void InitializeStage(StageModel? stage, int stageNumber, bool preservePlayerState = false);
    void ResetPlayer();
    void SetTwoPlayerMode(bool enable);
    void TogglePause();
    void SetPause(bool paused);
    void SetInput(InputState input);
    void SetP1Input(bool up, bool down, bool left, bool right, bool fire, bool pause);
    void ApplyRemoteP2Input(PlayerInputPacket input);
    CoopSyncSnapshotDto CreateNetworkSnapshot(uint frameIndex, uint ackP2Seq = 0);
    void ApplyNetworkSnapshot(CoopSyncSnapshotDto snapshot);
    RenderFrameDto Tick(double timestampMs);
    TelemetryData GetTelemetry(int fps);

    // Sandbox & Debug Tools
    bool SpawnEnemyDebug(EnemyType type, int spawnPoint = -1, bool isFlashing = false);
    void NukeAllEnemies();
    void SimulateClearStage();
    void ClearEnemies();
    void SetPlayerStarPower(int starLevel, int playerIndex = 1);
    void TriggerEmote(RetroEmoteType emote, int playerIndex = 1);
    void SetDisconnectGracePeriod(bool active, float secondsRemaining = 15f, string disconnectedRole = "P2");
    void TogglePlayerShield(int playerIndex = 1);
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
    private bool _previousP1Fire = false;
    private bool _previousP2Fire = false;
    private bool _previousPause = false;

    private readonly RenderFrameDto _cachedFrame = new();
    private readonly List<BulletRenderDto> _bulletDtoPool = new(16);
    private readonly List<EnemyRenderDto> _enemyDtoPool = new(8);
    private readonly List<ExplosionRenderDto> _explosionDtoPool = new(16);
    private readonly List<PowerUpRenderDto> _powerUpDtoPool = new(4);
    private readonly List<ScorePopupRenderDto> _scorePopupDtoPool = new(8);

    private readonly List<PlayerTank> _playersList = new(2);

    private int _player1RespawnTimer = 0;
    private int _player2RespawnTimer = 0;
    private int _borrowLifeCooldownFrames = 0;
    private readonly List<byte> _pendingNetworkAudioEvents = new(8);

    // Stage Curtain & Tally State Variables
    private int _curtainTimer = 0;
    private const int CurtainDurationFrames = 75; // ~1.25s shutter wipe

    private int _tallyTimer = 0;
    private int _tallyStep = 0; // 0: init, 1: Basic, 2: Fast, 3: Power, 4: Armor, 5: summary, 6: done
    private int _tallyCountBasicP1 = 0;
    private int _tallyCountFastP1 = 0;
    private int _tallyCountPowerP1 = 0;
    private int _tallyCountArmorP1 = 0;

    private int _tallyCountBasicP2 = 0;
    private int _tallyCountFastP2 = 0;
    private int _tallyCountPowerP2 = 0;
    private int _tallyCountArmorP2 = 0;

    private int _tallyPostDelay = 0;
    private int _waveClearDelayTimer = 0;
    private int _gameOverDelayTimer = 0;
    private bool _gameOverSoundTriggered = false;
    private bool _nextStagePending = false;

    public PlayerTank Player { get; }
    public PlayerTank Player2 { get; }
    public bool IsTwoPlayerMode { get; set; } = false;
    public bool IsNetworkGuest { get; set; } = false;
    public int HighScore { get; set; } = 20000;
    public GameSettings Settings { get; private set; } = new();

    // Client-side Entity Interpolation states
    private NetworkEntityState _netStateP1 = new();
    private NetworkEntityState _netStateP2 = new();

    // Disconnect Grace Period State
    public bool IsDisconnectGracePeriodActive { get; private set; } = false;
    public float DisconnectGraceSecondsRemaining { get; private set; } = 15f;
    public string DisconnectedPeerRole { get; private set; } = "P2";

    public IDestructibleMap Map { get; }
    public ITankPhysics Physics { get; }
    public IBulletSystem Bullets { get; }
    public IEnemySystem Enemies { get; }
    public IPowerUpSystem PowerUps { get; }
    public IAudioEventQueue Audio { get; }

    public GameState State { get; private set; } = GameState.StageCurtain;
    public int CurrentStage { get; private set; } = 1;
    public int Score => Player.Score + (IsTwoPlayerMode ? Player2.Score : 0);
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

        Player = new PlayerTank { PlayerIndex = 1 };
        Player2 = new PlayerTank { PlayerIndex = 2 };

        ApplySettings(Settings);

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

    public void ApplySettings(GameSettings settings)
    {
        Settings = settings;

        // 1. Speeds & Fire Rate
        Player.Speed = PlayerTank.NormalSpeed * settings.GameSpeedMultiplier;
        Player2.Speed = PlayerTank.NormalSpeed * settings.GameSpeedMultiplier;

        Bullets.SpeedMultiplier = settings.GameSpeedMultiplier;
        Enemies.SpeedMultiplier = settings.GameSpeedMultiplier;
        Enemies.TotalWaveEnemies = settings.EnemyWaveSize;
        Enemies.FireIntervalFrames = settings.Preset == GameDifficultyPreset.KidsFriendly ? 55 : (settings.Preset == GameDifficultyPreset.Veteran ? 25 : 35);

        // 2. Armor & Lives
        Player.MaxHp = settings.PlayerArmorHp;
        Player.Hp = settings.PlayerArmorHp;

        Player2.MaxHp = settings.PlayerArmorHp;
        Player2.Hp = settings.PlayerArmorHp;
    }

    public void SetTwoPlayerMode(bool enable)
    {
        IsTwoPlayerMode = enable;
        Player2.IsActive = enable;
        if (enable && Player2.Lives <= 0)
        {
            Player2.Lives = Settings.StartingLives;
            Player2.Reset(8 * 16f, 12 * 16f);
        }
    }

    public void InitializeStage(StageModel? stage, int stageNumber, bool preservePlayerState = false)
    {
        CurrentStage = stageNumber;
        Map.LoadStage(stage?.Grid);
        Bullets.Clear();
        Audio.Clear();
        Enemies.InitializeWave(stage);
        PowerUps.Clear();

        if (Settings.FortifyEagleByDefault)
        {
            Map.FortifyEagleWithSteel(true);
        }
        
        _player1RespawnTimer = 0;
        _player2RespawnTimer = 0;
        _borrowLifeCooldownFrames = 0;
        _pendingNetworkAudioEvents.Clear();

        int p1Lives = preservePlayerState ? Math.Max(1, Player.Lives) : Settings.StartingLives;
        int p1Stars = preservePlayerState ? Player.StarPower : 0;
        Player.Lives = p1Lives;
        Player.MaxHp = Settings.PlayerArmorHp;
        Player.Hp = Settings.PlayerArmorHp; // Restore full armor HP on new stage
        Player.StarPower = p1Stars;
        Player.Reset(4 * 16f, 12 * 16f);

        int p2Lives = preservePlayerState ? Math.Max(1, Player2.Lives) : Settings.StartingLives;
        int p2Stars = preservePlayerState ? Player2.StarPower : 0;
        Player2.Lives = p2Lives;
        Player2.MaxHp = Settings.PlayerArmorHp;
        Player2.Hp = Settings.PlayerArmorHp;
        Player2.StarPower = p2Stars;
        Player2.Reset(8 * 16f, 12 * 16f);
        Player2.IsActive = IsTwoPlayerMode;

        _netStateP1.Reset();
        _netStateP2.Reset();

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
            Player.Lives = Settings.StartingLives;
        }
        _player1RespawnTimer = 0;
        _gameOverDelayTimer = 0;
        _gameOverSoundTriggered = false;
        Player.MaxHp = Settings.PlayerArmorHp;
        Player.Hp = Settings.PlayerArmorHp;
        Player.Reset(4 * 16f, 12 * 16f);

        if (IsTwoPlayerMode)
        {
            if (Player2.Lives <= 0)
            {
                Player2.Lives = Settings.StartingLives;
            }
            _player2RespawnTimer = 0;
            Player2.MaxHp = Settings.PlayerArmorHp;
            Player2.Hp = Settings.PlayerArmorHp;
            Player2.Reset(8 * 16f, 12 * 16f);
        }

        _netStateP1.Reset();
        _netStateP2.Reset();
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

    public void SetP1Input(bool up, bool down, bool left, bool right, bool fire, bool pause)
    {
        _currentInput.Up = up;
        _currentInput.Down = down;
        _currentInput.Left = left;
        _currentInput.Right = right;
        _currentInput.Fire = fire;
        _currentInput.Pause = pause;
    }

    public void ApplyRemoteP2Input(PlayerInputPacket input)
    {
        _currentInput.P2Up = input.Direction == 1;
        _currentInput.P2Right = input.Direction == 2;
        _currentInput.P2Down = input.Direction == 3;
        _currentInput.P2Left = input.Direction == 4;
        _currentInput.P2Fire = input.IsFiring;

        if (input.Emote != RetroEmoteType.None)
        {
            Player2.TriggerEmote(input.Emote);
            Audio.Enqueue(AudioSoundEffect.RadioChirp);
        }

        if ((input.BorrowLifeReq || input.IsFiring) && Player2.Lives <= 0 && !Player2.IsActive && Player.Lives >= 2 && _borrowLifeCooldownFrames <= 0)
        {
            _borrowLifeCooldownFrames = 120; // 2-second cooldown to prevent duplicate/spam deductions
            Player.Lives--;
            Player2.Lives = 1;
            Player2.Reset(8 * 16f, 12 * 16f);
            Audio.Enqueue(AudioSoundEffect.Life);
            _pendingNetworkAudioEvents.Add((byte)AudioSoundEffect.Life);
        }
    }

    public CoopSyncSnapshotDto CreateNetworkSnapshot(uint frameIndex, uint ackP2Seq = 0)
    {
        var snapshot = new CoopSyncSnapshotDto
        {
            FrameIndex = frameIndex,
            AckP2Sequence = ackP2Seq,
            RemainingEnemyWaveCount = Enemies.EnemiesRemaining,
            IsEagleDestroyed = Map.IsEagleDestroyed
        };

        // P1 Snapshot
        snapshot.Player1 = new TankNetworkSnapshot
        {
            X = Player.X,
            Y = Player.Y,
            Direction = (byte)Player.Direction,
            IsMoving = Player.IsMoving,
            IsActive = Player.IsActive,
            Lives = Player.Lives,
            Hp = Player.Hp,
            StarTier = Player.StarPower,
            ShieldTimeRemaining = Player.ShieldTimer,
            InvulnerableTimer = Player.InvulnerableTimer,
            IsDestroyed = !Player.IsActive || Player.Lives <= 0,
            ActiveEmote = Player.EmoteTimer > 0 ? Player.ActiveEmote : RetroEmoteType.None
        };

        // P2 Snapshot
        snapshot.Player2 = new TankNetworkSnapshot
        {
            X = Player2.X,
            Y = Player2.Y,
            Direction = (byte)Player2.Direction,
            IsMoving = Player2.IsMoving,
            IsActive = Player2.IsActive,
            Lives = Player2.Lives,
            Hp = Player2.Hp,
            StarTier = Player2.StarPower,
            ShieldTimeRemaining = Player2.ShieldTimer,
            InvulnerableTimer = Player2.InvulnerableTimer,
            IsDestroyed = !Player2.IsActive || Player2.Lives <= 0,
            ActiveEmote = Player2.EmoteTimer > 0 ? Player2.ActiveEmote : RetroEmoteType.None
        };

        // Enemies Snapshot
        var activeEnemies = Enemies.ActiveEnemies;
        var enemiesList = new List<EnemyNetworkSnapshot>(activeEnemies.Count);
        foreach (var enemy in activeEnemies)
        {
            enemiesList.Add(new EnemyNetworkSnapshot
            {
                Id = enemy.Id,
                TankType = (byte)enemy.Type,
                X = enemy.X,
                Y = enemy.Y,
                Direction = (byte)enemy.Direction,
                Health = enemy.Hp,
                IsFlashing = enemy.IsFlashing,
                IsSpawning = enemy.IsSpawning,
                SpawnAnimProgress = enemy.SpawnTimer
            });
        }
        snapshot.Enemies = enemiesList.ToArray();

        // Bullets Snapshot
        var activeBullets = Bullets.ActiveBullets;
        var bulletsList = new List<BulletNetworkSnapshot>(activeBullets.Count);
        int bId = 0;
        foreach (var b in activeBullets)
        {
            if (b.IsActive)
            {
                bulletsList.Add(new BulletNetworkSnapshot
                {
                    Id = bId++,
                    Owner = (byte)(b.OwnerPlayer == 1 ? 0 : (b.OwnerPlayer == 2 ? 1 : 2)),
                    X = b.X,
                    Y = b.Y,
                    Direction = (byte)b.Direction,
                    IsActive = b.IsActive
                });
            }
        }
        snapshot.Bullets = bulletsList.ToArray();

        // PowerUp
        var activePowerUps = PowerUps.ActivePowerUps;
        if (activePowerUps.Count > 0)
        {
            var p = activePowerUps[0];
            if (p.IsActive)
            {
                snapshot.ActivePowerUpType = (byte)p.Type;
                snapshot.PowerUpX = p.X;
                snapshot.PowerUpY = p.Y;
            }
        }

        // Destructible Map Sync
        if (Map.IsDirty || frameIndex % 30 == 0)
        {
            snapshot.SubTiles = Map.GetSubTileBytes();
        }

        // Synchronized GameState & Stage Tally Data (FEAT-03)
        snapshot.GameState = (byte)State;
        snapshot.StageNumber = CurrentStage;
        snapshot.TallyStep = _tallyStep;
        snapshot.TallyCountBasicP1 = _tallyCountBasicP1;
        snapshot.TallyCountFastP1 = _tallyCountFastP1;
        snapshot.TallyCountPowerP1 = _tallyCountPowerP1;
        snapshot.TallyCountArmorP1 = _tallyCountArmorP1;
        snapshot.TallyCountBasicP2 = _tallyCountBasicP2;
        snapshot.TallyCountFastP2 = _tallyCountFastP2;
        snapshot.TallyCountPowerP2 = _tallyCountPowerP2;
        snapshot.TallyCountArmorP2 = _tallyCountArmorP2;
        snapshot.ScoreP1 = Player.Score;
        snapshot.ScoreP2 = Player2.Score;

        // Pending Audio Events Sync (e.g. Power-Up Pickup SFX, Tally ticks)
        if (_pendingNetworkAudioEvents.Count > 0)
        {
            snapshot.AudioEvents = _pendingNetworkAudioEvents.ToArray();
            _pendingNetworkAudioEvents.Clear();
        }

        return snapshot;
    }

    public void ApplyNetworkSnapshot(CoopSyncSnapshotDto snapshot)
    {
        // Check if player died on this frame to trigger explosion locally on Guest
        if (Player.IsActive && !snapshot.Player1.IsActive)
        {
            Bullets.SpawnExplosion(Player.X, Player.Y, true);
            Audio.Enqueue(AudioSoundEffect.Explosion);
        }
        if (Player2.IsActive && !snapshot.Player2.IsActive)
        {
            Bullets.SpawnExplosion(Player2.X, Player2.Y, true);
            Audio.Enqueue(AudioSoundEffect.Explosion);
        }

        // Synchronize Game State & Stage Tally Screen from Host (FEAT-03)
        if (IsNetworkGuest)
        {
            State = (GameState)snapshot.GameState;
            CurrentStage = snapshot.StageNumber;
            _tallyStep = snapshot.TallyStep;
            _tallyCountBasicP1 = snapshot.TallyCountBasicP1;
            _tallyCountFastP1 = snapshot.TallyCountFastP1;
            _tallyCountPowerP1 = snapshot.TallyCountPowerP1;
            _tallyCountArmorP1 = snapshot.TallyCountArmorP1;
            _tallyCountBasicP2 = snapshot.TallyCountBasicP2;
            _tallyCountFastP2 = snapshot.TallyCountFastP2;
            _tallyCountPowerP2 = snapshot.TallyCountPowerP2;
            _tallyCountArmorP2 = snapshot.TallyCountArmorP2;
            Player.Score = snapshot.ScoreP1;
            Player2.Score = snapshot.ScoreP2;
        }

        // Apply Host Simulation to Guest with Smooth Interpolation Target
        _netStateP1.UpdateTarget(snapshot.Player1.X, snapshot.Player1.Y);
        Player.X = _netStateP1.CurrentX;
        Player.Y = _netStateP1.CurrentY;
        Player.Direction = (Direction)snapshot.Player1.Direction;
        Player.IsMoving = snapshot.Player1.IsMoving;
        Player.IsActive = snapshot.Player1.IsActive;
        Player.Lives = snapshot.Player1.Lives;
        Player.Hp = snapshot.Player1.Hp;
        Player.StarPower = snapshot.Player1.StarTier;
        Player.ShieldTimer = (int)snapshot.Player1.ShieldTimeRemaining;
        Player.ShieldActive = snapshot.Player1.ShieldTimeRemaining > 0;
        Player.InvulnerableTimer = snapshot.Player1.InvulnerableTimer;

        _netStateP2.UpdateTarget(snapshot.Player2.X, snapshot.Player2.Y);
        Player2.X = _netStateP2.CurrentX;
        Player2.Y = _netStateP2.CurrentY;
        Player2.Direction = (Direction)snapshot.Player2.Direction;
        Player2.IsMoving = snapshot.Player2.IsMoving;
        Player2.IsActive = snapshot.Player2.IsActive;
        Player2.Lives = snapshot.Player2.Lives;
        Player2.Hp = snapshot.Player2.Hp;
        Player2.StarPower = snapshot.Player2.StarTier;
        Player2.ShieldTimer = (int)snapshot.Player2.ShieldTimeRemaining;
        Player2.ShieldActive = snapshot.Player2.ShieldTimeRemaining > 0;
        Player2.InvulnerableTimer = snapshot.Player2.InvulnerableTimer;
        IsTwoPlayerMode = true;

        // Sync Emotes from Snapshot (Guest side)
        if (snapshot.Player1.ActiveEmote != RetroEmoteType.None && Player.ActiveEmote != snapshot.Player1.ActiveEmote)
        {
            Player.TriggerEmote(snapshot.Player1.ActiveEmote);
            Audio.Enqueue(AudioSoundEffect.RadioChirp);
        }
        if (snapshot.Player2.ActiveEmote != RetroEmoteType.None && Player2.ActiveEmote != snapshot.Player2.ActiveEmote)
        {
            Player2.TriggerEmote(snapshot.Player2.ActiveEmote);
            Audio.Enqueue(AudioSoundEffect.RadioChirp);
        }

        // Sync Bullets and Enemies to Guest Engine
        if (snapshot.Bullets != null)
        {
            Bullets.SyncFromNetwork(snapshot.Bullets);
        }
        if (snapshot.Enemies != null)
        {
            Enemies.SyncFromNetwork(snapshot.Enemies, snapshot.RemainingEnemyWaveCount);
        }

        // Sync Active Power-Up Item to Guest Engine
        PowerUps.SyncFromNetwork(snapshot.ActivePowerUpType, snapshot.PowerUpX, snapshot.PowerUpY);

        // Sync Destructible Map SubTiles from Host
        if (snapshot.SubTiles != null)
        {
            Map.LoadSubTileBytes(snapshot.SubTiles);
        }

        // Play Synchronized Audio Events on Guest (e.g. Power-Up Pickup SFX, Tally ticks)
        if (snapshot.AudioEvents != null)
        {
            foreach (var sfxByte in snapshot.AudioEvents)
            {
                var sfx = (AudioSoundEffect)sfxByte;
                if (sfx != AudioSoundEffect.None)
                {
                    Audio.Enqueue(sfx);
                }
            }
        }
    }

    public bool SpawnEnemyDebug(EnemyType type, int spawnPoint = -1, bool isFlashing = false)
    {
        return Enemies.TrySpawnEnemyDebug(type, spawnPoint, isFlashing);
    }

    public void NukeAllEnemies()
    {
        Enemies.NukeAllEnemies(Bullets, Audio, pts => Player.Score += pts);
    }

    public void SimulateClearStage()
    {
        Enemies.SimulateClearAllEnemies(Bullets, Audio, pts => Player.Score += pts);
    }

    public void ClearEnemies()
    {
        Enemies.Clear();
    }

    public void SetPlayerStarPower(int starLevel, int playerIndex = 1)
    {
        if (playerIndex == 2)
        {
            Player2.StarPower = Math.Clamp(starLevel, 0, 3);
        }
        else
        {
            Player.StarPower = Math.Clamp(starLevel, 0, 3);
        }
    }

    public void TriggerEmote(RetroEmoteType emote, int playerIndex = 1)
    {
        if (emote == RetroEmoteType.None) return;
        var target = playerIndex == 2 ? Player2 : Player;
        target.TriggerEmote(emote);
        Audio.Enqueue(AudioSoundEffect.RadioChirp);
    }

    public void SetDisconnectGracePeriod(bool active, float secondsRemaining = 15f, string disconnectedRole = "P2")
    {
        IsDisconnectGracePeriodActive = active;
        DisconnectGraceSecondsRemaining = secondsRemaining;
        DisconnectedPeerRole = disconnectedRole;

        if (active)
        {
            if (State == GameState.Playing)
            {
                State = GameState.Paused;
                Audio.Enqueue(AudioSoundEffect.Pause);
                Audio.Enqueue(AudioSoundEffect.EngineStop);
            }
        }
        else
        {
            if (State == GameState.Paused)
            {
                State = GameState.Playing;
                Audio.Enqueue(AudioSoundEffect.Pause);
            }
        }
    }

    public void TogglePlayerShield(int playerIndex = 1)
    {
        var target = playerIndex == 2 ? Player2 : Player;
        target.ShieldActive = !target.ShieldActive;
        if (target.ShieldActive) target.ShieldTimer = 600; // 10s
    }

    public void ToggleEagleSteel(bool fortified)
    {
        Map.FortifyEagleWithSteel(fortified);
    }

    public void SpawnPowerUpDebug(PowerUpType type)
    {
        PowerUps.SpawnPowerUpDebug(type, audioQueue: Audio);
        _pendingNetworkAudioEvents.Add((byte)AudioSoundEffect.BonusAppear);
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
        _tallyCountBasicP1 = 0;
        _tallyCountFastP1 = 0;
        _tallyCountPowerP1 = 0;
        _tallyCountArmorP1 = 0;
        _tallyCountBasicP2 = 0;
        _tallyCountFastP2 = 0;
        _tallyCountPowerP2 = 0;
        _tallyCountArmorP2 = 0;
        _tallyPostDelay = 0;
        _nextStagePending = false;

        Audio.Enqueue(AudioSoundEffect.StageClear);
        _pendingNetworkAudioEvents.Add((byte)AudioSoundEffect.StageClear);
    }

    private void UpdateStageTally()
    {
        _tallyTimer++;

        // Step 0: Initial delay (45 frames)
        if (_tallyStep == 0)
        {
            if (_tallyTimer >= 45)
            {
                _tallyStep = 1;
                _tallyTimer = 0;
            }
            return;
        }

        // Step 1: Count Basic Tanks
        if (_tallyStep == 1)
        {
            int targetP1 = Enemies.KillsByTypeP1[(int)EnemyType.Basic];
            int targetP2 = Enemies.KillsByTypeP2[(int)EnemyType.Basic];
            bool doneP1 = _tallyCountBasicP1 >= targetP1;
            bool doneP2 = !IsTwoPlayerMode || _tallyCountBasicP2 >= targetP2;

            if (!doneP1 || !doneP2)
            {
                if (_tallyTimer >= 8)
                {
                    _tallyTimer = 0;
                    if (!doneP1) _tallyCountBasicP1++;
                    if (!doneP2) _tallyCountBasicP2++;
                    Audio.Enqueue(AudioSoundEffect.TallyTick);
                    _pendingNetworkAudioEvents.Add((byte)AudioSoundEffect.TallyTick);
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
            int targetP1 = Enemies.KillsByTypeP1[(int)EnemyType.Fast];
            int targetP2 = Enemies.KillsByTypeP2[(int)EnemyType.Fast];
            bool doneP1 = _tallyCountFastP1 >= targetP1;
            bool doneP2 = !IsTwoPlayerMode || _tallyCountFastP2 >= targetP2;

            if (!doneP1 || !doneP2)
            {
                if (_tallyTimer >= 8)
                {
                    _tallyTimer = 0;
                    if (!doneP1) _tallyCountFastP1++;
                    if (!doneP2) _tallyCountFastP2++;
                    Audio.Enqueue(AudioSoundEffect.TallyTick);
                    _pendingNetworkAudioEvents.Add((byte)AudioSoundEffect.TallyTick);
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
            int targetP1 = Enemies.KillsByTypeP1[(int)EnemyType.Power];
            int targetP2 = Enemies.KillsByTypeP2[(int)EnemyType.Power];
            bool doneP1 = _tallyCountPowerP1 >= targetP1;
            bool doneP2 = !IsTwoPlayerMode || _tallyCountPowerP2 >= targetP2;

            if (!doneP1 || !doneP2)
            {
                if (_tallyTimer >= 8)
                {
                    _tallyTimer = 0;
                    if (!doneP1) _tallyCountPowerP1++;
                    if (!doneP2) _tallyCountPowerP2++;
                    Audio.Enqueue(AudioSoundEffect.TallyTick);
                    _pendingNetworkAudioEvents.Add((byte)AudioSoundEffect.TallyTick);
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
            int targetP1 = Enemies.KillsByTypeP1[(int)EnemyType.Armor];
            int targetP2 = Enemies.KillsByTypeP2[(int)EnemyType.Armor];
            bool doneP1 = _tallyCountArmorP1 >= targetP1;
            bool doneP2 = !IsTwoPlayerMode || _tallyCountArmorP2 >= targetP2;

            if (!doneP1 || !doneP2)
            {
                if (_tallyTimer >= 8)
                {
                    _tallyTimer = 0;
                    if (!doneP1) _tallyCountArmorP1++;
                    if (!doneP2) _tallyCountArmorP2++;
                    Audio.Enqueue(AudioSoundEffect.TallyTick);
                    _pendingNetworkAudioEvents.Add((byte)AudioSoundEffect.TallyTick);
                }
            }
            else
            {
                if (_tallyTimer >= 15)
                {
                    _tallyStep = 5;
                    _tallyTimer = 0;
                    Audio.Enqueue(AudioSoundEffect.TallyDone);
                    _pendingNetworkAudioEvents.Add((byte)AudioSoundEffect.TallyDone);
                }
            }
            return;
        }

        // Step 5: Total Summary, Winner & Finish Delay
        if (_tallyStep == 5)
        {
            _tallyPostDelay++;
            // Give players ~5.0 seconds (~300 frames) to read results, or press Fire after ~1.5s (90 frames) to advance
            if (_tallyPostDelay >= 300 || ((_currentInput.Fire || _currentInput.P2Fire) && _tallyPostDelay >= 90))
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

        // Player 1 Fire trigger
        if (!IsNetworkGuest && _currentInput.Fire && !_previousP1Fire && State == GameState.Playing)
        {
            Bullets.TryFirePlayerBullet(Player, Audio);
        }
        _previousP1Fire = _currentInput.Fire;

        // Player 2 Fire trigger (Host or Local 2P only — Guest fires via input packet to Host)
        if (!IsNetworkGuest && IsTwoPlayerMode && _currentInput.P2Fire && !_previousP2Fire && State == GameState.Playing)
        {
            Bullets.TryFirePlayerBullet(Player2, Audio);
        }
        _previousP2Fire = _currentInput.P2Fire;

        // Build active players list for this tick
        _playersList.Clear();
        if (Player.IsActive || Player.Lives > 0) _playersList.Add(Player);
        if (IsTwoPlayerMode && (Player2.IsActive || Player2.Lives > 0)) _playersList.Add(Player2);

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
                // Host handles Tally state machine and broadcasts snapshot; Guest observes
                if (!IsNetworkGuest)
                {
                    UpdateStageTally();
                }
            }
            else if (State == GameState.Playing)
            {
                // 1. Tank Physics (P1 and P2 Movement & Collision)
                Physics.UpdatePlayer(
                    Player,
                    _currentInput.Up,
                    _currentInput.Down,
                    _currentInput.Left,
                    _currentInput.Right,
                    Map,
                    Enemies.ActiveEnemies,
                    IsTwoPlayerMode ? Player2 : null,
                    Audio);

                if (IsTwoPlayerMode)
                {
                    Physics.UpdatePlayer(
                        Player2,
                        _currentInput.P2Up,
                        _currentInput.P2Down,
                        _currentInput.P2Left,
                        _currentInput.P2Right,
                        Map,
                        Enemies.ActiveEnemies,
                        Player,
                        Audio);
                }

                // 1b. Check Borrow Life for P1 (from P2) or Local P2 (from P1)
                if (IsTwoPlayerMode && _borrowLifeCooldownFrames <= 0)
                {
                    // P1 borrows from P2 (Host or Local P1)
                    if (_currentInput.Fire && Player.Lives <= 0 && !Player.IsActive && Player2.Lives >= 2)
                    {
                        _borrowLifeCooldownFrames = 120;
                        Player2.Lives--;
                        Player.Lives = 1;
                        Player.Reset(4 * 16f, 12 * 16f);
                        Audio.Enqueue(AudioSoundEffect.Life);
                        _pendingNetworkAudioEvents.Add((byte)AudioSoundEffect.Life);
                    }
                    // Local P2 borrows from P1 (when not running as Network Guest)
                    else if (!IsNetworkGuest && _currentInput.P2Fire && Player2.Lives <= 0 && !Player2.IsActive && Player.Lives >= 2)
                    {
                        _borrowLifeCooldownFrames = 120;
                        Player.Lives--;
                        Player2.Lives = 1;
                        Player2.Reset(8 * 16f, 12 * 16f);
                        Audio.Enqueue(AudioSoundEffect.Life);
                        _pendingNetworkAudioEvents.Add((byte)AudioSoundEffect.Life);
                    }
                }

                // 2. Enemy AI & Movement (Host & Solo only — Guest synchronizes from Host Snapshot)
                if (!IsNetworkGuest)
                {
                    Enemies.Update(_playersList, Map, Bullets, Audio, (enemy, ownerPlayer) => 
                    {
                        if (ownerPlayer == 2)
                        {
                            Player2.Score += enemy.PointValue;
                        }
                        else
                        {
                            Player.Score += enemy.PointValue;
                        }

                        Enemies.RecordKill(enemy.Type, ownerPlayer);
                        if (enemy.IsFlashing)
                        {
                            PowerUps.DropRandomPowerUp(enemy.X, enemy.Y, Audio);
                            _pendingNetworkAudioEvents.Add((byte)AudioSoundEffect.BonusAppear);
                        }
                    });

                    // 3. Bullets & Explosions Update
                    Bullets.Update(_playersList, Enemies.ActiveEnemies, Map, Audio, (enemy, ownerPlayer) =>
                    {
                        if (ownerPlayer == 2)
                        {
                            Player2.Score += enemy.PointValue;
                        }
                        else
                        {
                            Player.Score += enemy.PointValue;
                        }

                        Enemies.RecordKill(enemy.Type, ownerPlayer);
                        if (enemy.IsFlashing)
                        {
                            PowerUps.DropRandomPowerUp(enemy.X, enemy.Y, Audio);
                            _pendingNetworkAudioEvents.Add((byte)AudioSoundEffect.BonusAppear);
                        }
                    });

                    // 4. Power-Up System Update
                    PowerUps.Update(_playersList, Enemies, Map, Bullets, Audio, (pts, playerIdx) =>
                    {
                        if (playerIdx == 2) Player2.Score += pts;
                        else Player.Score += pts;
                    }, (pType, playerIdx) =>
                    {
                        // Record audio event to broadcast to network (Guest)
                        var sfx = pType == PowerUpType.TankLife ? AudioSoundEffect.Life : AudioSoundEffect.Bonus;
                        _pendingNetworkAudioEvents.Add((byte)sfx);
                    });
                }

                // 5. Destructible Map Shovel countdown update
                Map.UpdateShovelTimer();

                // 6. Player 1 Respawn Countdown
                if (!Player.IsActive && Player.Lives > 0)
                {
                    _player1RespawnTimer++;
                    if (_player1RespawnTimer >= 60) // 1 second delay
                    {
                        _player1RespawnTimer = 0;
                        Player.Reset(4 * 16f, 12 * 16f);
                    }
                }

                // Player 2 Respawn Countdown
                if (IsTwoPlayerMode && !Player2.IsActive && Player2.Lives > 0)
                {
                    _player2RespawnTimer++;
                    if (_player2RespawnTimer >= 60)
                    {
                        _player2RespawnTimer = 0;
                        Player2.Reset(8 * 16f, 12 * 16f);
                    }
                }

                // 6b. Player Emote Timers countdown
                if (Player.EmoteTimer > 0)
                {
                    Player.EmoteTimer--;
                    if (Player.EmoteTimer <= 0) Player.ActiveEmote = RetroEmoteType.None;
                }
                if (Player2.EmoteTimer > 0)
                {
                    Player2.EmoteTimer--;
                    if (Player2.EmoteTimer <= 0) Player2.ActiveEmote = RetroEmoteType.None;
                }

                // 6c. Borrow Life Cooldown decrement
                if (_borrowLifeCooldownFrames > 0)
                {
                    _borrowLifeCooldownFrames--;
                }

                // 7. Check Stage Cleared Condition
                if (Enemies.IsWaveCleared)
                {
                    _waveClearDelayTimer++;
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

                // 8. Check Game Over Conditions
                bool p1Dead = Player.Lives <= 0 && !Player.IsActive;
                bool p2Dead = !IsTwoPlayerMode || (Player2.Lives <= 0 && !Player2.IsActive);
                bool isGameOverCondition = Map.IsEagleDestroyed || (p1Dead && p2Dead);

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

        // Apply smooth lerp interpolation for remote network replication
        if (_netStateP1.Initialized)
        {
            _netStateP1.Interpolate(0.40f);
            Player.X = _netStateP1.CurrentX;
            Player.Y = _netStateP1.CurrentY;
        }
        if (_netStateP2.Initialized)
        {
            _netStateP2.Interpolate(0.40f);
            Player2.X = _netStateP2.CurrentX;
            Player2.Y = _netStateP2.CurrentY;
        }

        // Live High Score Update
        if (Score > HighScore)
        {
            HighScore = Score;
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
        _cachedFrame.PlayerHp = Player.Hp;
        _cachedFrame.PlayerMaxHp = Player.MaxHp;
        _cachedFrame.PlayerInvulnerable = Player.InvulnerableTimer > 0;
        _cachedFrame.PlayerEmote = (byte)(Player.EmoteTimer > 0 ? Player.ActiveEmote : RetroEmoteType.None);
        _cachedFrame.Lives = Player.Lives;
        _cachedFrame.Score = Player.Score;

        // Player 2 Properties
        _cachedFrame.IsTwoPlayer = IsTwoPlayerMode;
        _cachedFrame.Player2X = Player2.X;
        _cachedFrame.Player2Y = Player2.Y;
        _cachedFrame.Player2Dir = (byte)Player2.Direction;
        _cachedFrame.Player2AnimFrame = Player2.AnimFrame;
        _cachedFrame.Player2Shield = Player2.ShieldActive;
        _cachedFrame.Player2ShieldFrame = Player2.ShieldFrame;
        _cachedFrame.Player2Active = IsTwoPlayerMode && Player2.IsActive;
        _cachedFrame.Player2StarPower = Player2.StarPower;
        _cachedFrame.Player2Hp = Player2.Hp;
        _cachedFrame.Player2MaxHp = Player2.MaxHp;
        _cachedFrame.Player2Invulnerable = Player2.InvulnerableTimer > 0;
        _cachedFrame.Player2Emote = (byte)(Player2.EmoteTimer > 0 ? Player2.ActiveEmote : RetroEmoteType.None);
        _cachedFrame.Player2Lives = Player2.Lives;
        _cachedFrame.Player2Score = Player2.Score;

        _cachedFrame.HighScore = HighScore;
        _cachedFrame.EagleDestroyed = Map.IsEagleDestroyed;
        _cachedFrame.IsPaused = IsPaused;
        _cachedFrame.MapDirty = Map.IsDirty;
        _cachedFrame.EnemiesRemaining = Enemies.EnemiesRemaining;
        _cachedFrame.EnemiesActive = Enemies.ActiveEnemyCount;
        _cachedFrame.IsGameOver = State == GameState.GameOver;
        _cachedFrame.GameState = (byte)State;
        _cachedFrame.StageNumber = CurrentStage;
        _cachedFrame.CurtainProgress = Math.Clamp(1.0f - ((float)_curtainTimer / CurtainDurationFrames), 0f, 1f);

        // Kills & Tally info
        _cachedFrame.KillsBasic = Enemies.KillsByTypeP1[(int)EnemyType.Basic];
        _cachedFrame.KillsFast = Enemies.KillsByTypeP1[(int)EnemyType.Fast];
        _cachedFrame.KillsPower = Enemies.KillsByTypeP1[(int)EnemyType.Power];
        _cachedFrame.KillsArmor = Enemies.KillsByTypeP1[(int)EnemyType.Armor];

        _cachedFrame.KillsBasicP2 = Enemies.KillsByTypeP2[(int)EnemyType.Basic];
        _cachedFrame.KillsFastP2 = Enemies.KillsByTypeP2[(int)EnemyType.Fast];
        _cachedFrame.KillsPowerP2 = Enemies.KillsByTypeP2[(int)EnemyType.Power];
        _cachedFrame.KillsArmorP2 = Enemies.KillsByTypeP2[(int)EnemyType.Armor];

        _cachedFrame.TallyStep = _tallyStep;
        _cachedFrame.TallyCountBasic = _tallyCountBasicP1;
        _cachedFrame.TallyCountFast = _tallyCountFastP1;
        _cachedFrame.TallyCountPower = _tallyCountPowerP1;
        _cachedFrame.TallyCountArmor = _tallyCountArmorP1;

        _cachedFrame.TallyCountBasicP2 = _tallyCountBasicP2;
        _cachedFrame.TallyCountFastP2 = _tallyCountFastP2;
        _cachedFrame.TallyCountPowerP2 = _tallyCountPowerP2;
        _cachedFrame.TallyCountArmorP2 = _tallyCountArmorP2;

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
        _cachedTelemetry.Hp = Player.Hp;
        _cachedTelemetry.MaxHp = Player.MaxHp;
        _cachedTelemetry.Score = Player.Score;

        _cachedTelemetry.IsTwoPlayer = IsTwoPlayerMode;
        _cachedTelemetry.P2X = (int)MathF.Round(Player2.X);
        _cachedTelemetry.P2Y = (int)MathF.Round(Player2.Y);
        _cachedTelemetry.P2Lives = Player2.Lives;
        _cachedTelemetry.P2Hp = Player2.Hp;
        _cachedTelemetry.P2MaxHp = Player2.MaxHp;
        _cachedTelemetry.P2Score = Player2.Score;
        _cachedTelemetry.HighScore = HighScore;

        _cachedTelemetry.EnemiesLeft = Enemies.EnemiesRemaining;
        _cachedTelemetry.EnemiesActive = Enemies.ActiveEnemyCount;
        _cachedTelemetry.IsGameOver = State == GameState.GameOver;
        return _cachedTelemetry;
    }
}
