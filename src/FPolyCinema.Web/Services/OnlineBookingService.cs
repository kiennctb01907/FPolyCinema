using System.Text.Json;
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
/// Ví thành viên (SPEC 3.13-04): Users.WalletBalance chỉ thay đổi cùng một bản ghi WalletTransactions trong cùng transaction.
/// Trừ ví khi không đủ số dư thì WALLET_INSUFFICIENT.
/// </summary>
public static class WalletService
{
    public static long Debit(int userId, long amount, string reason, int? billId, DateTime now, SqlConnection conn, SqlTransaction tx)
    {
        if (amount <= 0) return UserDAL.GetById(userId, conn, tx)?.WalletBalance ?? 0;
        var balance = UserDAL.ChangeWallet(userId, -amount, conn, tx);
        if (balance == null)
        {
            var have = UserDAL.GetById(userId, conn, tx)?.WalletBalance ?? 0;
            throw new BusinessRuleException(ErrorCodes.WalletInsufficient, $"Số dư ví {Fmt.Money(have)} chưa đủ để thanh toán {Fmt.Money(amount)}. Bạn chọn VietQR hoặc nạp thêm ví tại quầy.");
        }
        WalletTransactionDAL.Insert(new WalletTransaction { UserId = userId, Amount = -amount, BalanceAfter = balance.Value, Reason = reason, BillId = billId, CreatedAt = now }, conn, tx);
        return balance.Value;
    }

    public static long Credit(int userId, long amount, string reason, int? billId, DateTime now, SqlConnection conn, SqlTransaction tx)
    {
        var balance = UserDAL.ChangeWallet(userId, amount, conn, tx) ?? throw new BusinessRuleException(ErrorCodes.WalletInsufficient, "Không cập nhật được ví.");
        WalletTransactionDAL.Insert(new WalletTransaction { UserId = userId, Amount = amount, BalanceAfter = balance, Reason = reason, BillId = billId, CreatedAt = now }, conn, tx);
        return balance;
    }
}

/// <summary>Một phương án cụm ghế gửi cho giao diện.</summary>
public sealed record SuggestCluster(List<int> SeatIds, List<string> Labels, string Range, int Score);

/// <summary>Gợi ý chuyển sang suất khác còn cụm ghế đạt ngưỡng (SPEC 4.1-12).</summary>
public sealed record CrossShowtimeHint(int ShowtimeId, string StartTime, string Range, int Score, string Message, string Url);

/// <summary>Kết quả gợi ý ghế: điểm từng ghế theo persona (bản đồ điểm AI), tối đa 3 cụm, gợi ý chuyển suất.</summary>
public sealed record SeatSuggestion(string Persona, int Quantity, Dictionary<int, int> Scores, List<SuggestCluster> Clusters, CrossShowtimeHint? SwitchTo);

/// <summary>
/// Gợi ý ghế bằng AI Seat Scoring (SPEC 4.1): chấm điểm theo persona, tìm cụm K ghế bằng ClusterFinder; nếu phương án tốt nhất dưới
/// GoodClusterScore thì tìm suất cùng phim, cùng rạp, cùng LanguageFormat, StartTime lệch trong ±CrossShowtimeWindowMinutes còn cụm đạt ngưỡng.
/// </summary>
public class SeatSuggestService
{
    private readonly IClock _clock;
    private readonly CinemaRules _rules;

    public SeatSuggestService(IClock clock, CinemaRules rules)
    {
        _clock = clock;
        _rules = rules;
    }

    public SeatSuggestion Suggest(int showtimeId, string? persona, int quantity, int? billId)
    {
        persona = SeatPersona.All.Contains(persona) ? persona! : SeatPersona.BestViewAudio;
        quantity = Math.Clamp(quantity, 1, 10);
        var info = SeatMapService.Info(showtimeId) ?? throw CategoryService.NotFound("Không tìm thấy suất chiếu.");
        var (seats, rows, cols, ratio) = Seats(info, billId);
        var scores = ClusterFinder.ScoreAll(seats, rows, cols, ratio, persona);
        var clusters = ClusterFinder.Find(seats, rows, cols, ratio, persona, quantity);
        CrossShowtimeHint? hint = null;
        if (clusters.Count == 0 || clusters[0].Score < _rules.GoodClusterScore) hint = FindSwitch(info, persona, quantity);
        return new SeatSuggestion(persona, quantity, scores, clusters.Select(c => new SuggestCluster(c.SeatIds.ToList(), c.Seats.Select(s => s.Label).ToList(), c.Range, c.Score)).ToList(), hint);
    }

    private (List<ClusterSeat> Seats, int Rows, int Cols, decimal Ratio) Seats(ShowtimeInfo info, int? billId)
    {
        var now = _clock.Now;
        var seats = ShowtimeSeatDAL.GetStates(info.Showtime.Id).Select(s =>
        {
            var eff = SeatMapService.Effective(s, now);
            bool free = eff == SeatDisplayStatus.Available || (eff == SeatDisplayStatus.Held && billId != null && s.HoldBillId == billId);
            return new ClusterSeat(s.SeatId, s.RowIndex, s.ColIndex, s.Label, s.SeatType, s.PairGroupId, free);
        }).ToList();
        return (seats, info.Auditorium.TotalRows, info.Auditorium.TotalCols, info.Auditorium.IdealRowRatio);
    }

    private CrossShowtimeHint? FindSwitch(ShowtimeInfo info, string persona, int quantity)
    {
        var now = _clock.Now;
        var s = info.Showtime;
        var window = TimeSpan.FromMinutes(_rules.CrossShowtimeWindowMinutes);
        var candidates = ShowtimeDAL.ForCustomer(info.Auditorium.CinemaId, s.MovieId, s.StartTime - window, s.StartTime + window + TimeSpan.FromSeconds(1), now)
            .Where(c => c.ShowtimeId != s.Id && c.LanguageFormat == s.LanguageFormat
                        && SeatHoldRules.IsSalesOpen(c.FeatureStartTime, c.OperationalStatus, BillChannel.Online, now, _rules))
            .OrderBy(c => Math.Abs((c.StartTime - s.StartTime).TotalMinutes));
        foreach (var c in candidates)
        {
            var other = SeatMapService.Info(c.ShowtimeId)!;
            var (seats, rows, cols, ratio) = Seats(other, null);
            var best = ClusterFinder.Find(seats, rows, cols, ratio, persona, quantity, 1).FirstOrDefault();
            if (best == null || best.Score < _rules.GoodClusterScore) continue;
            var type = best.Seats.Select(x => x.SeatType).Distinct().Count() == 1 ? LowerFirst(StatusDisplay.Of(StatusDisplay.Seat, best.Seats[0].SeatType).Label) : "ghế";
            bool central = Math.Abs(best.Seats.Average(x => x.Col) - SeatScoring.IdealCol(cols)) <= cols / 4.0;
            var msg = $"Suất {Fmt.Time(c.StartTime)} còn cụm {type}{(central ? " trung tâm" : string.Empty)} {best.Score}/100 điểm. Chuyển sang suất này?";
            return new CrossShowtimeHint(c.ShowtimeId, Fmt.Time(c.StartTime), best.Range, best.Score, msg, $"/booking/{c.ShowtimeId}?persona={persona}&qty={quantity}");
        }
        return null;
    }

    /// <summary>"Ghế VIP" → "ghế VIP": chỉ viết thường chữ đầu, giữ nguyên chữ viết tắt (SPEC 4.1-12).</summary>
    private static string LowerFirst(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..];
}

/// <summary>Ảnh chụp một vé lúc bắt đầu thanh toán (lưu trong Payments.RawPayload của yêu cầu PENDING) để xử lý webhook đến muộn.</summary>
public sealed record TicketSnapshot(int SeatId, string Label, string SeatType, string Audience, long ListPrice, long FinalPrice);

/// <summary>Kết quả bấm "Thanh toán": đã xong (ví) thì có ticketUrl, còn VietQR thì chuyển sang màn hình QR.</summary>
public sealed record OnlineCheckoutResult(string Code, bool Done, string? TicketUrl, string? PayUrl);

/// <summary>Kết quả xử lý webhook: mã HTTP và câu trả lời.</summary>
public sealed record WebhookResult(int Status, string Message);

/// <summary>
/// Đặt vé online (SPEC 3.5, 3.7, 3.9; UI C3, C4). Khách phải đăng nhập; hóa đơn có Channel ONLINE, CustomerId là khách. Mỗi khách có tối đa một
/// hóa đơn waiting; giữ ghế dùng chung SeatHoldService (kênh ONLINE), bắp nước, voucher, điểm dùng chung BillService.
/// Thanh toán bằng ví FPoly (xong ngay) hoặc VietQR qua trang mô phỏng /payment-sim/pay và webhook /payments/webhook (HMAC-SHA256).
/// </summary>
public class OnlineBookingService
{
    private readonly IClock _clock;
    private readonly CinemaRules _rules;
    private readonly BillService _bills;
    private readonly BillStatusService _status;
    private readonly SeatHoldService _holds;
    private readonly InventoryService _inventory;
    private readonly LoyaltyService _loyalty;
    private readonly RealtimeNotifier _notifier;
    private readonly PaymentOptions _payment;

    public OnlineBookingService(IClock clock, CinemaRules rules, BillService bills, BillStatusService status, SeatHoldService holds, InventoryService inventory,
        LoyaltyService loyalty, RealtimeNotifier notifier, Microsoft.Extensions.Options.IOptions<PaymentOptions> payment)
    {
        _clock = clock;
        _rules = rules;
        _bills = bills;
        _status = status;
        _holds = holds;
        _inventory = inventory;
        _loyalty = loyalty;
        _notifier = notifier;
        _payment = payment.Value;
    }

    private string HoldExpiredMessage => $"Đã hết {_rules.SeatHoldMinutes} phút giữ ghế. Bạn chọn lại ghế để tiếp tục nhé.";

    // ------------------------------------------------------------------ hóa đơn waiting của khách

    /// <summary>Hóa đơn waiting của khách; đã hết hạn giữ ghế thì hủy EXPIRED ngay. Trả null nếu chưa có.</summary>
    public async Task<Bill?> Current(int userId)
    {
        var bill = BillDAL.GetWaitingOnline(userId);
        if (bill == null || !SeatHoldRules.IsExpired(bill.HoldExpiresAt, _clock.Now)) return bill;
        var changes = DBUtil.InTransaction((conn, tx) => _bills.ExpireBill(bill.Id, _clock.Now, conn, tx));
        if (changes != null) await _notifier.SeatMapUpdated(changes);
        return BillDAL.GetWaitingOnline(userId);
    }

    /// <summary>Hóa đơn waiting của khách cho suất này (null nếu hóa đơn đang ở suất khác hoặc chưa có).</summary>
    public async Task<Bill?> CurrentFor(int userId, int showtimeId)
    {
        var b = await Current(userId);
        return b != null && (b.ShowtimeId == showtimeId || b.ShowtimeId == null) ? b : null;
    }

    /// <summary>
    /// Lấy hoặc tạo hóa đơn waiting cho suất. Hóa đơn đang dở ở rạp khác thì hủy (CANCELLED_BEFORE_PAYMENT) để đặt suất mới.
    /// </summary>
    public async Task<Bill> Ensure(int userId, int showtimeId)
    {
        var info = SeatMapService.Info(showtimeId) ?? throw CategoryService.NotFound("Không tìm thấy suất chiếu.");
        var customer = UserDAL.GetById(userId);
        if (customer == null || customer.Role != Roles.Customer)
            throw new BusinessRuleException(ErrorCodes.AccessDenied, "Đặt vé online dành cho tài khoản khách hàng. Bạn đăng nhập bằng tài khoản khách để đặt vé.");
        var bill = await Current(userId);
        if (bill != null && bill.CinemaId != info.Auditorium.CinemaId)
        {
            await CancelWaiting(userId);
            bill = null;
        }
        if (bill != null) return bill;
        var now = _clock.Now;
        bill = new Bill
        {
            Code = CodeGenerator.BillCode(now), Channel = BillChannel.Online, CinemaId = info.Auditorium.CinemaId, UserId = userId, CustomerId = userId, CreatedAt = now,
        };
        bill.Id = BillDAL.Insert(bill);
        return bill;
    }

    /// <summary>Khách bỏ đơn đang dở (CANCELLED_BEFORE_PAYMENT), trả ghế.</summary>
    public async Task CancelWaiting(int userId)
    {
        var bill = BillDAL.GetWaitingOnline(userId);
        if (bill == null) return;
        var now = _clock.Now;
        var changes = DBUtil.InTransaction((conn, tx) =>
        {
            var b = BillDAL.GetForUpdate(bill.Id, conn, tx)!;
            if (b.Status != BillStatus.Waiting) return new Dictionary<int, List<SeatChange>>();
            var ch = BillService.ReleaseHeld(b, conn, tx);
            PaymentDAL.FailPending(b.Id, conn, tx);
            _status.ChangeStatus(b.Id, BillStatus.Cancel, CancelReason.CancelledBeforePayment, Roles.Customer, now, conn, tx);
            return ch;
        });
        await _notifier.SeatMapUpdated(changes);
    }

    private async Task<Bill> Require(int userId, int showtimeId)
    {
        var b = await CurrentFor(userId, showtimeId);
        if (b == null) throw new BusinessRuleException(ErrorCodes.PaymentExpired, HoldExpiredMessage);
        return b;
    }

    // ------------------------------------------------------------------ bước 2: giữ ghế

    /// <summary>Đặt lựa chọn ghế của khách trong suất (SPEC 3.5, kênh ONLINE). Tài khoản có ngày sinh mà chưa đủ tuổi thì chặn ngay (SPEC 3.1).</summary>
    public async Task<Cart> Hold(int userId, int showtimeId, IReadOnlyCollection<int> seatIds, bool wheelchairConfirmed = false)
    {
        var info = SeatMapService.Info(showtimeId) ?? throw CategoryService.NotFound("Không tìm thấy suất chiếu.");
        var customer = UserDAL.GetById(userId);
        if (customer?.BirthDate is DateTime bd && seatIds.Count > 0 && !AgeRules.CanWatch(info.Movie.AgeRating, AgeRules.AgeOn(bd, _clock.Now)))
            throw new BusinessRuleException(ErrorCodes.AgeRestricted,
                $"Phim {info.Movie.AgeRating} dành cho khán giả từ đủ {AgeRules.MinimumAge(info.Movie.AgeRating)} tuổi. Theo ngày sinh trong tài khoản, bạn chưa đủ tuổi xem phim này.");
        var bill = await Ensure(userId, showtimeId);
        var res = await _holds.Hold(bill.Id, showtimeId, seatIds, new HoldOptions(BillChannel.Online, Roles.Customer, userId, WheelchairConfirmed: wheelchairConfirmed));
        return _bills.BuildCart(res.Bill, new List<string>());
    }

    // ------------------------------------------------------------------ bước 3: bắp nước và ưu đãi

    public async Task<Cart> UpdateQuantity(int userId, int showtimeId, int drinkId, string? action) =>
        _bills.MutateBill((await Require(userId, showtimeId)).Id, _bills.QuantityChange(drinkId, action));

    public async Task<Cart> ApplyVoucher(int userId, int showtimeId, string? code) =>
        _bills.MutateBill((await Require(userId, showtimeId)).Id, _bills.VoucherChange(code));

    public async Task<Cart> ApplyPoints(int userId, int showtimeId, int points) =>
        _bills.MutateBill((await Require(userId, showtimeId)).Id, _bills.PointsChange(points));

    /// <summary>Lưu các ô xác nhận độ tuổi, giờ chiếu cho trẻ em và người giám hộ (SPEC 3.1).</summary>
    public async Task<Cart> Confirm(int userId, int showtimeId, bool age, bool guardian) =>
        _bills.MutateBill((await Require(userId, showtimeId)).Id, (b, conn, tx) =>
        {
            b.AgeConfirmed = age;
            b.GuardianConfirmed = guardian;
            BillDAL.Update(b, conn, tx);
        });

    public async Task<Cart?> Cart(int userId, int showtimeId)
    {
        var b = await CurrentFor(userId, showtimeId);
        return b == null ? null : _bills.BuildCart(b, new List<string>());
    }

    // ------------------------------------------------------------------ bước 4: thanh toán

    /// <summary>
    /// Bấm "Thanh toán": kiểm hạn giữ ghế (PAYMENT_EXPIRED), giờ bán online (SALES_CLOSED), luật độ tuổi online, giỏ có vé.
    /// WALLET: trừ ví (WALLET_INSUFFICIENT) và chuyển finish ngay. VIETQR_SIM: tạo yêu cầu thanh toán PENDING (lưu ảnh chụp ghế) rồi chuyển sang màn hình QR.
    /// </summary>
    public async Task<OnlineCheckoutResult> Checkout(int userId, int showtimeId, string? method)
    {
        if (method is not (PaymentMethod.VietQrSim or PaymentMethod.Wallet)) throw FieldErrors.Of("method", "Bạn chọn phương thức thanh toán.");
        var bill = await Require(userId, showtimeId);
        var now = _clock.Now;
        var (result, changes) = DBUtil.InTransaction((conn, tx) =>
        {
            var b = BillDAL.GetForUpdate(bill.Id, conn, tx)!;
            if (b.Status != BillStatus.Waiting) throw new BusinessRuleException(ErrorCodes.InvalidTransition, "Đơn này đã thanh toán hoặc đã hủy. Bạn tải lại trang.");
            if (SeatHoldRules.IsExpired(b.HoldExpiresAt, now)) throw new BusinessRuleException(ErrorCodes.PaymentExpired, HoldExpiredMessage);
            var info = SeatMapService.Info(b.ShowtimeId ?? showtimeId, conn, tx)!;
            if (!SeatHoldRules.IsSalesOpen(info.Showtime.FeatureStartTime, info.Showtime.OperationalStatus, BillChannel.Online, now, _rules))
                throw new BusinessRuleException(ErrorCodes.SalesClosed, $"Suất {Fmt.Time(info.Showtime.StartTime)} đã hết giờ bán online. Bạn chọn suất khác.");
            ShowtimeSeatDAL.LockForShowtime(info.Showtime.Id, conn, tx);
            var totals = _bills.Recalculate(b.Id, conn, tx);
            if (totals.Notices.Count > 0) throw FieldErrors.Of("bill", totals.Notices[0] + " Bạn kiểm tra lại đơn rồi thanh toán.");
            if (totals.Tickets.Count == 0) throw FieldErrors.Of("bill", "Bạn chọn ít nhất một ghế trước khi thanh toán.");
            b = BillDAL.GetById(b.Id, conn, tx)!;
            var customer = UserDAL.GetById(userId, conn, tx)!;
            var age = OnlineAgeRules.Check(info.Movie.AgeRating, customer.BirthDate, now, info.Showtime.StartTime, info.Showtime.EndTime, b.AgeConfirmed, b.GuardianConfirmed,
                _rules.ChildU13LatestEnd, _rules.ChildU16LatestEnd);
            if (!age.Ok) throw new BusinessRuleException(age.ErrorCode!, age.Message!);
            var snapshot = totals.Tickets.Select(t => new TicketSnapshot(t.SeatId, t.Label, t.SeatType, t.Audience, t.ListPrice, t.ListPrice - t.Discount)).ToList();

            PaymentDAL.FailPending(b.Id, conn, tx);
            if (method == PaymentMethod.Wallet)
            {
                WalletService.Debit(userId, b.Total, $"Thanh toán hóa đơn {b.Code}", b.Id, now, conn, tx);
                var booked = Finalize(b, snapshot, PaymentMethod.Wallet, null, b.Total, null, null, now, conn, tx);
                return (new OnlineCheckoutResult(b.Code, true, TicketUrl(b.Code), null), booked);
            }
            PaymentDAL.Insert(new Payment
            {
                BillId = b.Id, Method = PaymentMethod.VietQrSim, Direction = PaymentDirection.In, Amount = b.Total, Status = PaymentStatus.Pending,
                RawPayload = JsonSerializer.Serialize(snapshot), CreatedAt = now,
            }, conn, tx);
            return (new OnlineCheckoutResult(b.Code, false, null, $"/booking/pay/{b.Code}"), (Dictionary<int, List<SeatChange>>?)null);
        });
        if (changes != null)
        {
            await _notifier.SeatMapUpdated(changes);
            await _notifier.PaymentStatusChanged(bill.Id, result.Code, BillStatus.Finish, null, "Thanh toán thành công", result.TicketUrl);
        }
        return result;
    }

    public static string TicketUrl(string code) => $"/tickets/{code}";

    /// <summary>
    /// Chuyển hóa đơn sang finish (SPEC 3.7-03): tạo vé có QR riêng, ghế BOOKED, ghi khoản thanh toán (cập nhật yêu cầu PENDING nếu có),
    /// chuyển finish qua BillStateMachine (Actors.System), bắp nước giữ kho (RESERVE) với FnbStatus PENDING_PICKUP, trừ lượt voucher, cộng điểm.
    /// Ghế phải đang HELD bởi hóa đơn. Trả các ghế đã đổi để phát SEAT_MAP_UPDATED sau khi commit.
    /// </summary>
    private Dictionary<int, List<SeatChange>> Finalize(Bill b, IReadOnlyList<TicketSnapshot> tickets, string method, string? txnId, long amount, Payment? pending,
        string? rawPayload, DateTime now, SqlConnection conn, SqlTransaction tx)
    {
        int showtimeId = b.ShowtimeId!.Value;
        var booked = new List<SeatChange>();
        foreach (var t in tickets)
        {
            string qr;
            do qr = CodeGenerator.TicketQrCode(); while (TicketDAL.QrExists(qr, conn, tx));
            int ticketId = TicketDAL.Insert(new Ticket
            {
                BillId = b.Id, ShowtimeId = showtimeId, SeatId = t.SeatId, AudienceType = t.Audience, ListPrice = t.ListPrice, FinalPrice = t.FinalPrice,
                VatRate = _rules.TicketVatPercent, QrCode = qr, CheckInStatus = TicketCheckInStatus.NotCheckedIn, CreatedAt = now,
            }, conn, tx);
            if (ShowtimeSeatDAL.Book(showtimeId, t.SeatId, b.Id, ticketId, conn, tx) != 1)
                throw new BusinessRuleException(ErrorCodes.SeatTaken, $"Ghế {t.Label} không còn được giữ cho đơn này.");
            booked.Add(new SeatChange(t.SeatId, SeatDisplayStatus.Booked, null, null));
        }

        if (pending != null)
        {
            pending.Status = PaymentStatus.Succeeded;
            pending.ProviderTxnId = txnId;
            pending.Amount = amount;
            pending.RawPayload = rawPayload ?? pending.RawPayload;
            PaymentDAL.Update(pending, conn, tx);
        }
        else if (amount > 0 || method == PaymentMethod.Wallet)
        {
            PaymentDAL.Insert(new Payment
            {
                BillId = b.Id, Method = method, Direction = PaymentDirection.In, Amount = amount, Status = PaymentStatus.Succeeded, ProviderTxnId = txnId, RawPayload = rawPayload, CreatedAt = now,
            }, conn, tx);
        }

        _status.ChangeStatus(b.Id, BillStatus.Finish, null, Actors.System, now, conn, tx);
        b = BillDAL.GetById(b.Id, conn, tx)!;
        b.CheckInStatus = BillCheckInStatus.NotCheckedIn;
        if (BillDetailDAL.GetByBill(b.Id, conn, tx).Count > 0)
        {
            b.FnbStatus = FnbStatus.PendingPickup;
            _inventory.ReserveForBill(b.Id, b.CinemaId, b.CustomerId, now, conn, tx);
        }
        b.HoldExpiresAt = null;
        BillDAL.Update(b, conn, tx);
        if (b.VoucherId is int vid && VoucherDAL.ChangeUsed(vid, 1, conn, tx) != 1)
            throw new BusinessRuleException(ErrorCodes.VoucherInvalid, "Voucher vừa hết lượt dùng. Bạn gỡ voucher rồi thanh toán lại.");
        _loyalty.OnFinish(b, now, conn, tx);
        return new Dictionary<int, List<SeatChange>> { [showtimeId] = booked };
    }

    // ------------------------------------------------------------------ màn hình QR và trang mô phỏng cổng thanh toán

    /// <summary>Đơn của khách theo mã (null nếu không phải của khách).</summary>
    public static Bill? OwnBill(int userId, string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var b = BillDAL.GetByCode(code.Trim());
        return b != null && b.CustomerId == userId && b.Channel == BillChannel.Online ? b : null;
    }

    /// <summary>Đường dẫn trang mô phỏng thanh toán của hóa đơn (mã QR trỏ tới đây).</summary>
    public static string SimUrl(int billId) => $"/payment-sim/pay?billId={billId}";

    /// <summary>Hai nội dung webhook đã ký sẵn cho trang mô phỏng: thành công và thất bại, mỗi lần mở trang có mã giao dịch mới.</summary>
    public (string Success, string SuccessSig, string Fail, string FailSig) SimPayloads(Bill b)
    {
        string Make(string status) => JsonSerializer.Serialize(new { billId = b.Id, providerTxnId = CodeGenerator.SimTxnId(), status, amount = b.Total });
        var ok = Make(PaymentStatus.Succeeded);
        var fail = Make(PaymentStatus.Failed);
        var secret = _payment.EffectiveSecret;
        return (ok, WebhookSignature.Compute(secret, ok), fail, WebhookSignature.Compute(secret, fail));
    }

    /// <summary>Nội dung webhook gửi từ cổng thanh toán.</summary>
    private sealed record WebhookPayload(int BillId, string? ProviderTxnId, string? Status, long Amount);

    /// <summary>
    /// POST /payments/webhook (SPEC 3.7): sai chữ ký thì 401; ProviderTxnId đã xử lý thì trả OK mà không làm lại.
    /// Thành công khi hóa đơn còn waiting và ghế còn giữ: finish. Đến muộn (hóa đơn đã EXPIRED): giữ lại đúng các ghế cũ; được hết thì finish,
    /// không được thì OVERBOOKED_REFUNDED (hoàn tiền, tặng voucher CompensationVoucherAmount, tạo Notifications). Thất bại: ghi FAILED, hóa đơn vẫn waiting.
    /// </summary>
    public async Task<WebhookResult> HandleWebhook(string body, string? signature)
    {
        if (!WebhookSignature.Verify(_payment.EffectiveSecret, body, signature)) return new WebhookResult(401, "Chữ ký không hợp lệ.");
        WebhookPayload? p;
        try { p = JsonSerializer.Deserialize<WebhookPayload>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException) { p = null; }
        if (p == null || p.BillId <= 0 || string.IsNullOrWhiteSpace(p.ProviderTxnId) || p.Status is not (PaymentStatus.Succeeded or PaymentStatus.Failed))
            return new WebhookResult(400, "Nội dung webhook không hợp lệ.");
        if (PaymentDAL.GetByProviderTxnId(p.ProviderTxnId) != null) return new WebhookResult(200, "Đã xử lý trước đó.");

        var now = _clock.Now;
        Dictionary<int, List<SeatChange>> seatChanges = new();
        (int BillId, string Code, string Status, string? Reason, string Message, string? Url)? evt = null;
        try
        {
            DBUtil.InTransaction((conn, tx) =>
            {
                var b = BillDAL.GetForUpdate(p.BillId, conn, tx);
                if (b == null) throw CategoryService.NotFound("Không tìm thấy hóa đơn.");
                if (PaymentDAL.GetByProviderTxnId(p.ProviderTxnId, conn, tx) != null) return; // webhook lặp đến cùng lúc
                var pending = PaymentDAL.LatestPending(b.Id, conn, tx);

                if (p.Status == PaymentStatus.Failed)
                {
                    PaymentDAL.Insert(new Payment
                    {
                        BillId = b.Id, Method = PaymentMethod.VietQrSim, Direction = PaymentDirection.In, Amount = p.Amount, Status = PaymentStatus.Failed,
                        ProviderTxnId = p.ProviderTxnId, RawPayload = body, CreatedAt = now,
                    }, conn, tx);
                    evt = (b.Id, b.Code, PaymentStatus.Failed, null, "Thanh toán chưa thành công. Bạn thử lại trước khi hết thời gian giữ ghế.", null);
                    return;
                }

                // Thành công: hóa đơn còn waiting nhưng đã quá hạn thì xử lý như đến muộn
                if (b.Status == BillStatus.Waiting && SeatHoldRules.IsExpired(b.HoldExpiresAt, now))
                {
                    var released = _bills.ExpireBill(b.Id, now, conn, tx);
                    if (released != null) Merge(seatChanges, released);
                    b = BillDAL.GetById(b.Id, conn, tx)!;
                }

                if (b.Status == BillStatus.Waiting)
                {
                    ShowtimeSeatDAL.LockForShowtime(b.ShowtimeId!.Value, conn, tx);
                    var totals = _bills.Recalculate(b.Id, conn, tx);
                    b = BillDAL.GetById(b.Id, conn, tx)!;
                    var snap = totals.Tickets.Select(t => new TicketSnapshot(t.SeatId, t.Label, t.SeatType, t.Audience, t.ListPrice, t.ListPrice - t.Discount)).ToList();
                    if (snap.Count == 0 || p.Amount < b.Total) throw new BusinessRuleException(ErrorCodes.ValidationFailed, "Số tiền thanh toán không khớp với hóa đơn.");
                    Merge(seatChanges, Finalize(b, snap, PaymentMethod.VietQrSim, p.ProviderTxnId, p.Amount, pending, body, now, conn, tx));
                    evt = (b.Id, b.Code, BillStatus.Finish, null, "Thanh toán thành công", TicketUrl(b.Code));
                    return;
                }

                if (b.Status == BillStatus.Cancel && b.CancelReason == CancelReason.Expired && pending?.RawPayload != null && b.ShowtimeId is int sid)
                {
                    var snap = JsonSerializer.Deserialize<List<TicketSnapshot>>(pending.RawPayload) ?? new List<TicketSnapshot>();
                    var ids = snap.Select(t => t.SeatId).ToList();
                    ShowtimeSeatDAL.LockForShowtime(sid, conn, tx);
                    int held = ids.Count == 0 ? 0 : ShowtimeSeatDAL.Hold(sid, b.Id, now.AddMinutes(_rules.SeatHoldMinutes), now, ids, conn, tx);
                    if (ids.Count > 0 && held == ids.Count)
                    {
                        // S3.7-05: giữ lại được đủ ghế cũ → finish
                        foreach (var t in snap) ShowtimeSeatDAL.SetAudience(b.Id, t.SeatId, t.Audience, conn, tx);
                        Merge(seatChanges, Finalize(b, snap, PaymentMethod.VietQrSim, p.ProviderTxnId, p.Amount, pending, body, now, conn, tx));
                        evt = (b.Id, b.Code, BillStatus.Finish, null, "Thanh toán thành công", TicketUrl(b.Code));
                        return;
                    }
                    // S3.7-06: ghế đã bán cho người khác → trả các ghế vừa giữ được, hoàn tiền, tặng voucher, thông báo
                    ShowtimeSeatDAL.Release(sid, b.Id, ids, conn, tx);
                    pending.Status = PaymentStatus.Succeeded;
                    pending.ProviderTxnId = p.ProviderTxnId;
                    pending.Amount = p.Amount;
                    pending.RawPayload = body;
                    PaymentDAL.Update(pending, conn, tx);
                    _status.ChangeStatus(b.Id, BillStatus.Cancel, CancelReason.OverbookedRefunded, Actors.System, now, conn, tx);
                    var voucher = RefundOverbooked(b, p.Amount, now, conn, tx);
                    evt = (b.Id, b.Code, BillStatus.Cancel, CancelReason.OverbookedRefunded,
                        $"Ghế bạn chọn đã được bán trong lúc chờ thanh toán. Đã hoàn {Fmt.Money(p.Amount)} và tặng voucher {voucher} trị giá {Fmt.Money(_rules.CompensationVoucherAmount)}.", "/account?tab=voucher");
                    return;
                }

                // Hóa đơn đã thanh toán hoặc đã hủy vì lý do khác: ghi nhận rồi hoàn lại ngay khoản tiền này
                PaymentDAL.Insert(new Payment
                {
                    BillId = b.Id, Method = PaymentMethod.VietQrSim, Direction = PaymentDirection.In, Amount = p.Amount, Status = PaymentStatus.Succeeded,
                    ProviderTxnId = p.ProviderTxnId, RawPayload = body, CreatedAt = now,
                }, conn, tx);
                RefundVietQr(b.Id, p.Amount, now, conn, tx);
                if (b.CustomerId is int cid)
                    NotificationDAL.Insert(new Notification
                    {
                        UserId = cid, Title = "Đã hoàn tiền giao dịch", Body = $"Đơn {b.Code} không cần thanh toán thêm nên khoản {Fmt.Money(p.Amount)} đã được hoàn về phương thức gốc.",
                        Link = TicketUrl(b.Code), CreatedAt = now,
                    }, conn, tx);
            });
        }
        catch (BusinessRuleException ex)
        {
            return new WebhookResult(ex.HttpStatus, ex.Message);
        }
        await _notifier.SeatMapUpdated(seatChanges);
        if (evt is { } e) await _notifier.PaymentStatusChanged(e.BillId, e.Code, e.Status, e.Reason, e.Message, e.Url);
        return new WebhookResult(200, "OK");
    }

    private static void RefundVietQr(int billId, long amount, DateTime now, SqlConnection conn, SqlTransaction tx) =>
        PaymentDAL.Insert(new Payment
        {
            BillId = billId, Method = PaymentMethod.VietQrSim, Direction = PaymentDirection.Refund, Amount = amount, Status = PaymentStatus.Succeeded,
            ProviderTxnId = "SIMR" + CodeGenerator.SimTxnId()[3..], CreatedAt = now,
        }, conn, tx);

    /// <summary>OVERBOOKED_REFUNDED: hoàn tiền về phương thức gốc, tặng voucher bù, tạo thông báo. Trả mã voucher.</summary>
    private string RefundOverbooked(Bill b, long amount, DateTime now, SqlConnection conn, SqlTransaction tx)
    {
        RefundVietQr(b.Id, amount, now, conn, tx);
        string code;
        do code = CodeGenerator.CompensationVoucherCode(); while (VoucherDAL.CodeExists(code, conn, tx));
        VoucherDAL.Insert(new Voucher
        {
            Code = code, Type = VoucherType.Amount, Value = _rules.CompensationVoucherAmount, MinBillAmount = 0, ValidFrom = now, ValidUntil = now.AddDays(_rules.CompensationVoucherValidDays),
            UsageLimit = 1, UsedCount = 0, OwnerUserId = b.CustomerId, Source = VoucherSource.Compensation, Active = true,
        }, conn, tx);
        if (b.CustomerId is int cid)
            NotificationDAL.Insert(new Notification
            {
                UserId = cid, Title = "Đơn vé đã được hoàn tiền",
                Body = $"Thanh toán cho đơn {b.Code} đến sau khi hết giờ giữ ghế và ghế đã được bán cho khách khác. Đã hoàn {Fmt.Money(amount)} về phương thức gốc và tặng bạn voucher {code} trị giá {Fmt.Money(_rules.CompensationVoucherAmount)}, dùng đến {Fmt.Date(now.AddDays(_rules.CompensationVoucherValidDays))}.",
                Link = "/account?tab=voucher", CreatedAt = now,
            }, conn, tx);
        return code;
    }

    private static void Merge(Dictionary<int, List<SeatChange>> into, IReadOnlyDictionary<int, List<SeatChange>> from)
    {
        foreach (var (k, v) in from)
        {
            if (!into.TryGetValue(k, out var list)) into[k] = list = new List<SeatChange>();
            list.AddRange(v);
        }
    }

    // ------------------------------------------------------------------ tự hủy vé (SPEC 3.9)

    /// <summary>Xét điều kiện tự hủy của một hóa đơn của khách.</summary>
    public SelfCancelCheck SelfCancelCheck(Bill b, int userId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        var now = _clock.Now;
        var start = b.ShowtimeId is int sid ? ShowtimeDAL.GetById(sid, conn, tx)?.StartTime ?? now : now;
        var tickets = TicketDAL.RowsByBill(b.Id, conn, tx).Select(t => t.CheckInStatus).ToList();
        var monthStart = SelfCancelRules.MonthStart(now);
        int cancels = b.CustomerId is int cid ? BillDAL.CountSelfCancels(cid, monthStart, monthStart.AddMonths(1), conn, tx) : 0;
        var facts = new SelfCancelFacts(b.Channel, b.Status, b.CustomerId == userId, b.CorporateBookingId != null, start, tickets, b.FnbStatus, cancels);
        return SelfCancelRules.Check(facts, now, _rules.SelfCancelMinMinutesBeforeStart, _rules.SelfCancelMaxPerMonth);
    }

    /// <summary>
    /// Khách tự hủy (SPEC 3.9): kiểm lại điều kiện trong transaction (SELF_CANCEL_NOT_ALLOWED); hóa đơn chuyển cancel CANCELLED_BY_USER; hoàn 100% phần đã trả
    /// vào ví (WalletTransactions); voucher và điểm đã dùng được trả lại, điểm đã cộng bị thu hồi; ghế về AVAILABLE (SEAT_MAP_UPDATED); giải phóng ReservedStock.
    /// </summary>
    public async Task<long> SelfCancel(int userId, string? code)
    {
        var bill = OwnBill(userId, code) ?? throw CategoryService.NotFound("Không tìm thấy vé.");
        var now = _clock.Now;
        var (refund, changes) = DBUtil.InTransaction((conn, tx) =>
        {
            var b = BillDAL.GetForUpdate(bill.Id, conn, tx)!;
            var check = SelfCancelCheck(b, userId, conn, tx);
            if (!check.Ok) throw new BusinessRuleException(ErrorCodes.SelfCancelNotAllowed, check.Reason!);
            var seats = ShowtimeSeatDAL.SeatsOfBillTickets(b.Id, conn, tx);
            _status.ChangeStatus(b.Id, BillStatus.Cancel, CancelReason.CancelledByUser, Roles.Customer, now, conn, tx);
            ShowtimeSeatDAL.ReleaseForBill(b.Id, conn, tx);
            TicketDAL.SetCheckInStatusForBill(b.Id, TicketCheckInStatus.Voided, conn, tx);

            long paid = PaymentDAL.RowsByBill(b.Id, conn, tx).Where(x => x.Status == PaymentStatus.Succeeded)
                .Sum(x => x.Direction == PaymentDirection.In ? x.Amount : -x.Amount);
            if (paid > 0)
            {
                PaymentDAL.Insert(new Payment
                {
                    BillId = b.Id, Method = PaymentMethod.Wallet, Direction = PaymentDirection.Refund, Amount = paid, Status = PaymentStatus.Succeeded, CreatedAt = now,
                }, conn, tx);
                WalletService.Credit(userId, paid, $"Hoàn tiền tự hủy vé {b.Code}", b.Id, now, conn, tx);
            }
            b = BillDAL.GetById(b.Id, conn, tx)!;
            _inventory.ReleaseForVoid(b, userId, now, conn, tx);
            b.CheckInStatus = BillCheckInStatus.Voided;
            if (b.FnbStatus != FnbStatus.None) b.FnbStatus = FnbStatus.Voided;
            BillDAL.Update(b, conn, tx);
            if (b.VoucherId is int vid) VoucherDAL.ChangeUsed(vid, -1, conn, tx);
            _loyalty.OnCancel(b, now, conn, tx);
            NotificationDAL.Insert(new Notification
            {
                UserId = userId, Title = "Đã hủy vé", Body = $"Đơn {b.Code} đã được hủy. Đã hoàn {Fmt.Money(paid)} vào ví FPoly của bạn.", Link = TicketUrl(b.Code), CreatedAt = now,
            }, conn, tx);
            var ch = seats.GroupBy(s => s.ShowtimeId).ToDictionary(g => g.Key, g => g.Select(s => new SeatChange(s.SeatId, SeatDisplayStatus.Available, null, null)).ToList());
            return (paid, ch);
        });
        await _notifier.SeatMapUpdated(changes);
        return refund;
    }
}
