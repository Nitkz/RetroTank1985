using System.Net.Http.Json;
using RetroTank1985.Shared.Models;

namespace RetroTank1985.Client.Services;

public class StageService
{
    private readonly HttpClient _http;
    private StageManifest? _cachedManifest;
    private readonly Dictionary<int, StageModel> _cachedStages = new();

    public StageService(HttpClient http)
    {
        _http = http;
    }

    public async Task<StageManifest?> GetManifestAsync()
    {
        if (_cachedManifest != null)
            return _cachedManifest;

        try
        {
            _cachedManifest = await _http.GetFromJsonAsync<StageManifest>("data/stages/manifest.json");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading stages manifest: {ex.Message}");
        }

        return _cachedManifest;
    }

    public async Task<StageModel?> GetStageAsync(int stageNumber)
    {
        if (_cachedStages.TryGetValue(stageNumber, out var cached))
            return cached;

        try
        {
            var fileName = $"stage_{stageNumber:D2}.json";
            var stage = await _http.GetFromJsonAsync<StageModel>($"data/stages/{fileName}");
            if (stage != null)
            {
                _cachedStages[stageNumber] = stage;
                return stage;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading stage {stageNumber}: {ex.Message}");
        }

        return null;
    }
}
