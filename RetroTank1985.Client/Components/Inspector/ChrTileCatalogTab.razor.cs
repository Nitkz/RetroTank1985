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
            await JS.InvokeVoidAsync("StageInspector.renderTankPreview", $"tankCanvas_{tank.Id}", tank.EnemyType, tank.Dir, 0, 3);
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
}
