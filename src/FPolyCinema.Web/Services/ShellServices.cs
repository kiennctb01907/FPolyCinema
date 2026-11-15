using System.Security.Claims;
using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Infrastructure.Seed;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace FPolyCinema.Web.Services;

/// <summary>
/// Rạp đang chọn (ô chọn rạp ở header khách và topbar quản trị, UI mục 4). Nhân viên gắn với một rạp luôn ở rạp của mình;
/// quản lý toàn chuỗi chọn một rạp hoặc "Toàn chuỗi"; khách chọn rạp để xem. Lựa chọn lưu trong cookie fpc_cinema.
/// </summary>
public class CinemaContextService
{
    public const string CookieName = "fpc_cinema";
    private readonly IHttpContextAccessor _http;
    private List<Cinema>? _cinemas;

    public CinemaContextService(IHttpContextAccessor http) => _http = http;

    public IReadOnlyList<Cinema> Cinemas => _cinemas ??= CinemaDAL.GetActive();

    private ClaimsPrincipal? User => _http.HttpContext?.User;

    /// <summary>Rạp gắn với tài khoản nhân viên (claim CinemaId), nếu có.</summary>
    public int? BoundCinemaId => int.TryParse(User?.FindFirstValue(AppClaims.CinemaId), out var id) ? id : null;

    public bool IsChainManager => User?.IsInRole(Roles.Manager) == true && BoundCinemaId == null;

    /// <summary>Được tự chọn rạp không (nhân viên đã gắn rạp thì không).</summary>
    public bool CanChoose => BoundCinemaId == null;

    /// <summary>Rạp hiện tại; null nghĩa là toàn chuỗi (chỉ với quản lý toàn chuỗi).</summary>
    public int? CurrentCinemaId
    {
        get
        {
            if (BoundCinemaId.HasValue) return BoundCinemaId;
            var raw = _http.HttpContext?.Request.Cookies[CookieName];
            if (int.TryParse(raw, out var id) && Cinemas.Any(c => c.Id == id)) return id;
            if (IsChainManager) return null;
            return Cinemas.FirstOrDefault()?.Id;
        }
    }

    public Cinema? Current => CurrentCinemaId is int id ? Cinemas.FirstOrDefault(c => c.Id == id) : null;

    /// <summary>Lưu lựa chọn; trả false nếu không được chọn hoặc rạp không hợp lệ.</summary>
    public bool Select(int? cinemaId)
    {
        var ctx = _http.HttpContext;
        if (ctx == null || !CanChoose) return false;
        if (cinemaId is null)
        {
            if (!IsChainManager) return false;
            ctx.Response.Cookies.Delete(CookieName);
            ctx.Response.Cookies.Append(CookieName, "all", CookieOpts());
            return true;
        }
        if (!Cinemas.Any(c => c.Id == cinemaId)) return false;
        ctx.Response.Cookies.Append(CookieName, cinemaId.Value.ToString(), CookieOpts());
        return true;
    }

    private static CookieOptions CookieOpts() => new() { HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(180) };
}

/// <summary>Một tài khoản mẫu của thanh đổi vai trò (CONTRACTS mục 11).</summary>
public sealed record DemoAccount(string Slug, string Role, string Email, string Label);

/// <summary>Thanh đổi vai trò và /demo/switch-role (SPEC 5): 5 tài khoản mẫu, mỗi vai trò một tài khoản.</summary>
public static class DemoAccounts
{
    public static readonly IReadOnlyList<DemoAccount> All = new[]
    {
        new DemoAccount("manager", Roles.Manager, "manager@fpolycinema.vn", "Quản lý rạp"),
        new DemoAccount("shift", Roles.ShiftManager, "shift@fpolycinema.vn", "Quản lý ca"),
        new DemoAccount("cashier", Roles.POSCashier, "cashier@fpolycinema.vn", "Thu ngân"),
        new DemoAccount("gate", Roles.TicketInspector, "gate@fpolycinema.vn", "Soát vé"),
        new DemoAccount("khach", Roles.Customer, SeedCatalog.KhachEmail, "Khách hàng"),
    };

    /// <summary>
    /// Tài khoản mẫu thêm, không có nút trên thanh đổi vai trò: khách thứ hai cho kịch bản "Hai khách cùng giành một ghế" của trung tâm demo (SPEC S5-11).
    /// </summary>
    public static readonly IReadOnlyList<DemoAccount> Extra = new[]
    {
        new DemoAccount("khach2", Roles.Customer, SeedCatalog.Khach2Email, "Khách hàng 2"),
    };

    /// <summary>Tìm theo tên vai trò (Manager…) hoặc tên ngắn (manager, cashier…, khach2), không phân biệt hoa thường.</summary>
    public static DemoAccount? Find(string? roleOrSlug) =>
        All.Concat(Extra).FirstOrDefault(a => string.Equals(a.Role, roleOrSlug, StringComparison.OrdinalIgnoreCase) || string.Equals(a.Slug, roleOrSlug, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Thông tin người đang đăng nhập cho layout (tên, vai trò hiển thị, chữ cái đầu).</summary>
public sealed record ShellUser(int Id, string Name, string Email, string Role, string RoleLabel, string Initials);

/// <summary>
/// Dữ liệu dùng chung của 4 layout: người dùng, rạp, số yêu cầu phê duyệt đang chờ (chuông topbar), ca đang mở (topbar POS),
/// cờ bật công cụ demo. Layout gọi qua @inject, không truy cập DAL trực tiếp.
/// </summary>
public class ShellService
{
    private readonly IHttpContextAccessor _http;
    private readonly IClock _clock;
    private readonly DemoOptions _demo;

    public ShellService(IHttpContextAccessor http, CinemaContextService cinema, IClock clock, IOptionsSnapshot<DemoOptions> demo)
    {
        _http = http;
        Cinema = cinema;
        _clock = clock;
        _demo = demo.Value;
    }

    public CinemaContextService Cinema { get; }

    public bool DemoEnabled => _demo.EnableTools;

    public DateTime Now => _clock.Now;

    public ShellUser? User
    {
        get
        {
            var p = _http.HttpContext?.User;
            if (p?.Identity?.IsAuthenticated != true) return null;
            int.TryParse(p.FindFirstValue(ClaimTypes.NameIdentifier), out var id);
            var name = p.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
            var role = p.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            return new ShellUser(id, name, p.FindFirstValue(ClaimTypes.Email) ?? string.Empty, role, StatusDisplay.RoleLabel(role), Fmt.Initials(name));
        }
    }

    public bool IsStaff => RoleRouting.IsStaff(User?.Role);

    public bool IsManager => User?.Role == Roles.Manager;

    public bool IsSupervisor => User?.Role is Roles.Manager or Roles.ShiftManager;

    public string HomePath => RoleRouting.HomePath(User?.Role);

    /// <summary>Số yêu cầu phê duyệt đang chờ của rạp hiện tại (chỉ quản lý ca và quản lý rạp thấy chuông).</summary>
    public int PendingApprovals => IsSupervisor ? SupervisorApprovalDAL.CountPending(Cinema.CurrentCinemaId, _clock.Now) : 0;

    public IReadOnlyList<PendingApprovalRow> PendingApprovalList(int limit = 5) =>
        IsSupervisor ? SupervisorApprovalDAL.ListPending(Cinema.CurrentCinemaId, _clock.Now, limit) : Array.Empty<PendingApprovalRow>();

    /// <summary>Ca đang mở của người dùng (topbar POS); null nếu chưa mở ca.</summary>
    public PosShift? OpenShift => User is { } u && u.Id > 0 ? PosShiftDAL.GetOpenByUser(u.Id) : null;

    public string? DemoSlug => User == null ? null : DemoAccounts.All.FirstOrDefault(a => string.Equals(a.Email, User.Email, StringComparison.OrdinalIgnoreCase))?.Slug;
}
