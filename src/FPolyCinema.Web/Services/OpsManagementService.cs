using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Utils;

namespace FPolyCinema.Web.Services;

/// <summary>Dữ liệu nhập khi thuê trọn suất hoặc phát hành loạt voucher.</summary>
public sealed class CorporateInput
{
    public string Type { get; set; } = CorporateType.PrivateScreening;
    public string? CompanyName { get; set; }
    public string? ContactName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public int? ShowtimeId { get; set; }
    public int? VoucherCount { get; set; }
    public DateTime? ValidUntil { get; set; }
    public long ContractAmount { get; set; }
}

/// <summary>
/// Nghiệp vụ quản lý của Phase 6 (SupervisorOrManager và Manager):
/// - Giải phóng ghế nội bộ của từng suất (SPEC 3.2-07), ghi AuditLogs HOUSE_SEAT_RELEASED.
/// - Hủy suất do sự cố (SPEC 3.10 mục 2): trong một transaction hủy suất, hoàn tiền, tặng voucher bồi thường, ghi AuditLogs SHOWTIME_CANCELLED.
/// - Doanh nghiệp (SPEC 3.10 mục 3): thuê trọn suất (IsPrivate, hóa đơn CORPORATE cho toàn bộ ghế) và phát hành loạt voucher FREE_TICKET.
/// </summary>
public class OpsManagementService
{
    private readonly IClock _clock;
    private readonly CinemaRules _rules;
    private readonly RealtimeNotifier _notifier;
    private readonly BillStatusService _status;
    private readonly InventoryService _inventory;
    private readonly LoyaltyService _loyalty;

    public OpsManagementService(IClock clock, CinemaRules rules, RealtimeNotifier notifier, BillStatusService status, InventoryService inventory, LoyaltyService loyalty)
    {
        _clock = clock;
        _rules = rules;
        _notifier = notifier;
        _status = status;
        _inventory = inventory;
        _loyalty = loyalty;
    }

    private static ShowtimeInfo InfoFor(int showtimeId, int? cinemaId)
    {
        var info = SeatMapService.Info(showtimeId) ?? throw CategoryService.NotFound("Không tìm thấy suất chiếu.");
        if (cinemaId != null && info.Auditorium.CinemaId != cinemaId) throw FieldErrors.Of("showtimeId", "Suất này thuộc rạp khác.");
        return info;
    }

    // ------------------------------------------------------------------ ghế nội bộ

    /// <summary>Suất còn ghế nội bộ trong khoảng [now − 3 giờ, now + 24 giờ) và các suất cần nhắc (bắt đầu trong HouseSeatReminderMinutes).</summary>
    public (List<HouseSeatRow> All, List<HouseSeatRow> Reminders) HouseSeats(int? cinemaId)
    {
        var now = _clock.Now;
        var all = ShowtimeSeatDAL.HouseSeats(cinemaId, now.AddHours(-3), now.AddHours(24)).Where(r => HouseSeatRules.CannotReleaseReason(r.OperationalStatus, r.EndTime, now) == null).ToList();
        return (all, all.Where(r => HouseSeatRules.NeedsReminder(r.StartTime, r.HouseSeats, now, _rules.HouseSeatReminderMinutes)).ToList());
    }

    /// <summary>
    /// Giải phóng ghế nội bộ (HOUSE_HOLD → AVAILABLE) của một suất vào bất kỳ lúc nào trước khi suất kết thúc; seatIds rỗng là mọi ghế nội bộ.
    /// Ghi AuditLogs và phát SEAT_MAP_UPDATED. Trả số ghế đã giải phóng.
    /// </summary>
    public async Task<int> ReleaseHouseSeats(int showtimeId, IReadOnlyCollection<int>? seatIds, int userId, int? cinemaId)
    {
        var info = InfoFor(showtimeId, cinemaId);
        var now = _clock.Now;
        var reason = HouseSeatRules.CannotReleaseReason(info.Showtime.OperationalStatus, info.Showtime.EndTime, now);
        if (reason != null) throw new BusinessRuleException(ErrorCodes.InvalidTransition, reason);
        var released = DBUtil.InTransaction((conn, tx) =>
        {
            var states = ShowtimeSeatDAL.LockForShowtime(showtimeId, conn, tx);
            var ids = ShowtimeSeatDAL.ReleaseHouse(showtimeId, seatIds, conn, tx);
            if (ids.Count > 0)
                AuditLogDAL.Insert(new AuditLog
                {
                    UserId = userId, Action = AuditAction.HouseSeatReleased, EntityType = "Showtimes", EntityId = showtimeId.ToString(),
                    Detail = $"Giải phóng ghế nội bộ {string.Join(", ", states.Where(s => ids.Contains(s.SeatId)).Select(s => s.Label))} – {info.Movie.Title} {Fmt.DateTime(info.Showtime.StartTime)}, {info.Auditorium.Name}.",
                    CreatedAt = now,
                }, conn, tx);
            return ids;
        });
        if (released.Count == 0) throw new BusinessRuleException(ErrorCodes.InvalidTransition, "Suất này không còn ghế nội bộ nào để giải phóng.");
        await _notifier.SeatMapUpdated(showtimeId, released.Select(id => new SeatChange(id, SeatDisplayStatus.Available, null, null)).ToList());
        return released.Count;
    }

    // ------------------------------------------------------------------ hủy suất do sự cố

    /// <summary>
    /// Hủy suất do sự cố: chọn suất, nhập lý do, gõ lại mã suất (IncidentRules). Trong một transaction: OperationalStatus = CANCELLED; mọi hóa đơn finish
    /// chuyển cancel INCIDENT_COMPENSATED, hoàn toàn bộ số đã trả theo phương thức gốc, vé VOIDED, bắp nước chưa làm thì giải phóng kho, hóa đơn có tài khoản
    /// được tặng voucher bồi thường kèm Notifications; hóa đơn waiting chuyển EXPIRED; giải phóng ghế. Sau đó phát sự kiện và trả tổng kết.
    /// </summary>
    public async Task<IncidentSummary> CancelShowtime(int showtimeId, string? reason, string? confirmCode, int userId, string userRole, int? cinemaId)
    {
        var info = InfoFor(showtimeId, cinemaId);
        var errors = IncidentRules.Validate(showtimeId, info.Showtime.OperationalStatus, reason, confirmCode);
        if (errors.Count > 0) throw FieldErrors.Of(errors);
        var now = _clock.Now;
        int cid = info.Auditorium.CinemaId;
        var stockBefore = InventoryService.Snapshot(cid);
        var shift = PosShiftDAL.GetOpenByUser(userId);
        int? shiftId = shift?.CinemaId == cid ? shift.Id : null;
        var kdsRemoved = new List<(int BillId, int? Pickup)>();
        var fnbChanged = new List<(int BillId, int? Pickup)>();

        var (summary, seats) = DBUtil.InTransaction((conn, tx) =>
        {
            var st = ShowtimeDAL.GetForUpdate(showtimeId, conn, tx)!;
            var again = IncidentRules.Validate(showtimeId, st.OperationalStatus, reason, confirmCode);
            if (again.Count > 0) throw FieldErrors.Of(again);
            ShowtimeSeatDAL.LockForShowtime(showtimeId, conn, tx);
            int bills = 0, tickets = 0, vouchers = 0, expired = 0;
            long refunded = 0;

            foreach (var b0 in BillDAL.ByShowtime(showtimeId, BillStatus.Finish, conn, tx))
            {
                bills++;
                tickets += TicketDAL.GetByBill(b0.Id, conn, tx).Count(t => t.CheckInStatus is TicketCheckInStatus.NotCheckedIn or TicketCheckInStatus.CheckedIn);
                _status.ChangeStatus(b0.Id, BillStatus.Cancel, CancelReason.IncidentCompensated, userRole, now, conn, tx);
                TicketDAL.SetCheckInStatusForBill(b0.Id, TicketCheckInStatus.Voided, conn, tx);
                foreach (var p in PaymentDAL.RowsByBill(b0.Id, conn, tx).Where(p => p.Status == PaymentStatus.Succeeded).GroupBy(p => p.Method))
                {
                    long amount = p.Where(x => x.Direction == PaymentDirection.In).Sum(x => x.Amount) - p.Where(x => x.Direction == PaymentDirection.Refund).Sum(x => x.Amount);
                    if (amount <= 0) continue;
                    // BUG-077: tiền mặt hoàn trừ vào két của ca đang mở (như hủy hóa đơn, SPEC 3.6-23), để biên bản đóng ca tính đúng ExpectedCash
                    if (p.Key == PaymentMethod.Cash && shiftId == null)
                        throw new BusinessRuleException(ErrorCodes.ShiftNotOpen,
                            $"Suất có hóa đơn {b0.Code} trả bằng tiền mặt. Hoàn tiền mặt cần trừ vào két của ca đang mở: bạn mở ca tại một máy POS của rạp này rồi hủy suất lại.");
                    PaymentDAL.Insert(new Payment
                    {
                        BillId = b0.Id, PosShiftId = p.Key == PaymentMethod.Cash ? shiftId : null, Method = p.Key, Direction = PaymentDirection.Refund, Amount = amount,
                        Status = PaymentStatus.Succeeded, ProviderTxnId = p.Key == PaymentMethod.VietQrSim ? CodeGenerator.SimTxnId() + "R" : null, CreatedAt = now,
                    }, conn, tx);
                    if (p.Key == PaymentMethod.Wallet && b0.CustomerId is int wc) WalletService.Credit(wc, amount, $"Hoàn tiền do hủy suất, hóa đơn {b0.Code}", b0.Id, now, conn, tx);
                    refunded += amount;
                }
                var b = BillDAL.GetById(b0.Id, conn, tx)!;
                _inventory.ReleaseForVoid(b, userId, now, conn, tx);
                if (b.FnbStatus is FnbStatus.Preparing or FnbStatus.ReadyForPickup) kdsRemoved.Add((b.Id, b.PickupNumber));
                if (b.FnbStatus != FnbStatus.None) fnbChanged.Add((b.Id, b.PickupNumber));
                if (b.CheckInStatus != BillCheckInStatus.None) b.CheckInStatus = BillCheckInStatus.Voided;
                if (b.FnbStatus != FnbStatus.None) b.FnbStatus = FnbStatus.Voided;
                BillDAL.Update(b, conn, tx);
                if (b.VoucherId is int vid) VoucherDAL.ChangeUsed(vid, -1, conn, tx);
                _loyalty.OnCancel(b, now, conn, tx);
                if (b.CustomerId is int customer)
                {
                    string code;
                    do code = CodeGenerator.CompensationVoucherCode(); while (VoucherDAL.CodeExists(code, conn, tx));
                    VoucherDAL.Insert(new Voucher
                    {
                        Code = code, Type = VoucherType.Amount, Value = _rules.CompensationVoucherAmount, MinBillAmount = 0, ValidFrom = now,
                        ValidUntil = now.AddDays(_rules.CompensationVoucherValidDays), UsageLimit = 1, UsedCount = 0, OwnerUserId = customer, Source = VoucherSource.Compensation, Active = true,
                    }, conn, tx);
                    vouchers++;
                    NotificationDAL.Insert(new Notification
                    {
                        UserId = customer, Title = "Suất chiếu đã bị hủy",
                        Body = $"Suất {info.Movie.Title} lúc {Fmt.DateTime(info.Showtime.StartTime)} tại {info.Auditorium.Name} đã bị hủy do sự cố. Đơn {b.Code} đã được hoàn {Fmt.Money(b.Total)} theo phương thức gốc và bạn được tặng voucher {code} trị giá {Fmt.Money(_rules.CompensationVoucherAmount)}, dùng đến {Fmt.Date(now.AddDays(_rules.CompensationVoucherValidDays))}.",
                        Link = "/account?tab=voucher", CreatedAt = now,
                    }, conn, tx);
                }
            }

            foreach (var w in BillDAL.ByShowtime(showtimeId, BillStatus.Waiting, conn, tx))
            {
                BillService.ReleaseHeld(w, conn, tx);
                _status.ChangeStatus(w.Id, BillStatus.Cancel, CancelReason.Expired, Actors.System, now, conn, tx);
                expired++;
            }

            var freed = ShowtimeSeatDAL.ReleaseAll(showtimeId, conn, tx);
            ShowtimeDAL.Cancel(showtimeId, reason!.Trim(), conn, tx);
            var sum = new IncidentSummary(bills, tickets, refunded, vouchers, expired);
            AuditLogDAL.Insert(new AuditLog
            {
                UserId = userId, Action = AuditAction.ShowtimeCancelled, EntityType = "Showtimes", EntityId = showtimeId.ToString(),
                Detail = $"Hủy suất {IncidentRules.ConfirmCode(showtimeId)} – {info.Movie.Title} {Fmt.DateTime(info.Showtime.StartTime)}, {info.Auditorium.Name}. Lý do: {reason.Trim()}. " +
                         $"{bills} hóa đơn, {tickets} vé, đã hoàn {Fmt.Money(refunded)}, {vouchers} voucher bồi thường.",
                CreatedAt = now,
            }, conn, tx);
            return (sum, freed);
        });

        await _notifier.SeatMapUpdated(showtimeId, seats.Select(id => new SeatChange(id, SeatDisplayStatus.Available, null, null)).ToList());
        await _notifier.ShowtimeOpsChanged(cid, showtimeId, OperationalStatus.Cancelled);
        foreach (var (billId, pickup) in kdsRemoved)
            await _notifier.KdsOrderUpdated(cid, new { billId, pickupNumber = pickup is int n ? CodeGenerator.FormatPickupNumber(n) : null, fnbStatus = FnbStatus.Voided });
        foreach (var (billId, pickup) in fnbChanged)
            await _notifier.FnbStatusChanged(billId, FnbStatus.Voided, pickup, StatusDisplay.Of(StatusDisplay.Fnb, FnbStatus.Voided).Label);
        await InventoryService.PublishAlerts(_notifier, stockBefore);
        return summary;
    }

    // ------------------------------------------------------------------ doanh nghiệp

    private static Dictionary<string, string> ContactErrors(CorporateInput input)
    {
        var e = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(input.CompanyName)) e["CompanyName"] = "Bạn nhập tên doanh nghiệp.";
        if (string.IsNullOrWhiteSpace(input.ContactName)) e["ContactName"] = "Bạn nhập người liên hệ.";
        if (!string.IsNullOrWhiteSpace(input.Email) && !input.Email.Contains('@')) e["Email"] = "Email chưa đúng định dạng.";
        return e;
    }

    /// <summary>
    /// Thuê trọn suất (SPEC 3.10-08): chỉ với suất chưa bán vé nào. Suất được đánh dấu IsPrivate (ẩn khỏi web khách) và có một hóa đơn CORPORATE cho toàn bộ
    /// ghế (kể cả ghế nội bộ, trừ ghế bảo trì), giá theo hợp đồng chia đều từng ghế; ghi khoản chuyển khoản theo hợp đồng. Trả Id hợp đồng.
    /// </summary>
    public async Task<int> PrivateScreening(CorporateInput input, int userId, string userRole, int? cinemaId)
    {
        var errors = ContactErrors(input);
        if (input.ShowtimeId is not int sid) { errors["ShowtimeId"] = "Bạn chọn suất chiếu."; throw FieldErrors.Of(errors); }
        var info = InfoFor(sid, cinemaId);
        var now = _clock.Now;
        var (id, seatIds) = DBUtil.InTransaction((conn, tx) =>
        {
            var st = ShowtimeDAL.GetForUpdate(sid, conn, tx)!;
            var states = ShowtimeSeatDAL.LockForShowtime(sid, conn, tx);
            int held = states.Count(s => SeatMapService.Effective(s, now) == SeatDisplayStatus.Held);
            var e = ContactErrors(input);
            foreach (var (k, v) in CorporateRules.ValidatePrivateScreening(st.OperationalStatus, st.IsPrivate, TicketDAL.CountActiveForShowtime(sid, conn, tx), held, st.StartTime, now, input.ContractAmount))
                e[k] = v;
            if (e.Count > 0) throw FieldErrors.Of(e);
            // Ghế HELD đã hết hạn được dọn trước khi bán trọn suất
            foreach (var g in states.Where(s => s.Status == ShowtimeSeatStatus.Held && s.HoldBillId != null).GroupBy(s => s.HoldBillId!.Value))
                ShowtimeSeatDAL.Release(sid, g.Key, g.Select(s => s.SeatId).ToList(), conn, tx);
            var seats = ShowtimeSeatDAL.SellableForPrivate(sid, conn, tx);
            if (seats.Count == 0) throw FieldErrors.Of("ShowtimeId", "Suất không còn ghế nào để bán.");

            var bill = new Bill
            {
                Code = CodeGenerator.BillCode(now), Channel = BillChannel.Corporate, CinemaId = info.Auditorium.CinemaId, ShowtimeId = sid, UserId = userId, CreatedAt = now,
            };
            BillDAL.Insert(bill, conn, tx);
            var prices = CorporateRules.SplitContract(input.ContractAmount, seats.Count);
            for (int i = 0; i < seats.Count; i++)
            {
                string qr;
                do qr = CodeGenerator.TicketQrCode(); while (TicketDAL.QrExists(qr, conn, tx));
                int ticketId = TicketDAL.Insert(new Ticket
                {
                    BillId = bill.Id, ShowtimeId = sid, SeatId = seats[i].SeatId, AudienceType = AudienceType.Adult, ListPrice = prices[i], FinalPrice = prices[i],
                    VatRate = _rules.TicketVatPercent, QrCode = qr, CheckInStatus = TicketCheckInStatus.NotCheckedIn, CreatedAt = now,
                }, conn, tx);
                if (ShowtimeSeatDAL.BookDirect(sid, seats[i].SeatId, ticketId, conn, tx) != 1)
                    throw new BusinessRuleException(ErrorCodes.SeatTaken, $"Ghế {seats[i].Label} vừa có người chọn. Bạn thử lại.");
            }
            bill.TicketSubtotal = input.ContractAmount;
            bill.Total = input.ContractAmount;
            BillDAL.Update(bill, conn, tx);
            PaymentDAL.Insert(new Payment
            {
                BillId = bill.Id, Method = PaymentMethod.VietQrSim, Direction = PaymentDirection.In, Amount = input.ContractAmount, Status = PaymentStatus.Succeeded,
                ProviderTxnId = CodeGenerator.SimTxnId(), RawPayload = "Chuyển khoản theo hợp đồng doanh nghiệp", CreatedAt = now,
            }, conn, tx);
            int cbId = CorporateBookingDAL.Insert(new CorporateBooking
            {
                CompanyName = input.CompanyName!.Trim(), ContactName = input.ContactName!.Trim(), Phone = input.Phone?.Trim(), Email = input.Email?.Trim(),
                Type = CorporateType.PrivateScreening, ShowtimeId = sid, ContractAmount = input.ContractAmount, BillId = bill.Id, CreatedBy = userId, CreatedAt = now,
            }, conn, tx);
            var b = BillDAL.GetById(bill.Id, conn, tx)!;
            b.CorporateBookingId = cbId;
            BillDAL.Update(b, conn, tx);
            _status.ChangeStatus(bill.Id, BillStatus.Finish, null, userRole, now, conn, tx);
            b = BillDAL.GetById(bill.Id, conn, tx)!;
            b.CheckInStatus = BillCheckInStatus.NotCheckedIn;
            BillDAL.Update(b, conn, tx);
            ShowtimeDAL.SetPrivate(sid, true, conn, tx);
            return (cbId, seats.Select(s => s.SeatId).ToList());
        });
        await _notifier.SeatMapUpdated(sid, seatIds.Select(x => new SeatChange(x, SeatDisplayStatus.Booked, null, null)).ToList());
        return id;
    }

    /// <summary>
    /// Phát hành loạt voucher FREE_TICKET (SPEC 3.10-09): nhập số lượng và hạn dùng, gắn CorporateBookingId. Mỗi mã dùng một lần, trả cho một vé ghế STANDARD
    /// hoặc VIP, không áp cho SWEETBOX (VoucherRules). Trả Id hợp đồng.
    /// </summary>
    public int VoucherBatch(CorporateInput input, int userId)
    {
        var now = _clock.Now;
        var e = ContactErrors(input);
        foreach (var (k, v) in CorporateRules.ValidateVoucherBatch(input.VoucherCount, input.ValidUntil, now, input.ContractAmount)) e[k] = v;
        if (e.Count > 0) throw FieldErrors.Of(e);
        return DBUtil.InTransaction((conn, tx) =>
        {
            int cbId = CorporateBookingDAL.Insert(new CorporateBooking
            {
                CompanyName = input.CompanyName!.Trim(), ContactName = input.ContactName!.Trim(), Phone = input.Phone?.Trim(), Email = input.Email?.Trim(),
                Type = CorporateType.VoucherBatch, VoucherCount = input.VoucherCount, ContractAmount = input.ContractAmount, CreatedBy = userId, CreatedAt = now,
            }, conn, tx);
            for (int i = 0; i < input.VoucherCount!.Value; i++)
            {
                string code;
                do code = CodeGenerator.CorporateVoucherCode(); while (VoucherDAL.CodeExists(code, conn, tx));
                VoucherDAL.Insert(new Voucher
                {
                    Code = code, Type = VoucherType.FreeTicket, Value = 0, MinBillAmount = 0, ValidFrom = now, ValidUntil = input.ValidUntil!.Value,
                    UsageLimit = 1, UsedCount = 0, Source = VoucherSource.Corporate, CorporateBookingId = cbId, Active = true,
                }, conn, tx);
            }
            return cbId;
        });
    }
}
