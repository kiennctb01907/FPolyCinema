namespace FPolyCinema.Web.Domain.Constants;

/// <summary>Vai trò người dùng (CONTRACTS mục 1, cột Users.Role).</summary>
public static class Roles
{
    public const string Customer = "Customer";
    public const string POSCashier = "POSCashier";
    public const string TicketInspector = "TicketInspector";
    public const string ShiftManager = "ShiftManager";
    public const string Manager = "Manager";

    public static readonly string[] All = { Customer, POSCashier, TicketInspector, ShiftManager, Manager };

    /// <summary>Các vai trò thuộc policy PosStaff (CONTRACTS mục 2).</summary>
    public static readonly string[] PosStaff = { POSCashier, ShiftManager, Manager };

    /// <summary>Các vai trò thuộc policy SupervisorOrManager (CONTRACTS mục 2).</summary>
    public static readonly string[] SupervisorOrManager = { ShiftManager, Manager };

    public static bool IsValid(string? role) => role != null && Array.IndexOf(All, role) >= 0;
}

/// <summary>
/// Tác nhân không phải người dùng khi đổi trạng thái hóa đơn (CONTRACTS mục 5):
/// webhook thanh toán, bộ quét giữ ghế hết hạn.
/// </summary>
public static class Actors
{
    public const string System = "System";
}
