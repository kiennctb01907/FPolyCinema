using FPolyCinema.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace FPolyCinema.Web.Services;

/// <summary>Một ghế đổi trạng thái trong sự kiện SEAT_MAP_UPDATED (CONTRACTS mục 6, payload camelCase).</summary>
public sealed record SeatChange(int SeatId, string Status, DateTime? HoldExpiresAt, int? HoldBillId);

/// <summary>Tên sự kiện SignalR (CONTRACTS mục 6).</summary>
public static class RealtimeEvents
{
    public const string SeatMapUpdated = "SEAT_MAP_UPDATED";
}

/// <summary>
/// Phát sự kiện thời gian thực bằng IHubContext&lt;CinemaHub&gt;. Service gọi sau khi transaction đã commit; chỉ gửi các ghế thay đổi.
/// Lỗi gửi không làm hỏng thao tác đã ghi DB (client tự tải lại trạng thái bằng HTTP khi kết nối lại).
/// </summary>
public class RealtimeNotifier
{
    private readonly IHubContext<CinemaHub> _hub;
    private readonly ILogger<RealtimeNotifier> _log;

    public RealtimeNotifier(IHubContext<CinemaHub> hub, ILogger<RealtimeNotifier> log)
    {
        _hub = hub;
        _log = log;
    }

    /// <summary>Kênh 1: SEAT_MAP_UPDATED { showtimeId, changes: [{ seatId, status, holdExpiresAt, holdBillId }] }.</summary>
    public async Task SeatMapUpdated(int showtimeId, IReadOnlyCollection<SeatChange> changes)
    {
        if (changes.Count == 0) return;
        try
        {
            await _hub.Clients.Group(CinemaHub.ShowtimeGroup(showtimeId)).SendAsync(RealtimeEvents.SeatMapUpdated, new
            {
                showtimeId,
                changes = changes.Select(c => new
                {
                    seatId = c.SeatId,
                    status = c.Status,
                    holdExpiresAt = c.HoldExpiresAt?.ToString("yyyy-MM-ddTHH:mm:ss"),
                    holdBillId = c.HoldBillId,
                }),
            });
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Không gửi được SEAT_MAP_UPDATED cho suất {ShowtimeId}.", showtimeId);
        }
    }

    /// <summary>Gửi nhiều nhóm thay đổi (theo suất).</summary>
    public async Task SeatMapUpdated(IEnumerable<KeyValuePair<int, List<SeatChange>>> byShowtime)
    {
        foreach (var (showtimeId, changes) in byShowtime) await SeatMapUpdated(showtimeId, changes);
    }
}
