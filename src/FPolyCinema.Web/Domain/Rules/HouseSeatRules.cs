using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>
/// Ghế nội bộ (SPEC 3.2-07): SupervisorOrManager giải phóng được ghế nội bộ của từng suất vào bất kỳ lúc nào (suất chưa kết thúc, chưa hủy);
/// dashboard quản lý ca nhắc trước giờ chiếu HouseSeatReminderMinutes phút. Logic thuần, có unit test.
/// </summary>
public static class HouseSeatRules
{
    /// <summary>Nhắc giải phóng: suất còn ghế HOUSE_HOLD và bắt đầu trong khoảng (now, now + reminder].</summary>
    public static bool NeedsReminder(DateTime startTime, int houseSeatsLeft, DateTime now, int reminderMinutes) =>
        houseSeatsLeft > 0 && startTime > now && startTime <= now.AddMinutes(reminderMinutes);

    /// <summary>Lý do không giải phóng được (null khi được).</summary>
    public static string? CannotReleaseReason(string operationalStatus, DateTime endTime, DateTime now)
    {
        if (operationalStatus == OperationalStatus.Cancelled) return "Suất đã bị hủy.";
        if (operationalStatus is OperationalStatus.CleaningInProgress or OperationalStatus.Completed || now >= endTime) return "Suất đã kết thúc.";
        return null;
    }
}
