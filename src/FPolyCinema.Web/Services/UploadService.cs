using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace FPolyCinema.Web.Services;

/// <summary>
/// Upload và xóa ảnh qua FileUtil (SPEC 2.6) vào thư mục Uploads:Path. Không xóa ảnh mà món hoặc phim vẫn đang dùng,
/// và FileUtil không bao giờ xóa ảnh mẫu trong uploads/seed.
/// </summary>
public class UploadService
{
    private readonly string _root;

    public UploadService(IWebHostEnvironment env, IOptions<UploadOptions> options)
    {
        _root = options.Value.ResolveRoot(env.ContentRootPath);
    }

    /// <summary>Đường dẫn web hợp lệ do FileUtil.Upload tạo ra (hoặc ảnh mẫu).</summary>
    public static bool IsUploadPath(string path) =>
        path.StartsWith("/" + FileUtil.UploadFolder + "/", StringComparison.Ordinal) && !path.Contains("..") && !path.Contains('\\');

    /// <summary>Lưu ảnh, trả đường dẫn /uploads/{tên file}. File sai định dạng hoặc quá 5MB: FileUploadException.</summary>
    public string Upload(IFormFile file) => FileUtil.Upload(file, _root);

    /// <summary>Xóa ảnh do người dùng gọi (nút xóa ảnh của form): chặn nếu ảnh đang được món hoặc phim dùng.</summary>
    public bool Delete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !IsUploadPath(path)) throw FieldErrors.Of("fileName", "Đường dẫn ảnh không hợp lệ. Bạn tải ảnh lên lại rồi thử xóa.");
        if (IsInUse(path))
            throw new BusinessRuleException(ErrorCodes.DeleteBlocked, "Ảnh này đang được dùng cho một món hoặc phim nên chưa xóa được. Bạn đổi ảnh rồi bấm Lưu, ảnh cũ sẽ tự xóa.");
        return FileUtil.Delete(path, _root);
    }

    /// <summary>Xóa ảnh cũ sau khi đổi hoặc xóa món, nếu không còn món, phim nào dùng.</summary>
    public bool DeleteIfUnused(string path) => IsUploadPath(path) && !IsInUse(path) && FileUtil.Delete(path, _root);

    private static bool IsInUse(string path) => DrinkDAL.CountByImage(path) > 0 || MovieDAL.CountByPoster(path) > 0;
}
