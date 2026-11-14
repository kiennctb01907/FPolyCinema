using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.Services;

/// <summary>Hóa đơn được quét ở màn hình đổi ghế: suất, vé và vé nào còn đổi được.</summary>
public sealed record SwapBill(Bill Bill, ShowtimeInfo Info, List<GateTicketRow> Tickets, Dictionary<int, string?> Blocked);

/// <summary>Báo giá đổi ghế: từng cặp vé cũ → ghế mới, chênh lệch và số phải thu thêm.</summary>
public sealed record SwapQuoteResult(List<SwapLine> Lines, SwapQuote Quote, string Message);

/// <summary>Một cặp đổi ghế: vé cũ, ghế cũ, ghế mới, giá cũ, giá mới.</summary>
public sealed record SwapLine(int TicketId, string OldSeat, int NewSeatId, string NewSeat, string NewSeatType, long OldPrice, long NewPrice);

/// <summary>Kết quả đổi ghế: số đã thu thêm, đường dẫn in vé mới.</summary>
public sealed record SwapResult(string BillCode, long Charged, List<string> NewSeats, string PrintUrl, string Message);

/// <summary>
/// Đổi hoặc nâng hạng ghế tại quầy (SPEC 3.10 mục 1, route /pos/swap-seat): quét vé, chọn ghế mới trong cùng suất qua SeatHoldService. Chênh lệch = giá mới −
/// giá cũ (cùng bảng giá hiện hành và cùng đối tượng vé); dương thì thu thêm (Payments IN), không dương thì đổi ngang, không hoàn tiền. Vé cũ chuyển SWAPPED
/// (QR cũ hết hiệu lực), vé mới có QR mới trong cùng hóa đơn. Chỉ đổi được vé NOT_CHECKED_IN, trước FeatureStartTime. Ghi AuditLogs SEAT_SWAPPED.
/// </summary>
public class SeatSwapService
{
    private readonly IClock _clock;
    private readonly CinemaRules _rules;
    private readonly SeatHoldService _holds;
    private readonly RealtimeNotifier _notifier;

    public SeatSwapService(IClock clock, CinemaRules rules, SeatHoldService holds, RealtimeNotifier notifier)
    {
        _clock = clock;
        _rules = rules;
        _holds = holds;
        _notifier = notifier;
    }

    /// <summary>Tìm hóa đơn theo mã hóa đơn hoặc QR vé ở rạp của quầy; kèm lý do từng vé không đổi được.</summary>
    public SwapBill Find(string? code, int cinemaId)
    {
        var (bill, _) = GateService.Find(code);
        if (bill == null || bill.CinemaId != cinemaId) throw FieldErrors.Of("code", "Không tìm thấy hóa đơn của rạp này với mã vừa quét.");
        if (bill.ShowtimeId is not int sid) throw FieldErrors.Of("code", "Hóa đơn chỉ có bắp nước, không có vé để đổi.");
        var info = SeatMapService.Info(sid)!;
        var now = _clock.Now;
        var tickets = TicketDAL.GateRows(bill.Id).Where(t => t.CheckInStatus is TicketCheckInStatus.NotCheckedIn or TicketCheckInStatus.CheckedIn).ToList();
        if (tickets.Count == 0) throw FieldErrors.Of("code", "Hóa đơn không còn vé nào hiệu lực.");
        var blocked = tickets.ToDictionary(t => t.TicketId, t => SeatSwapRules.CannotSwapReason(bill.Status, t.CheckInStatus, info.Showtime.FeatureStartTime, now));
        return new SwapBill(bill, info, tickets, blocked);
    }

    private (List<GateTicketRow> Old, List<SeatStateRow> NewSeats, List<SwapLine> Lines) Prepare(Bill bill, ShowtimeInfo info, IReadOnlyList<int> ticketIds,
        IReadOnlyList<int> newSeatIds, SqlConnection? conn, SqlTransaction? tx)
    {
        var now = _clock.Now;
        if (ticketIds.Count == 0) throw FieldErrors.Of("ticketIds", "Bạn chọn vé cần đổi.");
        if (newSeatIds.Count != ticketIds.Count) throw FieldErrors.Of("seatIds", $"Bạn chọn đúng {ticketIds.Count} ghế mới cho {ticketIds.Count} vé cần đổi.");
        if (newSeatIds.Distinct().Count() != newSeatIds.Count || ticketIds.Distinct().Count() != ticketIds.Count) throw FieldErrors.Of("seatIds", "Có ghế hoặc vé bị chọn trùng.");
        var all = TicketDAL.GateRows(bill.Id, conn, tx);
        var old = ticketIds.Select(id => all.FirstOrDefault(t => t.TicketId == id) ?? throw FieldErrors.Of("ticketIds", "Vé không thuộc hóa đơn này.")).ToList();
        foreach (var t in old)
        {
            var reason = SeatSwapRules.CannotSwapReason(bill.Status, t.CheckInStatus, info.Showtime.FeatureStartTime, now);
            if (reason != null) throw new BusinessRuleException(ErrorCodes.InvalidTransition, $"Ghế {t.SeatLabel}: {reason}");
        }
        // Ghế đôi cũ phải đổi cả cặp
        foreach (var t in old.Where(t => t.SeatType == SeatType.Sweetbox && t.PairGroupId != null))
        {
            var partner = all.FirstOrDefault(x => x.PairGroupId == t.PairGroupId && x.TicketId != t.TicketId && x.CheckInStatus == TicketCheckInStatus.NotCheckedIn);
            if (partner != null && !ticketIds.Contains(partner.TicketId))
                throw new BusinessRuleException(ErrorCodes.SweetboxPair, $"Ghế đôi {t.SeatLabel}–{partner.SeatLabel} được đổi theo cặp. Bạn chọn cả hai vé nhé.");
        }
        var states = ShowtimeSeatDAL.GetStates(info.Showtime.Id, conn, tx).ToDictionary(s => s.SeatId);
        var pricer = new TicketPricer(info, _rules, conn, tx);
        var newSeats = newSeatIds.Select(id => states.TryGetValue(id, out var s) ? s : throw FieldErrors.Of("seatIds", "Có ghế không thuộc suất này.")).ToList();
        // Ghép theo thứ tự nhãn để ghế đôi cũ sang ghế đôi mới theo đúng cặp
        var oldSorted = old.OrderBy(t => t.SeatType).ThenBy(t => t.SeatLabel.Length).ThenBy(t => t.SeatLabel, StringComparer.Ordinal).ToList();
        var newSorted = newSeats.OrderBy(s => s.SeatType).ThenBy(s => s.Label.Length).ThenBy(s => s.Label, StringComparer.Ordinal).ToList();
        var lines = oldSorted.Select((t, i) => new SwapLine(t.TicketId, t.SeatLabel, newSorted[i].SeatId, newSorted[i].Label, newSorted[i].SeatType,
            pricer.Price(t.SeatType, t.AudienceType), pricer.Price(newSorted[i].SeatType, t.AudienceType))).ToList();
        return (oldSorted, newSorted, lines);
    }

    /// <summary>Báo giá đổi ghế (không đổi dữ liệu).</summary>
    public SwapQuoteResult Quote(int billId, int cinemaId, IReadOnlyList<int> ticketIds, IReadOnlyList<int> newSeatIds)
    {
        var bill = BillDAL.GetById(billId);
        if (bill == null || bill.CinemaId != cinemaId || bill.ShowtimeId is not int sid) throw FieldErrors.Of("code", "Không tìm thấy hóa đơn của rạp này.");
        var info = SeatMapService.Info(sid)!;
        var (_, _, lines) = Prepare(bill, info, ticketIds, newSeatIds, null, null);
        var q = SeatSwapRules.Quote(lines.Select(l => l.OldPrice), lines.Select(l => l.NewPrice));
        return new SwapQuoteResult(lines, q, q.IsUpgrade ? $"Nâng hạng: thu thêm {Fmt.Money(q.Charge)}." : "Đổi ngang, không thu thêm và không hoàn tiền.");
    }

    /// <summary>
    /// Đổi ghế trong một transaction: giữ ghế mới qua SeatHoldService.HoldForSwap; vé cũ SWAPPED, ghế cũ AVAILABLE; vé mới (QR mới, SwappedFromTicketId)
    /// gắn ghế mới (BOOKED); thu thêm phần chênh dương bằng CASH hoặc VIETQR_SIM vào ca hiện tại; ghi AuditLogs. Sau đó phát SEAT_MAP_UPDATED và CHECKIN_UPDATED.
    /// </summary>
    public async Task<SwapResult> Swap(PosContext ctx, int billId, IReadOnlyList<int> ticketIds, IReadOnlyList<int> newSeatIds, string? method, long? cashReceived,
        bool wheelchairConfirmed, bool overrideOrphan, string? overrideReason)
    {
        var now = _clock.Now;
        var (result, showtimeId, changes) = DBUtil.InTransaction((conn, tx) =>
        {
            var bill = BillDAL.GetForUpdate(billId, conn, tx);
            if (bill == null || bill.CinemaId != ctx.CinemaId || bill.ShowtimeId is not int sid) throw FieldErrors.Of("code", "Không tìm thấy hóa đơn của rạp này.");
            var info = SeatMapService.Info(sid, conn, tx)!;
            var (old, _, lines) = Prepare(bill, info, ticketIds, newSeatIds, conn, tx);
            var q = SeatSwapRules.Quote(lines.Select(l => l.OldPrice), lines.Select(l => l.NewPrice));
            if (q.IsUpgrade)
            {
                if (method is not (PaymentMethod.Cash or PaymentMethod.VietQrSim)) throw FieldErrors.Of("method", $"Nâng hạng cần thu thêm {Fmt.Money(q.Charge)}. Bạn chọn tiền mặt hoặc chuyển khoản.");
                if (method == PaymentMethod.Cash && (cashReceived ?? 0) < q.Charge) throw FieldErrors.Of("cashReceived", $"Tiền khách đưa chưa đủ. Cần ít nhất {Fmt.Money(q.Charge)}.");
            }
            var freed = old.Select(t => t.SeatId).ToList();
            _holds.HoldForSwap(conn, tx, bill, sid, lines.Select(l => l.NewSeatId).ToList(), freed,
                new HoldOptions(BillChannel.Pos, ctx.Role, ctx.UserId, overrideOrphan, overrideReason, wheelchairConfirmed), now);

            // Phần thu thêm chia cho từng vé mới theo phần tăng giá (tổng đúng bằng số thu thêm của hóa đơn)
            var extra = SeatSwapRules.AllocateCharge(q.Charge, lines.Select(x => x.NewPrice - x.OldPrice).ToList());
            var seatChanges = new List<SeatChange>();
            for (int li = 0; li < lines.Count; li++)
            {
                var l = lines[li];
                var t = old.First(x => x.TicketId == l.TicketId);
                if (TicketDAL.SetCheckInStatus(t.TicketId, TicketCheckInStatus.NotCheckedIn, TicketCheckInStatus.Swapped, conn, tx) != 1)
                    throw new BusinessRuleException(ErrorCodes.InvalidTransition, $"Vé ghế {t.SeatLabel} vừa vào cửa hoặc đã đổi ở máy khác.");
                ShowtimeSeatDAL.Unbook(sid, t.SeatId, t.TicketId, conn, tx);
                string qr;
                do qr = CodeGenerator.TicketQrCode(); while (TicketDAL.QrExists(qr, conn, tx));
                int newId = TicketDAL.Insert(new Ticket
                {
                    BillId = bill.Id, ShowtimeId = sid, SeatId = l.NewSeatId, AudienceType = t.AudienceType, ListPrice = l.NewPrice,
                    FinalPrice = t.FinalPrice + extra[li], VatRate = _rules.TicketVatPercent, QrCode = qr,
                    CheckInStatus = TicketCheckInStatus.NotCheckedIn, SwappedFromTicketId = t.TicketId, CreatedAt = now,
                }, conn, tx);
                if (ShowtimeSeatDAL.Book(sid, l.NewSeatId, bill.Id, newId, conn, tx) != 1)
                    throw new BusinessRuleException(ErrorCodes.SeatTaken, $"Ghế {l.NewSeat} vừa có người chọn. Bạn chọn ghế khác.");
                seatChanges.Add(new SeatChange(t.SeatId, SeatDisplayStatus.Available, null, null));
                seatChanges.Add(new SeatChange(l.NewSeatId, SeatDisplayStatus.Booked, null, null));
            }
            if (q.IsUpgrade)
            {
                PaymentDAL.Insert(new Payment
                {
                    BillId = bill.Id, PosShiftId = ctx.Shift.Id, Method = method!, Direction = PaymentDirection.In, Amount = q.Charge, Status = PaymentStatus.Succeeded,
                    ProviderTxnId = method == PaymentMethod.VietQrSim ? CodeGenerator.SimTxnId() : null, RawPayload = "Thu thêm khi nâng hạng ghế", CreatedAt = now,
                }, conn, tx);
                bill.TicketSubtotal += q.Charge;
                bill.Total += q.Charge;
                BillDAL.Update(bill, conn, tx);
            }
            AuditLogDAL.Insert(new AuditLog
            {
                UserId = ctx.UserId, Action = AuditAction.SeatSwapped, EntityType = "Bill", EntityId = bill.Id.ToString(),
                Detail = $"Đổi ghế hóa đơn {bill.Code}: " + string.Join(", ", lines.Select(l => $"{l.OldSeat} → {l.NewSeat}")) +
                         (q.IsUpgrade ? $"; thu thêm {Fmt.Money(q.Charge)} ({StatusDisplay.Of(StatusDisplay.Payment, method).Label})" : "; đổi ngang"),
                CreatedAt = now,
            }, conn, tx);
            var res = new SwapResult(bill.Code, q.Charge, lines.Select(l => l.NewSeat).ToList(), $"/pos/print?billId={bill.Id}",
                $"Đã đổi {string.Join(", ", lines.Select(l => $"{l.OldSeat} → {l.NewSeat}"))}." + (q.IsUpgrade ? $" Đã thu thêm {Fmt.Money(q.Charge)}." : " Đổi ngang, không thu thêm."));
            return (res, sid, seatChanges);
        });
        await _notifier.SeatMapUpdated(showtimeId, changes);
        var b = BillDAL.GetById(billId)!;
        await _notifier.CheckinUpdated(billId, GateService.CheckinPayload(billId, b.CheckInStatus));
        return result;
    }
}
