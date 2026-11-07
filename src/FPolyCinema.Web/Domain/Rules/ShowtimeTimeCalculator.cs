using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Bốn mốc thời gian của một suất chiếu (SPEC 3.3).</summary>
public sealed record ShowtimeTimes(DateTime StartTime, DateTime FeatureStartTime, DateTime EndTime, DateTime CleaningEndTime);

/// <summary>
/// Tính các mốc của suất chiếu (SPEC 3.3). Logic thuần, có unit test.
/// - StartTime: mở cửa và bắt đầu quảng cáo, làm tròn theo bội số ScheduleRoundMinutes (gần nhất, đúng nửa thì lên).
/// - FeatureStartTime = StartTime + AdsMinutes.
/// - EndTime = FeatureStartTime + DurationMinutes.
/// - CleaningEndTime = EndTime + CleaningMinutes, làm tròn lên bội số ScheduleRoundMinutes.
/// </summary>
public static class ShowtimeTimeCalculator
{
    public static ShowtimeTimes Compute(DateTime requestedStart, int durationMinutes, CinemaRules rules)
    {
        if (durationMinutes <= 0) throw new ArgumentOutOfRangeException(nameof(durationMinutes), "Thời lượng phim phải lớn hơn 0.");
        var start = RoundToNearest(requestedStart, rules.ScheduleRoundMinutes);
        var feature = start.AddMinutes(rules.AdsMinutes);
        var end = feature.AddMinutes(durationMinutes);
        var cleaningEnd = RoundUp(end.AddMinutes(rules.CleaningMinutes), rules.ScheduleRoundMinutes);
        return new ShowtimeTimes(start, feature, end, cleaningEnd);
    }

    /// <summary>Làm tròn đến bội số step phút gần nhất tính từ 00:00 của cùng ngày, bỏ phần giây.</summary>
    public static DateTime RoundToNearest(DateTime t, int stepMinutes)
    {
        if (stepMinutes <= 1) return TrimSeconds(t);
        var stepTicks = TimeSpan.FromMinutes(stepMinutes).Ticks;
        var ofDay = t.TimeOfDay.Ticks;
        var rem = ofDay % stepTicks;
        var rounded = rem * 2 >= stepTicks ? ofDay - rem + stepTicks : ofDay - rem;
        return t.Date.AddTicks(rounded);
    }

    /// <summary>Làm tròn lên bội số step phút tính từ 00:00 của cùng ngày.</summary>
    public static DateTime RoundUp(DateTime t, int stepMinutes)
    {
        if (stepMinutes <= 1)
        {
            var trimmed = TrimSeconds(t);
            return trimmed < t ? trimmed.AddMinutes(1) : trimmed;
        }
        var stepTicks = TimeSpan.FromMinutes(stepMinutes).Ticks;
        var ofDay = t.TimeOfDay.Ticks;
        var rem = ofDay % stepTicks;
        return rem == 0 ? t : t.Date.AddTicks(ofDay - rem + stepTicks);
    }

    private static DateTime TrimSeconds(DateTime t) => new(t.Ticks - t.Ticks % TimeSpan.TicksPerMinute, t.Kind);
}
