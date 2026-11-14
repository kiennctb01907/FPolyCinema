using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Chênh lệch khi đổi ghế: tổng giá mới, tổng giá cũ, số phải thu thêm (không âm).</summary>
public sealed record SwapQuote(long OldTotal, long NewTotal, long Difference, long Charge)
{
    public bool IsUpgrade => Charge > 0;
}

/// <summary>
/// Đổi hoặc nâng hạng ghế (SPEC 3.10 mục 1). Logic thuần, có unit test.
/// - Chỉ đổi được khi vé NOT_CHECKED_IN và trước FeatureStartTime.
/// - Chênh lệch = giá mới − giá cũ. Dương thì thu thêm (Payments IN); không dương thì đổi ngang, không hoàn tiền.
/// </summary>
public static class SeatSwapRules
{
    /// <summary>Lý do không đổi được vé (null khi đổi được).</summary>
    public static string? CannotSwapReason(string billStatus, string ticketStatus, DateTime featureStartTime, DateTime now)
    {
        if (billStatus != BillStatus.Finish) return "Chỉ đổi ghế cho hóa đơn đã thanh toán.";
        if (ticketStatus == TicketCheckInStatus.CheckedIn) return "Vé đã vào cửa nên không đổi ghế được.";
        if (ticketStatus != TicketCheckInStatus.NotCheckedIn) return "Vé đã bị hủy hoặc đã đổi trước đó.";
        if (now >= featureStartTime) return $"Phim đã bắt đầu lúc {Fmt.Time(featureStartTime)} nên không đổi ghế được.";
        return null;
    }

    /// <summary>Chênh lệch giữa giá niêm yết của ghế mới và ghế cũ (cùng đối tượng vé).</summary>
    public static SwapQuote Quote(IEnumerable<long> oldPrices, IEnumerable<long> newPrices)
    {
        long o = oldPrices.Sum(), n = newPrices.Sum(), diff = n - o;
        return new SwapQuote(o, n, diff, Math.Max(0, diff));
    }

    /// <summary>
    /// Phân bổ số thu thêm của cả lần đổi cho từng vé mới, theo phần tăng giá của từng vé (vé hạ hạng không nhận), dư dồn vào vé tăng nhiều nhất,
    /// để tổng giá cuối của các vé luôn khớp với số tiền hóa đơn thu thêm.
    /// </summary>
    public static long[] AllocateCharge(long charge, IReadOnlyList<long> increases)
    {
        var result = new long[increases.Count];
        long positive = increases.Where(x => x > 0).Sum();
        if (charge <= 0 || positive <= 0) return result;
        for (int i = 0; i < increases.Count; i++) if (increases[i] > 0) result[i] = charge * increases[i] / positive;
        int top = Enumerable.Range(0, increases.Count).OrderByDescending(i => increases[i]).First();
        result[top] += charge - result.Sum();
        return result;
    }

    /// <summary>Giá cuối của vé mới: giá khách đã trả cho vé cũ cộng phần thu thêm của vé này (không bao giờ thấp hơn giá đã trả).</summary>
    public static long NewFinalPrice(long oldFinalPrice, long oldListPrice, long newListPrice) =>
        oldFinalPrice + Math.Max(0, newListPrice - oldListPrice);
}
