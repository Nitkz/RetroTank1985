using System.Text.Json;
using Microsoft.JSInterop;

namespace RetroTank1985.Client.Services;

public class AudioPreferences
{
    public bool IsMuted { get; set; } = false;
    public float SfxVolume { get; set; } = 1.0f;
    public float BgmVolume { get; set; } = 1.0f;
}

public class GameStorageService
{
    private const string HighScoreKey = "retrotank_highscore";
    private const string MaxStageKey = "retrotank_max_stage";
    private const string AudioPrefsKey = "retrotank_audio_prefs";

    private readonly IJSRuntime _js;

    public GameStorageService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<int> GetHighScoreAsync()
    {
        try
        {
            var val = await _js.InvokeAsync<string?>("localStorage.getItem", HighScoreKey);
            if (int.TryParse(val, out int score))
            {
                return Math.Max(20000, score);
            }
        }
        catch
        {
            // Ignore if localStorage unavailable (e.g. SSR or incognito restrictions)
        }
        return 20000;
    }

    public async Task SaveHighScoreAsync(int score)
    {
        try
        {
            int current = await GetHighScoreAsync();
            if (score > current)
            {
                await _js.InvokeVoidAsync("localStorage.setItem", HighScoreKey, score.ToString());
            }
        }
        catch
        {
            // Ignore
        }
    }

    public async Task<int> GetMaxStageAsync()
    {
        try
        {
            var val = await _js.InvokeAsync<string?>("localStorage.getItem", MaxStageKey);
            if (int.TryParse(val, out int stage))
            {
                return Math.Clamp(stage, 1, 35);
            }
        }
        catch
        {
            // Ignore
        }
        return 1;
    }

    public async Task SaveMaxStageAsync(int stage)
    {
        try
        {
            int current = await GetMaxStageAsync();
            if (stage > current)
            {
                await _js.InvokeVoidAsync("localStorage.setItem", MaxStageKey, Math.Clamp(stage, 1, 35).ToString());
            }
        }
        catch
        {
            // Ignore
        }
    }

    public async Task<AudioPreferences> GetAudioPreferencesAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", AudioPrefsKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var prefs = JsonSerializer.Deserialize<AudioPreferences>(json);
                if (prefs != null) return prefs;
            }
        }
        catch
        {
            // Ignore
        }
        return new AudioPreferences();
    }

    public async Task SaveAudioPreferencesAsync(AudioPreferences prefs)
    {
        try
        {
            var json = JsonSerializer.Serialize(prefs);
            await _js.InvokeVoidAsync("localStorage.setItem", AudioPrefsKey, json);
        }
        catch
        {
            // Ignore
        }
    }
}
