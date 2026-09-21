namespace RetroTank1985.Shared.Models.Network;

/// <summary>
/// คุณภาพการเชื่อมต่อและสถานะการตัดการเชื่อมต่อชั่วคราว (Grace Period)
/// </summary>
public class ConnectionQualityDto
{
    public int PingMs { get; set; }
    public float PacketLossPercent { get; set; }
    
    /// <summary>
    /// สัญญาณอินเทอร์เน็ตไม่เสถียร (Ping > 120ms)
    /// </summary>
    public bool IsLagging => PingMs > 120;

    /// <summary>
    /// ผู้เล่นหลุด กำลังอยู่ในช่วง Grace Period เพื่อรอเชื่อมต่อใหม่
    /// </summary>
    public bool IsReconnecting { get; set; }

    /// <summary>
    /// เวลานับถอยหลังรอ Reconnect (วินาที)
    /// </summary>
    public int ReconnectCountdownSeconds { get; set; } = 15;
}
