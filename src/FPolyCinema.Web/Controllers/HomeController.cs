using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Infrastructure.Web;
using FPolyCinema.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>Trang chủ khách, trang từ chối truy cập và trang lỗi (UI D3), trang điều khoản và chính sách hủy vé.</summary>
[AllowAnonymous]
public class HomeController : AppControllerBase
{
    private readonly CinemaRules _rules;

    public HomeController(CinemaRules rules) => _rules = rules;

    // TODO(Phase 5): trang chủ C1 (hero, mua vé nhanh, lưới phim, combo, ưu đãi).
    [HttpGet("/")]
    public IActionResult Index() => View();

    /// <summary>Từ chối truy cập (AccessDeniedPath): layout của khu vực đang đứng, icon 48px, nút về trang chính của vai trò.</summary>
    [HttpGet("/Home/AccessDenied")]
    public IActionResult AccessDenied(string? returnUrl)
    {
        if (WantsJson) return ApiFail(ErrorCodes.AccessDenied, ApiResponse.DefaultMessage(ErrorCodes.AccessDenied));
        ViewData["Layout"] = RoleRouting.LayoutOf(RoleRouting.AreaOfPath(returnUrl, CurrentRole));
        Response.StatusCode = 403;
        return View("Error", new ErrorVm
        {
            StatusCode = 403,
            Title = "Không có quyền truy cập",
            Message = "Tài khoản của bạn không có quyền dùng chức năng này. Bạn quay về trang chính hoặc đăng nhập bằng tài khoản khác.",
            Icon = "bi-shield-lock",
            HomeUrl = RoleRouting.HomePath(CurrentRole),
            HomeLabel = "Về trang chính của tôi",
        });
    }

    /// <summary>Trang lỗi chung (UseExceptionHandler và UseStatusCodePagesWithReExecute). Không hiện stack trace.</summary>
    [Route("/Home/Error")]
    [IgnoreAntiforgeryToken]
    public IActionResult Error(int? code)
    {
        var ex = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        var reexec = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
        var originalPath = ex?.Path ?? reexec?.OriginalPath ?? Request.Path.Value;
        int status = ex?.Error is BusinessRuleException b ? b.HttpStatus : ex != null ? 500 : code ?? 500;
        var vm = new ErrorVm { StatusCode = status, SupportCode = HttpContext.TraceIdentifier, HomeUrl = RoleRouting.HomePath(CurrentRole) };
        switch (ex?.Error)
        {
            case BusinessRuleException br:
                vm.Title = "Chưa làm được việc này";
                vm.Message = br.Message;
                vm.Icon = "bi-exclamation-triangle";
                break;
            case null when status == 404:
                vm.Title = "Không tìm thấy trang";
                vm.Message = "Trang bạn tìm không có hoặc đã được chuyển đi. Bạn kiểm tra lại đường dẫn hoặc quay về trang chính.";
                vm.Icon = "bi-signpost-split";
                break;
            case null when status == 400:
                vm.Title = "Yêu cầu chưa hợp lệ";
                vm.Message = "Dữ liệu gửi lên chưa hợp lệ hoặc phiên làm việc đã hết hạn. Bạn tải lại trang rồi thử lại.";
                vm.Icon = "bi-exclamation-circle";
                break;
            case null when status is 401 or 403:
                vm.Title = "Không có quyền truy cập";
                vm.Message = "Tài khoản của bạn không có quyền dùng chức năng này.";
                vm.Icon = "bi-shield-lock";
                break;
        }
        if (WantsJson)
        {
            var errCode = ex?.Error is BusinessRuleException be ? be.Code : ApiResponse.CodeOfStatus(status);
            var message = ex?.Error is BusinessRuleException bm ? bm.Message : ApiResponse.DefaultMessage(errCode);
            return new JsonResult(ApiResponse.Fail(errCode, message, status >= 500 ? new { supportCode = HttpContext.TraceIdentifier } : null)) { StatusCode = status };
        }
        ViewData["Layout"] = RoleRouting.LayoutOf(RoleRouting.AreaOfPath(originalPath, CurrentRole));
        Response.StatusCode = status;
        return View(vm);
    }

    [HttpGet("/policies/terms")]
    public IActionResult Terms() => View();

    [HttpGet("/policies/cancellation")]
    public IActionResult Cancellation() => View(_rules);
}
