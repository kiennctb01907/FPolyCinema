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

    // ---------------------------------------------------------------- Phase 4 (CONTRACTS C8-07)
    /// <summary>Ghế vừa có người khác giữ hoặc mua (HTTP 409); details.seats là danh sách nhãn ghế.</summary>
    public const string SeatTaken = "SEAT_TAKEN";

    /// <summary>Cách chọn để lại một ghế trống lẻ (SPEC 3.5.1); details có ghế bị bỏ lẻ và gợi ý.</summary>
    public const string OrphanSeat = "ORPHAN_SEAT";

    /// <summary>Ghế đôi phải chọn, giữ, bán cả cặp (SPEC 3.2).</summary>
    public const string SweetboxPair = "SWEETBOX_PAIR";

    /// <summary>Chọn chỗ xe lăn phải xác nhận "Vị trí dành cho khách dùng xe lăn" (SPEC 3.2).</summary>
    public const string WheelchairConfirmRequired = "WHEELCHAIR_CONFIRM_REQUIRED";

    /// <summary>Suất đã hết giờ bán (online đến FeatureStartTime + OnlineSalesCutoffMinutes, quầy + PosSalesCutoffMinutes).</summary>
    public const string SalesClosed = "SALES_CLOSED";

    /// <summary>Người dùng chưa có ca OPEN tại máy POS này (SPEC 3.6).</summary>
    public const string ShiftNotOpen = "SHIFT_NOT_OPEN";

    /// <summary>Ca còn hóa đơn waiting nên chưa đóng được (SPEC 3.6).</summary>
    public const string ShiftHasWaitingBills = "SHIFT_HAS_WAITING_BILLS";

    /// <summary>Món không đủ nguyên liệu theo định mức (SPEC 3.11).</summary>
    public const string StockInsufficient = "STOCK_INSUFFICIENT";

    /// <summary>Hủy hóa đơn đã thanh toán cần quản lý duyệt (CONTRACTS C5-03).</summary>
    public const string ApprovalRequired = "APPROVAL_REQUIRED";

    /// <summary>PIN quản lý chưa đúng; details.remaining là số lần thử còn lại.</summary>
    public const string PinInvalid = "PIN_INVALID";

    /// <summary>PIN quản lý đang bị khóa do nhập sai quá ManagerPinMaxAttempts lần; details.lockedUntil.</summary>
    public const string PinLocked = "PIN_LOCKED";

    /// <summary>Voucher không dùng được; message ghi lý do cụ thể (SPEC 3.13).</summary>
    public const string VoucherInvalid = "VOUCHER_INVALID";

    /// <summary>Số điểm đổi không hợp lệ (vượt số dư hoặc vượt MaxPointsRedeemPercent giá trị hóa đơn).</summary>
    public const string PointsInvalid = "POINTS_INVALID";

    /// <summary>Đã hết thời gian giữ ghế của hóa đơn (CONTRACTS C8-08; quầy dùng khi thanh toán sau lúc hết hạn).</summary>
    public const string PaymentExpired = "PAYMENT_EXPIRED";

    // ---------------------------------------------------------------- Phase 5 (CONTRACTS C8-08)
    /// <summary>Phim T13 trở lên hoặc suất có giới hạn giờ cho trẻ em: khách phải tích "Tôi xác nhận người xem đủ tuổi theo quy định" (SPEC 3.1).</summary>
    public const string AgeConfirmRequired = "AGE_CONFIRM_REQUIRED";

    /// <summary>Hóa đơn không đủ điều kiện tự hủy (SPEC 3.9); message ghi lý do cụ thể.</summary>
    public const string SelfCancelNotAllowed = "SELF_CANCEL_NOT_ALLOWED";

    /// <summary>Số dư ví không đủ để thanh toán (SPEC 3.13).</summary>
    public const string WalletInsufficient = "WALLET_INSUFFICIENT";

    /// <summary>Mã HTTP của từng mã lỗi; mặc định 422 (vi phạm quy tắc nghiệp vụ).</summary>
    public static int HttpStatusOf(string code) => code switch
    {
        InvalidTransition => 409,
        SeatTaken => 409,
        ValidationFailed => 400,
        AuthRequired => 401,
        AccessDenied => 403,
        MustChangePassword => 403,
        NotFound => 404,
        ServerError => 500,
        _ => 422,
    };
}
