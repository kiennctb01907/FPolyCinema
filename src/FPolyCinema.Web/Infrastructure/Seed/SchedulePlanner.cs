using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;

namespace FPolyCinema.Web.Infrastructure.Seed;

/// <summary>Phim đưa vào bộ xếp lịch mẫu.</summary>
public sealed record PlanMovie(int Index, int DurationMinutes, string AgeRating, string[] LanguageFormats, DateTime ReleaseDate,
    bool IsVietnamese, bool HasLicense, bool IsAnimation);

/// <summary>Khoảng hiệu lực KDM của một phim tại một rạp.</summary>
public sealed record PlanKdm(int MovieIndex, int CinemaIndex, DateTime ValidFrom, DateTime ValidUntil);

/// <summary>Một suất chiếu mẫu đã tính đủ 4 mốc thời gian.</summary>
public sealed record PlannedShowtime(int CinemaIndex, int AuditoriumIndex, int MovieIndex, DateTime OperatingDate, ShowtimeTimes Times, string LanguageFormat);

/// <summary>
/// Xếp lịch chiếu mẫu cho DbSeeder (SPEC 8.1: suất chiếu hợp lệ mọi ràng buộc). Logic thuần, có unit test.
/// Mỗi suất đều thỏa:
/// - Không chồng giờ trong cùng phòng: các khoảng [StartTime, CleaningEndTime) không giao nhau.
/// - Phim có giấy phép phân loại và khác C; đã khởi chiếu; LanguageFormat thuộc Movies.LanguageFormats.
/// - KDM của phim tại rạp phủ trọn [FeatureStartTime, EndTime].
/// - StartTime trong khung OperatingDayStart đến LastShowStartLatest, làm tròn theo ScheduleRoundMinutes.
/// - Không quá MaxSameMinuteStarts phòng cùng rạp bắt đầu cùng một phút.
/// - Phim K kết thúc trước ChildU13LatestEnd và phim T13 kết thúc trước ChildU16LatestEnd (tránh cảnh báo giờ chiếu trẻ em).
/// Phim hoạt hình chiếu bản DUBBED trước 18:00, SUBTITLED từ 18:00; phim T18 chỉ xếp từ 12:00.
/// </summary>
public static class SchedulePlanner
{
    /// <summary>Danh sách phát theo phòng (index phim trong SeedCatalog.Movies): IMAX ưu tiên phim bom tấn.</summary>
    public static readonly int[][] Playlists =
    {
        new[] { 7, 2, 5, 3, 7, 2, 5, 0 },
        new[] { 3, 2, 4, 5, 1, 8, 6, 0 },
        new[] { 0, 4, 6, 1, 3, 8, 2, 7 },
    };

    public static List<PlannedShowtime> Plan(IReadOnlyList<DateTime> operatingDates, int cinemaCount, int auditoriumsPerCinema,
        IReadOnlyList<PlanMovie> movies, IReadOnlyList<PlanKdm> kdms, CinemaRules rules)
    {
        var result = new List<PlannedShowtime>();
        var byIndex = movies.ToDictionary(m => m.Index);
        for (int day = 0; day < operatingDates.Count; day++)
        {
            var date = operatingDates[day].Date;
            for (int cinema = 0; cinema < cinemaCount; cinema++)
            {
                var startsAtMinute = new Dictionary<DateTime, int>();
                for (int aud = 0; aud < auditoriumsPerCinema; aud++)
                {
                    var playlist = Playlists[aud % Playlists.Length];
                    int pointer = (day + cinema) % playlist.Length;
                    var t = date + rules.OperatingDayStart.Add(TimeSpan.FromMinutes(60 + aud * 20 + cinema * 10));
                    int slot = 0;
                    while (true)
                    {
                        var start = ShowtimeTimeCalculator.RoundToNearest(t, rules.ScheduleRoundMinutes);
                        if (start.TimeOfDay > rules.LastShowStartLatest || start.Date != date) break;
                        while (startsAtMinute.TryGetValue(start, out var n) && n >= rules.MaxSameMinuteStarts) start = start.AddMinutes(rules.ScheduleRoundMinutes);
                        if (start.TimeOfDay > rules.LastShowStartLatest) break;

                        PlannedShowtime? chosen = null;
                        for (int k = 0; k < playlist.Length && chosen == null; k++)
                        {
                            var idx = playlist[(pointer + k) % playlist.Length];
                            if (!byIndex.TryGetValue(idx, out var movie)) continue;
                            chosen = TryPlace(movie, cinema, aud, date, start, kdms, rules);
                            if (chosen != null) pointer = (pointer + k + 1) % playlist.Length;
                        }
                        if (chosen == null)
                        {
                            // Không phim nào hợp lệ ở mốc này: thử mốc sau 30 phút
                            t = start.AddMinutes(30);
                            continue;
                        }
                        result.Add(chosen);
                        startsAtMinute[start] = startsAtMinute.TryGetValue(start, out var c) ? c + 1 : 1;
                        int gap = ((day + aud + slot) % 3) * rules.ScheduleRoundMinutes;
                        t = chosen.Times.CleaningEndTime.AddMinutes(gap);
                        slot++;
                    }
                }
            }
        }
        return result;
    }

    private static PlannedShowtime? TryPlace(PlanMovie movie, int cinema, int aud, DateTime date, DateTime start, IReadOnlyList<PlanKdm> kdms, CinemaRules rules)
    {
        if (!movie.HasLicense || movie.AgeRating == AgeRating.C) return null;
        if (movie.ReleaseDate.Date > date) return null;
        if (movie.AgeRating == AgeRating.T18 && start.TimeOfDay < new TimeSpan(12, 0, 0)) return null;
        if (movie.IsAnimation && start.TimeOfDay >= new TimeSpan(18, 0, 0)) return null;

        var times = ShowtimeTimeCalculator.Compute(start, movie.DurationMinutes, rules);
        if (movie.AgeRating == AgeRating.K && !EndsBefore(times.EndTime, date, rules.ChildU13LatestEnd)) return null;
        if (movie.AgeRating == AgeRating.T13 && !EndsBefore(times.EndTime, date, rules.ChildU16LatestEnd)) return null;

        bool kdmOk = kdms.Any(k => k.MovieIndex == movie.Index && k.CinemaIndex == cinema && k.ValidFrom <= times.FeatureStartTime && times.EndTime <= k.ValidUntil);
        if (!kdmOk) return null;

        var language = PickLanguage(movie, start);
        if (language == null) return null;
        return new PlannedShowtime(cinema, aud, movie.Index, date, times, language);
    }

    /// <summary>Suất kết thúc trong cùng ngày vận hành và trước mốc giờ cho trước.</summary>
    public static bool EndsBefore(DateTime end, DateTime operatingDate, TimeSpan latest) =>
        end.Date == operatingDate.Date && end.TimeOfDay < latest;

    private static string? PickLanguage(PlanMovie movie, DateTime start)
    {
        if (movie.IsAnimation)
        {
            var preferred = start.TimeOfDay < new TimeSpan(18, 0, 0) ? LanguageFormat.Dubbed : LanguageFormat.Subtitled;
            if (movie.LanguageFormats.Contains(preferred)) return preferred;
        }
        return movie.LanguageFormats.FirstOrDefault();
    }
}
