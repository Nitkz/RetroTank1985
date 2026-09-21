using RetroTank1985.Shared.Enums;

namespace RetroTank1985.Shared.Models.Network;

/// <summary>
/// อีเวนต์การกดยืมชีวิตเพื่อนร่วมทีม (Life Steal / Borrow Mechanic)
/// </summary>
public class LifeBorrowEventDto
{
    /// <summary>
    /// ผู้เล่นที่ขอยืม (ผู้เล่นที่ตายจนชีวิตเหลือ 0)
    /// </summary>
    public CoopPlayerSlotIndex Requester { get; set; }

    /// <summary>
    /// ผู้เล่นที่ถูกยืม (ผู้เล่นที่ยังมีชีวิต >= 2)
    /// </summary>
    public CoopPlayerSlotIndex Target { get; set; }

    /// <summary>
    /// จำนวนชีวิตที่เหลือของผู้ถูกยืมหลังถูกหักออกไป 1 ตัว
    /// </summary>
    public int TargetRemainingLives { get; set; }

    /// <summary>
    /// ผลการยืมสำเร็จหรือไม่
    /// </summary>
    public bool Success { get; set; } = true;
}
