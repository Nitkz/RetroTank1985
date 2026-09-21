using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace RetroTank1985.Client.Components.Inspector;

public partial class ChrTileCatalogTab : ComponentBase
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private int _selectedPalIdx = 0;

    public class TankPreviewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int EnemyType { get; set; }
        public int Dir { get; set; } = 0;
    }

    private List<TankPreviewModel> _previewTanks = new()
    {
        new() { Id = "p1", Name = "PLAYER 1", EnemyType = -1, Dir = 0 },
        new() { Id = "p2", Name = "PLAYER 2", EnemyType = -2, Dir = 0 },
        new() { Id = "t0", Name = "BASIC TANK", EnemyType = 0, Dir = 2 },
        new() { Id = "t1", Name = "FAST TANK", EnemyType = 1, Dir = 2 },
        new() { Id = "t2", Name = "POWER TANK", EnemyType = 2, Dir = 2 },
        new() { Id = "t3", Name = "ARMOR TANK", EnemyType = 3, Dir = 2 },
    };

    public class ArmorShowcaseModel
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Badge { get; set; } = string.Empty;
        public string TitleColor { get; set; } = "#ffffff";
        public string BorderColor { get; set; } = "#334155";
        public int ArmorLevel { get; set; } = 0; // 0=none, 1=Red(1HP), 2=Yellow(2HP), 3=Green(3HP), 4=Cyan(Kid Invuln)
        public int ShieldType { get; set; } = 0; // 0=none, 1=Classic NES, 2=Cyber Field
    }

    private List<ArmorShowcaseModel> _armorShowcaseTanks = new()
    {
        new() 
        { 
            Id = "arm_1hp", 
            Title = "1 HP (NO ARMOR)", 
            Description = "ตัวเปล่าดั้งเดิม โดน 1 นัดแตก", 
            Badge = "💥 1-HIT NORMAL", 
            TitleColor = "#94a3b8", 
            BorderColor = "#334155", 
            ArmorLevel = 1 
        },
        new() 
        { 
            Id = "arm_2hp", 
            Title = "2 HP (1x SHIELD)", 
            Description = "มีเกราะทองกันได้ 1 นัด", 
            Badge = "⚡ 2 HP (AMBER RING)", 
            TitleColor = "#fbbf24", 
            BorderColor = "#d97706", 
            ArmorLevel = 2 
        },
        new() 
        { 
            Id = "arm_3hp", 
            Title = "3 HP (2x SHIELD)", 
            Description = "มีเกราะเขียวกันได้ 2 นัด", 
            Badge = "🛡️ 3 HP (GREEN RING)", 
            TitleColor = "#34d399", 
            BorderColor = "#059669", 
            ArmorLevel = 3 
        },
        new() 
        { 
            Id = "arm_4hp", 
            Title = "5 HP (KIDS SAFE)", 
            Description = "เกราะฟ้าพิเศษสำหรับเด็ก", 
            Badge = "👶 5 HP (CYAN RING)", 
            TitleColor = "#38bdf8", 
            BorderColor = "#0284c7", 
            ArmorLevel = 4 
        },
        new() 
        { 
            Id = "shield_nes", 
            Title = "CLASSIC NES SHIELD", 
            Description = "ชิลด์อมตะเกิดใหม่ดั้งเดิม", 
            Badge = "✨ NES INVULN", 
            TitleColor = "#f1c40f", 
            BorderColor = "#ca8a04", 
            ShieldType = 1 
        },
        new() 
        { 
            Id = "shield_cyber", 
            Title = "FORCE FIELD DUAL", 
            Description = "เกราะเขียว + ชิลด์ไฮเทค", 
            Badge = "🔮 FULL DEFENSE", 
            TitleColor = "#a78bfa", 
            BorderColor = "#7c3aed", 
            ArmorLevel = 3, 
            ShieldType = 2 
        }
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await RenderCatalogAsync();
        }
    }

    public async Task RenderCatalogAsync()
    {
        await RenderCHRSheetCanvas();
        await RenderAllTankPreviews();
        await RenderAllArmorShowcases();
    }

    public async Task RenderCHRSheetCanvas()
    {
        try
        {
            await JS.InvokeVoidAsync("StageInspector.renderCHRSheet", "chrCanvas", _selectedPalIdx, 2);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CHR render error: {ex.Message}");
        }
    }

    private async Task OnPaletteChanged(int newPal)
    {
        _selectedPalIdx = newPal;
        await RenderCHRSheetCanvas();
    }

    private async Task SetTankDir(TankPreviewModel tank, int dir)
    {
        tank.Dir = dir;
        await RenderTankPreview(tank);
    }

    private async Task RenderTankPreview(TankPreviewModel tank)
    {
        try
        {
            await JS.InvokeVoidAsync("StageInspector.renderTankPreview", $"tankCanvas_{tank.Id}", tank.EnemyType, tank.Dir, 0, 3, 0, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Tank preview error: {ex.Message}");
        }
    }

    public async Task RenderAllTankPreviews()
    {
        foreach (var tank in _previewTanks)
        {
            await RenderTankPreview(tank);
        }
    }

    public async Task RenderAllArmorShowcases()
    {
        foreach (var sample in _armorShowcaseTanks)
        {
            try
            {
                // Player 1 base (enemyType = -1), Dir = 0 (Up), animFrame = 0, scale = 3
                await JS.InvokeVoidAsync("StageInspector.renderTankPreview", $"armorCanvas_{sample.Id}", -1, 0, 0, 3, sample.ArmorLevel, sample.ShieldType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Armor preview error {sample.Id}: {ex.Message}");
            }
        }
    }
}
