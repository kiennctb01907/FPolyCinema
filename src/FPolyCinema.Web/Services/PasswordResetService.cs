using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FPolyCinema.Web.Services;

/// <summary>Kết quả bước 1 (gửi mã): luôn giống nhau dù email có tài khoản hay không, để không lộ tài khoản.</summary>
public sealed record OtpRequestResult(int ValidSeconds, int ResendAfterSeconds);

/// <summary>
/// Quên mật khẩu bằng OTP (SPEC 2.5, Lab 5 bài 2 và 4, UC02):
/// 1. Nhập email. 2. Gửi mã OTP 6 số qua email, hạn 5 phút, sai tối đa 5 lần (OTP_INVALID).
/// 3. Đúng mã: đặt lại mật khẩu bằng chuỗi ngẫu nhiên, lưu dạng băm, gửi mật khẩu mới qua email.
/// 4. Users.MustChangePassword = 1 nên lần đăng nhập kế tiếp phải đổi mật khẩu.
/// </summary>
public class PasswordResetService
{
    private readonly IClock _clock;
    private readonly AuthOptions _options;
    private readonly IEmailSender _email;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(IClock clock, IOptions<AuthOptions> options, IEmailSender email, ILogger<PasswordResetService> logger)
    {
        _clock = clock;
        _options = options.Value;
        _email = email;
        _logger = logger;
    }

    public const string GenericSentMessage = "Nếu email này có tài khoản, mã xác nhận đã được gửi tới hộp thư. Mã có hiệu lực 5 phút.";

    /// <summary>Bước 1–2: tạo và gửi OTP. Email không có tài khoản (hoặc bị khóa) vẫn trả cùng kết quả nhưng không gửi gì.</summary>
    public OtpRequestResult RequestOtp(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) throw FieldErrors.Of("Email", "Bạn chưa nhập email.");
        var now = _clock.Now;
        var result = new OtpRequestResult(_options.OtpValidMinutes * 60, OtpRules.ResendCooldownSeconds);
        var user = UserDAL.GetByEmail(email);
        if (user == null || !user.Active) return result;

        var latest = PasswordResetOtpDAL.GetLatestForUser(user.Id);
        DateTime? lastCreated = latest == null ? null : OtpRules.CreatedAt(latest.ExpiresAt, _options.OtpValidMinutes);
        if (!OtpRules.CanResend(now, lastCreated))
        {
            // Vừa gửi trong 60 giây: không gửi thêm, báo thời gian còn phải chờ
            return result with { ResendAfterSeconds = OtpRules.SecondsUntilResend(now, lastCreated) };
        }

        var code = OtpRules.GenerateCode(_options.OtpLength);
        DBUtil.InTransaction((conn, tx) =>
        {
            PasswordResetOtpDAL.ExpireAllForUser(user.Id, now, conn, tx);
            PasswordResetOtpDAL.Insert(new PasswordResetOtp
            {
                UserId = user.Id,
                OtpHash = PasswordUtil.Hash(code),
                ExpiresAt = now.AddMinutes(_options.OtpValidMinutes),
                FailedAttempts = 0,
                UsedAt = null,
            }, conn, tx);
            return 0;
        });
        _email.Send(user.Email, "Mã xác nhận đặt lại mật khẩu FPoly Cinema",
            $"Xin chào {user.FullName},\n\nMã xác nhận để đặt lại mật khẩu của bạn là: {code}\n" +
            $"Mã có hiệu lực {_options.OtpValidMinutes} phút và chỉ dùng được một lần.\n\n" +
            "Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua thư này.\n\nFPoly Cinema");
        _logger.LogInformation("Đã gửi OTP đặt lại mật khẩu cho người dùng {UserId}.", user.Id);
        return result;
    }

    /// <summary>
    /// Bước 3: kiểm tra mã. Đúng thì cấp mật khẩu ngẫu nhiên (Auth:NewPasswordLength ký tự), băm, đặt MustChangePassword = 1
    /// và gửi mật khẩu mới qua email. Sai thì OTP_INVALID kèm số lần còn lại; hết hạn, đã dùng hoặc hết lượt thì OTP_EXPIRED.
    /// </summary>
    public void VerifyOtp(string? email, string? code)
    {
        var now = _clock.Now;
        if (!OtpRules.IsWellFormed(code, _options.OtpLength))
            throw FieldErrors.Of("Otp", $"Bạn nhập đủ {_options.OtpLength} chữ số của mã xác nhận.");
        var user = string.IsNullOrWhiteSpace(email) ? null : UserDAL.GetByEmail(email);
        var otp = user == null || !user.Active ? null : PasswordResetOtpDAL.GetLatestForUser(user.Id);
        if (user == null || otp == null) throw Expired();

        var check = OtpRules.Check(now, otp.ExpiresAt, otp.UsedAt, otp.FailedAttempts, _options.OtpMaxAttempts, PasswordUtil.Verify(otp.OtpHash, code));
        switch (check)
        {
            case OtpCheck.Expired:
                throw Expired();
            case OtpCheck.Wrong:
            case OtpCheck.WrongNoAttemptsLeft:
            {
                int failed = PasswordResetOtpDAL.IncrementFailed(otp.Id);
                int remaining = OtpRules.RemainingAttempts(failed, _options.OtpMaxAttempts);
                var message = remaining > 0
                    ? $"Mã xác nhận chưa đúng. Bạn còn {remaining} lần thử."
                    : "Mã xác nhận chưa đúng và đã hết lượt thử. Bấm Gửi lại mã để nhận mã mới.";
                throw new BusinessRuleException(ErrorCodes.OtpInvalid, message, new { remaining });
            }
        }

        var newPassword = PasswordRules.Generate(_options.NewPasswordLength);
        bool done = DBUtil.InTransaction((conn, tx) =>
        {
            if (PasswordResetOtpDAL.MarkUsed(otp.Id, now, conn, tx) == 0) return false;
            PasswordResetOtpDAL.ExpireAllForUser(user.Id, now, conn, tx);
            UserDAL.UpdatePassword(user.Id, PasswordUtil.Hash(newPassword), mustChangePassword: true, conn, tx);
            return true;
        });
        if (!done) throw Expired();
        _email.Send(user.Email, "Mật khẩu mới của bạn tại FPoly Cinema",
            $"Xin chào {user.FullName},\n\nMật khẩu mới của bạn là: {newPassword}\n" +
            "Lần đăng nhập tới, hệ thống sẽ yêu cầu bạn đặt mật khẩu của riêng mình.\n\nFPoly Cinema");
        _logger.LogInformation("Đã đặt lại mật khẩu cho người dùng {UserId} bằng OTP.", user.Id);
    }

    private static BusinessRuleException Expired() =>
        new(ErrorCodes.OtpExpired, "Mã xác nhận đã hết hạn. Bấm Gửi lại mã để nhận mã mới.");
}
