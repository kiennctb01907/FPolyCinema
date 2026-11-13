using System.Security.Claims;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure.Web;
using FPolyCinema.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>
/// Lớp cơ sở của các controller: người dùng hiện tại, phản hồi AJAX theo CONTRACTS mục 8, gắn lỗi theo ô vào ModelState,
/// toast sau khi chuyển trang. Không có action nào.
/// </summary>
public abstract class AppControllerBase : Controller
{
    protected int CurrentUserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    protected string? CurrentRole => User.FindFirstValue(ClaimTypes.Role);

    protected bool WantsJson => RequestKinds.WantsJson(Request);

    /// <summary>Request tải lại vùng danh sách (ui.loadRegion thêm partial=1).</summary>
    protected bool IsPartialRequest => Request.Query["partial"] == "1";

    protected JsonResult ApiOk(object? data = null) => Json(ApiResponse.Ok(data));

    protected JsonResult ApiFail(string code, string message, object? details = null) =>
        new(ApiResponse.Fail(code, message, details)) { StatusCode = ErrorCodes.HttpStatusOf(code) };

    /// <summary>Lỗi ModelState thành VALIDATION_FAILED (400) kèm từ điển tên ô → câu lỗi.</summary>
    protected JsonResult ApiValidation()
    {
        var details = ModelState.Where(kv => kv.Value?.Errors.Count > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors[0].ErrorMessage);
        var first = details.Values.FirstOrDefault() ?? ApiResponse.DefaultMessage(ErrorCodes.ValidationFailed);
        return ApiFail(ErrorCodes.ValidationFailed, first, details);
    }

    /// <summary>Gắn lỗi của lớp nghiệp vụ vào ModelState để form hiện ngay dưới ô.</summary>
    protected void AddErrors(BusinessRuleException ex)
    {
        if (ex.Details is IDictionary<string, string> fields)
            foreach (var (k, v) in fields) ModelState.AddModelError(k, v);
        else
            ModelState.AddModelError(string.Empty, ex.Message);
    }

    protected void Toast(string type, string message) => Flash.Add(TempData, type, message);

    /// <summary>Trang lỗi thân thiện 404 (UI D3) cho request thường.</summary>
    protected IActionResult NotFoundPage(string message = "Không tìm thấy dữ liệu bạn cần. Có thể dữ liệu đã bị xóa.")
    {
        if (WantsJson) return ApiFail(ErrorCodes.NotFound, message);
        ViewData["Layout"] = RoleRouting.LayoutOf(RoleRouting.AreaOfPath(Request.Path, CurrentRole));
        Response.StatusCode = 404;
        return View("~/Views/Home/Error.cshtml", new ErrorVm
        {
            StatusCode = 404, Title = "Không tìm thấy", Message = message, Icon = "bi-search",
            HomeUrl = RoleRouting.HomePath(CurrentRole), SupportCode = HttpContext.TraceIdentifier,
        });
    }

    /// <summary>URL nội bộ an toàn để quay lại; không hợp lệ thì về trang chính của vai trò.</summary>
    protected string SafeReturn(string? returnUrl, string? role = null) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : RoleRouting.HomePath(role ?? CurrentRole);

    /// <summary>Trạng thái lọc "active"/"inactive" thành bool?.</summary>
    protected static bool? ParseStatus(string? status) => status switch { "active" => true, "inactive" => false, _ => null };
}
