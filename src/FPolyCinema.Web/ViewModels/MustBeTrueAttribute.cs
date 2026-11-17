using System.ComponentModel.DataAnnotations;

namespace FPolyCinema.Web.ViewModels;

/// <summary>
/// Ô tích bắt buộc phải chọn (ví dụ đồng ý điều khoản). Chỉ kiểm ở server; phía trình duyệt dùng [Required] đi kèm,
/// vì quy tắc "required" của jQuery Validate với checkbox nghĩa là phải tích. Không dùng [Range(typeof(bool), "true", "true")]:
/// quy tắc range phía trình duyệt so "true" với "True" nên luôn báo lỗi dù đã tích (BUG-075).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MustBeTrueAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is true;
}
