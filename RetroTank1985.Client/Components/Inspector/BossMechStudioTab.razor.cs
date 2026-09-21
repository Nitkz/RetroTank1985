using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace RetroTank1985.Client.Components.Inspector;

public partial class BossMechStudioTab : ComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private string _selectedPalette = "manta";
    private string _selectedAnim = "idle";
    private string _selectedTerrain = "cave";
    private int _scale = 6;
    private bool _showTileGrid = true;
    private bool _showPixelGrid = false;
    private bool _layerHorns = true;
    private bool _layerEyes = true;
    private bool _layerWings = true;
    private bool _layerCannons = true;
    private bool _layerCore = true;
    private bool _layerTracks = true;

    private string _exportedCSharpCode = string.Empty;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Task.Delay(100);
            await RenderBossAsync();
            await RenderPlayerHyperAsync();
            await RenderVillainAsync();
            await StartSimulationAsync();
            await LoadExportedCodeAsync();
        }
    }

    public async Task RenderBossAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("BossStudio.renderBossPreview", "bossPreviewCanvas", new
            {
                scale = _scale,
                showGrid = _showPixelGrid,
                showTileGrid = _showTileGrid,
                showCoords = true,
                animState = _selectedAnim,
                paletteKey = _selectedPalette,
                layerHorns = _layerHorns,
                layerEyes = _layerEyes,
                layerWings = _layerWings,
                layerCannons = _layerCannons,
                layerCore = _layerCore,
                layerTracks = _layerTracks,
                phase = _selectedAnim == "damaged" ? 3 : 1
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Render boss preview error: {ex.Message}");
        }
    }

    public async Task RenderPlayerHyperAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("BossStudio.renderPlayerHyperPreview", "playerHyperCanvas", 4);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Render player hyper error: {ex.Message}");
        }
    }

    public async Task RenderVillainAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("BossStudio.renderVillainPreview", "villainCanvas", 4);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Render villain error: {ex.Message}");
        }
    }

    public async Task StartSimulationAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("BossStudio.setTerrainTheme", _selectedTerrain);
            await JS.InvokeVoidAsync("BossStudio.startMiniArena", "bossArenaCanvas", new { scale = 2 });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Start arena error: {ex.Message}");
        }
    }

    private async Task OnTerrainChanged(string terrain)
    {
        _selectedTerrain = terrain;
        await JS.InvokeVoidAsync("BossStudio.setTerrainTheme", terrain);
    }

    private async Task OnPaletteChanged(string palKey)
    {
        _selectedPalette = palKey;
        await JS.InvokeVoidAsync("BossStudio.setPalette", palKey);
        await RenderBossAsync();
    }

    private async Task OnAnimChanged(string anim)
    {
        _selectedAnim = anim;
        await RenderBossAsync();
    }

    private async Task SetScale(int scale)
    {
        _scale = scale;
        await RenderBossAsync();
    }

    private async Task PlayerMove(int dx)
    {
        await JS.InvokeVoidAsync("BossStudio.playerMove", dx);
    }

    private async Task PlayerFire(string weapon)
    {
        await JS.InvokeVoidAsync("BossStudio.playerFire", weapon);
    }

    private async Task PlayerReloadAsync()
    {
        await JS.InvokeVoidAsync("BossStudio.playerReload");
    }

    private async Task BossTriggerJump()
    {
        await JS.InvokeVoidAsync("BossStudio.bossTriggerJump");
    }

    private async Task BossTriggerFire()
    {
        await JS.InvokeVoidAsync("BossStudio.bossTriggerFire");
    }

    private async Task PlayBossShootSfx()
    {
        await JS.InvokeVoidAsync("nesSynth.playShoot", 2);
    }

    private async Task PlayBossSlamSfx()
    {
        await JS.InvokeVoidAsync("nesSynth.playExplosion", true);
    }

    private async Task LoadExportedCodeAsync()
    {
        try
        {
            var res = await JS.InvokeAsync<ExportResult>("BossStudio.exportSpriteData");
            if (res != null)
            {
                _exportedCSharpCode = res.CSharpCode;
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Export code error: {ex.Message}");
        }
    }

    public class ExportResult
    {
        public string CSharpCode { get; set; } = string.Empty;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("BossStudio.stopMiniArena");
        }
        catch
        {
            // Ignore during teardown
        }
    }
}
