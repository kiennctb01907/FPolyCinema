namespace FPolyCinema.Web.Infrastructure;

/// <summary>
/// Đồng hồ thật: lấy giờ UTC của máy rồi đổi sang múi giờ CinemaRules.TimeZoneId.
/// Đây là file duy nhất được đọc đồng hồ hệ thống.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <summary>Tên múi giờ dự phòng trên Windows cũ không có cơ sở dữ liệu IANA.</summary>
    public const string WindowsFallbackTimeZoneId = "SE Asia Standard Time";

    private readonly TimeZoneInfo _zone;

    public SystemClock(string timeZoneId)
    {
        _zone = ResolveTimeZone(timeZoneId);
    }

    public SystemClock(CinemaRules rules) : this(rules.TimeZoneId) { }

    public TimeZoneInfo Zone => _zone;

    public DateTime Now
    {
        get
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zone);
            // Bỏ phần lẻ dưới giây để khớp cột DATETIME2(0)
            return DateTime.SpecifyKind(local.AddTicks(-(local.Ticks % TimeSpan.TicksPerSecond)), DateTimeKind.Unspecified);
        }
    }

    public DateTime Today => Now.Date;

    /// <summary>Tìm múi giờ theo tên; không có thì thử tên Windows, cuối cùng dùng UTC+7 cố định.</summary>
    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        foreach (var id in new[] { timeZoneId, "Asia/Ho_Chi_Minh", WindowsFallbackTimeZoneId })
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }
        return TimeZoneInfo.CreateCustomTimeZone("UTC+07", TimeSpan.FromHours(7), "Giờ Việt Nam", "Giờ Việt Nam");
    }
}
