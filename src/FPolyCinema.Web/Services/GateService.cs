using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Utils;

namespace FPolyCinema.Web.Services;

/// <summary>Một vé hiện trên thẻ kết quả soát vé.</summary>
public sealed record GateTicketView(int TicketId, string Seat, string AudienceLabel, string CheckInStatus, string StatusLabel, bool Admittable, bool Preselected);

/// <summary>Kết quả một lần quét ở máy soát vé (UI P3).</summary>
public sealed record GateScanResult(GateVerdict Verdict, int? BillId, string? BillCode, int? ShowtimeId, string? MovieTitle, string? AgeRating, string? AgeExplanation,
    string? AuditoriumName, DateTime? StartTime, DateTime? EndTime, string? OtherRoomNote, List<GateTicketView> Tickets);

/// <summary>Kết quả cho vào: số vé vừa vào, trạng thái vào cửa của hóa đơn, sĩ số suất.</summary>
public sealed record GateAdmitResult(int Admitted, string BillCheckInStatus, int CheckedIn, int Sold, string Message);

/// <summary>
/// Soát vé (SPEC 3.8, UI P3): quét QR hóa đơn (hiện mọi vé) hoặc QR vé (hiện vé đó cùng các vé khác của hóa đơn); kiểm theo thứ tự của GateRules,
/// cảnh báo vàng cần nhân viên xác nhận; cho vào toàn bộ hoặc từng ghế, Bills.CheckInStatus tự tính (CheckInRules); phát CHECKIN_UPDATED tới bill_{id}
/// và OCCUPANCY_UPDATED tới auditorium_{id}.
/// </summary>
public class GateService
{
    private readonly IClock _clock;
    private readonly CinemaRules _rules;
    private readonly RealtimeNotifier _notifier;

    public GateService(IClock clock, CinemaRules rules, RealtimeNotifier notifier)
    {
        _clock = clock;
        _rules = rules;
        _notifier = notifier;
    }

    /// <summary>Tìm hóa đơn theo mã hóa đơn (HD…) hoặc mã QR vé (TK…); trả cả vé được quét nếu là QR vé.</summary>
    public static (Bill? Bill, Ticket? Ticket) Find(string? code)
    {
        var c = (code ?? string.Empty).Trim().ToUpperInvariant();
        if (c.Length == 0 || c.Length > 40) return (null, null);
        if (c.StartsWith("TK", StringComparison.Ordinal))
        {
            var t = TicketDAL.GetByQr(c);
            return t == null ? (null, null) : (BillDAL.GetById(t.BillId), t);
        }
        return (BillDAL.GetByCode(c), null);
    }

    private GateInput Input(Bill? bill, Ticket? scanned, int gateCinemaId, ShowtimeInfo? info, List<GateTicketRow> rows)
    {
        bool ready = info != null && ShowtimeOpsService.RoomReady(info.Auditorium.Id, info.Showtime.StartTime);
        var tickets = rows.Select(r => new GateTicket(r.TicketId, r.SeatLabel, r.AudienceType, StatusDisplay.Of(StatusDisplay.Audience, r.AudienceType).Label,
            r.CheckInStatus, r.RequiresDocument, r.CheckedInAt)).ToList();
        return new GateInput(bill != null, bill?.Status, bill?.CinemaId, gateCinemaId, scanned?.Id, tickets, info?.Showtime.OperationalStatus,
            info?.Showtime.StartTime, info?.Showtime.EndTime, info?.Movie.AgeRating, ready, _clock.Now);
    }

    /// <summary>Quét mã ở cửa của rạp gateCinemaId; auditoriumId là phòng đang soát (để nhắc khi vé thuộc phòng khác).</summary>
    public GateScanResult Scan(string? code, int gateCinemaId, int? auditoriumId)
    {
        var (bill, scanned) = Find(code);
        var info = bill?.ShowtimeId is int sid ? SeatMapService.Info(sid) : null;
        var rows = bill != null ? TicketDAL.GateRows(bill.Id) : new List<GateTicketRow>();
        if (bill != null && info == null && rows.Count == 0)
            return new GateScanResult(new GateVerdict(GateVerdict.Reject, GateReason.TicketInvalid, "Hóa đơn chỉ có bắp nước, không có vé xem phim.", new(), new()),
                bill.Id, bill.Code, null, null, null, null, null, null, null, null, new());
        var verdict = GateRules.Check(Input(bill, scanned, gateCinemaId, info, rows), _rules);
        string? otherRoom = info != null && auditoriumId != null && info.Auditorium.Id != auditoriumId && verdict.Kind != GateVerdict.Reject
            ? $"Vé này vào {info.Auditorium.Name}, không phải phòng đang soát." : null;
        var views = rows.Select(r => new GateTicketView(r.TicketId, r.SeatLabel, StatusDisplay.Of(StatusDisplay.Audience, r.AudienceType).Label, r.CheckInStatus,
            StatusDisplay.Of(StatusDisplay.Ticket, r.CheckInStatus).Label, verdict.AdmittableTicketIds.Contains(r.TicketId),
            verdict.AdmittableTicketIds.Contains(r.TicketId) && (scanned == null || scanned.Id == r.TicketId))).ToList();
        return new GateScanResult(verdict, bill?.Id, bill?.Code, info?.Showtime.Id, info?.Movie.Title, info?.Movie.AgeRating,
            info == null ? null : StatusDisplay.AgeExplanation(info.Movie.AgeRating), info?.Auditorium.Name, info?.Showtime.StartTime, info?.Showtime.EndTime, otherRoom, views);
    }

    /// <summary>
    /// Cho vào: ticketIds rỗng là mọi vé còn chờ của hóa đơn. Kiểm lại toàn bộ trong transaction (khóa hóa đơn); có cảnh báo vàng thì nhân viên phải
    /// xác nhận (warningsConfirmed). Vé chuyển CHECKED_IN, Bills.CheckInStatus tính lại; phát CHECKIN_UPDATED và OCCUPANCY_UPDATED sau khi commit.
    /// </summary>
    public async Task<GateAdmitResult> Admit(int billId, IReadOnlyCollection<int>? ticketIds, bool warningsConfirmed, int userId, int gateCinemaId)
    {
        var now = _clock.Now;
        var (bill, info, admitted, status) = DBUtil.InTransaction((conn, tx) =>
        {
            var b = BillDAL.GetForUpdate(billId, conn, tx) ?? throw CategoryService.NotFound("Không tìm thấy hóa đơn.");
            var si = b.ShowtimeId is int sid ? SeatMapService.Info(sid, conn, tx) : null;
            var rows = TicketDAL.GateRows(b.Id, conn, tx);
            var verdict = GateRules.Check(Input(b, null, gateCinemaId, si, rows), _rules);
            if (verdict.Kind == GateVerdict.Reject) throw new BusinessRuleException(ErrorCodes.InvalidTransition, verdict.Message, new { reason = verdict.Reason });
            if (verdict.Kind == GateVerdict.Warn && !warningsConfirmed)
                throw FieldErrors.Of("warningsConfirmed", "Bạn xác nhận đã kiểm tra các cảnh báo trước khi cho khách vào.");
            var chosen = ticketIds is { Count: > 0 } ? ticketIds.ToList() : verdict.AdmittableTicketIds;
            if (chosen.Any(id => !verdict.AdmittableTicketIds.Contains(id)))
                throw FieldErrors.Of("ticketIds", "Có vé đã vào cửa hoặc không thuộc hóa đơn này. Bạn quét lại mã.");
            int n = TicketDAL.CheckIn(b.Id, chosen, now, userId, conn, tx);
            if (n != chosen.Count) throw new BusinessRuleException(ErrorCodes.InvalidTransition, "Vé vừa được cho vào ở máy khác. Bạn quét lại mã.");
            var st = CheckInRules.BillStatusOf(TicketDAL.GetByBill(b.Id, conn, tx).Select(t => t.CheckInStatus));
            BillDAL.SetCheckInStatus(b.Id, st, conn, tx);
            return (b, si!, n, st);
        });
        var occ = ShowtimeDAL.Occupancy(info.Showtime.Id)!;
        await _notifier.CheckinUpdated(bill.Id, CheckinPayload(bill.Id, status));
        await _notifier.OccupancyUpdated(info.Auditorium.Id, info.Showtime.Id, occ.CheckedIn, occ.Sold);
        return new GateAdmitResult(admitted, status, occ.CheckedIn, occ.Sold,
            $"Đã cho {admitted} khách vào {info.Auditorium.Name}. Sĩ số {occ.CheckedIn}/{occ.Sold}.");
    }

    /// <summary>Nội dung CHECKIN_UPDATED: trạng thái vào cửa của hóa đơn và từng vé.</summary>
    public static object CheckinPayload(int billId, string checkInStatus) => new
    {
        billId,
        checkInStatus,
        tickets = TicketDAL.GateRows(billId).Select(t => new
        {
            ticketId = t.TicketId, seat = t.SeatLabel, checkInStatus = t.CheckInStatus,
            label = StatusDisplay.Of(StatusDisplay.Ticket, t.CheckInStatus).Label,
            checkedInAt = t.CheckedInAt?.ToString("yyyy-MM-ddTHH:mm:ss"),
        }),
    };
}
