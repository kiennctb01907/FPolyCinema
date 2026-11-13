using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Một ô có ghế do công cụ thiết kế phòng gửi lên.</summary>
public sealed record LayoutCell(int Row, int Col, string SeatType, bool IsHouseSeat, bool IsMaintenance);

/// <summary>Một ghế sau khi dựng sơ đồ: nhãn, chỉ số cặp ghế đôi (trong phòng), BaseViewScore và mức heatmap.</summary>
public sealed record LayoutSeat(int Row, int Col, string SeatType, bool IsHouseSeat, bool IsMaintenance, string Label, int? PairIndex, int Score, int HeatLevel);

/// <summary>
/// Quy tắc thuần của công cụ thiết kế phòng (SPEC 3.2, UI A4). Có unit test.
/// - Sơ đồ là lưới TotalRows × TotalCols; ô không có ghế là lối đi. Nhãn theo SeatLabeler.
/// - SWEETBOX đi theo cặp cố định: các ô sweetbox liền nhau trong một hàng ghép cặp từ trái sang; số ô lẻ hoặc ô lẻ loi là lỗi.
/// - BaseViewScore tính lại mỗi lần lưu theo SeatScoring, persona BEST_VIEW_AUDIO.
/// - Gợi ý HouseSeatsPerAuditorium ghế nội bộ ở trung tâm: hàng gần r_ideal nhất có đủ ghế liền nhau, đoạn gần c_ideal nhất.
/// - Heatmap: điểm 0–39, 40–59, 60–74, 75–89, 90–100 ứng với --heat-1 … --heat-5 (UI U2-06).
/// </summary>
public static class AuditoriumLayout
{
    public const int MaxRows = 26;
    public const int MaxCols = 40;

    /// <summary>Mức heatmap 1–5 của một điểm 0–100.</summary>
    public static int HeatLevel(int score) => score switch
    {
        >= 90 => 5,
        >= 75 => 4,
        >= 60 => 3,
        >= 40 => 2,
        _ => 1,
    };

    /// <summary>Hai mức sáng nhất (4, 5) dùng chữ tối; ba mức còn lại dùng chữ sáng (UI U2-06).</summary>
    public static bool HeatUsesDarkText(int level) => level >= 4;

    /// <summary>Kiểm tra sơ đồ gửi lên; trả danh sách câu lỗi (rỗng là hợp lệ).</summary>
    public static List<string> Validate(int rows, int cols, IReadOnlyCollection<LayoutCell> cells)
    {
        var errors = new List<string>();
        if (rows < 1 || rows > MaxRows) errors.Add($"Số hàng phải từ 1 đến {MaxRows}.");
        if (cols < 1 || cols > MaxCols) errors.Add($"Số cột phải từ 1 đến {MaxCols}.");
        if (errors.Count > 0) return errors;
        if (cells.Count == 0) { errors.Add("Sơ đồ chưa có ghế nào. Bạn tô ít nhất một ghế."); return errors; }

        var seen = new HashSet<(int, int)>();
        foreach (var c in cells)
        {
            if (c.Row < 1 || c.Row > rows || c.Col < 1 || c.Col > cols) { errors.Add($"Có ghế nằm ngoài lưới {rows} × {cols}. Bạn tải lại công cụ rồi thử lại."); break; }
            if (!seen.Add((c.Row, c.Col))) { errors.Add("Có hai ghế trùng một ô. Bạn tải lại công cụ rồi thử lại."); break; }
            if (!SeatType.All.Contains(c.SeatType)) { errors.Add("Có ghế mang loại không hợp lệ. Bạn tô lại ô đó."); break; }
        }
        if (errors.Count > 0) return errors;

        foreach (var row in cells.Where(c => c.SeatType == SeatType.Sweetbox).GroupBy(c => c.Row))
            foreach (var run in Runs(row.Select(c => c.Col)))
                if (run.Count % 2 != 0)
                {
                    var letter = SeatLabeler.RowLetter(row.Key);
                    errors.Add($"Hàng {letter} có {run.Count} ô ghế đôi liền nhau ở cột {run[0]}–{run[^1]}. Ghế đôi đi theo cặp, bạn tô thêm hoặc bỏ bớt một ô.");
                }
        return errors;
    }

    /// <summary>Dựng sơ đồ đầy đủ: nhãn, cặp ghế đôi, điểm BEST_VIEW_AUDIO và mức heatmap của từng ghế.</summary>
    public static List<LayoutSeat> Build(int rows, int cols, IEnumerable<LayoutCell> cells, decimal idealRowRatio)
    {
        var list = cells.OrderBy(c => c.Row).ThenBy(c => c.Col).ToList();
        var labels = SeatLabeler.AssignLabels(list.Select(c => (c.Row, c.Col)));
        var pairOf = new Dictionary<(int, int), int>();
        int pair = 0;
        foreach (var row in list.Where(c => c.SeatType == SeatType.Sweetbox).GroupBy(c => c.Row).OrderBy(g => g.Key))
            foreach (var run in Runs(row.Select(c => c.Col)))
                for (int i = 0; i + 1 < run.Count; i += 2)
                {
                    pair++;
                    pairOf[(row.Key, run[i])] = pair;
                    pairOf[(row.Key, run[i + 1])] = pair;
                }

        return list.Select(c =>
        {
            int score = SeatScoring.Score(rows, cols, c.Row, c.Col, c.SeatType, SeatPersona.BestViewAudio, idealRowRatio);
            return new LayoutSeat(c.Row, c.Col, c.SeatType, c.IsHouseSeat, c.IsMaintenance, labels[(c.Row, c.Col)],
                pairOf.TryGetValue((c.Row, c.Col), out var p) ? p : null, score, HeatLevel(score));
        }).ToList();
    }

    /// <summary>
    /// Gợi ý count ghế nội bộ ở trung tâm (SPEC 3.2): chỉ chọn ghế thường hoặc VIP không bảo trì; ưu tiên hàng gần r_ideal nhất
    /// có đủ count ghế liền nhau, trong hàng chọn đoạn có tâm gần c_ideal nhất. Không có hàng nào đủ thì lấy count ghế điểm cao nhất.
    /// </summary>
    public static List<(int Row, int Col)> SuggestHouseSeats(int rows, int cols, IEnumerable<LayoutCell> cells, int count, decimal idealRowRatio)
    {
        if (count <= 0) return new();
        var eligible = cells.Where(c => c.SeatType is SeatType.Standard or SeatType.Vip && !c.IsMaintenance).ToList();
        double rIdeal = SeatScoring.IdealRow(rows, idealRowRatio, SeatPersona.BestViewAudio);
        double cIdeal = SeatScoring.IdealCol(cols);
        foreach (var row in eligible.GroupBy(c => c.Row).OrderBy(g => Math.Abs(g.Key - rIdeal)).ThenByDescending(g => g.Key))
        {
            List<int>? best = null;
            double bestDist = double.MaxValue;
            foreach (var run in Runs(row.Select(c => c.Col)))
                for (int i = 0; i + count <= run.Count; i++)
                {
                    var window = run.GetRange(i, count);
                    double dist = Math.Abs(window.Average() - cIdeal);
                    if (dist < bestDist) { bestDist = dist; best = window; }
                }
            if (best != null) return best.Select(c => (row.Key, c)).ToList();
        }
        return Build(rows, cols, eligible, idealRowRatio).OrderByDescending(s => s.Score).ThenBy(s => Math.Abs(s.Col - cIdeal))
            .Take(count).Select(s => (s.Row, s.Col)).ToList();
    }

    /// <summary>
    /// Sơ đồ mới có đổi cấu trúc so với sơ đồ đang lưu không: đổi kích thước lưới, thêm, bớt ô ghế, đổi loại ghế hoặc ghế nội bộ.
    /// Chỉ bật tắt IsMaintenance thì không tính là đổi cấu trúc (SPEC 3.2).
    /// </summary>
    public static bool IsStructuralChange(int oldRows, int oldCols, IEnumerable<LayoutCell> oldCells, int newRows, int newCols, IEnumerable<LayoutCell> newCells)
    {
        if (oldRows != newRows || oldCols != newCols) return true;
        var a = oldCells.ToDictionary(c => (c.Row, c.Col));
        var b = newCells.ToDictionary(c => (c.Row, c.Col));
        if (a.Count != b.Count) return true;
        foreach (var (pos, cell) in a)
        {
            if (!b.TryGetValue(pos, out var other)) return true;
            if (other.SeatType != cell.SeatType || other.IsHouseSeat != cell.IsHouseSeat) return true;
        }
        return false;
    }

    /// <summary>Thống kê số ghế theo loại, số ghế nội bộ, bảo trì và sức chứa bán được (trừ ghế nội bộ, bảo trì).</summary>
    public static LayoutStats Stats(IEnumerable<LayoutCell> cells)
    {
        var list = cells.ToList();
        var byType = SeatType.All.ToDictionary(t => t, t => list.Count(c => c.SeatType == t));
        int house = list.Count(c => c.IsHouseSeat), maint = list.Count(c => c.IsMaintenance);
        int sellable = list.Count(c => !c.IsHouseSeat && !c.IsMaintenance);
        return new LayoutStats(list.Count, byType, house, maint, sellable);
    }

    /// <summary>Chia các cột thành các đoạn liền nhau (đã sắp tăng).</summary>
    public static List<List<int>> Runs(IEnumerable<int> cols)
    {
        var runs = new List<List<int>>();
        foreach (var c in cols.Distinct().OrderBy(x => x))
        {
            if (runs.Count == 0 || runs[^1][^1] != c - 1) runs.Add(new List<int>());
            runs[^1].Add(c);
        }
        return runs;
    }
}

/// <summary>Thống kê sơ đồ: tổng ô ghế, số ghế theo loại, ghế nội bộ, ghế bảo trì và sức chứa bán được.</summary>
public sealed record LayoutStats(int Total, IReadOnlyDictionary<string, int> ByType, int House, int Maintenance, int Sellable);
