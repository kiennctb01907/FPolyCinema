using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Web.Services;

/// <summary>Một phim trong hộp "AI tự động xếp lịch": có xếp được không (giấy phép, khác C, KDM còn hiệu lực trong khoảng ngày) và lý do nếu không.</summary>
public sealed record AiMovieOption(Movie Movie, bool Eligible, string? Reason);

/// <summary>Lựa chọn cho hộp "AI tự động xếp lịch": phim và phòng đang hoạt động của rạp.</summary>
public sealed record AiSchedulerChoices(List<AiMovieOption> Movies, List<Auditorium> Rooms);

/// <summary>
/// AI Smart Scheduler (SPEC 4.3, UC35): nạp dữ liệu cho AiScheduler (logic thuần). Mỗi lần chạy tính lại AiDemandScore của các phim và lưu vào Movies
/// (SPEC 4.3-01); tự lọc phim có giấy phép, khác C, KDM còn hiệu lực (SPEC 4.3-02); kết quả chỉ là bản nháp – ScheduleService.ApplyAiDrafts mới lưu.
/// </summary>
public class AiSchedulerService
{
    public const int MaxDays = 7;
    private readonly CinemaRules _rules;

    public AiSchedulerService(CinemaRules rules) => _rules = rules;

    /// <summary>Lý do phim không xếp được trong [from, to) tại rạp; null nếu xếp được.</summary>
    public static string? IneligibleReason(Movie m, IReadOnlyList<KdmWindow> kdms, DateTime from, DateTime to)
    {
        var show = AgeRules.CanSchedule(m.AgeRating, m.ClassificationLicenseNo);
        if (!show.Ok) return show.Message;
        if (!kdms.Any(k => k.ValidFrom < to && k.ValidUntil > from)) return "Chưa có KDM còn hiệu lực tại rạp trong khoảng ngày này.";
        return null;
    }

    public AiSchedulerChoices Choices(int cinemaId, DateTime fromDate, int days)
    {
        var (from, to) = Range(fromDate, days);
        var movies = MovieDAL.GetActiveByTitle().Select(m =>
        {
            var kdms = Kdms(m.Id, cinemaId);
            var reason = IneligibleReason(m, kdms, from, to);
            return new AiMovieOption(m, reason == null, reason);
        }).OrderByDescending(o => o.Eligible).ThenByDescending(o => o.Movie.AiDemandScore ?? 0).ThenBy(o => o.Movie.Title).ToList();
        return new AiSchedulerChoices(movies, AuditoriumDAL.GetActiveByCinema(cinemaId));
    }

    private static List<KdmWindow> Kdms(int movieId, int cinemaId) =>
        KdmLicenseDAL.GetByMovieCinema(movieId, cinemaId).Select(k => new KdmWindow(k.ValidFrom, k.ValidUntil)).ToList();

    private (DateTime From, DateTime To) Range(DateTime fromDate, int days) =>
        (fromDate.Date + _rules.OperatingDayStart, fromDate.Date.AddDays(Math.Clamp(days, 1, MaxDays)) + PricingCalculator.NightCutoff);

    /// <summary>Tính AiDemandScore cho các phim và lưu vào Movies.AiDemandScore (SPEC 4.3-01).</summary>
    public Dictionary<int, decimal> RefreshDemandScores(IEnumerable<Movie> movies, DateTime now)
    {
        var stats = MovieDAL.DemandStats(now.AddDays(-7), now);
        var today = PricingCalculator.OperatingDate(now);
        var result = new Dictionary<int, decimal>();
        foreach (var m in movies)
        {
            stats.TryGetValue(m.Id, out var s);
            var score = AiDemandScore.Compute(new DemandInput((today - m.ReleaseDate.Date).Days, s?.AvgOccupancy, s?.PresoldTickets ?? 0, m.IsBlockbuster));
            MovieDAL.UpdateDemandScore(m.Id, score);
            result[m.Id] = score;
        }
        return result;
    }

    /// <summary>Chạy bộ xếp lịch cho rạp từ fromDate trong days ngày (1–7) với các phim và phòng đã chọn; trả bản nháp.</summary>
    public (SchedulerResult Result, Dictionary<int, decimal> Scores) Generate(int cinemaId, DateTime fromDate, int days, IReadOnlyList<int> movieIds,
        IReadOnlyList<int> auditoriumIds, DateTime now)
    {
        var today = PricingCalculator.OperatingDate(now);
        if (days < 1 || days > MaxDays) throw FieldErrors.Of("days", $"Bạn chọn từ 1 đến {MaxDays} ngày.");
        if (fromDate.Date < today) throw FieldErrors.Of("fromDate", "Không xếp lịch cho ngày đã qua. Bạn chọn từ hôm nay trở đi.");
        var (from, to) = Range(fromDate, days);

        var movies = new List<SchedulerMovie>();
        var picked = MovieDAL.GetActiveByTitle().Where(m => movieIds.Contains(m.Id)).ToList();
        var scores = RefreshDemandScores(picked, now);
        foreach (var m in picked)
        {
            var kdms = Kdms(m.Id, cinemaId);
            if (IneligibleReason(m, kdms, from, to) != null) continue;
            movies.Add(new SchedulerMovie(ScheduleService.ToScheduleMovie(m), scores[m.Id], kdms));
        }
        if (movies.Count == 0) throw FieldErrors.Of("movieIds", "Bạn chọn ít nhất một phim có giấy phép và KDM còn hiệu lực tại rạp.");

        var rooms = AuditoriumDAL.GetActiveByCinema(cinemaId).Where(a => auditoriumIds.Contains(a.Id))
            .Select(a => new SchedulerRoom(a.Id, a.Name, SeatDAL.GetByAuditorium(a.Id).Count(s => !s.IsHouseSeat && !s.IsMaintenance))).ToList();
        if (rooms.Count == 0) throw FieldErrors.Of("auditoriumIds", "Bạn chọn ít nhất một phòng chiếu đang hoạt động.");

        var existing = ShowtimeDAL.ActiveNear(cinemaId, from.AddHours(-12), to.AddHours(12), now)
            .Select(r => new ExistingShowtime(ScheduleService.ToSlot(r), r.IsVietnameseFilm)).ToList();
        var year = ShowtimeDAL.CountVietnameseShare(cinemaId, new DateTime(fromDate.Year, 1, 1), new DateTime(fromDate.Year, 12, 31));
        var dates = Enumerable.Range(0, days).Select(i => fromDate.Date.AddDays(i)).ToList();
        var result = AiScheduler.Generate(new SchedulerInput(dates, movies, rooms, existing, now, new VietnameseShare(year.Vietnamese, year.Total)), _rules);
        return (result, scores);
    }
}
