using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.Services;

/// <summary>Kết quả tính lại hóa đơn: từng vé (giá niêm yết, phần giảm), từng dòng bắp nước, phần giảm voucher và điểm, lời nhắc.</summary>
public sealed record BillTotals(List<(int SeatId, string Label, string SeatType, string Audience, long ListPrice, long Discount)> Tickets,
    List<(BillDetail Line, long Discount)> Lines, long VoucherDiscount, long PointsDiscount, List<string> Notices);

/// <summary>
/// Hóa đơn và bán tại quầy (SPEC 3.6, slide bài 6 mở rộng). Mọi giao dịch là một bản ghi Bills; vé ở Tickets (mỗi ghế một vé, mỗi vé một QR),
/// bắp nước ở BillDetails; mỗi hóa đơn có tối đa một suất. Chỉ hóa đơn waiting được thêm, sửa, xóa dòng; mỗi lần thay đổi gọi Recalculate trong
/// cùng transaction. Trạng thái chỉ đổi qua BillStatusService (BillStateMachine).
/// </summary>
public class BillService
{
    private readonly IClock _clock;
    private readonly CinemaRules _rules;
    private readonly BillStatusService _status;
    private readonly RealtimeNotifier _notifier;
    private readonly InventoryService _inventory;
    private readonly LoyaltyService _loyalty;

    public BillService(IClock clock, CinemaRules rules, BillStatusService status, RealtimeNotifier notifier, InventoryService inventory, LoyaltyService loyalty)
    {
        _clock = clock;
        _rules = rules;
        _status = status;
        _notifier = notifier;
        _inventory = inventory;
        _loyalty = loyalty;
    }

    // ------------------------------------------------------------------ tính lại

    /// <summary>
    /// BillService.Recalculate: tính lại TicketSubtotal, FnbSubtotal, DiscountTotal, Total của hóa đơn waiting trong transaction đang mở.
    /// Giá vé theo PricingCalculator cho từng ghế đang giữ và đối tượng đã chọn; voucher kiểm lại (không còn hợp lệ thì gỡ, kèm lời nhắc);
    /// điểm đổi không quá giới hạn; giảm giá phân bổ theo tỷ lệ giá trị cho từng vé và dòng bắp nước, phần dư dồn vào dòng cuối (SPEC 3.4).
    /// </summary>
    public BillTotals Recalculate(int billId, SqlConnection conn, SqlTransaction tx)
    {
        var bill = BillDAL.GetById(billId, conn, tx) ?? throw CategoryService.NotFound("Không tìm thấy hóa đơn.");
        if (bill.Status != BillStatus.Waiting)
            throw new BusinessRuleException(ErrorCodes.InvalidTransition, "Chỉ hóa đơn chưa thanh toán mới sửa được.");
        var now = _clock.Now;
        var notices = new List<string>();

        var tickets = new List<(int SeatId, string Label, string SeatType, string Audience, long ListPrice)>();
        if (bill.ShowtimeId is int sid)
        {
            var info = SeatMapService.Info(sid, conn, tx)!;
            var pricer = new TicketPricer(info, _rules, conn, tx);
            foreach (var s in ShowtimeSeatDAL.GetStates(sid, conn, tx).Where(s => s.Status == ShowtimeSeatStatus.Held && s.HoldBillId == bill.Id))
            {
                var aud = s.HoldAudienceType ?? AudienceType.Adult;
                tickets.Add((s.SeatId, s.Label, s.SeatType, aud, pricer.Price(s.SeatType, aud)));
            }
        }
        var lines = BillDetailDAL.GetByBill(bill.Id, conn, tx);
        long ticketSub = tickets.Sum(t => t.ListPrice), fnbSub = lines.Sum(l => l.Price * l.Quantity), sub = ticketSub + fnbSub;

        long voucherDiscount = 0;
        if (bill.VoucherId is int vid)
        {
            var v = VoucherDAL.GetById(vid, conn, tx);
            var check = v == null ? VoucherCheck.Fail("Voucher không còn tồn tại.")
                : VoucherRules.Check(v, now, sub, bill.CustomerId, tickets.Select(t => (t.ListPrice, t.SeatType)));
            if (check.Ok) voucherDiscount = check.Discount;
            else
            {
                bill.VoucherId = null;
                notices.Add($"Đã gỡ voucher: {check.Reason}");
            }
        }

        long pointsBase = sub - voucherDiscount;
        if (bill.PointsUsed > 0)
        {
            var customer = bill.CustomerId is int cid ? UserDAL.GetById(cid, conn, tx) : null;
            int max = customer == null ? 0 : LoyaltyCalculator.MaxRedeemPoints(pointsBase, customer.LoyaltyPoints, _rules);
            if (bill.PointsUsed > max)
            {
                bill.PointsUsed = max;
                notices.Add(max == 0 ? "Đã bỏ đổi điểm vì hóa đơn không còn đổi được điểm." : $"Số điểm đổi được giảm còn {Fmt.Number(max)} theo giới hạn {_rules.MaxPointsRedeemPercent}% giá trị hóa đơn.");
            }
        }
        long pointsDiscount = LoyaltyCalculator.PointsValue(bill.PointsUsed, _rules);

        var alloc = DiscountAllocator.Allocate(voucherDiscount + pointsDiscount, tickets.Select(t => t.ListPrice).Concat(lines.Select(l => l.Price * l.Quantity)).ToList());
        var lineResult = new List<(BillDetail, long)>();
        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            long d = alloc[tickets.Count + i];
            long final = l.Price * l.Quantity - d;
            if (l.LineFinalAmount != final)
            {
                l.LineFinalAmount = final;
                BillDetailDAL.Update(l, conn, tx);
            }
            lineResult.Add((l, d));
        }

        bill.TicketSubtotal = ticketSub;
        bill.FnbSubtotal = fnbSub;
        bill.DiscountTotal = alloc.Sum();
        bill.Total = sub - bill.DiscountTotal;
        BillDAL.Update(bill, conn, tx);
        return new BillTotals(tickets.Select((t, i) => (t.SeatId, t.Label, t.SeatType, t.Audience, t.ListPrice, alloc[i])).ToList(), lineResult,
            voucherDiscount, pointsDiscount, notices);
    }

    // ------------------------------------------------------------------ hóa đơn tạm tính của máy POS

    /// <summary>
    /// Hóa đơn waiting của thu ngân trên máy này (SPEC 3.5-02); hóa đơn đã hết hạn giữ ghế thì hủy EXPIRED ngay (không chờ bộ quét). Trả null nếu chưa có.
    /// </summary>
    public async Task<Bill?> Current(PosContext ctx)
    {
        var bill = BillDAL.GetWaitingForShift(ctx.Shift.Id);
        if (bill == null) return null;
        if (!SeatHoldRules.IsExpired(bill.HoldExpiresAt, _clock.Now)) return bill;
        var changes = DBUtil.InTransaction((conn, tx) => ExpireBill(bill.Id, _clock.Now, conn, tx));
        if (changes != null) await _notifier.SeatMapUpdated(changes);
        return BillDAL.GetWaitingForShift(ctx.Shift.Id);
    }

    /// <summary>Lấy hoặc tạo hóa đơn waiting cho máy POS (lần giữ ghế hoặc thêm món đầu tiên tạo hóa đơn).</summary>
    public async Task<Bill> Ensure(PosContext ctx)
    {
        var bill = await Current(ctx);
        if (bill != null) return bill;
        var now = _clock.Now;
        bill = new Bill
        {
            Code = CodeGenerator.BillCode(now),
            Channel = BillChannel.Pos,
            CinemaId = ctx.CinemaId,
            UserId = ctx.UserId,
            PosShiftId = ctx.Shift.Id,
            TerminalCode = ctx.Shift.TerminalCode,
            CreatedAt = now,
        };
        BillDAL.Insert(bill);
        return bill;
    }

    /// <summary>Hủy EXPIRED một hóa đơn waiting đã hết hạn và trả ghế (bộ quét và POS dùng chung). Trả các ghế đã đổi theo suất, null nếu không còn cần hủy.</summary>
    public Dictionary<int, List<SeatChange>>? ExpireBill(int billId, DateTime now, SqlConnection conn, SqlTransaction tx)
    {
        var bill = BillDAL.GetForUpdate(billId, conn, tx);
        if (bill == null || bill.Status != BillStatus.Waiting || !SeatHoldRules.IsExpired(bill.HoldExpiresAt, now)) return null;
        var changes = ReleaseHeld(bill, conn, tx);
        _status.ChangeStatus(bill.Id, BillStatus.Cancel, CancelReason.Expired, Actors.System, now, conn, tx);
        return changes;
    }

    /// <summary>Hủy các hóa đơn waiting đã hết hạn của ca (trước khi đóng ca).</summary>
    public void ExpireStaleOfShift(int shiftId, DateTime now)
    {
        foreach (var b in BillDAL.WaitingForShift(shiftId).Where(b => SeatHoldRules.IsExpired(b.HoldExpiresAt, now)))
            DBUtil.InTransaction((conn, tx) => ExpireBill(b.Id, now, conn, tx));
    }

    /// <summary>Trả mọi ghế HELD của hóa đơn về AVAILABLE; trả các ghế đã đổi theo suất.</summary>
    public static Dictionary<int, List<SeatChange>> ReleaseHeld(Bill bill, SqlConnection conn, SqlTransaction tx)
    {
        var result = new Dictionary<int, List<SeatChange>>();
        foreach (var g in ShowtimeSeatDAL.HeldByBill(bill.Id, conn, tx).GroupBy(s => s.ShowtimeId))
        {
            var ids = g.Select(s => s.SeatId).ToList();
            ShowtimeSeatDAL.Release(g.Key, bill.Id, ids, conn, tx);
            result[g.Key] = ids.Select(id => new SeatChange(id, SeatDisplayStatus.Available, null, null)).ToList();
        }
        return result;
    }

    /// <summary>Chạy một thay đổi trên hóa đơn waiting của máy POS (tạo nếu cần) trong transaction rồi tính lại; trả giỏ và lời nhắc.</summary>
    private async Task<Cart> Mutate(PosContext ctx, Action<Bill, SqlConnection, SqlTransaction> change)
    {
        var bill = await Ensure(ctx);
        return MutateBill(bill.Id, change);
    }

    /// <summary>
    /// Chạy một thay đổi trên một hóa đơn waiting (quầy hoặc online) trong transaction có khóa hóa đơn, rồi tính lại; trả giỏ và lời nhắc.
    /// </summary>
    public Cart MutateBill(int billId, Action<Bill, SqlConnection, SqlTransaction> change)
    {
        var bill = BillDAL.GetById(billId) ?? throw CategoryService.NotFound("Không tìm thấy hóa đơn.");
        var notices = DBUtil.InTransaction((conn, tx) =>
        {
            var b = BillDAL.GetForUpdate(bill.Id, conn, tx)!;
            if (b.Status != BillStatus.Waiting) throw new BusinessRuleException(ErrorCodes.InvalidTransition, "Hóa đơn đã thanh toán hoặc đã hủy. Bạn tải lại màn hình.");
            change(b, conn, tx);
            return Recalculate(b.Id, conn, tx).Notices;
        });
        return BuildCart(BillDAL.GetById(bill.Id)!, notices);
    }

    // ------------------------------------------------------------------ đối tượng vé (SPEC 3.1)

    /// <summary>
    /// Chọn đối tượng cho một vé (seatId) hoặc mọi vé (seatId null). Áp quy tắc 3.1 ngay: vé trẻ em không hợp nhãn phim (AGE_RESTRICTED),
    /// suất kết thúc quá giờ cho trẻ em (CHILD_CURFEW). Các loại vé giảm theo luật bán được tại quầy vì nhân viên kiểm giấy tờ.
    /// </summary>
    public Task<Cart> SetAudience(PosContext ctx, int? seatId, string? audienceType) => Mutate(ctx, (b, conn, tx) =>
    {
        var discount = audienceType == null ? null : AudienceDiscountDAL.GetById(audienceType, conn, tx);
        if (discount == null) throw FieldErrors.Of("audienceType", "Bạn chọn đối tượng vé.");
        if (discount.PosOnly && b.Channel != BillChannel.Pos)
            throw FieldErrors.Of("audienceType", "Loại vé này chỉ bán tại quầy để nhân viên kiểm tra giấy tờ.");
        if (b.ShowtimeId is not int sid) throw FieldErrors.Of("seatId", "Hóa đơn chưa có vé nào.");
        var info = SeatMapService.Info(sid, conn, tx)!;
        var check = AgeRules.CheckTicket(audienceType!, info.Movie.AgeRating, info.Showtime.StartTime, info.Showtime.EndTime, _rules);
        if (!check.Ok) throw new BusinessRuleException(check.ErrorCode!, check.Message!, new { seatId });
        if (ShowtimeSeatDAL.SetAudience(b.Id, seatId, audienceType!, conn, tx) == 0)
            throw FieldErrors.Of("seatId", "Ghế này không còn trong hóa đơn. Bạn tải lại màn hình.");
    });

    /// <summary>Thu ngân tích "Có người giám hộ đi cùng" (phim K, SPEC 3.1).</summary>
    public Task<Cart> SetGuardian(PosContext ctx, bool confirmed) => Mutate(ctx, (b, conn, tx) =>
    {
        b.GuardianConfirmed = confirmed;
        BillDAL.Update(b, conn, tx);
    });

    // ------------------------------------------------------------------ bắp nước (/pos/update-quantity)

    /// <summary>Tăng, giảm, xóa một món (action incr, decr, delete). Món không đủ nguyên liệu bị chặn (STOCK_INSUFFICIENT).</summary>
    public Task<Cart> UpdateQuantity(PosContext ctx, int drinkId, string? action) => Mutate(ctx, QuantityChange(drinkId, action));

    /// <summary>Thay đổi bắp nước dùng chung cho quầy và online (action incr, decr, delete; STOCK_INSUFFICIENT khi thiếu nguyên liệu).</summary>
    public Action<Bill, SqlConnection, SqlTransaction> QuantityChange(int drinkId, string? action) => (b, conn, tx) =>
    {
        if (action is not ("incr" or "decr" or "delete")) throw FieldErrors.Of("action", "Thao tác không hợp lệ.");
        var lines = BillDetailDAL.GetByBill(b.Id, conn, tx);
        var line = lines.FirstOrDefault(l => l.DrinkId == drinkId);
        if (action == "incr")
        {
            var drink = DrinkDAL.GetById(drinkId, conn, tx);
            if (drink == null || !drink.Active) throw FieldErrors.Of("drinkId", "Món này không còn bán.");
            if (drink.IsSoldOut) throw new BusinessRuleException(ErrorCodes.StockInsufficient, $"{drink.Name} tạm hết nguyên liệu.", new { drinkId });
            var qty = lines.GroupBy(l => l.DrinkId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
            qty[drinkId] = (qty.TryGetValue(drinkId, out var q) ? q : 0) + 1;
            _inventory.EnsureAvailable(b.CinemaId, qty, conn, tx);
            if (line == null)
                BillDetailDAL.Insert(new BillDetail { BillId = b.Id, DrinkId = drinkId, Quantity = 1, Price = drink.Price, VatRate = _rules.FnbVatPercent, LineFinalAmount = drink.Price }, conn, tx);
            else
            {
                line.Quantity++;
                line.Price = drink.Price;
                BillDetailDAL.Update(line, conn, tx);
            }
        }
        else if (line != null)
        {
            if (action == "delete" || line.Quantity <= 1) BillDetailDAL.Delete(line.Id, conn, tx);
            else
            {
                line.Quantity--;
                BillDetailDAL.Update(line, conn, tx);
            }
        }
    };

    // ------------------------------------------------------------------ thành viên, voucher, điểm

    /// <summary>Tra cứu thành viên theo mã thành viên, số điện thoại hoặc email và gắn vào hóa đơn; chuỗi rỗng là bỏ thành viên.</summary>
    public Task<Cart> LookupMember(PosContext ctx, string? query) => Mutate(ctx, (b, conn, tx) =>
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            b.CustomerId = null;
            b.PointsUsed = 0;
        }
        else
        {
            var u = UserDAL.FindCustomer(query) ?? throw FieldErrors.Of("query", "Không tìm thấy thành viên. Bạn kiểm tra lại mã thành viên, số điện thoại hoặc email.");
            b.CustomerId = u.Id;
        }
        BillDAL.Update(b, conn, tx);
    });

    /// <summary>Áp voucher (SPEC 3.13); mã rỗng là gỡ voucher. Không hợp lệ thì VOUCHER_INVALID kèm lý do cụ thể.</summary>
    public Task<Cart> ApplyVoucher(PosContext ctx, string? code) => Mutate(ctx, VoucherChange(code));

    /// <summary>Áp hoặc gỡ voucher, dùng chung cho quầy và online.</summary>
    public Action<Bill, SqlConnection, SqlTransaction> VoucherChange(string? code) => (b, conn, tx) =>
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            b.VoucherId = null;
            BillDAL.Update(b, conn, tx);
            return;
        }
        var v = VoucherDAL.GetByCode(code, conn, tx) ?? throw new BusinessRuleException(ErrorCodes.VoucherInvalid, $"Không tìm thấy voucher mã {code.Trim().ToUpperInvariant()}.");
        var totals = Recalculate(b.Id, conn, tx);
        var bill = BillDAL.GetById(b.Id, conn, tx)!;
        var check = VoucherRules.Check(v, _clock.Now, bill.TicketSubtotal + bill.FnbSubtotal, bill.CustomerId, totals.Tickets.Select(t => (t.ListPrice, t.SeatType)));
        if (!check.Ok) throw new BusinessRuleException(ErrorCodes.VoucherInvalid, check.Reason!);
        bill.VoucherId = v.Id;
        BillDAL.Update(bill, conn, tx);
    };

    /// <summary>Đổi điểm: cần thành viên; 1 điểm = PointValueVnd, tối đa MaxPointsRedeemPercent giá trị hóa đơn (POINTS_INVALID).</summary>
    public Task<Cart> ApplyPoints(PosContext ctx, int points) => Mutate(ctx, PointsChange(points));

    /// <summary>Đổi điểm (0 là bỏ đổi điểm), dùng chung cho quầy và online.</summary>
    public Action<Bill, SqlConnection, SqlTransaction> PointsChange(int points) => (b, conn, tx) =>
    {
        if (b.CustomerId is not int cid)
        {
            if (points == 0) return;
            throw new BusinessRuleException(ErrorCodes.PointsInvalid, "Bạn tra cứu thành viên (F1) trước khi đổi điểm.");
        }
        b.PointsUsed = 0;
        BillDAL.Update(b, conn, tx);
        var totals = Recalculate(b.Id, conn, tx);
        var bill = BillDAL.GetById(b.Id, conn, tx)!;
        var customer = UserDAL.GetById(cid, conn, tx)!;
        var error = LoyaltyCalculator.ValidateRedeem(points, customer.LoyaltyPoints, bill.TicketSubtotal + bill.FnbSubtotal - totals.VoucherDiscount, _rules);
        if (error != null) throw new BusinessRuleException(ErrorCodes.PointsInvalid, error, new { max = LoyaltyCalculator.MaxRedeemPoints(bill.TicketSubtotal + bill.FnbSubtotal - totals.VoucherDiscount, customer.LoyaltyPoints, _rules) });
        bill.PointsUsed = points;
        BillDAL.Update(bill, conn, tx);
    };

    /// <summary>Gia hạn giữ ghế một lần thêm SeatHoldExtendMinutes (SPEC 3.5).</summary>
    public async Task<Cart> ExtendHold(PosContext ctx)
    {
        var bill = await Current(ctx) ?? throw FieldErrors.Of("bill", "Chưa có hóa đơn nào đang giữ ghế.");
        var now = _clock.Now;
        var changes = DBUtil.InTransaction((conn, tx) =>
        {
            var b = BillDAL.GetForUpdate(bill.Id, conn, tx)!;
            if (b.Status != BillStatus.Waiting || !SeatHoldRules.CanExtend(b.HoldExpiresAt, b.HoldExtended, now) || b.ShowtimeId == null)
                throw FieldErrors.Of("bill", b.HoldExtended ? "Hóa đơn này đã được gia hạn một lần." : "Hóa đơn chưa giữ ghế nào để gia hạn.");
            b.HoldExpiresAt = b.HoldExpiresAt!.Value.AddMinutes(_rules.SeatHoldExtendMinutes);
            b.HoldExtended = true;
            BillDAL.Update(b, conn, tx);
            ShowtimeSeatDAL.SetHoldExpiry(b.Id, b.HoldExpiresAt.Value, conn, tx);
            var seats = ShowtimeSeatDAL.HeldByBill(b.Id, conn, tx);
            return (b.ShowtimeId.Value, seats.Select(s => new SeatChange(s.SeatId, SeatDisplayStatus.Held, b.HoldExpiresAt, b.Id)).ToList());
        });
        await _notifier.SeatMapUpdated(changes.Item1, changes.Item2);
        return BuildCart(BillDAL.GetById(bill.Id)!, new List<string> { $"Đã gia hạn giữ ghế thêm {_rules.SeatHoldExtendMinutes} phút." });
    }

    // ------------------------------------------------------------------ hủy hóa đơn waiting (/pos/cancel)

    /// <summary>Hủy hóa đơn waiting (CANCELLED_BEFORE_PAYMENT) và trả ghế đang giữ.</summary>
    public async Task<string?> CancelWaiting(int billId, string actorRole, int cinemaId)
    {
        var now = _clock.Now;
        var (code, changes) = DBUtil.InTransaction((conn, tx) =>
        {
            var b = BillDAL.GetForUpdate(billId, conn, tx) ?? throw CategoryService.NotFound("Không tìm thấy hóa đơn.");
            if (b.CinemaId != cinemaId) throw FieldErrors.Of("billId", "Hóa đơn thuộc rạp khác.");
            var ch = ReleaseHeld(b, conn, tx);
            _status.ChangeStatus(b.Id, BillStatus.Cancel, CancelReason.CancelledBeforePayment, actorRole, now, conn, tx);
            return (b.Code, ch);
        });
        await _notifier.SeatMapUpdated(changes);
        return code;
    }

    // ------------------------------------------------------------------ thanh toán (/pos/checkout)

    /// <summary>
    /// Thanh toán tại quầy bằng CASH (nhập tiền khách đưa, tính tiền thối) hoặc VIETQR_SIM (thu ngân bấm "Đã nhận tiền"); dùng kèm voucher, điểm.
    /// Trong một transaction: kiểm lại hạn giữ ghế, luật độ tuổi và người giám hộ, kho; tạo Tickets và mã QR, ShowtimeSeats chuyển BOOKED; bắp nước
    /// chuyển PREPARING và trừ ActualStock; ghi Payments; chuyển finish qua BillStateMachine; cộng điểm; rồi phát SEAT_MAP_UPDATED.
    /// </summary>
    public async Task<CheckoutResult> Checkout(PosContext ctx, string? method, long? cashReceived)
    {
        if (method is not (PaymentMethod.Cash or PaymentMethod.VietQrSim or PaymentMethod.Wallet)) throw FieldErrors.Of("method", "Bạn chọn phương thức thanh toán.");
        var bill = await Current(ctx) ?? throw FieldErrors.Of("bill", "Hóa đơn trống. Bạn chọn ghế hoặc món trước.");
        var now = _clock.Now;
        var (result, changes) = DBUtil.InTransaction((conn, tx) =>
        {
            var b = BillDAL.GetForUpdate(bill.Id, conn, tx)!;
            if (b.Status != BillStatus.Waiting) throw new BusinessRuleException(ErrorCodes.InvalidTransition, "Hóa đơn đã thanh toán hoặc đã hủy.");
            if (SeatHoldRules.IsExpired(b.HoldExpiresAt, now))
                throw new BusinessRuleException(ErrorCodes.PaymentExpired, $"Đã hết {_rules.SeatHoldMinutes} phút giữ ghế. Bạn chọn lại ghế để tiếp tục nhé.");

            ShowtimeInfo? info = null;
            if (b.ShowtimeId is int sid)
            {
                info = SeatMapService.Info(sid, conn, tx)!;
                ShowtimeSeatDAL.LockForShowtime(sid, conn, tx);
                if (!SeatHoldRules.IsSalesOpen(info.Showtime.FeatureStartTime, info.Showtime.OperationalStatus, BillChannel.Pos, now, _rules))
                    throw new BusinessRuleException(ErrorCodes.SalesClosed, $"Suất {Fmt.Time(info.Showtime.StartTime)} đã hết giờ bán tại quầy. Bạn hủy hóa đơn và chọn suất khác.");
            }
            var totals = Recalculate(b.Id, conn, tx);
            if (totals.Notices.Count > 0)
                throw FieldErrors.Of("bill", totals.Notices[0] + " Bạn kiểm tra lại hóa đơn rồi thanh toán.");
            b = BillDAL.GetById(b.Id, conn, tx)!;
            if (totals.Tickets.Count == 0 && totals.Lines.Count == 0) throw FieldErrors.Of("bill", "Hóa đơn trống. Bạn chọn ghế hoặc món trước.");

            if (info != null)
            {
                foreach (var t in totals.Tickets)
                {
                    var c = AgeRules.CheckTicket(t.Audience, info.Movie.AgeRating, info.Showtime.StartTime, info.Showtime.EndTime, _rules);
                    if (!c.Ok) throw new BusinessRuleException(c.ErrorCode!, $"Ghế {t.Label}: {c.Message}", new { seatId = t.SeatId });
                }
                var g = AgeRules.CheckGuardian(info.Movie.AgeRating, totals.Tickets.Select(t => t.Audience), b.GuardianConfirmed);
                if (!g.Ok) throw new BusinessRuleException(g.ErrorCode!, g.Message!);
            }

            long received = method == PaymentMethod.Cash ? cashReceived ?? 0 : b.Total;
            if (method == PaymentMethod.Cash && received < b.Total)
                throw FieldErrors.Of("cashReceived", $"Tiền khách đưa chưa đủ. Cần ít nhất {Fmt.Money(b.Total)}.");

            // Vé và mã QR; ghế chuyển BOOKED
            var booked = new List<SeatChange>();
            foreach (var t in totals.Tickets)
            {
                string qr;
                do qr = CodeGenerator.TicketQrCode(); while (TicketDAL.QrExists(qr, conn, tx));
                int ticketId = TicketDAL.Insert(new Ticket
                {
                    BillId = b.Id, ShowtimeId = b.ShowtimeId!.Value, SeatId = t.SeatId, AudienceType = t.Audience, ListPrice = t.ListPrice,
                    FinalPrice = t.ListPrice - t.Discount, VatRate = _rules.TicketVatPercent, QrCode = qr, CheckInStatus = TicketCheckInStatus.NotCheckedIn, CreatedAt = now,
                }, conn, tx);
                if (ShowtimeSeatDAL.Book(b.ShowtimeId!.Value, t.SeatId, b.Id, ticketId, conn, tx) != 1)
                    throw new BusinessRuleException(ErrorCodes.SeatTaken, $"Ghế {t.Label} không còn được giữ cho hóa đơn này. Bạn chọn lại ghế.");
                booked.Add(new SeatChange(t.SeatId, SeatDisplayStatus.Booked, null, null));
            }

            // WALLET (SPEC 3.6-12): dành cho thành viên đã tra cứu; trừ ví theo 3.13 (WALLET_INSUFFICIENT)
            if (method == PaymentMethod.Wallet)
            {
                if (b.CustomerId is not int wid) throw FieldErrors.Of("method", "Thanh toán bằng ví cần tra cứu thành viên (F1) trước.");
                WalletService.Debit(wid, b.Total, $"Thanh toán hóa đơn {b.Code} tại quầy", b.Id, now, conn, tx);
            }

            if (b.Total > 0 || method == PaymentMethod.Wallet)
                PaymentDAL.Insert(new Payment
                {
                    BillId = b.Id, PosShiftId = ctx.Shift.Id, Method = method!, Direction = PaymentDirection.In, Amount = b.Total, Status = PaymentStatus.Succeeded,
                    ProviderTxnId = method == PaymentMethod.VietQrSim ? "SIM" + Guid.NewGuid().ToString("N")[..17].ToUpperInvariant() : null, CreatedAt = now,
                }, conn, tx);

            _status.ChangeStatus(b.Id, BillStatus.Finish, null, ctx.Role, now, conn, tx);
            b = BillDAL.GetById(b.Id, conn, tx)!;
            if (totals.Tickets.Count > 0) b.CheckInStatus = BillCheckInStatus.NotCheckedIn;
            if (totals.Lines.Count > 0)
            {
                var day = PricingCalculator.OperatingDate(now);
                b.FnbStatus = FnbStatus.Preparing;
                b.PickupNumber = CodeGenerator.NextPickupNumber(BillDAL.LastPickupNumber(b.CinemaId, day.Add(PricingCalculator.NightCutoff), day.AddDays(1).Add(PricingCalculator.NightCutoff), conn, tx));
                _inventory.ConsumeForBill(b.Id, b.CinemaId, ctx.UserId, now, conn, tx);
            }
            BillDAL.Update(b, conn, tx);
            if (b.VoucherId is int vid && VoucherDAL.ChangeUsed(vid, 1, conn, tx) != 1)
                throw new BusinessRuleException(ErrorCodes.VoucherInvalid, "Voucher vừa hết lượt dùng. Bạn gỡ voucher rồi thanh toán lại.");
            _loyalty.OnFinish(b, now, conn, tx);

            var res = new CheckoutResult(b.Id, b.Code, b.Total, received, received - b.Total, $"/pos/print?billId={b.Id}");
            return (res, b.ShowtimeId is int s2 ? new Dictionary<int, List<SeatChange>> { [s2] = booked } : new Dictionary<int, List<SeatChange>>());
        });
        await _notifier.SeatMapUpdated(changes);
        return result;
    }

    // ------------------------------------------------------------------ hủy hóa đơn đã thanh toán (/pos/request-void)

    /// <summary>
    /// Hủy hóa đơn đã thanh toán (VOIDED_BY_POS): thu ngân nhập lý do, quản lý duyệt bằng PIN ngay trên máy (CONTRACTS C5-03: nhân viên chuyển
    /// finish sang cancel luôn cần quản lý duyệt). Không cho hủy khi đã có vé vào cửa. Khi được duyệt: hoàn tiền theo phương thức gốc (tiền mặt trừ vào
    /// két của ca hiện tại), vé VOIDED, ghế AVAILABLE, bắp nước chưa làm thì giải phóng kho, thu hồi điểm, ghi SupervisorApprovals và AuditLog.
    /// </summary>
    public async Task<string> VoidPaid(int billId, string? reason, User approver, int requestedBy, int cinemaId, int? currentShiftId)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5) throw FieldErrors.Of("reason", "Bạn ghi lý do hủy (ít nhất 5 ký tự).");
        if (!Roles.SupervisorOrManager.Contains(approver.Role))
            throw new BusinessRuleException(ErrorCodes.ApprovalRequired, "Hủy hóa đơn đã thanh toán cần quản lý ca hoặc quản lý rạp duyệt.");
        var now = _clock.Now;
        var (code, changes) = DBUtil.InTransaction((conn, tx) =>
        {
            var b = BillDAL.GetForUpdate(billId, conn, tx) ?? throw CategoryService.NotFound("Không tìm thấy hóa đơn.");
            if (b.CinemaId != cinemaId) throw FieldErrors.Of("billId", "Hóa đơn thuộc rạp khác.");
            if (b.Status != BillStatus.Finish)
                throw new BusinessRuleException(ErrorCodes.InvalidTransition, "Chỉ hủy được hóa đơn đã thanh toán. Hóa đơn chưa thanh toán thì dùng Hủy hóa đơn.");
            if (TicketDAL.CountCheckedIn(b.Id, conn, tx) > 0)
                throw new BusinessRuleException(ErrorCodes.InvalidTransition, "Hóa đơn đã có vé vào cửa nên không hủy được.");

            SupervisorApprovalDAL.Insert(new SupervisorApproval
            {
                Type = ApprovalType.VoidBill, BillId = b.Id, RequestedBy = requestedBy, Reason = reason.Trim(), Status = ApprovalStatus.Approved,
                Method = ApprovalMethod.Pin, DecidedBy = approver.Id, DecidedAt = now, ExpiresAt = now.AddMinutes(_rules.ApprovalRequestExpireMinutes), CreatedAt = now,
            }, conn, tx);
            var seats = ShowtimeSeatDAL.SeatsOfBillTickets(b.Id, conn, tx);
            _status.ChangeStatus(b.Id, BillStatus.Cancel, CancelReason.VoidedByPos, approver.Role, now, conn, tx);
            ShowtimeSeatDAL.ReleaseForBill(b.Id, conn, tx);
            TicketDAL.SetCheckInStatusForBill(b.Id, TicketCheckInStatus.Voided, conn, tx);

            // Hoàn tiền theo phương thức gốc
            foreach (var p in PaymentDAL.RowsByBill(b.Id, conn, tx).Where(p => p.Status == PaymentStatus.Succeeded).GroupBy(p => p.Method))
            {
                long amount = p.Where(x => x.Direction == PaymentDirection.In).Sum(x => x.Amount) - p.Where(x => x.Direction == PaymentDirection.Refund).Sum(x => x.Amount);
                if (amount <= 0) continue;
                // Tiền mặt hoàn trừ vào két của ca hiện tại: người hủy phải có ca OPEN tại rạp của hóa đơn
                if (p.Key == PaymentMethod.Cash && currentShiftId == null)
                    throw new BusinessRuleException(ErrorCodes.ShiftNotOpen, "Hoàn tiền mặt cần trừ vào két của ca đang mở. Bạn mở ca tại máy POS của rạp này rồi hủy lại.");
                PaymentDAL.Insert(new Payment
                {
                    BillId = b.Id, PosShiftId = p.Key == PaymentMethod.Cash ? currentShiftId : null, Method = p.Key, Direction = PaymentDirection.Refund, Amount = amount,
                    Status = PaymentStatus.Succeeded, ProviderTxnId = p.Key == PaymentMethod.VietQrSim ? "SIMR" + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant() : null, CreatedAt = now,
                }, conn, tx);
                if (p.Key == PaymentMethod.Wallet && b.CustomerId is int wc)
                {
                    WalletService.Credit(wc, amount, $"Hoàn tiền hóa đơn {b.Code}", b.Id, now, conn, tx);
                }
            }

            b = BillDAL.GetById(b.Id, conn, tx)!;
            _inventory.ReleaseForVoid(b, approver.Id, now, conn, tx);
            if (b.CheckInStatus != BillCheckInStatus.None) b.CheckInStatus = BillCheckInStatus.Voided;
            if (b.FnbStatus != FnbStatus.None) b.FnbStatus = FnbStatus.Voided;
            BillDAL.Update(b, conn, tx);
            if (b.VoucherId is int vid) VoucherDAL.ChangeUsed(vid, -1, conn, tx);
            _loyalty.OnCancel(b, now, conn, tx);
            AuditLogDAL.Insert(new AuditLog
            {
                UserId = approver.Id, Action = AuditAction.VoidBill, EntityType = "Bill", EntityId = b.Id.ToString(),
                Detail = $"Hủy hóa đơn {b.Code} đã thanh toán ({Fmt.Money(b.Total)}), duyệt bằng PIN. Người yêu cầu #{requestedBy}. Lý do: {reason.Trim()}", CreatedAt = now,
            }, conn, tx);
            var ch = seats.GroupBy(s => s.ShowtimeId).ToDictionary(g => g.Key, g => g.Select(s => new SeatChange(s.SeatId, SeatDisplayStatus.Available, null, null)).ToList());
            return (b.Code, ch);
        });
        await _notifier.SeatMapUpdated(changes);
        return code;
    }

    // ------------------------------------------------------------------ giỏ hàng cho giao diện

    /// <summary>Hóa đơn tạm tính của máy POS (rỗng nếu chưa có hóa đơn).</summary>
    public async Task<Cart> Cart(PosContext ctx)
    {
        var bill = await Current(ctx);
        return bill == null ? new Cart() : BuildCart(bill, new List<string>());
    }

    /// <summary>Dựng hóa đơn tạm tính từ dữ liệu đã lưu (sau Recalculate).</summary>
    public Cart BuildCart(Bill bill, List<string> notices)
    {
        var now = _clock.Now;
        CartShowtime? show = null;
        var tickets = new List<CartTicket>();
        bool guardianRequired = false;
        var allocations = new Dictionary<int, long>();
        if (bill.ShowtimeId is int sid)
        {
            var info = SeatMapService.Info(sid)!;
            show = new CartShowtime(sid, info.Movie.Title, info.Movie.AgeRating, StatusDisplay.AgeExplanation(info.Movie.AgeRating), info.Showtime.StartTime,
                info.Showtime.EndTime, info.Auditorium.Name, info.Auditorium.Format, StatusDisplay.Of(StatusDisplay.Language, info.Showtime.LanguageFormat).Label);
            var pricer = new TicketPricer(info, _rules);
            var held = ShowtimeSeatDAL.GetStates(sid).Where(s => s.Status == ShowtimeSeatStatus.Held && s.HoldBillId == bill.Id).ToList();
            var audiences = held.Select(s => s.HoldAudienceType ?? AudienceType.Adult).ToList();
            var guardian = AgeRules.CheckGuardian(info.Movie.AgeRating, audiences, bill.GuardianConfirmed);
            guardianRequired = info.Movie.AgeRating == AgeRating.K && audiences.Contains(AudienceType.ChildU13);
            var lines0 = BillDetailDAL.GetByBill(bill.Id);
            var prices = held.Select(s => pricer.Price(s.SeatType, s.HoldAudienceType ?? AudienceType.Adult)).ToList();
            var alloc = DiscountAllocator.Allocate(bill.DiscountTotal, prices.Concat(lines0.Select(l => l.Price * l.Quantity)).ToList());
            for (int i = 0; i < held.Count; i++)
            {
                var s = held[i];
                var aud = audiences[i];
                var c = AgeRules.CheckTicket(aud, info.Movie.AgeRating, info.Showtime.StartTime, info.Showtime.EndTime, _rules);
                string? code = c.Ok ? (aud == AudienceType.ChildU13 && !guardian.Ok ? guardian.ErrorCode : null) : c.ErrorCode;
                string? warn = c.Ok ? (aud == AudienceType.ChildU13 && !guardian.Ok ? guardian.Message : null) : c.Message;
                tickets.Add(new CartTicket(s.SeatId, s.Label, s.SeatType, StatusDisplay.Of(StatusDisplay.Seat, s.SeatType).Label, aud,
                    StatusDisplay.Of(StatusDisplay.Audience, aud).Label, prices[i], prices[i] - alloc[i], code, warn));
            }
        }
        var lines = BillDetailDAL.RowsByBill(bill.Id);
        var drinks = lines.Select(l => DrinkDAL.GetById(l.DrinkId)!).ToList();
        var portions = _inventory.Portions(bill.CinemaId, drinks);
        var items = lines.Select(l => new CartItem(l.DrinkId, l.DrinkName, l.Image, l.Quantity, l.Price, l.Price * l.Quantity,
            Math.Min(999, portions.TryGetValue(l.DrinkId, out var p) ? p : 0))).ToList();

        CartMember? member = null;
        int maxPoints = 0;
        if (bill.CustomerId is int cid && UserDAL.GetById(cid) is { } u)
        {
            member = new CartMember(u.Id, u.FullName, u.MemberCode, u.Tier, StatusDisplay.Of(StatusDisplay.Tier, u.Tier).Label, u.LoyaltyPoints, LoyaltyCalculator.PointsValue(u.LoyaltyPoints, _rules), u.WalletBalance);
            long voucher = bill.DiscountTotal - LoyaltyCalculator.PointsValue(bill.PointsUsed, _rules);
            maxPoints = LoyaltyCalculator.MaxRedeemPoints(bill.TicketSubtotal + bill.FnbSubtotal - voucher, u.LoyaltyPoints, _rules);
        }
        var voucherCode = bill.VoucherId is int vid ? VoucherDAL.GetById(vid)?.Code : null;
        long pointsDiscount = LoyaltyCalculator.PointsValue(bill.PointsUsed, _rules);
        long vat = tickets.Sum(t => PricingCalculator.VatAmount(t.FinalPrice, _rules.TicketVatPercent))
                   + lines.Sum(l => PricingCalculator.VatAmount(l.LineFinalAmount, l.VatRate));
        return new Cart
        {
            BillId = bill.Id, Code = bill.Code, Showtime = show, HoldExpiresAt = bill.ShowtimeId != null ? bill.HoldExpiresAt : null,
            CanExtend = bill.ShowtimeId != null && SeatHoldRules.CanExtend(bill.HoldExpiresAt, bill.HoldExtended, now),
            Member = member, Tickets = tickets, Items = items, VoucherCode = voucherCode, VoucherDiscount = bill.DiscountTotal - pointsDiscount,
            PointsUsed = bill.PointsUsed, PointsDiscount = pointsDiscount, MaxPoints = maxPoints, TicketSubtotal = bill.TicketSubtotal, FnbSubtotal = bill.FnbSubtotal,
            DiscountTotal = bill.DiscountTotal, VatTotal = vat, Total = bill.Total, GuardianRequired = guardianRequired, GuardianConfirmed = bill.GuardianConfirmed, Notices = notices,
        };
    }
}
