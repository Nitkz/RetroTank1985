using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using RetroTank1985.Client.Components.Inspector;
using RetroTank1985.Shared.Models;
using RetroTank1985.Client.Services;

namespace RetroTank1985.Client.Pages;

public partial class StageInspector : ComponentBase
{
    [Inject] private StageService StageService { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private int _currentStageNum = 1;
    private StageModel? _currentStage;
    private bool _isInitialized = false;

    private StageArenaTab? _stageArenaRef;
    private ChrTileCatalogTab? _chrCatalogRef;
    private PowerUpsSpecialsTab? _powerUpsRef;
    private BossMechStudioTab? _bossStudioRef;

    protected override async Task OnInitializedAsync()
    {
        await LoadStage(_currentStageNum);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _isInitialized = true;
            await Task.Delay(200);
            if (_stageArenaRef != null)
            {
                await _stageArenaRef.RenderStageCanvasAsync();
            }
        }
    }

    private async Task OnActiveTabChanged(int tabIndex)
    {
        await Task.Delay(100);
        if (tabIndex == 0 && _stageArenaRef != null)
        {
            await _stageArenaRef.RenderStageCanvasAsync();
        }
        else if (tabIndex == 1 && _chrCatalogRef != null)
        {
            await _chrCatalogRef.RenderCatalogAsync();
        }
        else if (tabIndex == 2 && _powerUpsRef != null)
        {
            await _powerUpsRef.RenderAllItemsAsync();
        }
        else if (tabIndex == 3 && _bossStudioRef != null)
        {
            await _bossStudioRef.RenderBossAsync();
            await _bossStudioRef.RenderPlayerHyperAsync();
            await _bossStudioRef.StartSimulationAsync();
        }
    }




    private async Task LoadStage(int stageNum)
    {
        _currentStageNum = stageNum;
        _currentStage = await StageService.GetStageAsync(stageNum);
        if (_isInitialized && _stageArenaRef != null)
        {
            await Task.Delay(50);
            await _stageArenaRef.RenderStageCanvasAsync();
        }
    }
}
