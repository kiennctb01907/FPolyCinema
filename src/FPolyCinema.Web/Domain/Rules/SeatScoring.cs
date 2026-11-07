using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>
/// AI Seat Scoring – chấm điểm vị trí ghế 0–100 (SPEC 4.1). Logic thuần, có unit test.
/// Ký hiệu: R = TotalRows, C = TotalCols, r = RowIndex (1 là hàng gần màn hình), c = ColIndex (tính cả ô trống).
///   r_ideal = IdealRowRatio × R (persona NEAR_SCREEN dùng 0,40); c_ideal = (C + 1) / 2
///   S_raw   = 100 − (0,6 × |r − r_ideal| / R × 100 + 0,4 × |c − c_ideal| / (C / 2) × 100)
/// Điều chỉnh:
///   r ≤ 2 trừ 25 điểm; riêng NEAR_SCREEN chỉ trừ 10 điểm ở r = 1.
///   PRIVATE_COUPLE cộng 20 điểm nếu r ≥ 0,85R hoặc ghế là SWEETBOX.
///   NEAR_AISLE cộng 15 điểm nếu ghế ở đầu đoạn (kề lối đi hoặc tường).
///   S_final = round(S_raw + điều chỉnh), giới hạn trong [0, 100].
/// </summary>
public static class SeatScoring
{
    /// <summary>Tỷ lệ hàng lý tưởng mặc định của phòng (Auditoriums.IdealRowRatio).</summary>
    public const decimal DefaultIdealRowRatio = 0.65m;

    /// <summary>Tỷ lệ hàng lý tưởng riêng của persona NEAR_SCREEN.</summary>
    public const decimal NearScreenRowRatio = 0.40m;

    public static double IdealRow(int totalRows, decimal idealRowRatio, string persona)
    {
        var ratio = persona == SeatPersona.NearScreen ? NearScreenRowRatio : idealRowRatio;
        return (double)ratio * totalRows;
    }

    public static double IdealCol(int totalCols) => (totalCols + 1) / 2.0;

    /// <summary>Điểm trước điều chỉnh (chưa làm tròn).</summary>
    public static double RawScore(int totalRows, int totalCols, int row, int col, decimal idealRowRatio, string persona)
    {
        if (totalRows <= 0 || totalCols <= 0) throw new ArgumentOutOfRangeException(nameof(totalRows), "Kích thước phòng phải lớn hơn 0.");
        var rIdeal = IdealRow(totalRows, idealRowRatio, persona);
        var cIdeal = IdealCol(totalCols);
        var rowPenalty = 0.6 * Math.Abs(row - rIdeal) / totalRows * 100;
        var colPenalty = 0.4 * Math.Abs(col - cIdeal) / (totalCols / 2.0) * 100;
        return 100 - (rowPenalty + colPenalty);
    }

    /// <summary>Tổng điểm điều chỉnh theo hàng đầu và persona.</summary>
    public static int Adjustment(int totalRows, int row, string seatType, string persona, bool isSegmentEnd)
    {
        int adj = 0;
        if (persona == SeatPersona.NearScreen)
        {
            if (row == 1) adj -= 10;
        }
        else if (row <= 2)
        {
            adj -= 25;
        }
        if (persona == SeatPersona.PrivateCouple && (row >= 0.85 * totalRows || seatType == SeatType.Sweetbox)) adj += 20;
        if (persona == SeatPersona.NearAisle && isSegmentEnd) adj += 15;
        return adj;
    }

    /// <summary>Điểm cuối cùng 0–100 của một ghế với một persona.</summary>
    public static int Score(int totalRows, int totalCols, int row, int col, string seatType, string persona,
        decimal idealRowRatio = DefaultIdealRowRatio, bool isSegmentEnd = false)
    {
        var raw = RawScore(totalRows, totalCols, row, col, idealRowRatio, persona);
        var final = (int)Math.Round(raw + Adjustment(totalRows, row, seatType, persona, isSegmentEnd), MidpointRounding.AwayFromZero);
        return Math.Clamp(final, 0, 100);
    }

    /// <summary>
    /// Ghế ở đầu đoạn: ô bên trái hoặc bên phải là lối đi (không có ghế) hoặc là tường (ngoài lưới).
    /// </summary>
    /// <param name="seatColsInRow">Các ColIndex có ghế trong cùng hàng.</param>
    public static bool IsSegmentEnd(IReadOnlyCollection<int> seatColsInRow, int col, int totalCols)
    {
        bool leftOpen = col <= 1 || !seatColsInRow.Contains(col - 1);
        bool rightOpen = col >= totalCols || !seatColsInRow.Contains(col + 1);
        return leftOpen || rightOpen;
    }
}
