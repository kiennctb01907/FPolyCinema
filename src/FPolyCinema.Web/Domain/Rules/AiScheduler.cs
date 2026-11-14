using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Một phim đưa vào bộ xếp lịch: thông tin xếp suất, AiDemandScore và KDM tại rạp.</summary>
public sealed record SchedulerMovie(ScheduleMovie Movie, decimal Score, IReadOnlyList<KdmWindow> Kdms);

/// <summary>Một phòng đưa vào bộ xếp lịch, kèm sức chứa bán được.</summary>
public sealed record SchedulerRoom(int Id, string Name, int Capacity);

/// <summary>Một suất đã có của rạp (không bị hủy) kèm cờ phim Việt để đếm hạn ngạch.</summary>
public sealed record ExistingShowtime(ScheduleSlot Slot, bool IsVietnamese);

/// <summary>Suất nháp do AI xếp: chưa lưu; Warnings là các cảnh báo không chặn còn lại (ví dụ tỷ lệ phim Việt cả năm).</summary>
public sealed record DraftShowtime(int MovieId, string MovieTitle, string AgeRating, bool IsVietnamese, int AuditoriumId, string AuditoriumName,
    DateTime StartTime, DateTime FeatureStartTime, DateTime EndTime, DateTime CleaningEndTime, string LanguageFormat, IReadOnlyList<ScheduleIssue> Warnings);

/// <summary>Đầu vào của bộ xếp lịch: các ngày vận hành (1–7 ngày), phim, phòng, các suất đã có, thời điểm hiện tại, tỷ lệ phim Việt từ đầu năm.</summary>
public sealed record SchedulerInput(IReadOnlyList<DateTime> Days, IReadOnlyList<SchedulerMovie> Movies, IReadOnlyList<SchedulerRoom> Rooms,
    IReadOnlyList<ExistingShowtime> Existing, DateTime Now, VietnameseShare YearShare);

/// <summary>Kết quả: các suất nháp, số suất phim Việt và tổng số suất của các ngày đã xếp (gồm cả suất đã có), ghi chú cho quản lý.</summary>
public sealed record SchedulerResult(List<DraftShowtime> Drafts, int VietnameseCount, int TotalCount, List<string> Notes);

/// <summary>
/// AI Smart Scheduler (SPEC 4.3-03 → 4.3-08): thuật toán tham lam có ràng buộc. Logic thuần, có unit test.
/// 1. Số suất mục tiêu của mỗi phim trong ngày tỷ lệ với AiDemandScore, mỗi phim ít nhất 1 suất; tổng suất phim Việt ≥ VietnameseFilmTargetShare.
/// 2. Xếp phòng theo sức chứa giảm dần; khung 18:00–22:00 ở nửa số phòng lớn dành cho phim điểm cao, ưu tiên phim Việt (Điều 9 khoản 2).
/// 3. Điền từng phòng từ OperatingDayStart: suất sau bắt đầu từ CleaningEndTime của suất trước (đã làm tròn lên ScheduleRoundMinutes);
///    giờ bắt đầu lệch tối thiểu MinStartStaggerMinutes so với các phòng khác cùng rạp.
/// 4. Phim P, K có bản DUBBED: suất trước 18:00 xếp DUBBED, suất từ 18:00 xếp SUBTITLED (nếu phim có bản đó).
/// 5. Mọi suất qua ScheduleConstraintChecker: không có lỗi chặn; tránh cảnh báo (phim K kết thúc từ 22:00, T13 từ 23:00, quá nhiều phòng cùng phút);
///    phim T18 ưu tiên xếp từ 19:00.
/// 6. Không động vào các suất đã có (chỉ né chúng); kết quả chỉ là bản nháp.
/// </summary>
public static class AiScheduler
{
    public static readonly TimeSpan T18PreferredFrom = new(19, 0, 0);
    /// <summary>Hôm nay chỉ xếp suất bắt đầu sau thời điểm hiện tại ít nhất chừng này phút.</summary>
    public const int LeadMinutes = 30;

    private static readonly HashSet<string> AvoidWarnings = new() { ScheduleWarningCode.LateEndForAudience, ScheduleWarningCode.SameMinuteStarts };

    /// <summary>
    /// Số suất mục tiêu: totalSlots × điểm / tổng điểm (làm tròn xuống, chia phần dư theo phần lẻ lớn nhất), mỗi phim ít nhất 1.
    /// Sau đó nếu tổng suất phim Việt dưới ⌈targetShare × totalSlots⌉ thì chuyển suất từ phim nước ngoài điểm thấp nhất (còn hơn 1 suất) sang phim Việt điểm cao nhất.
    /// </summary>
    public static Dictionary<int, int> TargetCounts(IReadOnlyList<(int MovieId, decimal Score, bool IsVietnamese)> movies, int totalSlots, decimal vietnameseTargetShare)
    {
        var result = new Dictionary<int, int>();
        if (movies.Count == 0) return result;
        totalSlots = Math.Max(totalSlots, movies.Count);
        var sum = movies.Sum(m => Math.Max(m.Score, 0.01m));
        var raw = movies.Select(m => (m.MovieId, Value: totalSlots * Math.Max(m.Score, 0.01m) / sum)).ToList();
        foreach (var (id, v) in raw) result[id] = Math.Max(1, (int)Math.Floor(v));
        int remain = totalSlots - result.Values.Sum();
        foreach (var (id, _) in raw.OrderByDescending(r => r.Value - Math.Floor(r.Value)).ThenByDescending(r => r.Value))
        {
            if (remain <= 0) break;
            result[id]++;
            remain--;
        }
        // Quá tổng vì mức tối thiểu 1: bớt ở phim có nhiều suất nhất
        while (result.Values.Sum() > totalSlots && result.Values.Max() > 1)
        {
            var top = result.OrderByDescending(kv => kv.Value).First().Key;
            result[top]--;
        }

        var vn = movies.Where(m => m.IsVietnamese).OrderByDescending(m => m.Score).ToList();
        if (vn.Count == 0) return result;
        int need = (int)Math.Ceiling(vietnameseTargetShare * totalSlots);
        while (vn.Sum(m => result[m.MovieId]) < need)
        {
            var donors = movies.Where(m => !m.IsVietnamese && result[m.MovieId] > 1).OrderBy(m => m.Score).ToList();
            if (donors.Count == 0) break;
            result[donors[0].MovieId]--;
            // Phim Việt nhận thêm: phim có tỷ lệ suất/điểm thấp nhất
            var taker = vn.OrderBy(m => result[m.MovieId] / Math.Max(m.Score, 0.01m)).First();
            result[taker.MovieId]++;
        }
        return result;
    }

    /// <summary>Bản ngôn ngữ của suất (SPEC 4.3-06).</summary>
    public static string LanguageFor(ScheduleMovie m, DateTime startTime, CinemaRules rules)
    {
        var formats = m.LanguageFormats;
        bool family = m.AgeRating is AgeRating.P or AgeRating.K;
        if (family && formats.Contains(LanguageFormat.Dubbed))
            return startTime.TimeOfDay < NlpParser.EveningStart || !formats.Contains(LanguageFormat.Subtitled) ? LanguageFormat.Dubbed : LanguageFormat.Subtitled;
        if (formats.Contains(LanguageFormat.Subtitled)) return LanguageFormat.Subtitled;
        if (formats.Contains(LanguageFormat.Original)) return LanguageFormat.Original;
        return formats.FirstOrDefault() ?? LanguageFormat.Subtitled;
    }

    /// <summary>Ước lượng số suất một phòng chạy được trong ngày từ giờ bắt đầu sớm nhất (độ dài chu kỳ theo thời lượng phim trung bình).</summary>
    public static int EstimateSlots(DateTime earliest, DateTime lastStart, int averageDuration, CinemaRules rules)
    {
        if (earliest > lastStart) return 0;
        int cycle = rules.AdsMinutes + averageDuration + rules.CleaningMinutes + rules.ScheduleRoundMinutes;
        return (int)((lastStart - earliest).TotalMinutes / Math.Max(cycle, 1)) + 1;
    }

    public static SchedulerResult Generate(SchedulerInput input, CinemaRules rules)
    {
        var drafts = new List<DraftShowtime>();
        var notes = new List<string>();
        var existing = input.Existing.ToList();
        int stepMin = Math.Max(rules.ScheduleRoundMinutes, 1);
        var rooms = input.Rooms.OrderByDescending(r => r.Capacity).ThenBy(r => r.Name).ToList();
        int largeCount = Math.Max(1, (rooms.Count + 1) / 2);
        var large = rooms.Take(largeCount).Select(r => r.Id).ToHashSet();
        int vnTotal = 0, total = 0;

        foreach (var day in input.Days.Select(d => d.Date).Distinct().OrderBy(d => d))
        {
            var dayStart = day + rules.OperatingDayStart;
            var lastStart = day + rules.LastShowStartLatest;
            var earliest = dayStart;
            var minStart = ShowtimeTimeCalculator.RoundUp(input.Now.AddMinutes(LeadMinutes), stepMin);
            if (minStart > earliest) earliest = minStart;

            var daySlots = existing.Where(e => PricingCalculator.OperatingDate(e.Slot.StartTime) == day).ToList();
            int dayVn = daySlots.Count(e => e.IsVietnamese), dayTotal = daySlots.Count;
            if (earliest > lastStart)
            {
                vnTotal += dayVn; total += dayTotal;
                continue;
            }

            // Phim xếp được trong ngày: có giấy phép, khác C, có KDM phủ một phần ngày
            var movies = input.Movies.Where(m => AgeRules.CanSchedule(m.Movie.AgeRating, m.Movie.ClassificationLicenseNo).Ok
                                                 && m.Kdms.Any(k => k.ValidFrom < lastStart.AddHours(4) && k.ValidUntil > earliest)).ToList();
            if (movies.Count == 0 || rooms.Count == 0)
            {
                notes.Add($"{Fmt.WeekdayDate(day)}: không có phim nào có KDM hợp lệ để xếp.");
                vnTotal += dayVn; total += dayTotal;
                continue;
            }

            int avgDuration = (int)movies.Average(m => m.Movie.DurationMinutes);
            int estimate = rooms.Sum(r => EstimateSlots(earliest, lastStart, avgDuration, rules));
            var targets = TargetCounts(movies.Select(m => (m.Movie.Id, m.Score, m.Movie.IsVietnameseFilm)).ToList(), estimate, rules.VietnameseFilmTargetShare);
            var counts = movies.ToDictionary(m => m.Movie.Id, m => daySlots.Count(e => e.Slot.MovieId == m.Movie.Id));
            var median = movies.Select(m => m.Score).OrderBy(s => s).ElementAt(movies.Count / 2);
            int primeVn = 0, primeTotal = 0;

            foreach (var room in rooms)
            {
                var cursor = earliest;
                int? previousMovie = null;
                int guard = 0;
                while (cursor <= lastStart && guard++ < 1000)
                {
                    var allSlots = existing.Select(e => e.Slot).Concat(drafts.Select(ToSlot)).ToList();
                    var start = Stagger(cursor, room.Id, allSlots, rules);
                    if (start > lastStart) break;

                    // Né suất đã có cùng phòng
                    var blocking = allSlots.Where(s => s.AuditoriumId == room.Id && s.CleaningEndTime > start && s.StartTime < start.AddMinutes(rules.AdsMinutes + avgDuration + rules.CleaningMinutes))
                        .OrderBy(s => s.StartTime).FirstOrDefault();

                    bool needVn = dayVn < (int)Math.Ceiling(rules.VietnameseFilmTargetShare * (dayTotal + 1));
                    var feature = start.AddMinutes(rules.AdsMinutes);
                    bool prime = VietnameseFilmQuota.InPriorityWindow(feature, rules);
                    bool primeLarge = prime && large.Contains(room.Id);
                    bool primeNeedVn = primeLarge && primeVn < (int)Math.Ceiling(rules.VietnameseFilmTargetShare * (primeTotal + 1));

                    var ordered = movies.OrderByDescending(m => Priority(m, start, targets[m.Movie.Id] - counts[m.Movie.Id], needVn, primeLarge, primeNeedVn, median, previousMovie))
                        .ThenByDescending(m => m.Score).ToList();

                    DraftShowtime? placed = null;
                    foreach (var m in ordered)
                    {
                        var times = ShowtimeTimeCalculator.Compute(start, m.Movie.DurationMinutes, rules);
                        var lang = LanguageFor(m.Movie, times.StartTime, rules);
                        var slot = new ScheduleSlot(null, m.Movie.Id, room.Id, room.Name, times.StartTime, times.FeatureStartTime, times.EndTime, times.CleaningEndTime, lang);
                        var ctx = new ScheduleContext(m.Movie, m.Kdms, allSlots, Array.Empty<ScheduleSlot>(), input.YearShare, input.Now);
                        var check = ScheduleConstraintChecker.Check(slot, ctx, rules);
                        if (check.HasErrors || check.Warnings.Any(w => AvoidWarnings.Contains(w.Code))) continue;
                        placed = new DraftShowtime(m.Movie.Id, m.Movie.Title, m.Movie.AgeRating, m.Movie.IsVietnameseFilm, room.Id, room.Name, times.StartTime,
                            times.FeatureStartTime, times.EndTime, times.CleaningEndTime, lang, check.Warnings.ToList());
                        break;
                    }

                    if (placed == null)
                    {
                        // Không phim nào hợp ở mốc này: nhảy qua suất đang chắn hoặc dời 5 phút
                        cursor = blocking != null && blocking.CleaningEndTime > cursor ? blocking.CleaningEndTime : start.AddMinutes(stepMin);
                        continue;
                    }
                    drafts.Add(placed);
                    counts[placed.MovieId]++;
                    dayTotal++;
                    if (placed.IsVietnamese) dayVn++;
                    if (VietnameseFilmQuota.InPriorityWindow(placed.FeatureStartTime, rules) && large.Contains(room.Id))
                    {
                        primeTotal++;
                        if (placed.IsVietnamese) primeVn++;
                    }
                    previousMovie = placed.MovieId;
                    cursor = placed.CleaningEndTime;
                }
            }

            foreach (var m in movies.Where(m => counts[m.Movie.Id] == 0))
                notes.Add($"{Fmt.WeekdayDate(day)}: chưa xếp được suất nào cho phim {m.Movie.Title} vì không còn khung giờ hợp lệ.");
            vnTotal += dayVn;
            total += dayTotal;
        }
        return new SchedulerResult(drafts, vnTotal, total, notes);
    }

    /// <summary>
    /// Điểm ưu tiên chọn phim cho một mốc giờ: thiếu so với mục tiêu (×10); cần thêm phim Việt cho đủ VietnameseFilmTargetShare (+100);
    /// khung 18:00–22:00 ở phòng lớn: phim điểm cao (+15), phim Việt (+15, thêm +40 khi tỷ lệ phim Việt của khung còn thấp);
    /// phim T18 trước 19:00 (−60); tránh lặp lại phim vừa chiếu ở phòng (−1).
    /// </summary>
    public static decimal Priority(SchedulerMovie m, DateTime start, int deficit, bool needVietnamese, bool primeLargeRoom, bool primeNeedVietnamese,
        decimal medianScore, int? previousMovieId)
    {
        decimal p = deficit * 10m;
        if (needVietnamese && m.Movie.IsVietnameseFilm && deficit > -2) p += 100m;
        if (primeLargeRoom)
        {
            if (m.Score >= medianScore) p += 15m;
            if (m.Movie.IsVietnameseFilm) p += primeNeedVietnamese ? 55m : 15m;
        }
        if (m.Movie.AgeRating == AgeRating.T18 && start.TimeOfDay < T18PreferredFrom) p -= 60m;
        if (previousMovieId == m.Movie.Id) p -= 1m;
        return p;
    }

    /// <summary>Dời giờ bắt đầu (bước ScheduleRoundMinutes) cho tới khi lệch ít nhất MinStartStaggerMinutes với mọi suất của phòng khác.</summary>
    public static DateTime Stagger(DateTime start, int auditoriumId, IReadOnlyList<ScheduleSlot> slots, CinemaRules rules)
    {
        var t = ShowtimeTimeCalculator.RoundUp(start, Math.Max(rules.ScheduleRoundMinutes, 1));
        var gap = TimeSpan.FromMinutes(rules.MinStartStaggerMinutes);
        for (int i = 0; i < 200; i++)
        {
            var clash = slots.FirstOrDefault(s => s.AuditoriumId != auditoriumId && (s.StartTime - t).Duration() < gap);
            if (clash == null) return t;
            t = ShowtimeTimeCalculator.RoundUp(clash.StartTime + gap, Math.Max(rules.ScheduleRoundMinutes, 1));
        }
        return t;
    }

    public static ScheduleSlot ToSlot(DraftShowtime d) =>
        new(null, d.MovieId, d.AuditoriumId, d.AuditoriumName, d.StartTime, d.FeatureStartTime, d.EndTime, d.CleaningEndTime, d.LanguageFormat);
}
