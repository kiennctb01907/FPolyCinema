using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Một vé của hóa đơn được quét ở cửa.</summary>
public sealed record GateTicket(int TicketId, string SeatLabel, string AudienceType, string AudienceLabel, string CheckInStatus, bool RequiresDocument, DateTime? CheckedInAt);

/// <summary>Dữ liệu một lần quét ở máy soát vé. Found = false khi mã không tồn tại.</summary>
public sealed record GateInput(
    bool Found, string? BillStatus, int? BillCinemaId, int GateCinemaId, int? ScannedTicketId, IReadOnlyList<GateTicket> Tickets,
    string? OperationalStatus, DateTime? StartTime, DateTime? EndTime, string? AgeRating, bool RoomReady, DateTime Now);

/// <summary>Kết quả soát vé: Kind là ok (xanh), warn (vàng, cần xác nhận) hoặc reject (đỏ, kèm lý do).</summary>
public sealed record GateVerdict(string Kind, string? Reason, string Message, List<string> Warnings, List<int> AdmittableTicketIds)
{
    public const string Ok = "ok", Warn = "warn", Reject = "reject";
}

/// <summary>Lý do từ chối ở cửa (tên nội bộ cho giao diện, không phải mã lỗi AJAX).</summary>
public static class GateReason
{
    public const string NotFound = "NOT_FOUND";
    public const string NotPaid = "NOT_PAID";
    public const string TicketInvalid = "TICKET_INVALID";
    public const string WrongCinema = "WRONG_CINEMA";
    public const string RoomCleaning = "ROOM_CLEANING";
    public const string TooEarly = "TOO_EARLY";
    public const string Ended = "ENDED";
    public const string AlreadyCheckedIn = "ALREADY_CHECKED_IN";
}

/// <summary>
/// Soát vé (SPEC 3.8). Kiểm theo đúng thứ tự, dừng ở lỗi đầu tiên, mỗi lỗi một thông điệp riêng: 1. mã tồn tại; 2. hóa đơn finish; 3. vé không
/// VOIDED hay SWAPPED; 4. đúng rạp; 5. suất đang BOARDING, ADS_PLAYING hoặc FEATURE_PLAYING; 6. vé chưa CHECKED_IN. Sau đó là các cảnh báo vàng
/// (vẫn cho vào nhưng nhân viên phải xác nhận): phim T13 trở lên, phim K có vé trẻ em, suất muộn với trẻ em, vé cần giấy tờ. Logic thuần, có unit test.
/// </summary>
public static class GateRules
{
    /// <summary>Trạng thái suất cho phép vào cửa.</summary>
    public static bool IsOpenForEntry(string? status) =>
        status is OperationalStatus.Boarding or OperationalStatus.AdsPlaying or OperationalStatus.FeaturePlaying;

    public static GateVerdict Check(GateInput x, CinemaRules rules)
    {
        if (!x.Found) return Rejected(GateReason.NotFound, "Mã không tồn tại. Bạn kiểm tra lại mã trên vé.");
        if (x.BillStatus != BillStatus.Finish)
            return Rejected(GateReason.NotPaid, x.BillStatus == BillStatus.Waiting ? "Hóa đơn chưa thanh toán." : "Hóa đơn đã bị hủy, vé không còn hiệu lực.");

        var scanned = x.ScannedTicketId is int sid ? x.Tickets.FirstOrDefault(t => t.TicketId == sid) : null;
        var active = x.Tickets.Where(t => t.CheckInStatus is TicketCheckInStatus.NotCheckedIn or TicketCheckInStatus.CheckedIn).ToList();
        if (scanned != null && scanned.CheckInStatus == TicketCheckInStatus.Swapped)
            return Rejected(GateReason.TicketInvalid, $"Vé ghế {scanned.SeatLabel} đã đổi sang ghế khác, mã cũ hết hiệu lực.");
        if (scanned != null && scanned.CheckInStatus == TicketCheckInStatus.Voided)
            return Rejected(GateReason.TicketInvalid, $"Vé ghế {scanned.SeatLabel} đã bị hủy.");
        if (active.Count == 0) return Rejected(GateReason.TicketInvalid, "Hóa đơn không còn vé nào hiệu lực.");

        if (x.BillCinemaId != x.GateCinemaId) return Rejected(GateReason.WrongCinema, "Vé của rạp khác. Mời khách tới đúng rạp ghi trên vé.");

        if (!IsOpenForEntry(x.OperationalStatus))
        {
            if (x.OperationalStatus == OperationalStatus.Scheduled && x.StartTime is DateTime st)
            {
                if (x.Now >= st.AddMinutes(-rules.BoardingLeadMinutes) && !x.RoomReady)
                    return Rejected(GateReason.RoomCleaning, "Phòng đang dọn vệ sinh, chưa thể đón khách");
                return Rejected(GateReason.TooEarly, $"Chưa tới giờ vào. Mở cửa lúc {Fmt.Time(st.AddMinutes(-rules.BoardingLeadMinutes))}.");
            }
            return Rejected(GateReason.Ended, "Suất đã kết thúc");
        }

        if (scanned != null && scanned.CheckInStatus == TicketCheckInStatus.CheckedIn)
            return Rejected(GateReason.AlreadyCheckedIn, $"Vé ghế {scanned.SeatLabel} đã vào cửa lúc {(scanned.CheckedInAt is DateTime at ? Fmt.Time(at) : "trước đó")}.");
        var waiting = active.Where(t => t.CheckInStatus == TicketCheckInStatus.NotCheckedIn).ToList();
        if (waiting.Count == 0) return Rejected(GateReason.AlreadyCheckedIn, "Mọi vé của hóa đơn đã vào cửa.");

        var warnings = Warnings(x.AgeRating, x.StartTime!.Value, x.EndTime!.Value, waiting, rules);
        return new GateVerdict(warnings.Count > 0 ? GateVerdict.Warn : GateVerdict.Ok, null, warnings.Count > 0 ? "Cần xác nhận" : "Hợp lệ",
            warnings, waiting.Select(t => t.TicketId).ToList());
    }

    /// <summary>Cảnh báo vàng: T13 trở lên (CCCD hoặc VNeID), phim K có vé trẻ em (người giám hộ), suất kết thúc muộn với trẻ em, vé cần giấy tờ.</summary>
    public static List<string> Warnings(string? rating, DateTime start, DateTime end, IReadOnlyList<GateTicket> tickets, CinemaRules rules)
    {
        var list = new List<string>();
        if (AgeRules.RequiresMinimumAge(rating))
            list.Add($"Phim {rating}: kiểm tra CCCD hoặc VNeID, người xem phải đủ {AgeRules.MinimumAge(rating)} tuổi.");
        if (rating == AgeRating.K && tickets.Any(t => t.AudienceType == AudienceType.ChildU13))
            list.Add("Phim K có vé trẻ em dưới 13 tuổi: kiểm tra cha, mẹ hoặc người giám hộ đi cùng.");
        var curfew = OnlineAgeRules.CurfewNotice(start, end, rules.ChildU13LatestEnd, rules.ChildU16LatestEnd);
        if (curfew != null) list.Add($"Suất kết thúc lúc {AgeRules.EndText(start, end)}: {char.ToLowerInvariant(curfew[0])}{curfew[1..]}.");
        foreach (var g in tickets.Where(t => t.RequiresDocument).GroupBy(t => t.AudienceLabel))
            list.Add($"Vé {g.Key} (ghế {string.Join(", ", g.Select(t => t.SeatLabel))}): kiểm tra giấy tờ.");
        return list;
    }

    private static GateVerdict Rejected(string reason, string message) => new(GateVerdict.Reject, reason, message, new List<string>(), new List<int>());
}
