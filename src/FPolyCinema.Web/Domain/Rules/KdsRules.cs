using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>
/// Bếp, bảng gọi số và nút "Tôi đã tới rạp" (SPEC 3.11, UI P4, P5, C4). Logic thuần, có unit test.
/// </summary>
public static class KdsRules
{
    public const string WaitOk = "ok", WaitWarn = "warn", WaitLate = "late";

    /// <summary>Màu thời gian chờ của thẻ đơn: xanh dưới 5 phút, vàng 5–10 phút, đỏ trên 10 phút.</summary>
    public static string WaitLevel(TimeSpan waited) =>
        waited < TimeSpan.FromMinutes(5) ? WaitOk : waited <= TimeSpan.FromMinutes(10) ? WaitWarn : WaitLate;

    /// <summary>Nút "Tôi đã tới rạp – làm bắp nước ngay" chỉ hiện từ StartTime − ImHereWindowMinutesBeforeStart đến EndTime.</summary>
    public static bool InImHereWindow(DateTime startTime, DateTime endTime, DateTime now, int windowMinutes) =>
        now >= startTime.AddMinutes(-windowMinutes) && now <= endTime;

    /// <summary>
    /// Bắt đầu làm món cho đơn online (khách bấm "Tôi đã tới rạp" hoặc thu ngân quét QR ở quầy bắp nước): chỉ hóa đơn finish có bắp nước
    /// PENDING_PICKUP. Trả lý do khi không được (null khi được).
    /// </summary>
    public static string? CannotStartReason(string billStatus, string fnbStatus)
    {
        if (billStatus != BillStatus.Finish) return "Hóa đơn chưa thanh toán hoặc đã hủy.";
        return fnbStatus switch
        {
            FnbStatus.PendingPickup => null,
            FnbStatus.None => "Hóa đơn không có bắp nước.",
            FnbStatus.Preparing => "Bếp đang chuẩn bị đơn này.",
            FnbStatus.ReadyForPickup => "Đơn đã sẵn sàng, mời khách tới quầy nhận.",
            FnbStatus.Fulfilled => "Đơn bắp nước đã giao xong.",
            _ => "Đơn bắp nước đã bị hủy.",
        };
    }

    /// <summary>Chuyển trạng thái ở bếp: PREPARING → READY_FOR_PICKUP ("Sẵn sàng giao"), READY_FOR_PICKUP → FULFILLED ("Đã giao").</summary>
    public static bool CanAdvance(string from, string to) =>
        (from == FnbStatus.Preparing && to == FnbStatus.ReadyForPickup) || (from == FnbStatus.ReadyForPickup && to == FnbStatus.Fulfilled);
}
