namespace FPolyCinema.Web.Domain.Constants;

/// <summary>Tên policy phân quyền (CONTRACTS mục 2). Program.cs khai báo bằng AddPolicy với đúng tên này.</summary>
public static class Policies
{
    /// <summary>POSCashier, TicketInspector, ShiftManager, Manager – giữ đúng tên slide bài 4.</summary>
    public const string EmployeeOrManager = "EmployeeOrManager";

    /// <summary>Manager – giữ đúng tên slide bài 4.</summary>
    public const string ManagerOnly = "ManagerOnly";

    /// <summary>POSCashier, ShiftManager, Manager.</summary>
    public const string PosStaff = "PosStaff";

    /// <summary>TicketInspector, ShiftManager, Manager.</summary>
    public const string GateStaff = "GateStaff";

    /// <summary>ShiftManager, Manager.</summary>
    public const string SupervisorOrManager = "SupervisorOrManager";

    /// <summary>Customer.</summary>
    public const string CustomerOnly = "CustomerOnly";
}

/// <summary>Tên claim trong cookie đăng nhập (SPEC 2.5): ngoài NameIdentifier, Name, Email, Role của ClaimTypes.</summary>
public static class AppClaims
{
    /// <summary>Rạp của nhân viên; không có với khách và quản lý toàn chuỗi.</summary>
    public const string CinemaId = "CinemaId";

    /// <summary>"1" khi tài khoản đang dùng mật khẩu được cấp (Users.MustChangePassword = 1).</summary>
    public const string MustChangePassword = "MustChangePassword";

    /// <summary>Dấu của mật khẩu hiện tại; đổi mật khẩu thì các phiên mang dấu cũ bị đăng xuất.</summary>
    public const string PasswordStamp = "PasswordStamp";
}

/// <summary>Hành động ghi vào AuditLogs (CONTRACTS C3-39), mỗi phase thêm hành động của mình.</summary>
public static class AuditAction
{
    public const string UserLocked = "USER_LOCKED";
    public const string UserUnlocked = "USER_UNLOCKED";

    /// <summary>Sửa bảng giá: các dòng PricingRules của một rạp hoặc mức giảm AudienceDiscounts (SPEC 2.7).</summary>
    public const string PricingUpdated = "PRICING_UPDATED";

    /// <summary>Hủy hóa đơn đã thanh toán (VOIDED_BY_POS), quản lý duyệt (SPEC 2.7, 3.6).</summary>
    public const string VoidBill = "VOID_BILL";

    /// <summary>Nhân viên quầy bỏ qua luật ghế mồ côi kèm lý do (SPEC 2.7, 3.5).</summary>
    public const string OrphanSeatOverride = "ORPHAN_SEAT_OVERRIDE";
}
