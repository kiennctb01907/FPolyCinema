using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Infrastructure;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.Services;

/// <summary>Thông tin suất cần để tính giá vé và kiểm luật.</summary>
public sealed record ShowtimeInfo(Showtime Showtime, Auditorium Auditorium, Movie Movie);

/// <summary>
/// Tính giá vé của một suất theo bảng giá 6 chiều, ngày lễ và mức giảm đối tượng (PricingCalculator). Nạp bảng giá một lần cho mỗi suất.
/// </summary>
public sealed class TicketPricer
{
    private readonly ShowtimeInfo _info;
    private readonly PriceTable _table = new();
    private readonly List<DateTime> _holidays;
    private readonly Dictionary<string, AudienceDiscount> _discounts;
    private readonly CinemaRules _rules;

    public TicketPricer(ShowtimeInfo info, CinemaRules rules, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        _info = info;
        _rules = rules;
        foreach (var r in PricingRuleDAL.GetByCinema(info.Auditorium.CinemaId, conn, tx)) _table.Set(r.RuleType, r.RuleKey, r.Amount);
        _holidays = HolidayDAL.GetAll(conn, tx).Select(h => h.HolidayDate).ToList();
        _discounts = AudienceDiscountDAL.GetAll(conn, tx).ToDictionary(d => d.AudienceType);
    }

    public IReadOnlyDictionary<string, AudienceDiscount> Discounts => _discounts;

    /// <summary>Giá niêm yết của một vé theo loại ghế và đối tượng.</summary>
    public long Price(string seatType, string audienceType)
    {
        var pct = _discounts.TryGetValue(audienceType, out var d) ? d.DiscountPercent : 0m;
        var s = _info.Showtime;
        return PricingCalculator.Calculate(new PriceInput(s.StartTime, s.FeatureStartTime, _info.Auditorium.Format, seatType, _info.Movie.IsBlockbuster, pct),
            _table, _holidays, _rules).ListPrice;
    }
}

/// <summary>
/// Đọc sơ đồ ghế của suất cho _SeatMap (UI mục 8): trạng thái hiệu lực (ghế HELD hết hạn coi là trống, ghế bảo trì là MAINTENANCE – SPEC 3.5),
/// giá vé người lớn theo loại ghế, điểm BaseViewScore, cặp ghế đôi.
/// </summary>
public class SeatMapService
{
    private readonly IClock _clock;
    private readonly CinemaRules _rules;

    public SeatMapService(IClock clock, CinemaRules rules)
    {
        _clock = clock;
        _rules = rules;
    }

    public bool ShowtimeExists(int showtimeId) => showtimeId > 0 && ShowtimeDAL.GetById(showtimeId) != null;

    /// <summary>Suất, phòng và phim của một suất; null nếu không có.</summary>
    public static ShowtimeInfo? Info(int showtimeId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        var s = ShowtimeDAL.GetById(showtimeId, conn, tx);
        if (s == null) return null;
        var a = AuditoriumDAL.GetById(s.AuditoriumId, conn, tx)!;
        var m = MovieDAL.GetById(s.MovieId, conn, tx)!;
        return new ShowtimeInfo(s, a, m);
    }

    /// <summary>Nhãn điểm góc nhìn trong tooltip: từ 90 là "tọa độ vàng", từ GoodClusterScore là "góc nhìn tốt".</summary>
    public string ScoreNote(int score) => score >= 90 ? "tọa độ vàng" : score >= _rules.GoodClusterScore ? "góc nhìn tốt" : string.Empty;

    /// <summary>Trạng thái hiển thị của ghế thuộc hóa đơn của người đang xem (CONTRACTS C3-08).</summary>
    public const string Selected = "SELECTED";

    /// <summary>Dựng sơ đồ ghế; holdBillId là hóa đơn của người đang xem (ghế của hóa đơn này hiện là SELECTED).</summary>
    public SeatMapModel Build(int showtimeId, int? holdBillId)
    {
        var info = Info(showtimeId) ?? throw CategoryService.NotFound("Không tìm thấy suất chiếu.");
        var pricer = new TicketPricer(info, _rules);
        var prices = SeatType.All.ToDictionary(t => t, t => pricer.Price(t, AudienceType.Adult));
        var now = _clock.Now;
        var seats = new List<SeatMapSeat>();
        foreach (var s in ShowtimeSeatDAL.GetStates(showtimeId))
        {
            var status = Effective(s, now);
            if (holdBillId != null && status == SeatDisplayStatus.Held && s.HoldBillId == holdBillId) status = Selected;
            seats.Add(new SeatMapSeat(s.SeatId, s.RowIndex, s.ColIndex, s.Label, s.SeatType, status, s.IsHouseSeat, s.PairGroupId,
                s.BaseViewScore, ScoreNote(s.BaseViewScore), prices[s.SeatType], status is SeatDisplayStatus.Held or Selected ? s.HoldBillId : null));
        }
        return new SeatMapModel(showtimeId, info.Auditorium.TotalRows, info.Auditorium.TotalCols, holdBillId, seats);
    }

    /// <summary>Trạng thái hiệu lực gửi cho giao diện (SeatDisplayStatus).</summary>
    public static string Effective(SeatStateRow s, DateTime now) => SeatHoldRules.EffectiveStatus(s.Status, s.HoldExpiresAt, s.IsMaintenance, now);
}
