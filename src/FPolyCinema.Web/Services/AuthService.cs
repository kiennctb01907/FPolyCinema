using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Utils;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FPolyCinema.Web.Services;

/// <summary>Kết quả đăng nhập.</summary>
public enum LoginOutcome { Success, InvalidCredentials, Locked }

/// <summary>Dữ liệu đăng ký tài khoản khách (UC05).</summary>
public sealed record RegisterInput(string FullName, string Email, string? Phone, DateTime? BirthDate, string Password, string ConfirmPassword);

/// <summary>
/// Đăng nhập, đăng ký, đổi mật khẩu, cập nhật hồ sơ (SPEC 2.5, UC01, UC03, UC04, UC05).
/// Kiểm tra mật khẩu bằng UserDAL.Authenticate (PasswordHasher); cookie mang claims NameIdentifier, Name, Email, Role, CinemaId.
/// </summary>
public class AuthService
{
    private readonly IClock _clock;

    public AuthService(IClock clock) => _clock = clock;

    /// <summary>Đăng nhập theo slide bài 4. Sai mật khẩu hoặc email lạ: InvalidCredentials; đúng mật khẩu nhưng tài khoản bị khóa: Locked.</summary>
    public (LoginOutcome Outcome, User? User) Login(string email, string password)
    {
        var user = UserDAL.Authenticate(email, password);
        if (user != null) return (LoginOutcome.Success, user);
        var existing = string.IsNullOrWhiteSpace(email) ? null : UserDAL.GetByEmail(email);
        if (existing != null && !existing.Active && PasswordUtil.Verify(existing.PasswordHash, password)) return (LoginOutcome.Locked, null);
        return (LoginOutcome.InvalidCredentials, null);
    }

    /// <summary>Dấu của mật khẩu (16 ký tự đầu của SHA-256 chuỗi băm): đổi mật khẩu thì dấu đổi, các phiên cũ bị đăng xuất.</summary>
    public static string PasswordStamp(string passwordHash) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash ?? string.Empty)))[..16];

    /// <summary>Claims của phiên đăng nhập (SPEC 2.5): NameIdentifier, Name, Email, Role, CinemaId (với nhân viên), thêm cờ đổi mật khẩu và dấu mật khẩu.</summary>
    public static ClaimsPrincipal BuildPrincipal(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(AppClaims.MustChangePassword, user.MustChangePassword ? "1" : "0"),
            new(AppClaims.PasswordStamp, PasswordStamp(user.PasswordHash)),
        };
        if (user.CinemaId.HasValue && user.Role != Roles.Customer)
            claims.Add(new Claim(AppClaims.CinemaId, user.CinemaId.Value.ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public User? GetUser(int id) => UserDAL.GetById(id);

    public User? FindByEmail(string email) => UserDAL.GetByEmail(email);

    /// <summary>Kiểm tra phiên ở mỗi request (OnValidatePrincipal): đọc người dùng theo NameIdentifier và so dấu mật khẩu.</summary>
    public static bool ValidateSession(ClaimsPrincipal? principal)
    {
        if (!int.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return false;
        return IsSessionValid(UserDAL.GetById(id), principal?.FindFirstValue(AppClaims.PasswordStamp));
    }

    /// <summary>Phiên còn hợp lệ: người dùng còn tồn tại, đang hoạt động và mật khẩu chưa đổi kể từ lúc đăng nhập.</summary>
    public static bool IsSessionValid(User? user, string? stamp) =>
        user != null && user.Active && stamp != null && stamp == PasswordStamp(user.PasswordHash);

    /// <summary>Đăng ký tài khoản khách hàng (UC05): mã thành viên TV + 8 số, hạng MEMBER.</summary>
    public User Register(RegisterInput input)
    {
        var errors = new Dictionary<string, string>();
        var email = (input.Email ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(input.FullName)) errors["FullName"] = "Bạn chưa nhập họ tên.";
        if (email.Length == 0) errors["Email"] = "Bạn chưa nhập email.";
        else if (UserDAL.EmailExists(email)) errors["Email"] = "Email này đã có tài khoản. Bạn đăng nhập hoặc dùng Quên mật khẩu.";
        var pwError = PasswordRules.Validate(input.Password, input.ConfirmPassword);
        if (pwError != null) errors[pwError.Contains("nhập lại") ? "ConfirmPassword" : "Password"] = pwError;
        if (input.BirthDate.HasValue && input.BirthDate.Value.Date > _clock.Today) errors["BirthDate"] = "Ngày sinh không được sau hôm nay.";
        if (errors.Count > 0) throw FieldErrors.Of(errors);

        return DBUtil.InTransaction((conn, tx) =>
        {
            var user = new User
            {
                Email = email,
                PasswordHash = PasswordUtil.Hash(input.Password),
                FullName = input.FullName.Trim(),
                Phone = string.IsNullOrWhiteSpace(input.Phone) ? null : input.Phone.Trim(),
                BirthDate = input.BirthDate?.Date,
                Role = Roles.Customer,
                CinemaId = null,
                Active = true,
                MustChangePassword = false,
                MemberCode = CodeGenerator.MemberCode(UserDAL.MaxMemberSequence(conn, tx) + 1),
                LoyaltyPoints = 0,
                Tier = LoyaltyTier.Member,
                WalletBalance = 0,
                CreatedAt = _clock.Now,
            };
            UserDAL.Insert(user, conn, tx);
            return user;
        });
    }

    /// <summary>Đổi mật khẩu (UC03, A9, đổi mật khẩu bắt buộc): kiểm tra mật khẩu hiện tại, đặt mật khẩu mới, bỏ cờ MustChangePassword.</summary>
    public User ChangePassword(int userId, string? currentPassword, string? newPassword, string? confirmPassword)
    {
        var user = UserDAL.GetById(userId) ?? throw new BusinessRuleException(ErrorCodes.NotFound, ApiResponseText.NotFound);
        if (!PasswordUtil.Verify(user.PasswordHash, currentPassword))
            throw FieldErrors.Of("CurrentPassword", "Mật khẩu hiện tại chưa đúng. Bạn nhập lại, hoặc dùng Quên mật khẩu ở trang đăng nhập.");
        var error = PasswordRules.Validate(newPassword, confirmPassword, currentPassword);
        if (error != null) throw FieldErrors.Of(error.Contains("nhập lại") ? "ConfirmPassword" : "NewPassword", error);
        var hash = PasswordUtil.Hash(newPassword!);
        UserDAL.UpdatePassword(user.Id, hash, mustChangePassword: false);
        user.PasswordHash = hash;
        user.MustChangePassword = false;
        return user;
    }

    /// <summary>Cập nhật thông tin cá nhân (UC04): họ tên, số điện thoại, ngày sinh; email chỉ đọc.</summary>
    public User UpdateProfile(int userId, string? fullName, string? phone, DateTime? birthDate)
    {
        var user = UserDAL.GetById(userId) ?? throw new BusinessRuleException(ErrorCodes.NotFound, ApiResponseText.NotFound);
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(fullName)) errors["FullName"] = "Bạn chưa nhập họ tên.";
        else if (fullName.Trim().Length > 150) errors["FullName"] = "Họ tên tối đa 150 ký tự.";
        if (!string.IsNullOrWhiteSpace(phone) && !IsPhone(phone)) errors["Phone"] = "Số điện thoại gồm 9–11 chữ số, ví dụ 0901234567.";
        if (birthDate.HasValue && birthDate.Value.Date > _clock.Today) errors["BirthDate"] = "Ngày sinh không được sau hôm nay.";
        if (errors.Count > 0) throw FieldErrors.Of(errors);
        user.FullName = fullName!.Trim();
        user.Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        user.BirthDate = birthDate?.Date ?? (user.Role == Roles.Customer ? null : user.BirthDate);
        UserDAL.UpdateProfile(user.Id, user.FullName, user.Phone, user.BirthDate);
        return user;
    }

    /// <summary>Số điện thoại Việt Nam đơn giản: 9–11 chữ số, cho phép dấu + ở đầu.</summary>
    public static bool IsPhone(string phone)
    {
        var p = phone.Trim();
        if (p.StartsWith('+')) p = p[1..];
        return p.Length is >= 9 and <= 11 && p.All(char.IsDigit);
    }
}

/// <summary>Câu thông báo dùng chung ở lớp nghiệp vụ.</summary>
public static class ApiResponseText
{
    public const string NotFound = "Không tìm thấy dữ liệu bạn cần. Có thể dữ liệu đã bị xóa, bạn tải lại trang để xem mới nhất.";
}
