namespace RetroTank1985.Client.Models;

public enum TileType
{
    BrickTL = 0,
    BrickTR = 1,
    BrickBL = 2,
    BrickBR = 3,
    Brick = 4,
    SteelTL = 5,
    SteelTR = 6,
    SteelBL = 7,
    SteelBR = 8,
    Steel = 9,
    Water = 10,
    Trees = 11,
    Ice = 12,
    Empty = 13
}

public enum EnemyType
{
    Basic = 0,
    Fast = 1,
    Power = 2,
    Armor = 3
}

public class EnemyCounts
{
    public int Basic { get; set; }
    public int Fast { get; set; }
    public int Power { get; set; }
    public int Armor { get; set; }

    public int Total => Basic + Fast + Power + Armor;
}

public class StageModel
{
    public int StageNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<List<int>> Grid { get; set; } = new();
    public EnemyCounts EnemyCounts { get; set; } = new();
    public List<int> SpawnOrder { get; set; } = new();
}

public class StageManifestItem
{
    public int StageNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public int TotalEnemies { get; set; }
    public EnemyCounts EnemyCounts { get; set; } = new();
}

public class StageManifest
{
    public int TotalStages { get; set; }
    public List<StageManifestItem> Stages { get; set; } = new();
}
