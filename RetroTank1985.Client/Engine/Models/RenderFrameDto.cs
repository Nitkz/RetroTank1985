using System.Text.Json.Serialization;

namespace RetroTank1985.Client.Engine.Models;

/// <summary>
/// Lightweight Frame Snapshot DTO transferred from C# Game Brain to JS Fast Canvas & Audio Muscle.
/// </summary>
public class RenderFrameDto
{
    [JsonPropertyName("pX")]
    public float PlayerX { get; set; }

    [JsonPropertyName("pY")]
    public float PlayerY { get; set; }

    [JsonPropertyName("pDir")]
    public byte PlayerDir { get; set; }

    [JsonPropertyName("pAnim")]
    public int PlayerAnimFrame { get; set; }

    [JsonPropertyName("pShield")]
    public bool PlayerShield { get; set; }

    [JsonPropertyName("pShieldFrame")]
    public int PlayerShieldFrame { get; set; }

    [JsonPropertyName("pActive")]
    public bool PlayerActive { get; set; }

    [JsonPropertyName("pStarPower")]
    public int PlayerStarPower { get; set; }

    [JsonPropertyName("pHp")]
    public int PlayerHp { get; set; } = 1;

    [JsonPropertyName("pMaxHp")]
    public int PlayerMaxHp { get; set; } = 1;

    [JsonPropertyName("pInvuln")]
    public bool PlayerInvulnerable { get; set; }

    // Player 2 Properties (Green Tank)
    [JsonPropertyName("isTwoPlayer")]
    public bool IsTwoPlayer { get; set; }

    [JsonPropertyName("p2X")]
    public float Player2X { get; set; }

    [JsonPropertyName("p2Y")]
    public float Player2Y { get; set; }

    [JsonPropertyName("p2Dir")]
    public byte Player2Dir { get; set; }

    [JsonPropertyName("p2Anim")]
    public int Player2AnimFrame { get; set; }

    [JsonPropertyName("p2Shield")]
    public bool Player2Shield { get; set; }

    [JsonPropertyName("p2ShieldFrame")]
    public int Player2ShieldFrame { get; set; }

    [JsonPropertyName("p2Active")]
    public bool Player2Active { get; set; }

    [JsonPropertyName("p2StarPower")]
    public int Player2StarPower { get; set; }

    [JsonPropertyName("p2Hp")]
    public int Player2Hp { get; set; } = 1;

    [JsonPropertyName("p2MaxHp")]
    public int Player2MaxHp { get; set; } = 1;

    [JsonPropertyName("p2Invuln")]
    public bool Player2Invulnerable { get; set; }

    [JsonPropertyName("p2Lives")]
    public int Player2Lives { get; set; }

    [JsonPropertyName("p2Score")]
    public int Player2Score { get; set; }

    [JsonPropertyName("highScore")]
    public int HighScore { get; set; }

    [JsonPropertyName("bullets")]
    public List<BulletRenderDto> Bullets { get; set; } = new();

    [JsonPropertyName("explosions")]
    public List<ExplosionRenderDto> Explosions { get; set; } = new();

    [JsonPropertyName("audioQueue")]
    public List<byte> AudioQueue { get; set; } = new();

    [JsonPropertyName("subTiles")]
    public byte[]? SubTiles { get; set; }

    [JsonPropertyName("mapDirty")]
    public bool MapDirty { get; set; }

    [JsonPropertyName("eagleDestroyed")]
    public bool EagleDestroyed { get; set; }

    [JsonPropertyName("isPaused")]
    public bool IsPaused { get; set; }

    [JsonPropertyName("fps")]
    public int Fps { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("lives")]
    public int Lives { get; set; }

    [JsonPropertyName("enemies")]
    public List<EnemyRenderDto> Enemies { get; set; } = new();

    [JsonPropertyName("enemiesRemaining")]
    public int EnemiesRemaining { get; set; }

    [JsonPropertyName("enemiesActive")]
    public int EnemiesActive { get; set; }

    [JsonPropertyName("isGameOver")]
    public bool IsGameOver { get; set; }

    [JsonPropertyName("gameState")]
    public byte GameState { get; set; }

    [JsonPropertyName("stageNumber")]
    public int StageNumber { get; set; }

    [JsonPropertyName("curtainProgress")]
    public float CurtainProgress { get; set; }

    // P1 Kills
    [JsonPropertyName("killsBasic")]
    public int KillsBasic { get; set; }

    [JsonPropertyName("killsFast")]
    public int KillsFast { get; set; }

    [JsonPropertyName("killsPower")]
    public int KillsPower { get; set; }

    [JsonPropertyName("killsArmor")]
    public int KillsArmor { get; set; }

    // P2 Kills
    [JsonPropertyName("killsBasicP2")]
    public int KillsBasicP2 { get; set; }

    [JsonPropertyName("killsFastP2")]
    public int KillsFastP2 { get; set; }

    [JsonPropertyName("killsPowerP2")]
    public int KillsPowerP2 { get; set; }

    [JsonPropertyName("killsArmorP2")]
    public int KillsArmorP2 { get; set; }

    [JsonPropertyName("tallyStep")]
    public int TallyStep { get; set; }

    // P1 Tally
    [JsonPropertyName("tallyCountBasic")]
    public int TallyCountBasic { get; set; }

    [JsonPropertyName("tallyCountFast")]
    public int TallyCountFast { get; set; }

    [JsonPropertyName("tallyCountPower")]
    public int TallyCountPower { get; set; }

    [JsonPropertyName("tallyCountArmor")]
    public int TallyCountArmor { get; set; }

    // P2 Tally
    [JsonPropertyName("tallyCountBasicP2")]
    public int TallyCountBasicP2 { get; set; }

    [JsonPropertyName("tallyCountFastP2")]
    public int TallyCountFastP2 { get; set; }

    [JsonPropertyName("tallyCountPowerP2")]
    public int TallyCountPowerP2 { get; set; }

    [JsonPropertyName("tallyCountArmorP2")]
    public int TallyCountArmorP2 { get; set; }

    [JsonPropertyName("powerUps")]
    public List<PowerUpRenderDto> PowerUps { get; set; } = new();

    [JsonPropertyName("scorePopups")]
    public List<ScorePopupRenderDto> ScorePopups { get; set; } = new();
}

public class PowerUpRenderDto
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("type")]
    public byte Type { get; set; }

    [JsonPropertyName("visible")]
    public bool Visible { get; set; }
}

public class ScorePopupRenderDto
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("score")]
    public int Score { get; set; }
}

public class BulletRenderDto
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("dir")]
    public byte Dir { get; set; }
}

public class EnemyRenderDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("dir")]
    public byte Dir { get; set; }

    [JsonPropertyName("type")]
    public byte Type { get; set; }

    [JsonPropertyName("hp")]
    public int Hp { get; set; }

    [JsonPropertyName("isFlashing")]
    public bool IsFlashing { get; set; }

    [JsonPropertyName("isSpawning")]
    public bool IsSpawning { get; set; }

    [JsonPropertyName("spawnTimer")]
    public int SpawnTimer { get; set; }

    [JsonPropertyName("anim")]
    public int AnimFrame { get; set; }
}

public class ExplosionRenderDto
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("frame")]
    public int Frame { get; set; }

    [JsonPropertyName("big")]
    public bool Big { get; set; }
}


