using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace RetroTank1985.Client.Pages;

public partial class SoundBgm : ComponentBase
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private double _volume = 40;
    private bool _isMuted = false;
    private bool _isEnginePlaying = false;

    private List<BreadcrumbItem> _navItems = new()
    {
        new BreadcrumbItem("Hub", href: ""),
        new BreadcrumbItem("Sound & BGM", href: null, disabled: true)
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await UpdateVolume();
        }
    }

    private async Task OnVolumeChanged(double val)
    {
        _volume = val;
        await UpdateVolume();
    }

    private async Task UpdateVolume()
    {
        await JS.InvokeVoidAsync("nesSynth.setMasterVolume", _volume / 100.0);
    }

    private async Task ToggleMute()
    {
        _isMuted = await JS.InvokeAsync<bool>("nesSynth.toggleMute");
    }

    private async Task PlayShot() => await JS.InvokeVoidAsync("nesSynth.playShot");
    private async Task PlayHitBrick() => await JS.InvokeVoidAsync("nesSynth.playHitBrick");
    private async Task PlayHitSteel() => await JS.InvokeVoidAsync("nesSynth.playHitSteel");
    private async Task PlayHitArmor() => await JS.InvokeVoidAsync("nesSynth.playHitArmor");
    private async Task PlayExplosion(bool isLarge) => await JS.InvokeVoidAsync("nesSynth.playExplosion", isLarge);
    private async Task PlayBonusAppear() => await JS.InvokeVoidAsync("nesSynth.playBonusAppear");
    private async Task PlayBonus() => await JS.InvokeVoidAsync("nesSynth.playBonus");
    private async Task PlayLifeUp() => await JS.InvokeVoidAsync("nesSynth.playLifeUp");
    private async Task PlayPause() => await JS.InvokeVoidAsync("nesSynth.playPause");
    private async Task PlayEagleHit() => await JS.InvokeVoidAsync("nesSynth.playEagleHit");
    private async Task StopEagleAlarm() => await JS.InvokeVoidAsync("nesSynth.stopEagleAlarm");

    private async Task StartEngine()
    {
        _isEnginePlaying = true;
        await JS.InvokeVoidAsync("nesSynth.startEngine", false);
    }

    private async Task StopEngine()
    {
        _isEnginePlaying = false;
        await JS.InvokeVoidAsync("nesSynth.stopEngine");
    }

    private async Task PlayIntroBGM() => await JS.InvokeVoidAsync("nesSynth.playIntroBGM");
    private async Task PlayStageClearBGM() => await JS.InvokeVoidAsync("nesSynth.playStageClearBGM");
    private async Task PlayVictoryBGM() => await JS.InvokeVoidAsync("nesSynth.playVictoryBGM");
    private async Task StopBGM() => await JS.InvokeVoidAsync("nesSynth.stopBGM");
}
