namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng PasswordResetOtps: mã OTP đặt lại mật khẩu.</summary>
public class PasswordResetOtp
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string OtpHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? UsedAt { get; set; }
}
