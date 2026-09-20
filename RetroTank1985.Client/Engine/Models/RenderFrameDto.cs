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
