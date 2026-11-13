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

    // ---------------------------------------------------------------- Phase 3 (CONTRACTS C8-06)
    /// <summary>KDM của phim tại rạp không phủ trọn [FeatureStartTime, EndTime] của suất (SPEC 3.3).</summary>
    public const string KdmInvalid = "KDM_INVALID";

    /// <summary>Phim chưa có số Giấy phép phân loại phim nên chưa tạo suất được (SPEC 3.1).</summary>
    public const string LicenseMissing = "LICENSE_MISSING";

    /// <summary>Phim nhãn C (không được phép phổ biến) chỉ lưu hồ sơ, không tạo suất (SPEC 3.1).</summary>
    public const string RatingC = "RATING_C";

    /// <summary>Hai suất cùng phòng có khoảng [StartTime, CleaningEndTime) giao nhau (SPEC 3.3).</summary>
    public const string ShowtimeOverlap = "SHOWTIME_OVERLAP";

    /// <summary>Suất đã có khách mua hoặc đang giữ ghế (LOCKED_HAS_TICKETS) nên không đổi giờ, phòng hay phim (SPEC 3.3).</summary>
    public const string ShowtimeLocked = "SHOWTIME_LOCKED";

    /// <summary>Vé trẻ em không áp dụng cho phim có nhãn tuổi cao hơn (SPEC 3.1 mục 2).</summary>
    public const string AgeRestricted = "AGE_RESTRICTED";

    /// <summary>Vé trẻ em chỉ bán cho suất kết thúc trước mốc giờ theo quy định (SPEC 3.1 mục 4).</summary>
    public const string ChildCurfew = "CHILD_CURFEW";

    /// <summary>Phim K: vé trẻ dưới 13 tuổi cần người lớn hoặc người giám hộ đi cùng (SPEC 3.1 mục 3).</summary>
    public const string GuardianRequired = "GUARDIAN_REQUIRED";

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
