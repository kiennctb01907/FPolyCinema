using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Tests;

/// <summary>
/// Unit test quy tắc thuần của Phase 7: AiDemandScore (SPEC 4.3-01) và thuật toán xếp lịch AiScheduler (SPEC 4.3-03 → 4.3-08, SPEC 3.1-21).
/// </summary>
public class Phase7RulesTests
{
    private static readonly CinemaRules Rules = new();
    private static readonly DateTime Now = new(2026, 11, 12, 10, 0, 0);
    private static readonly DateTime Day = new(2026, 11, 20);

    // ------------------------------------------------------------------ AiDemandScore
    [Fact]
    public void Demand_WeightedFormula()
    {
        // 1 + 9 × (0,30 × 1 + 0,35 × 0,8 + 0,20 × 0,5 + 0,15 × 1) = 1 + 9 × 0,83 = 8,47
        Assert.Equal(8.47m, AiDemandScore.Compute(new DemandInput(0, 0.8m, 50, true)));
        // Phim cũ 90 ngày, không ai xem, không bán trước, không bom tấn: điểm thấp nhất 1
        Assert.Equal(1m, AiDemandScore.Compute(new DemandInput(90, 0m, 0, false)));
        // Mọi yếu tố tối đa: 10
        Assert.Equal(10m, AiDemandScore.Compute(new DemandInput(-5, 1m, 500, true)));
        // Chưa có suất nào trong 7 ngày qua: lấp đầy coi là 0,5 (1 + 9 × (0,30 × 0,5 + 0,35 × 0,5) = 3,925 → 3,93)
        Assert.Equal(3.93m, AiDemandScore.Compute(new DemandInput(30, null, 0, false)));
    }

    [Fact]
    public void Demand_FactorsMonotonicAndBounded()
    {
        var baseInput = new DemandInput(20, 0.4m, 10, false);
        var s = AiDemandScore.Compute(baseInput);
        Assert.True(AiDemandScore.Compute(baseInput with { DaysSinceRelease = 5 }) > s);
        Assert.True(AiDemandScore.Compute(baseInput with { AvgOccupancy7Days = 0.9m }) > s);
        Assert.True(AiDemandScore.Compute(baseInput with { PresoldTickets = 80 }) > s);
        Assert.True(AiDemandScore.Compute(baseInput with { IsBlockbuster = true }) > s);
        Assert.Equal(0m, AiDemandScore.Freshness(60));
        Assert.Equal(1m, AiDemandScore.Presale(1000));
        for (int d = -10; d < 200; d += 7)
            Assert.InRange(AiDemandScore.Compute(new DemandInput(d, d % 3 == 0 ? null : 0.3m, d, d % 2 == 0)), 1m, 10m);
    }

    // ------------------------------------------------------------------ AiScheduler
    private static ScheduleMovie Movie(int id, string title, string rating, bool vn, int duration, params string[] langs) =>
        new(id, title, rating, "GP-" + id, vn, langs, duration);

    private static readonly KdmWindow Kdm = new(new DateTime(2026, 11, 1), new DateTime(2026, 12, 31));

    private static List<SchedulerMovie> Movies() => new()
    {
        new(Movie(1, "Bom tấn T13", AgeRating.T13, false, 130, LanguageFormat.Subtitled), 9m, new[] { Kdm }),
        new(Movie(2, "Hoạt hình K", AgeRating.K, false, 95, LanguageFormat.Dubbed, LanguageFormat.Subtitled), 5m, new[] { Kdm }),
        new(Movie(3, "Phim Việt P", AgeRating.P, true, 110, LanguageFormat.Original), 4m, new[] { Kdm }),
        new(Movie(4, "Kinh dị T18", AgeRating.T18, false, 100, LanguageFormat.Subtitled), 6m, new[] { Kdm }),
    };

    private static readonly List<SchedulerRoom> Rooms = new() { new(10, "Phòng 1", 200), new(11, "Phòng 2", 120), new(12, "Phòng 3", 80) };

    private static SchedulerResult Run(List<ExistingShowtime>? existing = null) =>
        AiScheduler.Generate(new SchedulerInput(new[] { Day }, Movies(), Rooms, existing ?? new List<ExistingShowtime>(), Now, new VietnameseShare(0, 0)), Rules);

    [Fact]
    public void Scheduler_TargetCounts_ProportionalToScore_AtLeastOne_VietnameseShare()
    {
        var t = AiScheduler.TargetCounts(new List<(int, decimal, bool)> { (1, 9m, false), (2, 3m, false), (3, 1m, false), (4, 0.5m, true) }, 20, 0.22m);
        Assert.Equal(20, t.Values.Sum());
        Assert.True(t[1] > t[2] && t[2] >= t[3]);
        Assert.All(t.Values, v => Assert.True(v >= 1));
        // Phim Việt được nâng tới ⌈0,22 × 20⌉ = 5 suất
        Assert.Equal(5, t[4]);
        // Ít suất hơn số phim: mỗi phim vẫn ít nhất 1
        Assert.All(AiScheduler.TargetCounts(new List<(int, decimal, bool)> { (1, 9m, false), (2, 1m, false), (3, 1m, false) }, 2, 0m).Values, v => Assert.Equal(1, v));
    }

    [Fact]
    public void Scheduler_Language_DubbedBefore18_SubtitledAfter()
    {
        var k = Movie(2, "K", AgeRating.K, false, 90, LanguageFormat.Dubbed, LanguageFormat.Subtitled);
        Assert.Equal(LanguageFormat.Dubbed, AiScheduler.LanguageFor(k, Day.AddHours(17).AddMinutes(55), Rules));
        Assert.Equal(LanguageFormat.Subtitled, AiScheduler.LanguageFor(k, Day.AddHours(18), Rules));
        var dubOnly = Movie(5, "P", AgeRating.P, false, 90, LanguageFormat.Dubbed);
        Assert.Equal(LanguageFormat.Dubbed, AiScheduler.LanguageFor(dubOnly, Day.AddHours(20), Rules));
        var t16 = Movie(6, "T16", AgeRating.T16, false, 90, LanguageFormat.Dubbed, LanguageFormat.Subtitled);
        Assert.Equal(LanguageFormat.Subtitled, AiScheduler.LanguageFor(t16, Day.AddHours(10), Rules));
    }

    [Fact]
    public void Scheduler_Generate_PassesCheckerWithoutErrorsOrAvoidedWarnings()
    {
        var r = Run();
        Assert.NotEmpty(r.Drafts);
        var all = r.Drafts.Select(AiScheduler.ToSlot).ToList();
        var movies = Movies().ToDictionary(m => m.Movie.Id);
        for (int i = 0; i < r.Drafts.Count; i++)
        {
            var d = r.Drafts[i];
            var others = all.Where((_, j) => j != i).ToList();
            var check = ScheduleConstraintChecker.Check(all[i], new ScheduleContext(movies[d.MovieId].Movie, new[] { Kdm }, others, Array.Empty<ScheduleSlot>(), new VietnameseShare(0, 0), Now), Rules);
            Assert.False(check.HasErrors, string.Join("; ", check.Errors.Select(e => e.Message)));
            Assert.False(check.Has(ScheduleWarningCode.LateEndForAudience));
            Assert.False(check.Has(ScheduleWarningCode.SameMinuteStarts));
        }
    }

    [Fact]
    public void Scheduler_Generate_FillsRoomsFromDayStart_CleaningGap_Stagger()
    {
        var r = Run();
        foreach (var room in Rooms)
        {
            var list = r.Drafts.Where(d => d.AuditoriumId == room.Id).OrderBy(d => d.StartTime).ToList();
            Assert.NotEmpty(list);
            Assert.True(list[0].StartTime >= Day + Rules.OperatingDayStart);
            for (int i = 1; i < list.Count; i++) Assert.True(list[i].StartTime >= list[i - 1].CleaningEndTime);
            Assert.All(list, d => Assert.True(d.StartTime.TimeOfDay <= Rules.LastShowStartLatest));
        }
        // Phòng lớn nhất bắt đầu đúng OperatingDayStart
        Assert.Equal(Day + Rules.OperatingDayStart, r.Drafts.Where(d => d.AuditoriumId == 10).Min(d => d.StartTime));
        // Lệch tối thiểu MinStartStaggerMinutes giữa các phòng
        foreach (var a in r.Drafts)
            foreach (var b in r.Drafts.Where(b => b.AuditoriumId != a.AuditoriumId))
                Assert.True((a.StartTime - b.StartTime).Duration() >= TimeSpan.FromMinutes(Rules.MinStartStaggerMinutes));
    }

    [Fact]
    public void Scheduler_Generate_EveryMovieDaily_VietnameseTargetShare_T18After19_Language()
    {
        var r = Run();
        foreach (var m in Movies()) Assert.Contains(r.Drafts, d => d.MovieId == m.Movie.Id);
        Assert.True(VietnameseFilmQuota.Share(r.VietnameseCount, r.TotalCount) >= Rules.VietnameseFilmTargetShare);
        Assert.All(r.Drafts.Where(d => d.AgeRating == AgeRating.T18), d => Assert.True(d.StartTime.TimeOfDay >= AiScheduler.T18PreferredFrom));
        Assert.All(r.Drafts.Where(d => d.MovieId == 2), d =>
            Assert.Equal(d.StartTime.TimeOfDay < new TimeSpan(18, 0, 0) ? LanguageFormat.Dubbed : LanguageFormat.Subtitled, d.LanguageFormat));
        // Phim điểm cao xếp nhiều suất hơn phim điểm thấp
        Assert.True(r.Drafts.Count(d => d.MovieId == 1) > r.Drafts.Count(d => d.MovieId == 3 && !d.IsVietnamese));
        Assert.True(r.Drafts.Count(d => d.MovieId == 1) >= r.Drafts.Count(d => d.MovieId == 2));
    }

    [Fact]
    public void Scheduler_Generate_PrimeTimeLargeRoom_HighScoreOrVietnamese()
    {
        var r = Run();
        var median = Movies().Select(m => m.Score).OrderBy(s => s).ElementAt(2);
        var scores = Movies().ToDictionary(m => m.Movie.Id, m => m.Score);
        var prime = r.Drafts.Where(d => d.AuditoriumId == 10 && VietnameseFilmQuota.InPriorityWindow(d.FeatureStartTime, Rules)).ToList();
        Assert.NotEmpty(prime);
        Assert.All(prime, d => Assert.True(d.IsVietnamese || scores[d.MovieId] >= median));
        Assert.Contains(prime, d => d.IsVietnamese);
    }

    [Fact]
    public void Scheduler_Generate_KeepsExistingShowtimes_AndSkipsPastOrNoKdm()
    {
        var existing = new ScheduleSlot(99, 1, 10, "Phòng 1", Day.AddHours(14), Day.AddHours(14).AddMinutes(15), Day.AddHours(16).AddMinutes(25), Day.AddHours(16).AddMinutes(40), LanguageFormat.Subtitled);
        var r = Run(new List<ExistingShowtime> { new(existing, false) });
        Assert.DoesNotContain(r.Drafts, d => d.AuditoriumId == 10 && d.StartTime < existing.CleaningEndTime && existing.StartTime < d.CleaningEndTime);
        Assert.Equal(r.Drafts.Count + 1, r.TotalCount);

        // Ngày đã qua: không xếp gì; phim hết KDM: không xếp
        var past = AiScheduler.Generate(new SchedulerInput(new[] { Now.Date.AddDays(-1) }, Movies(), Rooms, new List<ExistingShowtime>(), Now, new VietnameseShare(0, 0)), Rules);
        Assert.Empty(past.Drafts);
        var noKdm = Movies().Select(m => m.Movie.Id == 4 ? m with { Kdms = new[] { new KdmWindow(new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)) } } : m).ToList();
        var r2 = AiScheduler.Generate(new SchedulerInput(new[] { Day }, noKdm, Rooms, new List<ExistingShowtime>(), Now, new VietnameseShare(0, 0)), Rules);
        Assert.DoesNotContain(r2.Drafts, d => d.MovieId == 4);
    }

    [Fact]
    public void Scheduler_Generate_TodayStartsAfterNow()
    {
        var r = AiScheduler.Generate(new SchedulerInput(new[] { Now.Date }, Movies(), Rooms, new List<ExistingShowtime>(), Now, new VietnameseShare(0, 0)), Rules);
        Assert.NotEmpty(r.Drafts);
        Assert.All(r.Drafts, d => Assert.True(d.StartTime >= Now.AddMinutes(AiScheduler.LeadMinutes)));
    }

    [Fact]
    public void Scheduler_Stagger_MovesStartAwayFromOtherRooms()
    {
        var other = new ScheduleSlot(1, 1, 11, "Phòng 2", Day.AddHours(10), Day.AddHours(10).AddMinutes(15), Day.AddHours(12), Day.AddHours(12).AddMinutes(20), LanguageFormat.Subtitled);
        Assert.Equal(Day.AddHours(10).AddMinutes(10), AiScheduler.Stagger(Day.AddHours(10).AddMinutes(5), 10, new[] { other }, Rules));
        Assert.Equal(Day.AddHours(10).AddMinutes(5), AiScheduler.Stagger(Day.AddHours(10).AddMinutes(5), 11, new[] { other }, Rules));
    }
}
