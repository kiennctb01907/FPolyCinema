using System.Security.Claims;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Utils;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FPolyCinema.Web.Infrastructure.Web;

/// <summary>
/// Xử lý lỗi tập trung cho AJAX (SPEC 2.7): bắt mọi ngoại lệ của request muốn JSON và trả đúng dạng CONTRACTS mục 8.
/// Request thường để UseExceptionHandler("/Home/Error") hiện trang lỗi thân thiện. Không bao giờ trả stack trace.
/// </summary>
public sealed class ApiErrorMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiErrorMiddleware> _logger;

    public ApiErrorMiddleware(RequestDelegate next, ILogger<ApiErrorMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (RequestKinds.WantsJson(context.Request) && !context.Response.HasStarted)
        {
            var (status, code, message, details) = Describe(ex, context.TraceIdentifier);
            if (status >= 500) _logger.LogError(ex, "Lỗi khi xử lý {Path}", context.Request.Path);
            else _logger.LogInformation("Từ chối {Path}: {Code} {Message}", context.Request.Path, code, message);
            context.Response.Clear();
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(ApiResponse.Fail(code, message, details));
        }
    }

    /// <summary>Đổi ngoại lệ thành (mã HTTP, mã lỗi, câu tiếng Việt, chi tiết).</summary>
    public static (int Status, string Code, string Message, object? Details) Describe(Exception ex, string traceId) => ex switch
    {
        BusinessRuleException b => (b.HttpStatus, b.Code, b.Message, b.Details),
        FileUploadException f => (400, ErrorCodes.ValidationFailed, f.Message, null),
        AntiforgeryValidationException => (400, ErrorCodes.ValidationFailed, "Phiên làm việc đã hết hạn. Bạn tải lại trang rồi thử lại.", null),
        BadHttpRequestException => (400, ErrorCodes.ValidationFailed, ApiResponse.DefaultMessage(ErrorCodes.ValidationFailed), null),
        _ => (500, ErrorCodes.ServerError, ApiResponse.DefaultMessage(ErrorCodes.ServerError), new { supportCode = traceId }),
    };
}

/// <summary>
/// Bộ lọc toàn cục AutoValidateAntiforgeryToken trả 400 khi thiếu token. Bộ lọc này đổi kết quả đó thành
/// JSON VALIDATION_FAILED (AJAX) hoặc trang lỗi thân thiện (request thường).
/// </summary>
public sealed class AntiforgeryResultFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not IAntiforgeryValidationFailedResult) return;
        const string message = "Phiên làm việc đã hết hạn. Bạn tải lại trang rồi thử lại.";
        if (RequestKinds.WantsJson(context.HttpContext.Request))
            context.Result = new JsonResult(ApiResponse.Fail(ErrorCodes.ValidationFailed, message)) { StatusCode = 400 };
        else
            context.Result = new ViewResult
            {
                ViewName = "~/Views/Home/Error.cshtml",
                StatusCode = 400,
                ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), context.ModelState)
                {
                    Model = new ViewModels.ErrorVm { StatusCode = 400, Title = "Phiên làm việc đã hết hạn", Message = message, SupportCode = context.HttpContext.TraceIdentifier },
                },
            };
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}

/// <summary>Khi Demo:EnableTools tắt, mọi đường dẫn bắt đầu bằng demo/ hoặc dev/ trả 404 (SPEC 5).</summary>
public sealed class DemoToolsMiddleware
{
    private readonly RequestDelegate _next;

    public DemoToolsMiddleware(RequestDelegate next) => _next = next;

    public static bool IsToolPath(PathString path) =>
        path.StartsWithSegments("/demo", StringComparison.OrdinalIgnoreCase) || path.StartsWithSegments("/dev", StringComparison.OrdinalIgnoreCase);

    public Task InvokeAsync(HttpContext context, IOptionsSnapshot<DemoOptions> options)
    {
        if (!options.Value.EnableTools && IsToolPath(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }
        return _next(context);
    }
}

/// <summary>
/// Đăng nhập bằng mật khẩu được cấp (Users.MustChangePassword = 1) thì mọi trang chuyển tới trang đổi mật khẩu bắt buộc;
/// AJAX nhận 403 MUST_CHANGE_PASSWORD (SPEC 2.5, UI C6).
/// </summary>
public sealed class MustChangePasswordMiddleware
{
    public const string ChangePasswordPath = "/Login/ChangePassword";
    private static readonly string[] Allowed = { ChangePasswordPath, "/Login/Logout", "/Login/Login", "/Home/Error", "/Home/AccessDenied", "/demo", "/dev" };
    private readonly RequestDelegate _next;

    public MustChangePasswordMiddleware(RequestDelegate next) => _next = next;

    public static bool IsAllowed(PathString path) => Allowed.Any(a => path.StartsWithSegments(a, StringComparison.OrdinalIgnoreCase));

    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true && user.FindFirstValue(AppClaims.MustChangePassword) == "1" && !IsAllowed(context.Request.Path))
        {
            if (RequestKinds.WantsJson(context.Request))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(ApiResponse.Fail(ErrorCodes.MustChangePassword, ApiResponse.DefaultMessage(ErrorCodes.MustChangePassword)));
                return;
            }
            context.Response.Redirect(ChangePasswordPath);
            return;
        }
        await _next(context);
    }
}
