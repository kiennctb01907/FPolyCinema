using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>
/// Một ghế của sơ đồ khi kiểm luật ghế mồ côi. Blocked: ghế đã bị người khác chiếm (HELD còn hạn của hóa đơn khác, BOOKED,
/// HOUSE_HOLD, MAINTENANCE). Ghế đang được chọn truyền riêng qua tập selected.
/// </summary>
public sealed record SeatSlot(int SeatId, int Row, int Col, string Label, string SeatType, bool Blocked);

/// <summary>Vi phạm luật ghế mồ côi: các ghế bị bỏ lẻ, gợi ý vị trí hợp lệ gần nhất (null nếu không có) và câu báo lỗi.</summary>
public sealed record OrphanViolation(IReadOnlyList<SeatSlot> Orphans, IReadOnlyList<SeatSlot> Suggestion, string Message);

/// <summary>
/// Luật ghế mồ côi (SPEC 3.5.1). Logic thuần, có unit test.
/// - Đoạn ghế (SPEC 3.2): dãy ghế liền nhau trong một hàng, ngăn cách bởi ô trống (lối đi) hoặc tường. Ghế WHEELCHAIR và SWEETBOX
///   không xét và được coi như biên, nên chúng cũng cắt đoạn.
/// - Ghế bị chiếm: Blocked hoặc đang được chọn.
/// - Vi phạm khi sau lựa chọn có ghế trống S mà hai bên S đều là ghế bị chiếm hoặc biên, và ít nhất một ghế kề S thuộc lựa chọn hiện tại
///   (ghế lẻ có sẵn từ trước không tính là lỗi của người đang chọn).
/// - Ngoại lệ: trong đoạn đó không còn vị trí nào khác để đặt cùng số ghế liền nhau mà không tạo ghế lẻ (thử mọi vị trí).
///   Việc PosStaff chủ động bỏ qua luật do SeatHoldService xử lý (cần lý do, ghi AuditLog).
/// - Lỗi trả về ghế bị bỏ lẻ và gợi ý: chọn thêm ghế đó, hoặc dời lựa chọn sang vị trí hợp lệ gần nhất.
/// </summary>
public static class OrphanSeatValidator
{
    /// <summary>Loại ghế được xét luật ghế mồ côi; các loại khác (xe lăn, ghế đôi) là biên.</summary>
    public static bool IsConsidered(string seatType) => seatType is SeatType.Standard or SeatType.Vip;

    /// <summary>Chia các ghế được xét thành đoạn: cùng hàng, cột liên tiếp.</summary>
    public static List<List<SeatSlot>> Segments(IEnumerable<SeatSlot> seats)
    {
        var result = new List<List<SeatSlot>>();
        foreach (var row in seats.Where(s => IsConsidered(s.SeatType)).GroupBy(s => s.Row).OrderBy(g => g.Key))
        {
            List<SeatSlot>? cur = null;
            foreach (var s in row.OrderBy(s => s.Col))
            {
                if (cur == null || s.Col != cur[^1].Col + 1)
                {
                    cur = new List<SeatSlot>();
                    result.Add(cur);
                }
                cur.Add(s);
            }
        }
        return result;
    }

    /// <summary>
    /// Kiểm một lựa chọn. Trả null khi hợp lệ (kể cả trường hợp ngoại lệ đoạn ngắn), ngược lại trả vi phạm đầu tiên theo thứ tự hàng.
    /// </summary>
    public static OrphanViolation? Validate(IEnumerable<SeatSlot> seats, IReadOnlySet<int> selected)
    {
        var all = seats.ToList();
        foreach (var seg in Segments(all))
        {
            var mine = seg.Select(s => selected.Contains(s.SeatId)).ToArray();
            int k = mine.Count(x => x);
            if (k == 0) continue;
            var blocked = seg.Select(s => s.Blocked).ToArray();
            var orphans = OrphansOf(blocked, mine);
            if (orphans.Count == 0) continue;

            // Ngoại lệ: không còn vị trí nào khác đặt được k ghế liền nhau mà không tạo ghế lẻ
            int first = Array.IndexOf(mine, true);
            int? best = null;
            for (int p = 0; p + k <= seg.Count; p++)
            {
                bool free = true;
                for (int i = p; i < p + k; i++) if (blocked[i]) { free = false; break; }
                if (!free) continue;
                var place = new bool[seg.Count];
                for (int i = p; i < p + k; i++) place[i] = true;
                if (OrphansOf(blocked, place).Count > 0) continue;
                if (best == null || Math.Abs(p - first) < Math.Abs(best.Value - first)) best = p;
            }
            if (best == null) continue;

            var orphanSeats = orphans.Select(i => seg[i]).ToList();
            var suggestion = seg.Skip(best.Value).Take(k).ToList();
            return new OrphanViolation(orphanSeats, suggestion, Message(all, seg, orphans, suggestion));
        }
        return null;
    }

    /// <summary>Chỉ số các ghế trống bị bỏ lẻ trong một đoạn: hai bên đều bị chiếm hoặc là biên, và có ít nhất một bên thuộc lựa chọn.</summary>
    private static List<int> OrphansOf(bool[] blocked, bool[] mine)
    {
        var list = new List<int>();
        int n = blocked.Length;
        bool Occupied(int i) => i < 0 || i >= n || blocked[i] || mine[i];
        bool Mine(int i) => i >= 0 && i < n && mine[i];
        for (int i = 0; i < n; i++)
        {
            if (blocked[i] || mine[i]) continue;
            if (Occupied(i - 1) && Occupied(i + 1) && (Mine(i - 1) || Mine(i + 1))) list.Add(i);
        }
        return list;
    }

    /// <summary>Câu báo lỗi theo UI mục 9, ví dụ "Cách chọn này để trống ghế G8 ở giữa. Bạn chọn thêm G8 hoặc dời sang G6–G7 nhé."</summary>
    private static string Message(List<SeatSlot> all, List<SeatSlot> seg, List<int> orphans, List<SeatSlot> suggestion)
    {
        var labels = string.Join(", ", orphans.Select(i => seg[i].Label));
        string where;
        if (orphans.Count > 1) where = string.Empty;
        else
        {
            int i = orphans[0];
            var s = seg[i];
            bool leftEdge = i == 0, rightEdge = i == seg.Count - 1;
            bool Wall(bool left) => !all.Any(o => o.Row == s.Row && (left ? o.Col < s.Col : o.Col > s.Col));
            bool Aisle(bool left) => !all.Any(o => o.Row == s.Row && o.Col == (left ? s.Col - 1 : s.Col + 1));
            if ((leftEdge && Wall(true)) || (rightEdge && Wall(false))) where = " sát tường";
            else if ((leftEdge && Aisle(true)) || (rightEdge && Aisle(false))) where = " sát lối đi";
            else where = " ở giữa";
        }
        var range = suggestion.Count == 1 ? suggestion[0].Label : $"{suggestion[0].Label}–{suggestion[^1].Label}";
        var add = orphans.Count == 1 ? seg[orphans[0]].Label : labels;
        return $"Cách chọn này để trống ghế {labels}{where}. Bạn chọn thêm {add} hoặc dời sang {range} nhé.";
    }
}
