using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>
/// Trạng thái vào cửa của hóa đơn và cờ "hóa đơn đã hoàn tất" (SPEC 3.8-09, CONTRACTS C3-38). Logic thuần, có unit test.
/// </summary>
public static class CheckInRules
{
    /// <summary>
    /// Bills.CheckInStatus tự tính từ các vé còn hiệu lực (bỏ vé SWAPPED, VOIDED): không có vé là NONE; chưa vé nào vào là NOT_CHECKED_IN;
    /// vào một phần là PARTIALLY_CHECKED_IN; vào hết là FULLY_CHECKED_IN.
    /// </summary>
    public static string BillStatusOf(IEnumerable<string> ticketStatuses)
    {
        var active = ticketStatuses.Where(s => s is TicketCheckInStatus.NotCheckedIn or TicketCheckInStatus.CheckedIn).ToList();
        if (active.Count == 0) return BillCheckInStatus.None;
        int inside = active.Count(s => s == TicketCheckInStatus.CheckedIn);
        if (inside == 0) return BillCheckInStatus.NotCheckedIn;
        return inside == active.Count ? BillCheckInStatus.FullyCheckedIn : BillCheckInStatus.PartiallyCheckedIn;
    }

    /// <summary>
    /// Hóa đơn "đã hoàn tất" (C3-38): Status finish, CheckInStatus là NONE hoặc FULLY_CHECKED_IN, FnbStatus là NONE hoặc FULFILLED.
    /// </summary>
    public static bool IsCompleted(string status, string checkInStatus, string fnbStatus) =>
        status == BillStatus.Finish
        && checkInStatus is BillCheckInStatus.None or BillCheckInStatus.FullyCheckedIn
        && fnbStatus is FnbStatus.None or FnbStatus.Fulfilled;
}
