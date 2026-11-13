using System.Security.Cryptography;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>
/// Quy tắc thuần cho mật khẩu: độ dài tối thiểu và thành phần khi người dùng tự đặt, sinh mật khẩu được cấp
/// (Auth:NewPasswordLength ký tự) và chấm điểm độ mạnh cho thước đo 0–4 trên giao diện.
/// </summary>
public static class PasswordRules
{
    public const int MinLength = 8;
    public const int MaxLength = 100;

    // Bỏ các ký tự dễ nhầm (0, O, 1, l, I) để người dùng gõ lại mật khẩu được cấp không sai
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digits = "23456789";

    /// <summary>
    /// Kiểm tra mật khẩu mới người dùng tự đặt. Trả null nếu hợp lệ, ngược lại là câu tiếng Việt nói rõ cần sửa gì.
    /// currentPlain là mật khẩu đang dùng (nếu biết) để không cho đặt lại y như cũ.
    /// </summary>
    public static string? Validate(string? password, string? confirm, string? currentPlain = null)
    {
        if (string.IsNullOrEmpty(password)) return "Bạn chưa nhập mật khẩu mới.";
        if (password.Length < MinLength) return $"Mật khẩu cần ít nhất {MinLength} ký tự.";
        if (password.Length > MaxLength) return $"Mật khẩu tối đa {MaxLength} ký tự.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return "Mật khẩu cần có cả chữ và số.";
        if (password.Trim().Length != password.Length) return "Mật khẩu không được bắt đầu hoặc kết thúc bằng khoảng trắng.";
        if (currentPlain != null && password == currentPlain) return "Mật khẩu mới phải khác mật khẩu hiện tại.";
        if (confirm != null && confirm != password) return "Mật khẩu nhập lại chưa khớp.";
        return null;
    }

    /// <summary>Sinh mật khẩu ngẫu nhiên length ký tự, luôn có chữ thường, chữ hoa và số.</summary>
    public static string Generate(int length)
    {
        if (length < 6) throw new ArgumentOutOfRangeException(nameof(length));
        var all = Lower + Upper + Digits;
        var chars = new char[length];
        chars[0] = Lower[RandomNumberGenerator.GetInt32(Lower.Length)];
        chars[1] = Upper[RandomNumberGenerator.GetInt32(Upper.Length)];
        chars[2] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        for (int i = 3; i < length; i++) chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        // Trộn vị trí (Fisher–Yates)
        for (int i = length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
    }

    /// <summary>Điểm độ mạnh 0–4: dưới MinLength là 0 hoặc 1; cộng điểm cho độ dài, chữ hoa lẫn thường, số, ký tự đặc biệt.</summary>
    public static int Strength(string? password)
    {
        if (string.IsNullOrEmpty(password)) return 0;
        int kinds = 0;
        if (password.Any(char.IsLower)) kinds++;
        if (password.Any(char.IsUpper)) kinds++;
        if (password.Any(char.IsDigit)) kinds++;
        if (password.Any(c => !char.IsLetterOrDigit(c))) kinds++;
        if (password.Length < MinLength) return kinds >= 2 ? 1 : 0;
        int score = 1;
        if (kinds >= 2) score++;
        if (kinds >= 3) score++;
        if (password.Length >= 12 && kinds >= 3) score++;
        return Math.Min(score, 4);
    }
}
