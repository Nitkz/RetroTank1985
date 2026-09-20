using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;

namespace RetroTank1985.Client.Components.Inspector;

public partial class PowerUpsSpecialsTab : ComponentBase
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    public class ItemDisplayModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int[] Tiles { get; set; } = Array.Empty<int>();
        public int PalIdx { get; set; } = 6;
        public bool Pt1 { get; set; } = false;
        public string Badge { get; set; } = string.Empty;
        public Color BadgeColor { get; set; } = Color.Default;
    }

    private List<ItemDisplayModel> _powerUpItems = new()
    {
        new() { Id = "helmet", Name = "HELMET", Description = "ให้เกราะ Forcefield อมตะชั่วคราวแก่รถถัง (192 frames)", Tiles = new[] { 0x80, 0x82, 0x81, 0x83 }, PalIdx = 6, Pt1 = false },
        new() { Id = "timer", Name = "TIMER", Description = "หยุดการเคลื่อนไหวและการยิงของศัตรูทั้งสนามชั่วคราว", Tiles = new[] { 0x84, 0x86, 0x85, 0x87 }, PalIdx = 6, Pt1 = false },
        new() { Id = "shovel", Name = "SHOVEL", Description = "เปลี่ยนกำแพงรอบฐานอินทรีเป็นเหล็กชั่วคราว (1200 frames)", Tiles = new[] { 0x88, 0x8A, 0x89, 0x8B }, PalIdx = 6, Pt1 = false },
        new() { Id = "star", Name = "STAR", Description = "อัปเกรดพลังยิง: 1★ ยิงเร็ว, 2★ ยิง 2 นัด, 3★ ทำลายเหล็ก", Tiles = new[] { 0x8C, 0x8E, 0x8D, 0x8F }, PalIdx = 6, Pt1 = false },
        new() { Id = "grenade", Name = "GRENADE", Description = "ทำลายรถถังศัตรูทั้งหมดที่อยู่บนสนามทันที (+คะแนนปกติ)", Tiles = new[] { 0x90, 0x92, 0x91, 0x93 }, PalIdx = 6, Pt1 = false },
        new() { Id = "life", Name = "1-UP TANK", Description = "เพิ่มชีวิตสำรองให้กับผู้เล่น 1 ชีวิต (Award Extra Life)", Tiles = new[] { 0x94, 0x96, 0x95, 0x97 }, PalIdx = 6, Pt1 = false },
    };

    private List<ItemDisplayModel> _specialItems = new()
    {
        new() { Id = "eagle_ok", Name = "EAGLE INTACT", Description = "ฐานบัญชาการปกติ (Phoenix HQ Alive)", Tiles = new[] { 0xC8, 0xCA, 0xC9, 0xCB }, PalIdx = 0, Pt1 = false, Badge = "ACTIVE HQ", BadgeColor = Color.Success },
        new() { Id = "eagle_dead", Name = "EAGLE DESTROYED", Description = "ฐานอินทรีถูกยิงทำลาย (Trigger Game Over)", Tiles = new[] { 0xCC, 0xCE, 0xCD, 0xCF }, PalIdx = 0, Pt1 = false, Badge = "GAME OVER", BadgeColor = Color.Error },
        new() { Id = "shield_anim", Name = "FORCE SHIELD", Description = "วงแสงบาเรียป้องกันตัวผู้เล่น (Player Shield)", Tiles = new[] { 0x28, 0x2A, 0x29, 0x2B }, PalIdx = 6, Pt1 = false, Badge = "DEFENSE", BadgeColor = Color.Info },
        new() { Id = "score_500", Name = "500 PTS FLASH", Description = "ป้ายคะแนนลอยเมื่อเก็บไอเทม (+500 Score)", Tiles = new[] { 0x3A, 0x3C, 0x3B, 0x3D }, PalIdx = 6, Pt1 = false, Badge = "+500 PTS", BadgeColor = Color.Warning },
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await RenderAllItemsAsync();
        }
    }

    public async Task RenderAllItemsAsync()
    {
        foreach (var item in _powerUpItems)
        {
            try
            {
                await JS.InvokeVoidAsync("StageInspector.renderMetaspriteCustom", $"itemCanvas_{item.Id}", item.Tiles, item.PalIdx, item.Pt1, 4);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Render item {item.Id} error: {ex.Message}");
            }
        }

        foreach (var sp in _specialItems)
        {
            try
            {
                await JS.InvokeVoidAsync("StageInspector.renderMetaspriteCustom", $"itemCanvas_{sp.Id}", sp.Tiles, sp.PalIdx, sp.Pt1, 4);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Render special {sp.Id} error: {ex.Message}");
            }
        }
    }

    private async Task PlayBonusSfx()
    {
        try
        {
            await JS.InvokeVoidAsync("nesSynth.playBonus");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Bonus sound error: {ex.Message}");
        }
    }
}
