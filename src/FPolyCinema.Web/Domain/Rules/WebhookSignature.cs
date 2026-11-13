using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>
/// Chữ ký webhook của cổng thanh toán (SPEC 3.7): HMAC-SHA256 trên nội dung gửi, khóa Payment:WebhookSecret, viết dạng hex chữ thường.
/// So sánh chữ ký theo thời gian cố định. Logic thuần, có unit test.
/// </summary>
public static class WebhookSignature
{
    public const string Header = "X-Signature";

    public static string Compute(string secret, string payload)
    {
        using var h = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    public static bool Verify(string secret, string payload, string? signature)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrWhiteSpace(signature)) return false;
        var expected = System.Text.Encoding.ASCII.GetBytes(Compute(secret, payload));
        var actual = System.Text.Encoding.ASCII.GetBytes(signature.Trim().ToLowerInvariant());
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
