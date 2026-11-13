namespace FPolyCinema.Web.Domain.Constants;

/// <summary>
/// Mã lỗi trả cho giao diện (CONTRACTS mục 8). Mỗi phase thêm các mã của mình.
/// </summary>
public static class ErrorCodes
{
    /// <summary>Chuyển trạng thái hóa đơn không có trong bảng CONTRACTS mục 5 (HTTP 409).</summary>
    public const string InvalidTransition = "INVALID_TRANSITION";

    // ---------------------------------------------------------------- Phase 2 (CONTRACTS C8-05, C8-09)
    /// <summary>Dữ liệu gửi lên không hợp lệ (HTTP 400).</summary>
    public const string ValidationFailed = "VALIDATION_FAILED";

    /// <summary>Không được xóa cứng vì đã có dữ liệu liên quan (SPEC 3.14); gợi ý chuyển sang ngưng hoạt động.</summary>
    public const string DeleteBlocked = "DELETE_BLOCKED";

    /// <summary>Mã OTP quên mật khẩu chưa đúng.</summary>
    public const string OtpInvalid = "OTP_INVALID";

    /// <summary>Mã OTP đã hết hạn, đã dùng hoặc đã hết lượt thử.</summary>
    public const string OtpExpired = "OTP_EXPIRED";

    /// <summary>Tài khoản đang dùng mật khẩu được cấp, phải đổi mật khẩu trước khi làm việc khác (HTTP 403).</summary>
    public const string MustChangePassword = "MUST_CHANGE_PASSWORD";

    /// <summary>Chưa đăng nhập hoặc phiên đã hết hạn (HTTP 401).</summary>
    public const string AuthRequired = "AUTH_REQUIRED";

    /// <summary>Tài khoản không có quyền dùng chức năng (HTTP 403).</summary>
    public const string AccessDenied = "ACCESS_DENIED";

    /// <summary>Không tìm thấy dữ liệu (HTTP 404).</summary>
    public const string NotFound = "NOT_FOUND";

    /// <summary>Lỗi không lường trước ở máy chủ (HTTP 500); không lộ chi tiết kỹ thuật.</summary>
    public const string ServerError = "SERVER_ERROR";

    /// <summary>Mã HTTP của từng mã lỗi; mặc định 422 (vi phạm quy tắc nghiệp vụ).</summary>
    public static int HttpStatusOf(string code) => code switch
    {
        InvalidTransition => 409,
        ValidationFailed => 400,
        AuthRequired => 401,
        AccessDenied => 403,
        MustChangePassword => 403,
        NotFound => 404,
        ServerError => 500,
        _ => 422,
    };
}
