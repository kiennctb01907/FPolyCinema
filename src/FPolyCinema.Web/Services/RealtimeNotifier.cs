using FPolyCinema.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace FPolyCinema.Web.Services;

/// <summary>Một ghế đổi trạng thái trong sự kiện SEAT_MAP_UPDATED (CONTRACTS mục 6, payload camelCase).</summary>
public sealed record SeatChange(int SeatId, string Status, DateTime? HoldExpiresAt, int? HoldBillId);

/// <summary>Tên sự kiện SignalR (CONTRACTS mục 6).</summary>
public static class RealtimeEvents
{
    public const string SeatMapUpdated = "SEAT_MAP_UPDATED";
    public const string PaymentStatusChanged = "PAYMENT_STATUS_CHANGED";

    // Kênh 2 – màn hình phụ của quầy
    public const string PosShowtimeSelected = "POS_SHOWTIME_SELECTED";
    public const string PosCartUpdated = "POS_CART_UPDATED";
    public const string PosPaymentQr = "POS_PAYMENT_QR";
    public const string PosPaymentDone = "POS_PAYMENT_DONE";
    public const string PosReset = "POS_RESET";
    public const string ApprovalDecided = "APPROVAL_DECIDED";

    // Kênh 3 – theo dõi một hóa đơn
    public const string CheckinUpdated = "CHECKIN_UPDATED";
    public const string FnbStatusChanged = "FNB_STATUS_CHANGED";

    // Kênh 4 – sĩ số phòng
    public const string OccupancyUpdated = "OCCUPANCY_UPDATED";

    // Kênh 5 – bếp và bảng gọi số
    public const string KdsOrderQueued = "KDS_ORDER_QUEUED";
    public const string KdsOrderUpdated = "KDS_ORDER_UPDATED";

    // Kênh 6 – vận hành phòng và bảng sảnh
    public const string ShowtimeOpsChanged = "SHOWTIME_OPS_CHANGED";

    // Kênh 7a – cảnh báo quản lý; 7b – hết món
    public const string ApprovalRequested = "APPROVAL_REQUESTED";
    public const string StockLow = "STOCK_LOW";
    public const string ProductSoldOut = "PRODUCT_SOLD_OUT";
    public const string ProductAvailable = "PRODUCT_AVAILABLE";
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

    /// <summary>
    /// Kênh 3: PAYMENT_STATUS_CHANGED { billId, code, status, cancelReason, message, ticketUrl } gửi nhóm bill_{billId} sau khi thanh toán
    /// thành công, thất bại, hoặc đến muộn phải hoàn tiền.
    /// </summary>
    public async Task PaymentStatusChanged(int billId, string code, string status, string? cancelReason, string message, string? ticketUrl)
    {
        try
        {
            await _hub.Clients.Group(CinemaHub.BillGroup(billId)).SendAsync(RealtimeEvents.PaymentStatusChanged, new { billId, code, status, cancelReason, message, ticketUrl });
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Không gửi được PAYMENT_STATUS_CHANGED cho hóa đơn {BillId}.", billId);
        }
    }

    /// <summary>Gửi một sự kiện tới một nhóm; lỗi gửi chỉ ghi log.</summary>
    private async Task Send(string group, string evt, object payload)
    {
        try
        {
            await _hub.Clients.Group(group).SendAsync(evt, payload);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Không gửi được {Event} tới nhóm {Group}.", evt, group);
        }
    }

    // ---------------------------------------------------------------- kênh 2: màn hình phụ của quầy (pos_terminal_{key})

    /// <summary>POS_SHOWTIME_SELECTED: thu ngân chọn suất (hoặc bỏ suất khi showtimeId null) – màn hình phụ tải sơ đồ ghế lớn.</summary>
    public Task PosShowtimeSelected(string terminalKey, object payload) => Send(CinemaHub.TerminalGroup(terminalKey), RealtimeEvents.PosShowtimeSelected, payload);

    /// <summary>POS_CART_UPDATED: giỏ hàng đổi (ghế đang chọn kèm điểm, vé, món, tổng tiền).</summary>
    public Task PosCartUpdated(string terminalKey, object payload) => Send(CinemaHub.TerminalGroup(terminalKey), RealtimeEvents.PosCartUpdated, payload);

    /// <summary>POS_PAYMENT_QR: thanh toán chuyển khoản – QR mô phỏng, số tiền, hạn đếm ngược.</summary>
    public Task PosPaymentQr(string terminalKey, object payload) => Send(CinemaHub.TerminalGroup(terminalKey), RealtimeEvents.PosPaymentQr, payload);

    /// <summary>POS_PAYMENT_DONE: thanh toán xong – mã hóa đơn, QR vé.</summary>
    public Task PosPaymentDone(string terminalKey, object payload) => Send(CinemaHub.TerminalGroup(terminalKey), RealtimeEvents.PosPaymentDone, payload);

    /// <summary>POS_RESET: hóa đơn bị hủy hoặc làm mới – màn hình phụ về trạng thái chờ.</summary>
    public Task PosReset(string terminalKey) => Send(CinemaHub.TerminalGroup(terminalKey), RealtimeEvents.PosReset, new { terminal = terminalKey });

    /// <summary>APPROVAL_DECIDED: kết quả duyệt từ xa gửi về máy POS đã yêu cầu và tới các quản lý của rạp (để đóng popup).</summary>
    public async Task ApprovalDecided(string? terminalKey, int cinemaId, object payload)
    {
        if (terminalKey != null) await Send(CinemaHub.TerminalGroup(terminalKey), RealtimeEvents.ApprovalDecided, payload);
        await Send(CinemaHub.ManagerAlertsGroup(cinemaId), RealtimeEvents.ApprovalDecided, payload);
    }

    // ---------------------------------------------------------------- kênh 3: theo dõi một hóa đơn (bill_{id})

    /// <summary>CHECKIN_UPDATED { billId, checkInStatus, tickets: [{ ticketId, seat, checkInStatus, checkedInAt }] }.</summary>
    public Task CheckinUpdated(int billId, object payload) => Send(CinemaHub.BillGroup(billId), RealtimeEvents.CheckinUpdated, payload);

    /// <summary>FNB_STATUS_CHANGED { billId, fnbStatus, pickupNumber, label }.</summary>
    public Task FnbStatusChanged(int billId, string fnbStatus, int? pickupNumber, string label) =>
        Send(CinemaHub.BillGroup(billId), RealtimeEvents.FnbStatusChanged,
            new { billId, fnbStatus, pickupNumber = pickupNumber is int n ? Domain.Rules.CodeGenerator.FormatPickupNumber(n) : null, label });

    // ---------------------------------------------------------------- kênh 4: sĩ số phòng (auditorium_{id})

    /// <summary>OCCUPANCY_UPDATED { showtimeId, checkedIn, sold }.</summary>
    public Task OccupancyUpdated(int auditoriumId, int showtimeId, int checkedIn, int sold) =>
        Send(CinemaHub.AuditoriumGroup(auditoriumId), RealtimeEvents.OccupancyUpdated, new { showtimeId, checkedIn, sold });

    // ---------------------------------------------------------------- kênh 5: bếp và bảng gọi số (fnb_kds_{cinemaId})

    /// <summary>KDS_ORDER_QUEUED: đơn mới vào cột "Đang chuẩn bị".</summary>
    public Task KdsOrderQueued(int cinemaId, object order) => Send(CinemaHub.KdsGroup(cinemaId), RealtimeEvents.KdsOrderQueued, order);

    /// <summary>KDS_ORDER_UPDATED: đơn đổi trạng thái (READY_FOR_PICKUP, FULFILLED, VOIDED).</summary>
    public Task KdsOrderUpdated(int cinemaId, object order) => Send(CinemaHub.KdsGroup(cinemaId), RealtimeEvents.KdsOrderUpdated, order);

    // ---------------------------------------------------------------- kênh 6: vận hành phòng và bảng sảnh (cinema_ops_{cinemaId})

    /// <summary>SHOWTIME_OPS_CHANGED { showtimeId, operationalStatus }.</summary>
    public Task ShowtimeOpsChanged(int cinemaId, int showtimeId, string operationalStatus) =>
        Send(CinemaHub.OpsGroup(cinemaId), RealtimeEvents.ShowtimeOpsChanged, new { showtimeId, operationalStatus });

    // ---------------------------------------------------------------- kênh 7a: cảnh báo quản lý (manager_alerts_{cinemaId})

    /// <summary>APPROVAL_REQUESTED: thu ngân gửi yêu cầu duyệt hủy hóa đơn từ xa.</summary>
    public Task ApprovalRequested(int cinemaId, object payload) => Send(CinemaHub.ManagerAlertsGroup(cinemaId), RealtimeEvents.ApprovalRequested, payload);

    /// <summary>STOCK_LOW { materials: [{ id, name, unit, available, minThreshold }] }.</summary>
    public Task StockLow(int cinemaId, object payload) => Send(CinemaHub.ManagerAlertsGroup(cinemaId), RealtimeEvents.StockLow, payload);

    // ---------------------------------------------------------------- kênh 7b: hết món (cinema_{cinemaId})

    /// <summary>PRODUCT_SOLD_OUT { drinkIds }.</summary>
    public Task ProductSoldOut(int cinemaId, IReadOnlyCollection<int> drinkIds) =>
        drinkIds.Count == 0 ? Task.CompletedTask : Send(CinemaHub.CinemaGroup(cinemaId), RealtimeEvents.ProductSoldOut, new { drinkIds });

    /// <summary>PRODUCT_AVAILABLE { drinkIds }.</summary>
    public Task ProductAvailable(int cinemaId, IReadOnlyCollection<int> drinkIds) =>
        drinkIds.Count == 0 ? Task.CompletedTask : Send(CinemaHub.CinemaGroup(cinemaId), RealtimeEvents.ProductAvailable, new { drinkIds });
}
