using System.Text;
using QRCoder;

namespace FPolyCinema.Web.Infrastructure;

/// <summary>Sinh mã QR dạng SVG ở server bằng QRCoder (SPEC 2.1); SVG chạy được trên Linux, không cần thư viện đồ họa của Windows.</summary>
public static class QrSvg
{
    // Cùng giá trị với token --ink và --white
    private const string Dark = "#0B0F19";
    private const string Light = "#FFFFFF";

    public static string Svg(string text, int pixelsPerModule = 8)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var svg = new SvgQRCode(data);
        return svg.GetGraphic(pixelsPerModule, Dark, Light, true, SvgQRCode.SizingMode.ViewBoxAttribute);
    }

    /// <summary>Ảnh QR dạng data URI để đặt vào thẻ img (không cần Html.Raw).</summary>
    public static string DataUri(string text) => "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(Svg(text)));
}
