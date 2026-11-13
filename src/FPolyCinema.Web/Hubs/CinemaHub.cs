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
}
