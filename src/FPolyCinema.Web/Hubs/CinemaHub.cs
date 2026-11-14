using System.Security.Claims;
using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FPolyCinema.Web.Hubs;

/// <summary>
/// Hub thời gian thực tại /hubs/cinema (CONTRACTS mục 6). Hub chỉ có hàm tham gia và rời nhóm; mọi thao tác thay đổi dữ liệu đi qua Controller
/// (POST có Anti-Forgery và phân quyền), service phát sự kiện bằng IHubContext&lt;CinemaHub&gt; qua RealtimeNotifier.
/// Mỗi hàm tham gia kiểm tra quyền theo cột "Ai được tham gia". Các kênh khác thêm ở phase của chúng.
/// </summary>
[AllowAnonymous]
public class CinemaHub : Hub
{
    public const string Path = "/hubs/cinema";

    /// <summary>Tên nhóm kênh 1 (sơ đồ ghế).</summary>
    public static string ShowtimeGroup(int showtimeId) => $"showtime_{showtimeId}";

    private readonly SeatMapService _seatMaps;

    public CinemaHub(SeatMapService seatMaps) => _seatMaps = seatMaps;

    /// <summary>Kênh 1 – sơ đồ ghế: mọi người được tham gia, kể cả khách chưa đăng nhập; suất phải tồn tại.</summary>
    public async Task JoinShowtime(int showtimeId)
    {
        if (!_seatMaps.ShowtimeExists(showtimeId))
            throw new HubException("Không tìm thấy suất chiếu.");
        await Groups.AddToGroupAsync(Context.ConnectionId, ShowtimeGroup(showtimeId));
    }

    public Task LeaveShowtime(int showtimeId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, ShowtimeGroup(showtimeId));

    /// <summary>Tên nhóm kênh 3 (theo dõi một hóa đơn).</summary>
    public static string BillGroup(int billId) => $"bill_{billId}";

    /// <summary>Kênh 3 – theo dõi một hóa đơn: chỉ chủ hóa đơn (khách đặt online) hoặc nhân viên.</summary>
    public async Task JoinBill(int billId)
    {
        var bill = BillDAL.GetById(billId) ?? throw new HubException("Không tìm thấy hóa đơn.");
        var user = Context.User;
        bool staff = user?.Identity?.IsAuthenticated == true && new[] { Roles.POSCashier, Roles.TicketInspector, Roles.ShiftManager, Roles.Manager }.Any(r => user.IsInRole(r));
        bool owner = int.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) && (bill.CustomerId == uid || bill.UserId == uid);
        if (!staff && !owner) throw new HubException("Bạn không có quyền theo dõi hóa đơn này.");
        await Groups.AddToGroupAsync(Context.ConnectionId, BillGroup(billId));
    }

    public Task LeaveBill(int billId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, BillGroup(billId));

    // ------------------------------------------------------------------ Phase 6

    /// <summary>Tên nhóm kênh 2. Khóa máy là {CinemaId}-{TerminalCode} (ShiftService.TerminalKey) vì mã máy POS-01 lặp lại ở mỗi rạp.</summary>
    public static string TerminalGroup(string terminalKey) => $"pos_terminal_{terminalKey}";

    public static string AuditoriumGroup(int auditoriumId) => $"auditorium_{auditoriumId}";

    public static string KdsGroup(int cinemaId) => $"fnb_kds_{cinemaId}";

    public static string OpsGroup(int cinemaId) => $"cinema_ops_{cinemaId}";

    public static string ManagerAlertsGroup(int cinemaId) => $"manager_alerts_{cinemaId}";

    public static string CinemaGroup(int cinemaId) => $"cinema_{cinemaId}";

    private bool InRole(params string[] roles) => Context.User?.Identity?.IsAuthenticated == true && roles.Any(r => Context.User.IsInRole(r));

    private int? UserId => int.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private int? BoundCinema => int.TryParse(Context.User?.FindFirstValue(AppClaims.CinemaId), out var id) ? id : null;

    /// <summary>Nhân viên gắn rạp chỉ tham gia nhóm của rạp mình; quản lý toàn chuỗi tham gia mọi rạp.</summary>
    private bool CinemaAllowed(int cinemaId) => BoundCinema == null ? Context.User?.IsInRole(Roles.Manager) == true : BoundCinema == cinemaId;

    /// <summary>
    /// Kênh 2 – màn hình phụ của quầy: PosStaff có ca OPEN đúng máy đó; màn hình phụ (isDisplay) tham gia ẩn danh, chỉ nhận.
    /// code là khóa máy {CinemaId}-{TerminalCode}.
    /// </summary>
    public async Task JoinTerminal(string code, bool isDisplay)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 40) throw new HubException("Mã máy POS không hợp lệ.");
        if (!isDisplay)
        {
            var shift = InRole(Roles.PosStaff) && UserId is int uid ? PosShiftDAL.GetOpenByUser(uid) : null;
            if (shift == null || ShiftService.TerminalKey(shift.CinemaId, shift.TerminalCode) != code)
                throw new HubException("Bạn cần mở ca tại máy này trước.");
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, TerminalGroup(code));
    }

    public Task LeaveTerminal(string code, bool isDisplay) => Groups.RemoveFromGroupAsync(Context.ConnectionId, TerminalGroup(code));

    /// <summary>Kênh 4 – sĩ số phòng: EmployeeOrManager.</summary>
    public async Task JoinAuditorium(int auditoriumId)
    {
        if (!InRole(Roles.POSCashier, Roles.TicketInspector, Roles.ShiftManager, Roles.Manager)) throw new HubException("Chỉ nhân viên xem được sĩ số phòng.");
        var a = AuditoriumDAL.GetById(auditoriumId) ?? throw new HubException("Không tìm thấy phòng chiếu.");
        if (!CinemaAllowed(a.CinemaId)) throw new HubException("Phòng này thuộc rạp khác.");
        await Groups.AddToGroupAsync(Context.ConnectionId, AuditoriumGroup(auditoriumId));
    }

    public Task LeaveAuditorium(int auditoriumId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, AuditoriumGroup(auditoriumId));

    /// <summary>Kênh 5 – bếp: PosStaff của rạp; bảng gọi số (isBoard) tham gia ẩn danh, chỉ nhận.</summary>
    public async Task JoinKds(int cinemaId, bool isBoard)
    {
        if (CinemaDAL.GetById(cinemaId) == null) throw new HubException("Không tìm thấy rạp.");
        if (!isBoard && !(InRole(Roles.PosStaff) && CinemaAllowed(cinemaId))) throw new HubException("Chỉ nhân viên quầy của rạp xem được màn hình bếp.");
        await Groups.AddToGroupAsync(Context.ConnectionId, KdsGroup(cinemaId));
    }

    public Task LeaveKds(int cinemaId, bool isBoard) => Groups.RemoveFromGroupAsync(Context.ConnectionId, KdsGroup(cinemaId));

    /// <summary>Kênh 6 – vận hành phòng và bảng sảnh: mọi người, chỉ nhận.</summary>
    public async Task JoinOps(int cinemaId)
    {
        if (CinemaDAL.GetById(cinemaId) == null) throw new HubException("Không tìm thấy rạp.");
        await Groups.AddToGroupAsync(Context.ConnectionId, OpsGroup(cinemaId));
    }

    public Task LeaveOps(int cinemaId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, OpsGroup(cinemaId));

    /// <summary>Kênh 7a – cảnh báo quản lý: SupervisorOrManager của rạp.</summary>
    public async Task JoinManagerAlerts(int cinemaId)
    {
        if (!InRole(Roles.ShiftManager, Roles.Manager) || !CinemaAllowed(cinemaId)) throw new HubException("Chỉ quản lý của rạp nhận được cảnh báo này.");
        await Groups.AddToGroupAsync(Context.ConnectionId, ManagerAlertsGroup(cinemaId));
    }

    public Task LeaveManagerAlerts(int cinemaId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, ManagerAlertsGroup(cinemaId));

    /// <summary>Kênh 7b – hết món: mọi người.</summary>
    public async Task JoinCinema(int cinemaId)
    {
        if (CinemaDAL.GetById(cinemaId) == null) throw new HubException("Không tìm thấy rạp.");
        await Groups.AddToGroupAsync(Context.ConnectionId, CinemaGroup(cinemaId));
    }

    public Task LeaveCinema(int cinemaId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, CinemaGroup(cinemaId));
}
