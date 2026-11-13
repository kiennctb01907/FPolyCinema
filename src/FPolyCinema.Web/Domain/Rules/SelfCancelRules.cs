using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Dữ kiện của một hóa đơn khi xét tự hủy vé (SPEC 3.9).</summary>
public sealed record SelfCancelFacts(string Channel, string Status, bool IsOwner, bool IsCorporate, DateTime StartTime,
    IReadOnlyCollection<string> TicketCheckInStatuses, string FnbStatus, int CancelsThisMonth);

/// <summary>Kết quả xét tự hủy: được hay không, lý do (câu cho người dùng) và số lần hủy còn lại trong tháng.</summary>
public sealed record SelfCancelCheck(bool Ok, string? Reason, int RemainingThisMonth, DateTime Deadline);

/// <summary>
/// Điều kiện khách tự hủy vé (SPEC 3.9). Logic thuần, có unit test.
/// - Hóa đơn ONLINE ở trạng thái finish, của chính khách đó, không phải hóa đơn CORPORATE.
/// - now ≤ StartTime − SelfCancelMinMinutesBeforeStart.
/// - Mọi vé đều NOT_CHECKED_IN; FnbStatus là NONE hoặc PENDING_PICKUP.
/// - Số lần CANCELLED_BY_USER trong tháng dương lịch hiện tại (giờ Việt Nam) nhỏ hơn SelfCancelMaxPerMonth.
/// Không đủ điều kiện thì SELF_CANCEL_NOT_ALLOWED kèm lý do cụ thể.
/// </summary>
public static class SelfCancelRules
{
    /// <summary>Đầu tháng dương lịch chứa thời điểm now (để đếm số lần đã tự hủy).</summary>
    public static DateTime MonthStart(DateTime now) => new(now.Year, now.Month, 1);

    public static SelfCancelCheck Check(SelfCancelFacts f, DateTime now, int minMinutesBeforeStart, int maxPerMonth)
    {
        var deadline = f.StartTime.AddMinutes(-minMinutesBeforeStart);
        int remaining = Math.Max(0, maxPerMonth - f.CancelsThisMonth);
        SelfCancelCheck No(string reason) => new(false, reason, remaining, deadline);

        if (!f.IsOwner) return No("Hóa đơn này không thuộc tài khoản của bạn.");
        if (f.Channel != BillChannel.Online || f.IsCorporate) return No("Chỉ vé mua online mới tự hủy được. Vé mua tại quầy hoặc vé doanh nghiệp bạn liên hệ quầy vé.");
        if (f.Status != BillStatus.Finish) return No(f.Status == BillStatus.Cancel ? "Vé này đã được hủy." : "Vé này chưa thanh toán xong.");
        if (now > deadline)
            return No($"Vé chỉ tự hủy được trước giờ chiếu {HoursText(minMinutesBeforeStart)}. Suất này bắt đầu lúc {f.StartTime:HH\\:mm}.");
        if (f.TicketCheckInStatuses.Any(s => s != TicketCheckInStatus.NotCheckedIn))
            return No("Vé đã có ghế vào cửa hoặc đã đổi nên không tự hủy được.");
        if (f.FnbStatus is not (FnbStatus.None or FnbStatus.PendingPickup))
            return No("Bắp nước của đơn này đã được làm nên không tự hủy được.");
        if (f.CancelsThisMonth >= maxPerMonth)
            return No($"Bạn đã tự hủy {f.CancelsThisMonth} lần trong tháng này (tối đa {maxPerMonth} lần mỗi tháng).");
        return new SelfCancelCheck(true, null, remaining, deadline);
    }

    /// <summary>"2 tiếng" cho 120 phút, "90 phút" cho 90 phút.</summary>
    public static string HoursText(int minutes) => minutes % 60 == 0 ? $"{minutes / 60} tiếng" : $"{minutes} phút";
}
