namespace FPolyCinema.Web.Infrastructure;

/// <summary>Khóa Auth (CONTRACTS mục 7): OTP quên mật khẩu và độ dài mật khẩu được cấp.</summary>
public class AuthOptions
{
    public const string SectionName = "Auth";
    public int OtpLength { get; set; } = 6;
    public int OtpValidMinutes { get; set; } = 5;
    public int OtpMaxAttempts { get; set; } = 5;
    public int NewPasswordLength { get; set; } = 10;
}

/// <summary>
/// Khóa Email (CONTRACTS mục 7). SmtpHost để trống thì IEmailSender ghi thư vào bảng EmailOutbox (xem ở /dev/mailbox).
/// SmtpPassword chỉ đặt qua biến môi trường Email__SmtpPassword.
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;

    public bool UseSmtp => !string.IsNullOrWhiteSpace(SmtpHost);
}

/// <summary>Khóa Demo (CONTRACTS mục 7): EnableTools = true chỉ trong Development.</summary>
public class DemoOptions
{
    public const string SectionName = "Demo";
    public bool EnableTools { get; set; }
}

/// <summary>Khóa Uploads (CONTRACTS mục 7): thư mục lưu ảnh, mặc định wwwroot/uploads (tính từ thư mục gốc của ứng dụng).</summary>
public class UploadOptions
{
    public const string SectionName = "Uploads";
    public string Path { get; set; } = "wwwroot/uploads";

    /// <summary>
    /// Thư mục cha của thư mục uploads, truyền cho FileUtil.Upload/Delete (FileUtil luôn ghi vào {cha}/uploads).
    /// Thư mục cấu hình phải có tên "uploads" để đường dẫn web /uploads/… giữ nguyên.
    /// </summary>
    public string ResolveRoot(string contentRoot)
    {
        var full = System.IO.Path.GetFullPath(System.IO.Path.IsPathRooted(Path) ? Path : System.IO.Path.Combine(contentRoot, Path));
        var name = System.IO.Path.GetFileName(full.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
        if (!string.Equals(name, Utils.FileUtil.UploadFolder, StringComparison.Ordinal))
            throw new InvalidOperationException($"Uploads:Path phải là một thư mục tên \"{Utils.FileUtil.UploadFolder}\" (đang là {Path}).");
        return System.IO.Path.GetDirectoryName(full.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar))!;
    }
}
