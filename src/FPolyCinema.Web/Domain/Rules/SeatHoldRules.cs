using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Một ghế đôi: Id ghế, nhãn và PairGroupId.</summary>
public sealed record PairSeat(int SeatId, string Label, int? PairGroupId);

/// <summary>
/// Luật cặp ghế đôi (SPEC 3.2): SWEETBOX luôn theo cặp cố định PairGroupId; chọn, giữ, bán, đổi, hủy luôn đi cả cặp (SWEETBOX_PAIR).
/// Logic thuần, có unit test.
/// </summary>
public static class SweetboxRule
{
    /// <summary>Các cặp bị chọn thiếu một ghế: trả danh sách nhãn của cả cặp, rỗng khi hợp lệ.</summary>
    public static List<IReadOnlyList<PairSeat>> IncompletePairs(IEnumerable<PairSeat> sweetboxSeats, IReadOnlySet<int> selected)
    {
        var list = new List<IReadOnlyList<PairSeat>>();
        foreach (var pair in sweetboxSeats.Where(s => s.PairGroupId != null).GroupBy(s => s.PairGroupId))
        {
            var seats = pair.OrderBy(s => s.Label.Length).ThenBy(s => s.Label, StringComparer.Ordinal).ToList();
            int chosen = seats.Count(s => selected.Contains(s.SeatId));
            if (chosen > 0 && chosen < seats.Count) list.Add(seats);
        }
        return list;
    }

    /// <summary>Thêm ghế còn lại của mỗi cặp có ít nhất một ghế được chọn (giao diện chọn cả cặp bằng một lần bấm).</summary>
    public static HashSet<int> ExpandToPairs(IEnumerable<PairSeat> sweetboxSeats, IEnumerable<int> selected)
    {
        var set = selected.ToHashSet();
        var all = sweetboxSeats.Where(s => s.PairGroupId != null).ToList();
        foreach (var g in all.Where(s => set.Contains(s.SeatId)).Select(s => s.PairGroupId).Distinct().ToList())
            foreach (var s in all.Where(x => x.PairGroupId == g)) set.Add(s.SeatId);
        return set;
    }

    /// <summary>Câu báo lỗi theo UI mục 9: "Ghế đôi M1–M2 được bán theo cặp. Bạn chọn cả hai ghế nhé."</summary>
    public static string Message(IReadOnlyList<PairSeat> pair) =>
        $"Ghế đôi {pair[0].Label}–{pair[^1].Label} được bán theo cặp. Bạn chọn cả hai ghế nhé.";
}

/// <summary>
/// Quy tắc giữ ghế (SPEC 3.5). Logic thuần, có unit test.
/// </summary>
public static class SeatHoldRules
{
    /// <summary>
    /// Trạng thái hiệu lực của ghế khi đọc (bước 3 và S3.5-13): ghế bảo trì là MAINTENANCE; ghế HELD đã hết hạn (HoldExpiresAt ≤ now)
    /// coi là AVAILABLE, nên ghế không bao giờ bị treo kể cả khi bộ quét chưa chạy.
    /// </summary>
    public static string EffectiveStatus(string status, DateTime? holdExpiresAt, bool isMaintenance, DateTime now)
    {
        if (isMaintenance) return SeatDisplayStatus.Maintenance;
        if (status == ShowtimeSeatStatus.Held && (holdExpiresAt == null || holdExpiresAt <= now)) return SeatDisplayStatus.Available;
        return status;
    }

    /// <summary>Ghế giữ được cho hóa đơn: đang AVAILABLE (theo trạng thái hiệu lực) hoặc đang HELD còn hạn bởi chính hóa đơn này.</summary>
    public static bool CanHold(string effectiveStatus, int? holdBillId, int billId) =>
        effectiveStatus == SeatDisplayStatus.Available || (effectiveStatus == SeatDisplayStatus.Held && holdBillId == billId);

    /// <summary>Hạn bán của suất theo kênh: online đến FeatureStartTime + OnlineSalesCutoffMinutes, quầy đến FeatureStartTime + PosSalesCutoffMinutes.</summary>
    public static DateTime SalesDeadline(DateTime featureStartTime, string channel, CinemaRules rules) =>
        featureStartTime.AddMinutes(channel == BillChannel.Pos ? rules.PosSalesCutoffMinutes : rules.OnlineSalesCutoffMinutes);

    /// <summary>Suất còn mở bán (SALES_CLOSED nếu không): chưa hủy và chưa quá hạn bán của kênh.</summary>
    public static bool IsSalesOpen(DateTime featureStartTime, string operationalStatus, string channel, DateTime now, CinemaRules rules) =>
        operationalStatus != OperationalStatus.Cancelled && now < SalesDeadline(featureStartTime, channel, rules);

    /// <summary>HoldExpiresAt: đặt ở lần giữ đầu tiên bằng now + SeatHoldMinutes; đổi ghế không làm tăng thời gian giữ.</summary>
    public static DateTime HoldExpiry(DateTime? current, DateTime now, CinemaRules rules) =>
        current ?? now.AddMinutes(rules.SeatHoldMinutes);

    /// <summary>Thu ngân gia hạn được một lần, khi hóa đơn còn hạn giữ ghế.</summary>
    public static bool CanExtend(DateTime? holdExpiresAt, bool alreadyExtended, DateTime now) =>
        !alreadyExtended && holdExpiresAt != null && holdExpiresAt > now;

    /// <summary>Hóa đơn waiting đã hết hạn giữ ghế (bộ quét sẽ hủy EXPIRED).</summary>
    public static bool IsExpired(DateTime? holdExpiresAt, DateTime now) => holdExpiresAt != null && holdExpiresAt <= now;
}
