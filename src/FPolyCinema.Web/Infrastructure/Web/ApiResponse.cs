using FPolyCinema.Web.Domain.Constants;
using Microsoft.AspNetCore.Http;

namespace FPolyCinema.Web.Infrastructure.Web;

/// <summary>
/// Dạng phản hồi AJAX chung (CONTRACTS mục 8):
/// thành công { "ok": true, "data": … }; lỗi { "ok": false, "error": { "code", "message", "details" } }.
/// </summary>
public static class ApiResponse
{
    public static object Ok(object? data = null) => new { ok = true, data };

    public static object Fail(string code, string message, object? details = null) =>
        new { ok = false, error = new { code, message, details } };

    /// <summary>Câu mặc định cho mỗi mã lỗi chung (UI mục 9: hai ý, chuyện gì xảy ra và cách sửa).</summary>
    public static string DefaultMessage(string code) => code switch
    {
        ErrorCodes.AuthRequired => "Phiên đăng nhập đã hết hạn. Bạn đăng nhập lại để tiếp tục.",
        ErrorCodes.AccessDenied => "Tài khoản của bạn không có quyền dùng chức năng này.",
        ErrorCodes.NotFound => "Không tìm thấy dữ liệu bạn cần. Có thể dữ liệu đã bị xóa, bạn tải lại trang để xem mới nhất.",
        ErrorCodes.ValidationFailed => "Dữ liệu chưa hợp lệ. Bạn kiểm tra lại các ô được đánh dấu.",
        ErrorCodes.MustChangePassword => "Bạn đang dùng mật khẩu được cấp. Đặt mật khẩu mới để tiếp tục.",
        _ => "Máy chủ đang gặp sự cố nên chưa làm được việc này. Bạn thử lại sau ít phút.",
    };

    /// <summary>Mã lỗi chung theo mã HTTP (dùng khi trang lỗi trả JSON cho AJAX).</summary>
    public static string CodeOfStatus(int status) => status switch
    {
        400 => ErrorCodes.ValidationFailed,
        401 => ErrorCodes.AuthRequired,
        403 => ErrorCodes.AccessDenied,
        404 => ErrorCodes.NotFound,
        _ => ErrorCodes.ServerError,
    };
}

/// <summary>Nhận biết request AJAX (ui.api gửi X-Requested-With và Accept: application/json).</summary>
public static class RequestKinds
{
    public static bool WantsJson(HttpRequest request)
    {
        if (string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)) return true;
        var accept = request.Headers.Accept.ToString();
        return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase) && !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }
}
