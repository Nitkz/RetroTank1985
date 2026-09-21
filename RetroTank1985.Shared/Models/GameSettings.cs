namespace RetroTank1985.Shared.Models;

public enum GameDifficultyPreset
{
    KidsFriendly = 0, // 5 Lives, 3 Armor HP, 0.8x Speed, relaxed AI, Eagle fortified
    Classic1985 = 1,  // 3 Lives, 1 Armor HP, 1.0x Speed, authentic NES AI
    Veteran = 2,      // 1 Life, 1 Armor HP, 1.2x Speed, fast aggressive AI
    Custom = 3        // User-configured settings
}

public class GameSettings
{
    public GameDifficultyPreset Preset { get; set; } = GameDifficultyPreset.Classic1985;

    // Movement & bullet speed multiplier (0.7f to 1.3f)
    public float GameSpeedMultiplier { get; set; } = 1.0f;

    // Player initial lives (1 to 10)
    public int StartingLives { get; set; } = 3;

    // Player armor HP (1 = 1-hit kill, 2 = 1 hit absorb, 3 = 2 hits absorb, 5 = Kid Mega Armor)
    public int PlayerArmorHp { get; set; } = 1;

    // Fortify eagle with steel by default
    public bool FortifyEagleByDefault { get; set; } = false;

    // Enemy wave size (10, 15, or 20)
    public int EnemyWaveSize { get; set; } = 20;

    public void ApplyPreset(GameDifficultyPreset preset)
    {
        Preset = preset;
        switch (preset)
        {
            case GameDifficultyPreset.KidsFriendly:
                GameSpeedMultiplier = 0.8f;
                StartingLives = 5;
                PlayerArmorHp = 3;
                FortifyEagleByDefault = true;
                EnemyWaveSize = 15;
                break;

            case GameDifficultyPreset.Classic1985:
                GameSpeedMultiplier = 1.0f;
                StartingLives = 3;
                PlayerArmorHp = 1;
                FortifyEagleByDefault = false;
                EnemyWaveSize = 20;
                break;

            case GameDifficultyPreset.Veteran:
                GameSpeedMultiplier = 1.2f;
                StartingLives = 1;
                PlayerArmorHp = 1;
                FortifyEagleByDefault = false;
                EnemyWaveSize = 20;
                break;

            case GameDifficultyPreset.Custom:
                // Retain current custom values
                break;
        }
    }
}
