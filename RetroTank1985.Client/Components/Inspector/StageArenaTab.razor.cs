using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using RetroTank1985.Shared.Models;

namespace RetroTank1985.Client.Components.Inspector;

public partial class StageArenaTab : ComponentBase
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public StageModel? CurrentStage { get; set; }
    [Parameter] public int CurrentStageNum { get; set; } = 1;
    [Parameter] public EventCallback<int> OnStageChanged { get; set; }

    private bool _showGrid = false;
    private bool _showSpawns = true;
    private bool _showCoords = false;
    private bool _showJson = false;

    private int _brickCount = 0;
    private int _steelCount = 0;
    private int _waterCount = 0;
    private int _treesCount = 0;
    private int _iceCount = 0;

    private StageModel? _lastRenderedStage;
    private bool _needsRender = false;

    protected override void OnParametersSet()
    {
        CalculateTerrainStats();
        if (CurrentStage != null && CurrentStage != _lastRenderedStage)
        {
            _needsRender = true;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender || _needsRender)
        {
            _needsRender = false;
            _lastRenderedStage = CurrentStage;
            await RenderStageCanvasAsync();
        }
    }

    private void CalculateTerrainStats()
    {
        if (CurrentStage == null) return;
        _brickCount = 0; _steelCount = 0; _waterCount = 0; _treesCount = 0; _iceCount = 0;

        foreach (var row in CurrentStage.Grid)
        {
            foreach (var t in row)
            {
                if (t >= 0 && t <= 4) _brickCount++;
                else if (t >= 5 && t <= 9) _steelCount++;
                else if (t == 10) _waterCount++;
                else if (t == 11) _treesCount++;
                else if (t == 12) _iceCount++;
            }
        }
    }

    public async Task RenderStageCanvasAsync()
    {
        if (CurrentStage == null) return;
        try
        {
            await JS.InvokeVoidAsync("StageInspector.renderStage", "stageCanvas", CurrentStage.Grid, new
            {
                showGrid = _showGrid,
                showSpawns = _showSpawns,
                showCoords = _showCoords,
                scale = 2
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Stage render error: {ex.Message}");
        }
    }

    private async Task NextStage()
    {
        if (CurrentStageNum < 35)
        {
            await OnStageChanged.InvokeAsync(CurrentStageNum + 1);
        }
    }

    private async Task PrevStage()
    {
        if (CurrentStageNum > 1)
        {
            await OnStageChanged.InvokeAsync(CurrentStageNum - 1);
        }
    }

    private async Task OnSliderChanged(int value)
    {
        if (value != CurrentStageNum)
        {
            await OnStageChanged.InvokeAsync(value);
        }
    }

    private async Task OnGridOptionChanged(bool val)
    {
        _showGrid = val;
        await RenderStageCanvasAsync();
    }

    private async Task OnSpawnsOptionChanged(bool val)
    {
        _showSpawns = val;
        await RenderStageCanvasAsync();
    }

    private async Task OnCoordsOptionChanged(bool val)
    {
        _showCoords = val;
        await RenderStageCanvasAsync();
    }

    private void ToggleJsonView()
    {
        _showJson = !_showJson;
    }

    private (string Label, string Color) GetEnemyBadge(int type)
    {
        return type switch
        {
            0 => ("B", "#f8fafc"),
            1 => ("F", "#f1c40f"),
            2 => ("P", "#e74c3c"),
            3 => ("A", "#2ecc71"),
            _ => ("?", "#64748b")
        };
    }
}
