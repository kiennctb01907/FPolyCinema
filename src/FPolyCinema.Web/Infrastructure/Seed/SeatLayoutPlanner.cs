using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;

namespace FPolyCinema.Web.Infrastructure.Seed;

/// <summary>Một ghế trong sơ đồ mẫu (chưa có Id).</summary>
public sealed record PlannedSeat(int Row, int Col, string Label, string SeatType, int? PairIndex, bool IsHouseSeat, bool IsMaintenance, int BaseViewScore);

/// <summary>
/// Dựng sơ đồ ghế mẫu cho một phòng (SPEC 8.1 bước 6): lưới Rows × Cols, các cột lối đi để trống,
/// VIP ở các hàng giữa (khối giữa), hàng SWEETBOX cuối theo cặp (nếu có), 2 ghế xe lăn ở hàng cuối thường,
/// 4 ghế nội bộ ở trung tâm, 1 ghế bảo trì. Nhãn theo SeatLabeler, BaseViewScore theo SeatScoring (BEST_VIEW_AUDIO).
/// Logic thuần để unit test được.
/// </summary>
public static class SeatLayoutPlanner
{
    public const int WheelchairSeats = 2;
    public const int MaintenanceSeats = 1;

    public static List<PlannedSeat> Plan(AuditoriumSpec spec, int houseSeats, decimal idealRowRatio = SeatScoring.DefaultIdealRowRatio)
    {
        int R = spec.Rows, C = spec.Cols;
        var aisles = new HashSet<int>(spec.AisleCols);
        var seatCols = Enumerable.Range(1, C).Where(c => !aisles.Contains(c)).ToList();
        var blocks = Blocks(seatCols);
        var centerBlock = blocks.OrderByDescending(b => b.Count).ThenBy(b => Math.Abs(b.Average() - (C + 1) / 2.0)).First();

        int vipFrom = (int)Math.Ceiling(R * 0.4), vipTo = (int)Math.Floor(R * 0.75);
        int sweetRow = spec.SweetboxLastRow ? R : -1;
        int lastNormalRow = spec.SweetboxLastRow ? R - 1 : R;

        // Ghế nội bộ: hàng gần r_ideal nhất, 4 ghế giữa khối trung tâm
        int houseRow = Math.Clamp((int)Math.Round((double)idealRowRatio * R, MidpointRounding.AwayFromZero), 1, lastNormalRow);
        var houseCols = new HashSet<int>(MiddleOf(centerBlock, houseSeats));
        // Ghế bảo trì: hàng C (3), ghế đầu tiên của khối trung tâm
        int maintRow = Math.Min(3, lastNormalRow);
        int maintCol = centerBlock[0];
        // Ghế xe lăn: hai đầu ngoài cùng (kề tường) của hàng cuối thường
        var wheelchairCols = new HashSet<int> { seatCols.First(), seatCols.Last() };

        var cells = new List<(int Row, int Col)>();
        for (int r = 1; r <= R; r++) foreach (var c in seatCols) cells.Add((r, c));
        var labels = SeatLabeler.AssignLabels(cells);

        var seats = new List<PlannedSeat>();
        int pairIndex = 0;
        for (int r = 1; r <= R; r++)
        {
            var colsInRow = new HashSet<int>(seatCols);
            var pairOf = new Dictionary<int, int>();
            if (r == sweetRow)
            {
                foreach (var b in blocks)
                    for (int i = 0; i + 1 < b.Count; i += 2)
                    {
                        pairIndex++;
                        pairOf[b[i]] = pairIndex;
                        pairOf[b[i + 1]] = pairIndex;
                    }
            }
            foreach (var c in seatCols)
            {
                string type;
                if (r == sweetRow && pairOf.ContainsKey(c)) type = SeatType.Sweetbox;
                else if (r == lastNormalRow && wheelchairCols.Contains(c)) type = SeatType.Wheelchair;
                else if (r >= vipFrom && r <= vipTo && centerBlock.Contains(c)) type = SeatType.Vip;
                else type = SeatType.Standard;

                bool house = r == houseRow && houseCols.Contains(c);
                bool maint = r == maintRow && c == maintCol && !house;
                var segmentEnd = SeatScoring.IsSegmentEnd(colsInRow, c, C);
                int score = SeatScoring.Score(R, C, r, c, type, SeatPersona.BestViewAudio, idealRowRatio, segmentEnd);
                seats.Add(new PlannedSeat(r, c, labels[(r, c)], type, pairOf.TryGetValue(c, out var p) ? p : null, house, maint, score));
            }
        }
        return seats;
    }

    /// <summary>Chia các cột có ghế thành các đoạn liền nhau.</summary>
    public static List<List<int>> Blocks(IReadOnlyList<int> seatCols)
    {
        var blocks = new List<List<int>>();
        foreach (var c in seatCols)
        {
            if (blocks.Count == 0 || blocks[^1][^1] != c - 1) blocks.Add(new List<int>());
            blocks[^1].Add(c);
        }
        return blocks;
    }

    private static IEnumerable<int> MiddleOf(List<int> block, int count)
    {
        count = Math.Min(count, block.Count);
        int start = (block.Count - count) / 2;
        return block.Skip(start).Take(count);
    }
}
