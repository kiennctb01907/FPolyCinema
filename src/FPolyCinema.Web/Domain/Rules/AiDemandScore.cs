namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Dữ liệu đầu vào của AiDemandScore cho một phim.</summary>
/// <param name="DaysSinceRelease">Số ngày tính từ ReleaseDate tới ngày xếp lịch (âm là chưa khởi chiếu).</param>
/// <param name="AvgOccupancy7Days">Tỷ lệ lấp đầy trung bình 0–1 của các suất phim trong 7 ngày qua; null khi chưa có suất nào.</param>
/// <param name="PresoldTickets">Số vé đã bán trước cho các suất tương lai.</param>
/// <param name="IsBlockbuster">Phim bom tấn.</param>
public sealed record DemandInput(int DaysSinceRelease, decimal? AvgOccupancy7Days, int PresoldTickets, bool IsBlockbuster);

/// <summary>
/// AiDemandScore (SPEC 4.3-01): điểm nhu cầu thang 1–10 của một phim, tính bằng công thức trọng số. Logic thuần, có unit test.
///
///   Fresh     = 1 nếu chưa khởi chiếu hoặc khởi chiếu từ 0 ngày; giảm tuyến tính về 0 tại FreshnessDays (60 ngày): 1 − ngày / 60.
///   Occupancy = tỷ lệ lấp đầy trung bình 7 ngày qua (0–1); phim chưa có suất nào dùng NoHistoryOccupancy (0,5) để không bị phạt.
///   Presale   = min(1, vé bán trước / PresaleFullTickets (100 vé)).
///   Block     = 1 nếu là phim bom tấn, ngược lại 0.
///
///   AiDemandScore = 1 + 9 × (0,30 × Fresh + 0,35 × Occupancy + 0,20 × Presale + 0,15 × Block), làm tròn 2 chữ số, giới hạn trong [1, 10].
/// </summary>
public static class AiDemandScore
{
    public const decimal WeightFreshness = 0.30m;
    public const decimal WeightOccupancy = 0.35m;
    public const decimal WeightPresale = 0.20m;
    public const decimal WeightBlockbuster = 0.15m;
    public const int FreshnessDays = 60;
    public const int PresaleFullTickets = 100;
    public const decimal NoHistoryOccupancy = 0.5m;

    public static decimal Freshness(int daysSinceRelease) =>
        daysSinceRelease <= 0 ? 1m : Math.Max(0m, 1m - (decimal)daysSinceRelease / FreshnessDays);

    public static decimal Presale(int presoldTickets) => Math.Min(1m, Math.Max(0, presoldTickets) / (decimal)PresaleFullTickets);

    public static decimal Compute(DemandInput i)
    {
        var occ = Math.Clamp(i.AvgOccupancy7Days ?? NoHistoryOccupancy, 0m, 1m);
        var weighted = WeightFreshness * Freshness(i.DaysSinceRelease) + WeightOccupancy * occ + WeightPresale * Presale(i.PresoldTickets)
                       + WeightBlockbuster * (i.IsBlockbuster ? 1m : 0m);
        return Math.Clamp(Math.Round(1m + 9m * weighted, 2, MidpointRounding.AwayFromZero), 1m, 10m);
    }
}
