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
}
