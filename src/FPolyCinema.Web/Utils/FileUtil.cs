using FPolyCinema.Web.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace FPolyCinema.Web.Utils;

/// <summary>Lỗi khi upload hoặc xóa ảnh; Message là câu tiếng Việt cho người dùng.</summary>
public class FileUploadException : Exception
{
    public FileUploadException(string message) : base(message) { }
}

/// <summary>
/// Upload và xóa ảnh theo slide bài 4–5 (SPEC 2.6). Lớp tĩnh.
/// - Upload chỉ nhận .jpg, .jpeg, .png, .gif, .webp, kiểm tra cả phần mở rộng lẫn chữ ký đầu file, tối đa 5MB,
///   lưu thành wwwroot/uploads/{timestamp}_{shortGuid}{ext}.
/// - Delete chỉ xóa file nằm trực tiếp trong thư mục uploads (không xóa ảnh mẫu trong uploads/seed, không ra ngoài uploads).
/// Đường dẫn ghép bằng Path.Combine để chạy được cả trên Linux.
/// </summary>
public static class FileUtil
{
    public const string UploadFolder = "uploads";
    public const long MaxBytes = 5 * 1024 * 1024;
    public static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    /// <summary>Đồng hồ dùng cho phần timestamp của tên file (Program.cs gán đồng hồ của hệ thống).</summary>
    public static IClock Clock { get; set; } = new SystemClock(new CinemaRules().TimeZoneId);

    /// <summary>
    /// Lưu ảnh vào wwwroot/uploads và trả về đường dẫn web dạng /uploads/{tên file} để lưu vào DB.
    /// Ném FileUploadException nếu file không hợp lệ.
    /// </summary>
    public static string Upload(IFormFile file, string webRootPath)
    {
        if (file == null || file.Length == 0) throw new FileUploadException("Chưa chọn ảnh hoặc file rỗng.");
        if (file.Length > MaxBytes) throw new FileUploadException("Ảnh vượt quá 5MB.");

        var ext = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();
        if (Array.IndexOf(AllowedExtensions, ext) < 0)
            throw new FileUploadException("Chỉ nhận ảnh .jpg, .jpeg, .png, .gif hoặc .webp.");

        byte[] header = new byte[12];
        int read;
        using (var s = file.OpenReadStream())
        {
            read = s.Read(header, 0, header.Length);
        }
        if (!SignatureMatches(ext, header.AsSpan(0, read)))
            throw new FileUploadException("Nội dung file không phải ảnh đúng định dạng của phần mở rộng.");

        var dir = Path.Combine(webRootPath, UploadFolder);
        Directory.CreateDirectory(dir);
        var name = $"{Clock.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}{ext}";
        var fullPath = Path.Combine(dir, name);
        using (var output = new FileStream(fullPath, FileMode.CreateNew))
        {
            file.CopyTo(output);
        }
        return "/" + UploadFolder + "/" + name;
    }

    /// <summary>
    /// Xóa một ảnh đã upload. Nhận tên file hoặc đường dẫn /uploads/{tên file}.
    /// Trả về true nếu đã xóa; false nếu file không nằm trực tiếp trong uploads hoặc không tồn tại.
    /// </summary>
    public static bool Delete(string fileName, string webRootPath)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        var name = fileName.Replace('\\', '/');
        var prefix = "/" + UploadFolder + "/";
        if (name.StartsWith(prefix, StringComparison.Ordinal)) name = name[prefix.Length..];
        else if (name.StartsWith(UploadFolder + "/", StringComparison.Ordinal)) name = name[(UploadFolder.Length + 1)..];
        // Chỉ chấp nhận tên file đơn, không có thư mục con hay ".."
        if (name.Length == 0 || name.Contains('/') || name == "." || name == "..") return false;

        var uploadsDir = Path.GetFullPath(Path.Combine(webRootPath, UploadFolder));
        var fullPath = Path.GetFullPath(Path.Combine(uploadsDir, name));
        if (!string.Equals(Path.GetDirectoryName(fullPath), uploadsDir, StringComparison.Ordinal)) return false;
        if (!File.Exists(fullPath)) return false;
        File.Delete(fullPath);
        return true;
    }

    /// <summary>So chữ ký đầu file (magic bytes) với phần mở rộng.</summary>
    public static bool SignatureMatches(string extension, ReadOnlySpan<byte> header)
    {
        switch (extension.ToLowerInvariant())
        {
            case ".jpg":
            case ".jpeg":
                return header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
            case ".png":
                return header.Length >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
            case ".gif":
                return header.Length >= 6 && (header[..6].SequenceEqual("GIF87a"u8) || header[..6].SequenceEqual("GIF89a"u8));
            case ".webp":
                return header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8);
            default:
                return false;
        }
    }
}
