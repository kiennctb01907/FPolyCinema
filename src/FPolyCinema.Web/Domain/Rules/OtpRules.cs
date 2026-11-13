using System.Security.Cryptography;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Kết quả kiểm tra một lần nhập OTP.</summary>
public enum OtpCheck
{
    /// <summary>Đúng mã, còn hạn, còn lượt.</summary>
    Valid,
    /// <summary>Sai mã nhưng vẫn còn lượt thử.</summary>
    Wrong,
    /// <summary>Sai mã và đây là lượt cuối: mã bị vô hiệu.</summary>
    WrongNoAttemptsLeft,
    /// <summary>Không có mã đang dùng được: hết hạn, đã dùng hoặc đã hết lượt.</summary>
    Expired,
}

/// <summary>
/// Quy tắc thuần của OTP quên mật khẩu (SPEC 2.5): mã 6 số có hạn Auth:OtpValidMinutes phút,
/// sai tối đa Auth:OtpMaxAttempts lần (OTP_INVALID), hết hạn hoặc hết lượt thì OTP_EXPIRED.
/// "Gửi lại mã" chỉ được sau ResendCooldownSeconds giây (UI C6).
/// </summary>
public static class OtpRules
{
    public const int ResendCooldownSeconds = 60;

    /// <summary>Sinh mã gồm length chữ số bằng bộ sinh số ngẫu nhiên mật mã.</summary>
    public static string GenerateCode(int length)
    {
        if (length is < 4 or > 10) throw new ArgumentOutOfRangeException(nameof(length));
        var chars = new char[length];
        for (int i = 0; i < length; i++) chars[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        return new string(chars);
    }

    /// <summary>Mã có đúng định dạng: length chữ số.</summary>
    public static bool IsWellFormed(string? code, int length) =>
        code != null && code.Length == length && code.All(c => c is >= '0' and <= '9');

    /// <summary>
    /// Kiểm tra một lần nhập. usedAt khác null, đã quá expiresAt, hoặc đã sai đủ maxAttempts lần thì Expired.
    /// codeMatches là kết quả so mã băm (do service tính).
    /// </summary>
    public static OtpCheck Check(DateTime now, DateTime expiresAt, DateTime? usedAt, int failedAttempts, int maxAttempts, bool codeMatches)
    {
        if (usedAt.HasValue || now >= expiresAt || failedAttempts >= maxAttempts) return OtpCheck.Expired;
        if (codeMatches) return OtpCheck.Valid;
        return failedAttempts + 1 >= maxAttempts ? OtpCheck.WrongNoAttemptsLeft : OtpCheck.Wrong;
    }

    /// <summary>Số lượt còn lại sau khi đã sai failedAttempts lần.</summary>
    public static int RemainingAttempts(int failedAttempts, int maxAttempts) => Math.Max(0, maxAttempts - failedAttempts);

    /// <summary>Thời điểm tạo của mã, suy ra từ hạn dùng (bảng không có cột CreatedAt).</summary>
    public static DateTime CreatedAt(DateTime expiresAt, int validMinutes) => expiresAt.AddMinutes(-validMinutes);

    /// <summary>Được gửi mã mới chưa: chưa có mã nào, hoặc mã gần nhất đã tạo từ ResendCooldownSeconds giây trước.</summary>
    public static bool CanResend(DateTime now, DateTime? lastCreatedAt) =>
        lastCreatedAt is null || (now - lastCreatedAt.Value).TotalSeconds >= ResendCooldownSeconds;

    /// <summary>Số giây phải chờ trước khi được gửi lại (0 nếu đã được gửi).</summary>
    public static int SecondsUntilResend(DateTime now, DateTime? lastCreatedAt) =>
        lastCreatedAt is null ? 0 : Math.Max(0, ResendCooldownSeconds - (int)Math.Floor((now - lastCreatedAt.Value).TotalSeconds));
}
