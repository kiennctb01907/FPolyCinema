using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.Services;

/// <summary>
/// Kho bắp nước theo định mức – phần cơ bản của Phase 4 (SPEC 3.11): khả dụng = ActualStock − ReservedStock; món bán được n phần khi mọi
/// nguyên liệu có khả dụng ≥ n × định mức (STOCK_INSUFFICIENT); bán tại quầy trừ thẳng ActualStock khi hóa đơn finish (CONSUME).
/// Món không có định mức tại rạp thì không giới hạn theo kho.
/// </summary>
public partial class InventoryService
{
    /// <summary>Số phần còn bán được của từng món tại rạp (món IsSoldOut hoặc ngưng bán là 0).</summary>
    public Dictionary<int, int> Portions(int cinemaId, IEnumerable<Drink> drinks, IReadOnlyDictionary<int, int>? alreadyInCart = null)
    {
        var lines = RawMaterialDAL.StockLines(cinemaId);
        var result = new Dictionary<int, int>();
        foreach (var d in drinks)
        {
            if (!d.Active || d.IsSoldOut) { result[d.Id] = 0; continue; }
            var recipe = lines.Where(l => l.DrinkId == d.Id).Select(l => (l.PerPortion, l.Available)).ToList();
            result[d.Id] = StockCalculator.MaxPortions(recipe);
        }
        return result;
    }

    /// <summary>
    /// Kiểm đủ nguyên liệu cho cả giỏ (gộp theo nguyên liệu vì nhiều món dùng chung). Ném STOCK_INSUFFICIENT nêu tên món thiếu.
    /// </summary>
    public void EnsureAvailable(int cinemaId, IReadOnlyDictionary<int, int> quantities, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        var ids = quantities.Where(q => q.Value > 0).Select(q => q.Key).ToList();
        if (ids.Count == 0) return;
        var lines = RawMaterialDAL.StockLines(cinemaId, ids, conn, tx);
        Check(lines.Select(l => (l.DrinkId, l.RawMaterialId, l.PerPortion, l.Available)), quantities, conn, tx);
    }

    private static void Check(IEnumerable<(int DrinkId, int RawMaterialId, decimal PerPortion, decimal Available)> lines, IReadOnlyDictionary<int, int> quantities,
        SqlConnection? conn, SqlTransaction? tx)
    {
        var list = lines.ToList();
        foreach (var mat in list.GroupBy(l => l.RawMaterialId))
        {
            decimal need = mat.Sum(l => l.PerPortion * (quantities.TryGetValue(l.DrinkId, out var q) ? q : 0));
            var available = mat.First().Available;
            if (need <= available) continue;
            var drinkId = mat.OrderByDescending(l => quantities.TryGetValue(l.DrinkId, out var q) ? q : 0).First().DrinkId;
            var name = DrinkDAL.GetById(drinkId, conn, tx)?.Name ?? "Món này";
            throw new BusinessRuleException(ErrorCodes.StockInsufficient, $"{name} tạm hết nguyên liệu.", new { drinkId });
        }
    }

    /// <summary>
    /// Trừ kho khi hóa đơn bán tại quầy finish (SPEC 3.11-05): khóa nguyên liệu, kiểm lại khả dụng, ghi CONSUME theo định mức và giảm ActualStock.
    /// </summary>
    public void ConsumeForBill(int billId, int cinemaId, int? userId, DateTime now, SqlConnection conn, SqlTransaction tx)
    {
        var details = BillDetailDAL.GetByBill(billId, conn, tx);
        if (details.Count == 0) return;
        var lines = RawMaterialDAL.LockForBill(billId, cinemaId, conn, tx);
        var qty = details.GroupBy(d => d.DrinkId).ToDictionary(g => g.Key, g => g.Sum(d => d.Quantity));
        Check(lines.Select(l => (l.DrinkId, l.RawMaterialId, l.PerPortion, l.Available)), qty, conn, tx);
        InventoryTransactionDAL.InsertForBill(billId, cinemaId, InventoryTxnType.Consume, "Bán tại quầy", userId, now, conn, tx);
        RawMaterialDAL.ApplyForBill(billId, cinemaId, -1, 0, conn, tx);
    }

    /// <summary>
    /// Giữ kho khi đơn online thanh toán thành công (SPEC 3.11-03): khóa nguyên liệu, kiểm lại khả dụng, ghi RESERVE theo định mức và tăng ReservedStock.
    /// Bắp nước chờ khách tới lấy (FnbStatus PENDING_PICKUP); khi làm món ở Phase 6 mới trừ ActualStock.
    /// </summary>
    public void ReserveForBill(int billId, int cinemaId, int? userId, DateTime now, SqlConnection conn, SqlTransaction tx)
    {
        var details = BillDetailDAL.GetByBill(billId, conn, tx);
        if (details.Count == 0) return;
        var lines = RawMaterialDAL.LockForBill(billId, cinemaId, conn, tx);
        var qty = details.GroupBy(d => d.DrinkId).ToDictionary(g => g.Key, g => g.Sum(d => d.Quantity));
        Check(lines.Select(l => (l.DrinkId, l.RawMaterialId, l.PerPortion, l.Available)), qty, conn, tx);
        InventoryTransactionDAL.InsertForBill(billId, cinemaId, InventoryTxnType.Reserve, "Đơn online đã thanh toán, chờ lấy", userId, now, conn, tx);
        RawMaterialDAL.ApplyForBill(billId, cinemaId, 0, 1, conn, tx);
    }

    /// <summary>
    /// Hủy hóa đơn đã thanh toán (SPEC 3.6-23): bắp nước chưa làm (đơn online PENDING_PICKUP) thì giải phóng phần giữ (RELEASE);
    /// đã làm (PREPARING, READY_FOR_PICKUP, FULFILLED) thì không hoàn kho. Trả true nếu đã giải phóng.
    /// </summary>
    public bool ReleaseForVoid(Bill bill, int? userId, DateTime now, SqlConnection conn, SqlTransaction tx)
    {
        if (bill.FnbStatus != FnbStatus.PendingPickup) return false;
        InventoryTransactionDAL.InsertForBill(bill.Id, bill.CinemaId, InventoryTxnType.Release, "Hủy hóa đơn trước khi làm món", userId, now, conn, tx);
        RawMaterialDAL.ApplyForBill(bill.Id, bill.CinemaId, 0, -1, conn, tx);
        return true;
    }
}

/// <summary>
/// Điểm và hạng khi hóa đơn finish hoặc bị hủy (SPEC 3.13), dùng LoyaltyCalculator. Chạy trong transaction của hóa đơn.
/// </summary>
public class LoyaltyService
{
    private readonly CinemaRules _rules;

    public LoyaltyService(CinemaRules rules) => _rules = rules;

    /// <summary>Hóa đơn có CustomerId vừa finish: trừ điểm đã đổi, cộng điểm trên phần đã trả, xét lại hạng theo chi tiêu 12 tháng.</summary>
    public void OnFinish(Bill bill, DateTime now, SqlConnection conn, SqlTransaction tx)
    {
        if (bill.CustomerId is not int cid) return;
        var customer = UserDAL.GetById(cid, conn, tx);
        if (customer == null) return;
        int earned = LoyaltyCalculator.EarnPoints(bill.Total, customer.Tier, _rules);
        if (bill.PointsUsed > 0)
            PointTransactionDAL.Insert(new PointTransaction { UserId = cid, Points = -bill.PointsUsed, Reason = $"Đổi điểm cho hóa đơn {bill.Code}", BillId = bill.Id, CreatedAt = now }, conn, tx);
        if (earned > 0)
            PointTransactionDAL.Insert(new PointTransaction { UserId = cid, Points = earned, Reason = $"Tích điểm hóa đơn {bill.Code}", BillId = bill.Id, CreatedAt = now }, conn, tx);
        var tier = LoyaltyCalculator.TierFor(BillDAL.CustomerSpend(cid, now.AddMonths(-12), now, conn, tx), _rules);
        if (UserDAL.AddPoints(cid, earned - bill.PointsUsed, tier, conn, tx) != 1)
            throw new BusinessRuleException(ErrorCodes.PointsInvalid, "Thành viên không còn đủ điểm để đổi. Bạn bỏ đổi điểm rồi thử lại.");
    }

    /// <summary>Hóa đơn bị hủy: thu hồi điểm đã cộng, trả lại điểm đã đổi; xét lại hạng. Số dư không xuống dưới 0.</summary>
    public void OnCancel(Bill bill, DateTime now, SqlConnection conn, SqlTransaction tx)
    {
        if (bill.CustomerId is not int cid) return;
        var customer = UserDAL.GetById(cid, conn, tx);
        if (customer == null) return;
        var (earned, redeemed) = PointTransactionDAL.SumForBill(bill.Id, cid, conn, tx);
        if (earned == 0 && redeemed == 0) return;
        int delta = redeemed - earned;
        if (customer.LoyaltyPoints + delta < 0) delta = -customer.LoyaltyPoints;
        if (redeemed > 0)
            PointTransactionDAL.Insert(new PointTransaction { UserId = cid, Points = redeemed, Reason = $"Trả lại điểm đã đổi do hủy hóa đơn {bill.Code}", BillId = bill.Id, CreatedAt = now }, conn, tx);
        int revoke = redeemed - delta;
        if (revoke > 0)
            PointTransactionDAL.Insert(new PointTransaction { UserId = cid, Points = -revoke, Reason = $"Thu hồi điểm do hủy hóa đơn {bill.Code}", BillId = bill.Id, CreatedAt = now }, conn, tx);
        var tier = LoyaltyCalculator.TierFor(BillDAL.CustomerSpend(cid, now.AddMonths(-12), now, conn, tx), _rules);
        UserDAL.AddPoints(cid, delta, tier, conn, tx);
    }
}

/// <summary>
/// PIN quản lý (SPEC 2.5): 6 chữ số, lưu dạng băm; sai ManagerPinMaxAttempts lần thì khóa ManagerPinLockMinutes phút (ManagerPinRules).
/// Số lần sai ghi ngay vào DB (ngoài transaction của thao tác cần duyệt) để không bị hoàn tác.
/// </summary>
public class ManagerPinService
{
    private readonly IClock _clock;
    private readonly CinemaRules _rules;

    public ManagerPinService(IClock clock, CinemaRules rules)
    {
        _clock = clock;
        _rules = rules;
    }

    /// <summary>Kiểm PIN của người duyệt cho rạp; trả người duyệt khi đúng, ném PIN_INVALID (kèm số lần còn lại) hoặc PIN_LOCKED.</summary>
    public User Verify(int approverId, string? pin, int cinemaId)
    {
        var now = _clock.Now;
        var u = UserDAL.GetById(approverId);
        bool canApprove = u != null && u.Active && u.ManagerPinHash != null
            && (u.Role == Roles.Manager && (u.CinemaId == null || u.CinemaId == cinemaId) || u.Role == Roles.ShiftManager && u.CinemaId == cinemaId);
        if (!canApprove) throw FieldErrors.Of("approverId", "Bạn chọn quản lý ca hoặc quản lý rạp sẽ duyệt.");
        if (ManagerPinRules.IsLocked(u!.PinLockedUntil, now))
            throw new BusinessRuleException(ErrorCodes.PinLocked, $"PIN của {u.FullName} đang bị khóa do nhập sai nhiều lần. Thử lại sau {Fmt.Time(u.PinLockedUntil!.Value)}.",
                new { lockedUntil = u.PinLockedUntil!.Value.ToString("yyyy-MM-ddTHH:mm:ss"), locked = true });
        bool correct = ManagerPinRules.IsWellFormed(pin) && PasswordUtil.Verify(u.ManagerPinHash, pin);
        var after = ManagerPinRules.After(correct, u.PinFailedCount, now, _rules);
        UserDAL.UpdatePinState(u.Id, after.FailedCount, after.LockedUntil);
        if (correct) return u;
        if (after.LockedUntil != null)
            throw new BusinessRuleException(ErrorCodes.PinLocked, $"Nhập sai PIN {_rules.ManagerPinMaxAttempts} lần. PIN bị khóa {_rules.ManagerPinLockMinutes} phút, đến {Fmt.Time(after.LockedUntil.Value)}.",
                new { lockedUntil = after.LockedUntil.Value.ToString("yyyy-MM-ddTHH:mm:ss"), locked = true });
        throw new BusinessRuleException(ErrorCodes.PinInvalid, $"Mã PIN chưa đúng. Còn {after.Remaining} lần thử.", new { remaining = after.Remaining });
    }
}

/// <summary>
/// Mở ca và đóng ca mù (SPEC 3.6). Mỗi máy (TerminalCode) và mỗi thu ngân chỉ có một ca OPEN; mã máy chọn khi mở ca và lưu trong cookie của máy.
/// </summary>
public class ShiftService
{
    /// <summary>Cookie lưu mã máy POS trên trình duyệt.</summary>
    public const string TerminalCookie = "fpc_terminal";

    /// <summary>Số máy POS mỗi rạp (POS-01 … POS-04).</summary>
    public const int TerminalsPerCinema = 4;

    public const long MaxOpeningCashFloat = 50_000_000;

    private readonly IClock _clock;
    private readonly BillService _bills;

    public ShiftService(IClock clock, BillService bills)
    {
        _clock = clock;
        _bills = bills;
    }

    public static List<string> Terminals() => Enumerable.Range(1, TerminalsPerCinema).Select(CodeGenerator.TerminalCode).ToList();

    /// <summary>Khóa máy dùng cho nhóm SignalR kênh 2: {CinemaId}-{TerminalCode} (mã máy POS-01 lặp lại ở mỗi rạp).</summary>
    public static string TerminalKey(int cinemaId, string terminalCode) => $"{cinemaId}-{terminalCode}";

    /// <summary>
    /// Ngữ cảnh bán hàng: người dùng phải có ca OPEN tại đúng máy đang dùng (SHIFT_NOT_OPEN). Máy nhận biết bằng cookie mã máy đặt lúc mở ca;
    /// trình duyệt chưa có cookie hoặc cookie của máy khác thì không bán được.
    /// </summary>
    public PosContext? Resolve(int userId, string role, string? terminalCookie)
    {
        if (string.IsNullOrEmpty(terminalCookie)) return null;
        var shift = PosShiftDAL.GetOpenByUser(userId);
        if (shift == null || terminalCookie != shift.TerminalCode) return null;
        return new PosContext(userId, role, shift.CinemaId, shift);
    }

    /// <summary>Ca OPEN của người dùng ở bất kỳ máy nào – chỉ dùng để đóng ca (không phải thao tác bán).</summary>
    public static PosContext? ResolveOwn(int userId, string role) =>
        PosShiftDAL.GetOpenByUser(userId) is { } shift ? new PosContext(userId, role, shift.CinemaId, shift) : null;

    public PosContext Require(int userId, string role, string? terminalCookie) =>
        Resolve(userId, role, terminalCookie) ?? throw new BusinessRuleException(ErrorCodes.ShiftNotOpen, "Bạn cần mở ca trước khi bán hàng.");

    /// <summary>Mở ca: chọn máy, nhập tiền đầu ca (OpeningCashFloat).</summary>
    public PosShift Open(int userId, int cinemaId, string? terminal, long? openingCashFloat)
    {
        var errors = new Dictionary<string, string>();
        if (terminal == null || !Terminals().Contains(terminal)) errors["Terminal"] = "Bạn chọn máy POS.";
        if (openingCashFloat == null) errors["OpeningCashFloat"] = "Bạn nhập tiền đầu ca (két trống thì nhập 0).";
        else if (openingCashFloat < 0 || openingCashFloat > MaxOpeningCashFloat) errors["OpeningCashFloat"] = $"Tiền đầu ca từ 0 đến {Fmt.Money(MaxOpeningCashFloat)}.";
        if (errors.Count > 0) throw FieldErrors.Of(errors);
        return DBUtil.InTransaction((conn, tx) =>
        {
            if (PosShiftDAL.GetOpenByUser(userId, conn, tx) is { } mine)
                throw FieldErrors.Of("Terminal", $"Bạn đang có ca mở ở máy {mine.TerminalCode}. Đóng ca đó trước khi mở ca mới.");
            if (PosShiftDAL.GetOpenByTerminal(cinemaId, terminal!, conn, tx) is { } other)
            {
                var who = UserDAL.GetById(other.UserId, conn, tx)?.FullName ?? "nhân viên khác";
                throw FieldErrors.Of("Terminal", $"Máy {terminal} đang có ca mở của {who}. Bạn chọn máy khác.");
            }
            var shift = new PosShift
            {
                UserId = userId, CinemaId = cinemaId, TerminalCode = terminal!, OpenedAt = _clock.Now, OpeningCashFloat = openingCashFloat!.Value, Status = ShiftStatus.Open,
            };
            shift.Id = PosShiftDAL.Insert(shift, conn, tx);
            return shift;
        });
    }

    /// <summary>
    /// Đóng ca mù: ca không còn hóa đơn waiting (SHIFT_HAS_WAITING_BILLS); nhân viên đếm tiền theo mệnh giá; sau khi xác nhận mới tính
    /// ExpectedCash và Discrepancy (CashDropCalculator). Biên bản không sửa được sau khi chốt.
    /// </summary>
    public CashDropResult Close(PosContext ctx, IReadOnlyDictionary<long, int> counts)
    {
        var now = _clock.Now;
        _bills.ExpireStaleOfShift(ctx.Shift.Id, now);
        long counted;
        try { counted = CashDropCalculator.CountTotal(counts); }
        catch (ArgumentException ex) { throw FieldErrors.Of("counts", ex.Message); }
        return DBUtil.InTransaction((conn, tx) =>
        {
            int waiting = BillDAL.CountWaitingForShift(ctx.Shift.Id, conn, tx);
            if (waiting > 0)
                throw new BusinessRuleException(ErrorCodes.ShiftHasWaitingBills,
                    $"Ca còn {waiting} hóa đơn chưa thanh toán. Bạn thanh toán hoặc hủy các hóa đơn đó trước khi đóng ca.", new { waiting });
            var (cashIn, cashRefund) = PaymentDAL.CashOfShift(ctx.Shift.Id, conn, tx);
            var result = CashDropCalculator.Close(counted, ctx.Shift.OpeningCashFloat, cashIn, cashRefund);
            if (PosShiftDAL.Close(ctx.Shift.Id, now, result.Counted, result.Expected, result.Discrepancy, conn, tx) != 1)
                throw new BusinessRuleException(ErrorCodes.ShiftNotOpen, "Ca này đã được đóng trước đó.");
            return result;
        });
    }
}
