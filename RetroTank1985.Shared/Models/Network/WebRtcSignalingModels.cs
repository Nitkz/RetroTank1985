using RetroTank1985.Shared.Enums;

namespace RetroTank1985.Shared.Models.Network;

/// <summary>
/// ซองจดหมายส่งสัญญาณ WebRTC ผ่าน Signaling Server (SignalR หรือ Broker)
/// </summary>
public class WebRtcSignalMessage
{
    public string RoomCode { get; set; } = string.Empty;
    public string SenderConnectionId { get; set; } = string.Empty;
    public string TargetConnectionId { get; set; } = string.Empty;
    public WebRtcSignalType Type { get; set; }
    
    /// <summary>
    /// ข้อมูล Payload (เช่น SDP string หรือ ICE candidate JSON)
    /// </summary>
    public string Payload { get; set; } = string.Empty;

    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
