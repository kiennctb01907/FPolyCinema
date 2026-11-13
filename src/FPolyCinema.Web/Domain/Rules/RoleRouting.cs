using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Khu vực giao diện, mỗi khu vực một layout (UI mục 4).</summary>
public static class UiArea
{
    public const string Customer = "customer";
    public const string Admin = "admin";
    public const string Pos = "pos";
}

/// <summary>
/// Quy tắc thuần: trang chính và layout của từng vai trò (SPEC 2.5 "đăng nhập xong chuyển về trang chính của vai trò"),
/// khu vực của một đường dẫn (UI D3 "dùng layout của khu vực đang đứng").
/// </summary>
public static class RoleRouting
{
    // TODO(Phase 6): TicketInspector về /pos/gate-inspector khi có máy soát vé.
    /// <summary>Trang chính sau khi đăng nhập: Manager về dashboard /admin (UI A1, ManagerOnly); ShiftManager và POSCashier về màn hình bán /pos/index.</summary>
    public static string HomePath(string? role) => role switch
    {
        Roles.Manager => "/admin",
        Roles.ShiftManager or Roles.POSCashier => "/pos/index",
        Roles.TicketInspector => "/me",
        _ => "/",
    };

    /// <summary>Khu vực (layout) mặc định của vai trò.</summary>
    public static string AreaOfRole(string? role) => role switch
    {
        Roles.Manager or Roles.ShiftManager => UiArea.Admin,
        Roles.POSCashier or Roles.TicketInspector => UiArea.Pos,
        _ => UiArea.Customer,
    };

    /// <summary>
    /// Khu vực của một đường dẫn: /admin… và /scheduler là quản trị, /pos… là quầy; khách và người chưa đăng nhập
    /// luôn ở web khách. Đường dẫn không thuộc khu vực nào thì theo vai trò.
    /// </summary>
    public static string AreaOfPath(string? path, string? role)
    {
        if (string.IsNullOrEmpty(role) || role == Roles.Customer) return UiArea.Customer;
        var p = (path ?? string.Empty).Split('?')[0].ToLowerInvariant();
        if (p.StartsWith("/admin") || p.StartsWith("/scheduler")) return UiArea.Admin;
        if (p.StartsWith("/pos")) return UiArea.Pos;
        return AreaOfRole(role);
    }

    /// <summary>
    /// Sau khi đổi vai trò (thanh demo) có quay lại được trang đang đứng không: khách không vào khu vực quản trị, quầy và /me;
    /// nhân viên không quay lại trang đăng nhập, đăng ký.
    /// </summary>
    public static bool CanReturnTo(string? path, string? role)
    {
        var p = (path ?? string.Empty).Split('?')[0].ToLowerInvariant();
        if (p is "/login/login" or "/login/register" or "/login/forgotpassword") return false;
        if (role == Roles.Customer || string.IsNullOrEmpty(role))
            return !(p.StartsWith("/admin") || p.StartsWith("/pos") || p.StartsWith("/scheduler") || p == "/me" || p.StartsWith("/me/"));
        // Các trang chỉ quản lý rạp được vào (ManagerOnly)
        if (p.StartsWith("/admin/users") || p.EndsWith("/create") || p.EndsWith("/edit")) return role == Roles.Manager;
        return true;
    }

    /// <summary>Tên layout của khu vực.</summary>
    public static string LayoutOf(string area) => area switch
    {
        UiArea.Admin => "_LayoutAdmin",
        UiArea.Pos => "_LayoutPos",
        _ => "_LayoutCustomer",
    };

    /// <summary>Nhân viên (mọi vai trò trừ khách).</summary>
    public static bool IsStaff(string? role) => role is Roles.POSCashier or Roles.TicketInspector or Roles.ShiftManager or Roles.Manager;
}
