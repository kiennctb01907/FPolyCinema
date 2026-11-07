using FPolyCinema.Web.Entities;
using Microsoft.AspNetCore.Identity;

namespace FPolyCinema.Web.Utils;

/// <summary>
/// Băm và kiểm tra mật khẩu, PIN quản lý. Chỉ dùng PasswordHasher của Microsoft.Extensions.Identity.Core (SPEC 2.1);
/// không dùng phần nào khác của ASP.NET Identity.
/// </summary>
public static class PasswordUtil
{
    private static readonly PasswordHasher<User> Hasher = new();
    private static readonly User Subject = new();

    public static string Hash(string plain)
    {
        ArgumentException.ThrowIfNullOrEmpty(plain);
        return Hasher.HashPassword(Subject, plain);
    }

    public static bool Verify(string? hash, string? plain)
    {
        if (string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(plain)) return false;
        try
        {
            return Hasher.VerifyHashedPassword(Subject, hash, plain) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            // Chuỗi băm hỏng: coi như sai mật khẩu
            return false;
        }
    }
}
