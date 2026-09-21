using QRCoder;

namespace RetroTank1985.Shared.Services;

/// <summary>
/// Helper สร้างภาพ SVG QR Code ออฟไลน์ใน C# 100% โดยไม่ต้องพึ่งพา 3rd-party API ภายนอก
/// ทำงานได้ทั้งบน Server (.NET 9) และ Client (.NET 9 Blazor WebAssembly)
/// </summary>
public static class QrCodeGeneratorService
{
    /// <summary>
    /// สร้าง Data URI หรือ SVG String สำหรับ QR Code
    /// </summary>
    public static string GenerateSvgDataUri(string content, int pixelsPerModule = 10, string darkColorHex = "#000000", string lightColorHex = "#ffffff")
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new SvgQRCode(qrCodeData);
        var svgText = qrCode.GetGraphic(pixelsPerModule, darkColorHex, lightColorHex, drawQuietZones: true);

        // Convert to Base64 SVG Data URI
        var base64Svg = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svgText));
        return $"data:image/svg+xml;base64,{base64Svg}";
    }
}
