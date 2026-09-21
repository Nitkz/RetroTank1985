namespace RetroTank1985.Shared.Models.Network;

/// <summary>
/// WebRTC Session Description Protocol (SDP) Payload
/// </summary>
public class WebRtcSdpPayload
{
    public string Type { get; set; } = "offer"; // "offer" | "answer"
    public string Sdp { get; set; } = string.Empty;
}

/// <summary>
/// WebRTC Interactive Connectivity Establishment (ICE) Candidate Payload
/// </summary>
public class WebRtcIceCandidatePayload
{
    public string Candidate { get; set; } = string.Empty;
    public string? SdpMid { get; set; }
    public int? SdpMLineIndex { get; set; }
    public string? UsernameFragment { get; set; }
}
