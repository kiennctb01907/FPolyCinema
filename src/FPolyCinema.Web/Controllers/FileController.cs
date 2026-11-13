using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>
/// Upload và xóa ảnh (SPEC 2.6, slide bài 4–5): [HttpPost] Upload và Delete, [ValidateAntiForgeryToken],
/// [Authorize(Policy = "ManagerOnly")]. Form Đồ uống (và Phim từ Phase 3) gọi bằng fetch, nhận JSON theo CONTRACTS mục 8.
/// </summary>
[Authorize(Policy = Policies.ManagerOnly)]
public class FileController : AppControllerBase
{
    private readonly UploadService _uploads;

    public FileController(UploadService uploads) => _uploads = uploads;

    /// <summary>Nhận một ảnh (.jpg, .jpeg, .png, .gif, .webp, tối đa 5MB), trả { path: "/uploads/…" }.</summary>
    [HttpPost("/File/Upload")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public IActionResult Upload(IFormFile? file)
    {
        if (file == null) return ApiFail(ErrorCodes.ValidationFailed, "Bạn chưa chọn ảnh. Kéo thả hoặc bấm để chọn một ảnh.");
        try
        {
            return ApiOk(new { path = _uploads.Upload(file) });
        }
        catch (FileUploadException ex)
        {
            return ApiFail(ErrorCodes.ValidationFailed, ex.Message, new Dictionary<string, string> { ["file"] = ex.Message });
        }
    }

    /// <summary>Xóa một ảnh đã tải lên mà chưa món hay phim nào dùng (ảnh mẫu uploads/seed không bao giờ bị xóa).</summary>
    [HttpPost("/File/Delete")]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(string? fileName) => ApiOk(new { deleted = _uploads.Delete(fileName) });
}
