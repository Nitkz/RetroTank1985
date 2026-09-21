using MudBlazor;
using RetroTank1985.Shared.Models;

namespace RetroTank1985.Client.Helpers;

/// <summary>
/// Utility helper for formatting game UI elements, difficulty badges, and armor HP colors.
/// </summary>
public static class GameUiHelper
{
    public static string GetDifficultyLabel(GameDifficultyPreset preset) => preset switch
    {
        GameDifficultyPreset.KidsFriendly => "🐣 KIDS EASY",
        GameDifficultyPreset.Classic1985 => "🕹️ CLASSIC 1985",
        GameDifficultyPreset.Veteran => "⚔️ VETERAN",
        GameDifficultyPreset.Custom => "⚙️ CUSTOM",
        _ => "NORMAL"
    };

    public static Color GetDifficultyColor(GameDifficultyPreset preset) => preset switch
    {
        GameDifficultyPreset.KidsFriendly => Color.Success,
        GameDifficultyPreset.Classic1985 => Color.Warning,
        GameDifficultyPreset.Veteran => Color.Error,
        GameDifficultyPreset.Custom => Color.Info,
        _ => Color.Default
    };

    public static string GetHpBorderColor(int hp) => hp switch
    {
        <= 1 => "#ef4444",
        2 => "#f59e0b",
        3 => "#10b981",
        _ => "#06b6d4"
    };
}
