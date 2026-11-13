using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Một ghế của sơ đồ khi tìm cụm ghế: Free là ghế còn chọn được (trống, hoặc đang do chính người xem giữ).</summary>
public sealed record ClusterSeat(int SeatId, int Row, int Col, string Label, string SeatType, int? PairGroupId, bool Free);

/// <summary>Một phương án cụm ghế: các ghế liền nhau, điểm trung bình (làm tròn) và khoảng cách tới hàng lý tưởng.</summary>
public sealed record SeatCluster(IReadOnlyList<ClusterSeat> Seats, int Score, double RowDistance)
{
    /// <summary>Nhãn gọn, ví dụ "G6–G8" (một ghế thì "G6").</summary>
    public string Range => Seats.Count == 1 ? Seats[0].Label : $"{Seats[0].Label}–{Seats[^1].Label}";

    public IEnumerable<int> SeatIds => Seats.Select(s => s.SeatId);
}

/// <summary>
/// Tìm cụm K ghế (SPEC 4.1). Logic thuần, có unit test.
/// - Điểm từng ghế theo SeatScoring với persona đang chọn (ghế đầu đoạn tính theo ô lối đi hoặc tường trong hàng).
/// - Duyệt từng đoạn (dãy ghế liền nhau trong một hàng) bằng cửa sổ trượt K ghế trống liền nhau; ghế xe lăn cần xác nhận riêng nên
///   không đưa vào cụm và coi như biên; với SWEETBOX chỉ nhận nguyên cặp (cửa sổ không được cắt đôi một cặp).
/// - Bỏ các cụm vi phạm luật ghế mồ côi (OrphanSeatValidator).
/// - Xếp theo điểm trung bình giảm dần; bằng điểm thì ưu tiên cụm gần r_ideal hơn, rồi gần giữa phòng hơn.
/// - Trả tối đa 3 phương án, các phương án không dùng chung ghế để khách có lựa chọn khác nhau.
/// </summary>
public static class ClusterFinder
{
    public const int MaxResults = 3;

    /// <summary>Điểm của mọi ghế theo persona (dùng cho bản đồ điểm AI và tooltip).</summary>
    public static Dictionary<int, int> ScoreAll(IReadOnlyCollection<ClusterSeat> seats, int totalRows, int totalCols, decimal idealRowRatio, string persona)
    {
        var colsByRow = seats.GroupBy(s => s.Row).ToDictionary(g => g.Key, g => (IReadOnlyCollection<int>)g.Select(s => s.Col).ToHashSet());
        return seats.ToDictionary(s => s.SeatId, s => SeatScoring.Score(totalRows, totalCols, s.Row, s.Col, s.SeatType, persona, idealRowRatio,
            SeatScoring.IsSegmentEnd(colsByRow[s.Row], s.Col, totalCols)));
    }

    /// <summary>
    /// Tìm tối đa <paramref name="maxResults"/> cụm <paramref name="quantity"/> ghế tốt nhất. Danh sách rỗng khi không còn cụm nào hợp lệ.
    /// </summary>
    public static List<SeatCluster> Find(IReadOnlyCollection<ClusterSeat> seats, int totalRows, int totalCols, decimal idealRowRatio, string persona,
        int quantity, int maxResults = MaxResults)
    {
        if (quantity <= 0 || seats.Count == 0) return new List<SeatCluster>();
        var scores = ScoreAll(seats, totalRows, totalCols, idealRowRatio, persona);
        double rIdeal = SeatScoring.IdealRow(totalRows, idealRowRatio, persona);
        double cIdeal = SeatScoring.IdealCol(totalCols);
        var slots = seats.Select(s => new SeatSlot(s.SeatId, s.Row, s.Col, s.Label, s.SeatType, !s.Free)).ToList();

        var candidates = new List<(SeatCluster Cluster, double ColDistance)>();
        foreach (var segment in Segments(seats))
        {
            for (int i = 0; i + quantity <= segment.Count; i++)
            {
                var window = segment.GetRange(i, quantity);
                if (window.Any(s => !s.Free)) continue;
                if (!WholePairs(window, seats)) continue;
                if (OrphanSeatValidator.Validate(slots, window.Select(s => s.SeatId).ToHashSet()) != null) continue;
                int score = (int)Math.Round(window.Average(s => scores[s.SeatId]), MidpointRounding.AwayFromZero);
                double rowDist = Math.Abs(window[0].Row - rIdeal);
                double colDist = Math.Abs(window.Average(s => s.Col) - cIdeal);
                candidates.Add((new SeatCluster(window, score, rowDist), colDist));
            }
        }

        var result = new List<SeatCluster>();
        var used = new HashSet<int>();
        foreach (var c in candidates.OrderByDescending(c => c.Cluster.Score).ThenBy(c => c.Cluster.RowDistance).ThenBy(c => c.ColDistance)
                     .ThenBy(c => c.Cluster.Seats[0].Row).ThenBy(c => c.Cluster.Seats[0].Col))
        {
            if (c.Cluster.Seats.Any(s => used.Contains(s.SeatId))) continue;
            result.Add(c.Cluster);
            foreach (var s in c.Cluster.Seats) used.Add(s.SeatId);
            if (result.Count >= maxResults) break;
        }
        return result;
    }

    /// <summary>Đoạn ghế: cùng hàng, cột liên tiếp; ghế xe lăn và ô trống cắt đoạn.</summary>
    public static List<List<ClusterSeat>> Segments(IEnumerable<ClusterSeat> seats)
    {
        var result = new List<List<ClusterSeat>>();
        foreach (var row in seats.Where(s => s.SeatType != SeatType.Wheelchair).GroupBy(s => s.Row).OrderBy(g => g.Key))
        {
            List<ClusterSeat>? cur = null;
            foreach (var s in row.OrderBy(s => s.Col))
            {
                if (cur == null || s.Col != cur[^1].Col + 1)
                {
                    cur = new List<ClusterSeat>();
                    result.Add(cur);
                }
                cur.Add(s);
            }
        }
        return result;
    }

    /// <summary>Cửa sổ chứa ghế đôi thì phải chứa đủ cả cặp (cùng PairGroupId).</summary>
    private static bool WholePairs(IReadOnlyCollection<ClusterSeat> window, IReadOnlyCollection<ClusterSeat> all)
    {
        var ids = window.Select(s => s.SeatId).ToHashSet();
        foreach (var s in window.Where(s => s.SeatType == SeatType.Sweetbox))
        {
            if (s.PairGroupId is not int g) return false;
            if (all.Where(o => o.PairGroupId == g && o.SeatType == SeatType.Sweetbox).Any(o => !ids.Contains(o.SeatId))) return false;
        }
        return true;
    }
}
