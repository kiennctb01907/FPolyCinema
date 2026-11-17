using System.ComponentModel.DataAnnotations;

namespace FPolyCinema.Web.ViewModels;

/// <summary>Form đăng nhập (C6).</summary>
public class LoginVm
{
    [Required(ErrorMessage = "Bạn chưa nhập email.")]
    [EmailAddress(ErrorMessage = "Email chưa đúng định dạng, ví dụ ten@gmail.com.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bạn chưa nhập mật khẩu.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

/// <summary>Form đăng ký tài khoản khách (C6, UC05).</summary>
public class RegisterVm
{
    [Required(ErrorMessage = "Bạn chưa nhập họ tên.")]
    [StringLength(150, ErrorMessage = "Họ tên tối đa 150 ký tự.")]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bạn chưa nhập email.")]
    [EmailAddress(ErrorMessage = "Email chưa đúng định dạng, ví dụ ten@gmail.com.")]
    [StringLength(256, ErrorMessage = "Email tối đa 256 ký tự.")]
    public string Email { get; set; } = string.Empty;

    [RegularExpression(@"^\+?\d{9,11}$", ErrorMessage = "Số điện thoại gồm 9–11 chữ số, ví dụ 0901234567.")]
    [Display(Name = "Số điện thoại")]
    public string? Phone { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Ngày sinh")]
    public DateTime? BirthDate { get; set; }

    [Required(ErrorMessage = "Bạn chưa nhập mật khẩu.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu cần ít nhất 8 ký tự.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bạn chưa nhập lại mật khẩu.")]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại chưa khớp.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bạn cần đồng ý với điều khoản sử dụng.")]
    [MustBeTrue(ErrorMessage = "Bạn cần đồng ý với điều khoản sử dụng.")]
    public bool AcceptTerms { get; set; }
}

/// <summary>Form đổi mật khẩu (A9 và trang đổi mật khẩu bắt buộc C6).</summary>
public class ChangePasswordVm
{
    [Required(ErrorMessage = "Bạn chưa nhập mật khẩu hiện tại.")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bạn chưa nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu cần ít nhất 8 ký tự.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Bạn chưa nhập lại mật khẩu mới.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại chưa khớp.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>Đang ở trang đổi mật khẩu bắt buộc (MustChangePassword = 1).</summary>
    public bool Forced { get; set; }
}

/// <summary>Thông tin cá nhân (A9 với nhân viên, trang hồ sơ đơn giản với khách).</summary>
public class ProfileVm
{
    [Required(ErrorMessage = "Bạn chưa nhập họ tên.")]
    [StringLength(150, ErrorMessage = "Họ tên tối đa 150 ký tự.")]
    public string FullName { get; set; } = string.Empty;

    [RegularExpression(@"^\+?\d{9,11}$", ErrorMessage = "Số điện thoại gồm 9–11 chữ số, ví dụ 0901234567.")]
    public string? Phone { get; set; }

    [DataType(DataType.Date)]
    public DateTime? BirthDate { get; set; }

    // Chỉ đọc
    public string Email { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;
    public string? CinemaName { get; set; }
    public string Initials { get; set; } = string.Empty;
    public bool IsCustomer { get; set; }
    public string? MemberCode { get; set; }
    public string? TierLabel { get; set; }
    public int LoyaltyPoints { get; set; }
    public ChangePasswordVm Password { get; set; } = new();
}
