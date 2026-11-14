using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Các mốc giờ của một suất dùng cho vòng đời vận hành.</summary>
public sealed record ShowtimeClock(DateTime StartTime, DateTime FeatureStartTime, DateTime EndTime);

/// <summary>
/// Vòng đời vận hành của suất chiếu (SPEC 3.3, ShowtimeClockService cập nhật mỗi 30 giây). Logic thuần, có unit test.
/// SCHEDULED → BOARDING từ StartTime − BoardingLeadMinutes (nếu phòng sẵn sàng) → ADS_PLAYING từ StartTime → FEATURE_PLAYING từ FeatureStartTime
/// → CREDITS_ROLLING từ EndTime − CreditsMinutes → CLEANING_IN_PROGRESS từ EndTime → COMPLETED khi nhân viên bấm "Đã dọn xong".
/// </summary>
public static class ShowtimeOpsRules
{
    /// <summary>Thứ tự tiến của các trạng thái (không bao giờ lùi).</summary>
    private static readonly string[] Order =
    {
        OperationalStatus.Scheduled, OperationalStatus.Boarding, OperationalStatus.AdsPlaying, OperationalStatus.FeaturePlaying,
        OperationalStatus.CreditsRolling, OperationalStatus.CleaningInProgress, OperationalStatus.Completed,
    };

    public static int Rank(string status) => Array.IndexOf(Order, status);

    /// <summary>Trạng thái đúng với giờ hiện tại nếu phòng đã sẵn sàng (chưa tính "Đã dọn xong").</summary>
    public static string ByTime(ShowtimeClock t, DateTime now, CinemaRules rules)
    {
        if (now >= t.EndTime) return OperationalStatus.CleaningInProgress;
        if (now >= t.EndTime.AddMinutes(-rules.CreditsMinutes)) return OperationalStatus.CreditsRolling;
        if (now >= t.FeatureStartTime) return OperationalStatus.FeaturePlaying;
        if (now >= t.StartTime) return OperationalStatus.AdsPlaying;
        if (now >= t.StartTime.AddMinutes(-rules.BoardingLeadMinutes)) return OperationalStatus.Boarding;
        return OperationalStatus.Scheduled;
    }

    /// <summary>
    /// Phòng sẵn sàng khi suất trước trong phòng đã COMPLETED hoặc không có suất trước (SPEC 3.3-21). Suất trước bị hủy không tính là suất trước.
    /// </summary>
    public static bool IsRoomReady(string? previousStatus) =>
        previousStatus is null or OperationalStatus.Completed or OperationalStatus.Cancelled;

    /// <summary>
    /// Trạng thái kế tiếp của suất: COMPLETED và CANCELLED giữ nguyên; CLEANING_IN_PROGRESS chờ nhân viên bấm "Đã dọn xong"; phòng chưa sẵn sàng thì
    /// suất đứng ở SCHEDULED; khi phòng sẵn sàng thì nhảy thẳng tới trạng thái đúng với giờ hiện tại; không bao giờ lùi.
    /// </summary>
    public static string Next(string current, ShowtimeClock t, DateTime now, bool roomReady, CinemaRules rules)
    {
        if (current is OperationalStatus.Completed or OperationalStatus.Cancelled or OperationalStatus.CleaningInProgress) return current;
        if (current == OperationalStatus.Scheduled && !roomReady) return current;
        var target = ByTime(t, now, rules);
        return Rank(target) > Rank(current) ? target : current;
    }

    /// <summary>Suất trễ (dashboard quản lý ca báo đỏ): phòng chưa sẵn sàng nên còn SCHEDULED dù đã quá StartTime.</summary>
    public static bool IsLate(string current, DateTime startTime, DateTime now) =>
        current == OperationalStatus.Scheduled && now >= startTime;

    /// <summary>Nút "Đã dọn xong" chỉ dùng được cho suất đang CLEANING_IN_PROGRESS.</summary>
    public static bool CanMarkCleaned(string current) => current == OperationalStatus.CleaningInProgress;

    /// <summary>Suất đang chạy (cho bảng sĩ số trực tiếp): đã mở cửa và chưa kết thúc.</summary>
    public static bool IsRunning(string status) =>
        status is OperationalStatus.Boarding or OperationalStatus.AdsPlaying or OperationalStatus.FeaturePlaying or OperationalStatus.CreditsRolling;

    /// <summary>
    /// Nhãn trạng thái ở bảng sảnh (UI P6-03): "boarding" (Mời vào rạp), "playing" (Đang chiếu), "cleaning" (Đang dọn – kể cả suất đã tới giờ mở cửa
    /// mà phòng chưa sẵn sàng nên còn SCHEDULED), "scheduled" (Sắp chiếu).
    /// </summary>
    public static string SignageState(string status, DateTime startTime, DateTime now, int boardingLeadMinutes) => status switch
    {
        OperationalStatus.Boarding => "boarding",
        OperationalStatus.AdsPlaying or OperationalStatus.FeaturePlaying or OperationalStatus.CreditsRolling => "playing",
        OperationalStatus.CleaningInProgress => "cleaning",
        _ when now >= startTime.AddMinutes(-boardingLeadMinutes) => "cleaning",
        _ => "scheduled",
    };

    /// <summary>Suất thuộc một ngày vận hành đã qua mà chưa dọn xong: coi như đã dọn cuối ngày (để dữ liệu cũ không chặn phòng mãi).</summary>
    public static bool IsStaleDay(DateTime startTime, DateTime now) =>
        PricingCalculator.OperatingDate(startTime) < PricingCalculator.OperatingDate(now);
}
