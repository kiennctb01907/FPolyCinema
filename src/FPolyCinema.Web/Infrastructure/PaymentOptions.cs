using System.Security.Cryptography;

namespace FPolyCinema.Web.Infrastructure;

/// <summary>
/// Cấu hình thanh toán (appsettings mục "Payment", CONTRACTS C7-06). WebhookSecret là khóa ký HMAC-SHA256 của webhook trang mô phỏng thanh toán;
/// là bí mật nên để trống trong appsettings.json và đặt qua biến môi trường Payment__WebhookSecret. Để trống thì ứng dụng tự sinh một khóa ngẫu nhiên
/// cho mỗi lần chạy (trang mô phỏng và webhook chạy cùng tiến trình nên vẫn khớp).
/// </summary>
public class PaymentOptions
{
    public const string SectionName = "Payment";

    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>Khóa dùng thật: khóa cấu hình, hoặc khóa ngẫu nhiên sinh một lần khi chưa cấu hình.</summary>
    public string EffectiveSecret => string.IsNullOrWhiteSpace(WebhookSecret) ? GeneratedSecret : WebhookSecret;

    private static readonly string GeneratedSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
}
