using RetroTank1985.Shared.Enums;

namespace RetroTank1985.Shared.Models.Network;

/// <summary>
/// สถิติการสังหารรถถังและคะแนนของผู้เล่นแต่ละคนในด่าน
/// </summary>
public class PlayerTallyStats
{
    public string PlayerName { get; set; } = string.Empty;
    public int BasicKills { get; set; }
    public int FastKills { get; set; }
    public int PowerKills { get; set; }
    public int ArmorKills { get; set; }
    public int TotalScore { get; set; }
    public int PowerUpsCollected { get; set; }

    public int TotalKills => BasicKills + FastKills + PowerKills + ArmorKills;
}

/// <summary>
/// สรุปคะแนนท้ายด่านสำหรับ Online Co-Op (ส่งจาก Host -> Guest เพื่อแสดง Tally Screen พร้อมกัน)
/// </summary>
public class CoopStageTallyDto
{
    public int StageNumber { get; set; }
    public PlayerTallyStats Player1 { get; set; } = new();
    public PlayerTallyStats Player2 { get; set; } = new();
    
    /// <summary>
    /// ผู้เล่นที่ได้ตำแหน่ง MVP ประจำด่าน (null หากคะแนนเท่ากันเป๊ะ)
    /// </summary>
    public CoopPlayerSlotIndex? MvpPlayer { get; set; }
    
    public bool IsStagePassed { get; set; } = true;
    public int HighScore { get; set; }
}
