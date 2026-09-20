namespace RetroTank1985.Client.Engine.Enums;

/// <summary>
/// Audio events triggered by C# Game Brain and executed by Web Audio APU.
/// </summary>
public enum AudioSoundEffect : byte
{
    None = 0,
    Shot = 1,
    HitBrick = 2,
    HitSteel = 3,
    Explosion = 4,
    EagleHit = 5,
    Pause = 6,
    IntroBgm = 7,
    EngineStart = 8,
    EngineStop = 9,
    Bonus = 10,
    Life = 11,
    HitArmor = 12
}

/// <summary>
/// Overall state of the Battle City match.
/// </summary>
public enum GameState : byte
{
    Ready = 0,
    Playing = 1,
    Paused = 2,
    StageCurtain = 3,
    StageCleared = 4,
    GameOver = 5
}
