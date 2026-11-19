using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FPolyCinema.Tests;

/// <summary>CSDL mẫu riêng cho test đặt vé online Phase 5.</summary>
public sealed class Phase5DatabaseFixture : IDisposable
{
    public static readonly DateTime Now = new(2026, 11, 6, 14, 0, 0);

    public Phase5DatabaseFixture()
    {
        ConnectionString = TestDatabase.NewConnectionString("P5Test");
        WebRoot = Path.Combine(Path.GetTempPath(), "fpc-p5-" + Guid.NewGuid().ToString("N"));
        DBUtil.Init(ConnectionString);
        new DbInitializer(ConnectionString, RepoPaths.File("Database"), new FakeClock(Now), NullLogger.Instance).Initialize(resetDb: true);
        new DbSeeder(new FakeClock(Now), new CinemaRules(), WebRoot, NullLogger.Instance).SeedIfEmpty();
    }

    public string ConnectionString { get; }
    public string WebRoot { get; }

    public void Dispose()
    {
        TestDatabase.Drop(ConnectionString);
        try { Directory.Delete(WebRoot, true); } catch (IOException) { }
    }
}

/// <summary>
/// Test tích hợp Phase 5 trên CSDL mẫu: hóa đơn ONLINE của khách (SPEC 3.5-03); thanh toán VietQR qua webhook có chữ ký HMAC, chống lặp,
/// thất bại, đến muộn giữ lại ghế cũ hoặc OVERBOOKED_REFUNDED (SPEC 3.7); giữ kho RESERVE (SPEC 3.11); ví và WALLET_INSUFFICIENT (SPEC 3.13);
/// luật độ tuổi online (SPEC 3.1); tự hủy vé (SPEC 3.9); gợi ý cụm ghế và chuyển suất (SPEC 4.1); trả bằng ví tại quầy (SPEC 3.6-12).
/// </summary>
[Collection("Database")]
public class Phase5ServiceTests : IClassFixture<Phase5DatabaseFixture>
{
    private const string Secret = "khoa-thu-webhook";
    private readonly FakeClock _clock = new(Phase5DatabaseFixture.Now.AddMinutes(1));
    private readonly CinemaRules _rules = new();
    private readonly RecordingHub _hub = new();

    public Phase5ServiceTests(Phase5DatabaseFixture f) => DBUtil.Init(f.ConnectionString);

    private RealtimeNotifier Notifier() => new(_hub, NullLogger<RealtimeNotifier>.Instance);
    private BillService Bills() => new(_clock, _rules, new BillStatusService(_clock), Notifier(), new InventoryService(), new LoyaltyService(_rules));
    private SeatHoldService Holds() => new(_clock, _rules, Bills(), Notifier());
    private OnlineBookingService Online() => new(_clock, _rules, Bills(), new BillStatusService(_clock), Holds(), new InventoryService(), new LoyaltyService(_rules), Notifier(),
        Options.Create(new PaymentOptions { WebhookSecret = Secret }));

    private static User Customer(string email = "khach@fpolycinema.vn") => UserDAL.GetByEmail(email)!;

    /// <summary>Một suất còn bán online, không có giới hạn giờ cho trẻ em ở phim P/K nếu cần; khác các suất đã dùng.</summary>
    private CustomerShowtimeRow Show(Func<CustomerShowtimeRow, bool>? where = null)
    {
        var now = _clock.Now;
        return ShowtimeDAL.ForCustomer(null, null, now.AddHours(3), now.AddDays(3), now)
            .Where(s => s.FreeSeats >= 60 && SeatHoldRules.IsSalesOpen(s.FeatureStartTime, s.OperationalStatus, BillChannel.Online, now, _rules))
            .Where(s => where == null || where(s))
            .OrderBy(s => Guid.NewGuid()).First();
    }

    private List<int> Seats(int showtimeId, int qty, int? billId = null) =>
        new SeatSuggestService(_clock, _rules).Suggest(showtimeId, SeatPersona.BestViewAudio, qty, billId).Clusters.First().SeatIds;

    private async Task<(Bill Bill, CustomerShowtimeRow Show, List<int> SeatIds)> Booked(OnlineBookingService online, User u, int qty = 2, bool drink = false)
    {
        var show = Show();
        var seats = Seats(show.ShowtimeId, qty);
        await online.Hold(u.Id, show.ShowtimeId, seats);
        if (drink) await online.UpdateQuantity(u.Id, show.ShowtimeId, DrinkDAL.GetAll().First(d => d.Active && !d.IsSoldOut && d.IsCombo).Id, "incr");
        await online.Confirm(u.Id, show.ShowtimeId, true, true);
        return (BillDAL.GetWaitingOnline(u.Id)!, show, seats);
    }

    private static async Task<T> Throws<T>(Func<Task> act) where T : Exception
    {
        try { await act(); }
        catch (T ex) { return ex; }
        throw new Xunit.Sdk.XunitException($"Không ném {typeof(T).Name}.");
    }

    private void ResetWaiting(int userId)
    {
        if (BillDAL.GetWaitingOnline(userId) != null) Online().CancelWaiting(userId).GetAwaiter().GetResult();
    }

    // ------------------------------------------------------------------ hóa đơn ONLINE (SPEC 3.5-03, 3.1-16)
    [Fact]
    public async Task Online_HoldCreatesOnlineBillOfCustomer_StaffRejected_AdultTicketsOnly()
    {
        var u = Customer();
        ResetWaiting(u.Id);
        var online = Online();
        var show = Show();
        var cart = await online.Hold(u.Id, show.ShowtimeId, Seats(show.ShowtimeId, 2));
        var bill = BillDAL.GetById(cart.BillId!.Value)!;
        Assert.Equal((BillChannel.Online, u.Id, u.Id, BillStatus.Waiting), (bill.Channel, bill.CustomerId!.Value, bill.UserId!.Value, bill.Status));
        Assert.Equal(show.CinemaId, bill.CinemaId);
        // Online không bán vé trẻ em và các loại vé theo luật: mọi vé là người lớn
        Assert.All(cart.Tickets, t => Assert.Equal(AudienceType.Adult, t.AudienceType));
        var staff = UserDAL.GetByEmail("cashier@fpolycinema.vn")!;
        Assert.Equal(ErrorCodes.AccessDenied, (await Throws<BusinessRuleException>(() => online.Hold(staff.Id, show.ShowtimeId, Seats(show.ShowtimeId, 1)))).Code);
        // Đổi sang suất ở rạp khác thì đơn cũ bị hủy CANCELLED_BEFORE_PAYMENT, ghế cũ được trả
        var other = Show(s => s.CinemaId != show.CinemaId);
        await online.Hold(u.Id, other.ShowtimeId, Seats(other.ShowtimeId, 1));
        var old = BillDAL.GetById(bill.Id)!;
        Assert.Equal((BillStatus.Cancel, CancelReason.CancelledBeforePayment), (old.Status, old.CancelReason));
        Assert.Empty(ShowtimeSeatDAL.HeldByBill(bill.Id));
        ResetWaiting(u.Id);
    }

    // ------------------------------------------------------------------ VietQR + webhook (SPEC 3.7-01..03, 3.11-03)
    [Fact]
    public async Task Online_VietQrWebhook_SignatureIdempotencyFinish_ReservesStock_NotifiesBillGroup()
    {
        var u = Customer();
        ResetWaiting(u.Id);
        var online = Online();
        var (bill, show, seats) = await Booked(online, u, 2, drink: true);
        var reservedBefore = RawMaterialDAL.GetAll().Where(r => r.CinemaId == bill.CinemaId).Sum(r => r.ReservedStock);
        var res = await online.Checkout(u.Id, show.ShowtimeId, PaymentMethod.VietQrSim);
        Assert.False(res.Done);
        Assert.Equal($"/booking/pay/{bill.Code}", res.PayUrl);
        var pending = PaymentDAL.LatestPending(bill.Id)!;
        Assert.Equal((PaymentMethod.VietQrSim, PaymentStatus.Pending), (pending.Method, pending.Status));
        Assert.Contains($"\"SeatId\":{seats[0]}", pending.RawPayload);

        var (ok, okSig, _, _) = online.SimPayloads(BillDAL.GetById(bill.Id)!);
        Assert.Equal(401, (await online.HandleWebhook(ok, "sai-chu-ky")).Status);
        Assert.Equal(BillStatus.Waiting, BillDAL.GetById(bill.Id)!.Status);

        Assert.Equal(200, (await online.HandleWebhook(ok, okSig)).Status);
        var done = BillDAL.GetById(bill.Id)!;
        Assert.Equal((BillStatus.Finish, FnbStatus.PendingPickup, BillCheckInStatus.NotCheckedIn), (done.Status, done.FnbStatus, done.CheckInStatus));
        Assert.Equal(seats.Count, TicketDAL.RowsByBill(bill.Id).Count);
        Assert.All(seats, id => Assert.Equal(ShowtimeSeatStatus.Booked, ShowtimeSeatDAL.GetById(show.ShowtimeId, id)!.Status));
        Assert.True(RawMaterialDAL.GetAll().Where(r => r.CinemaId == bill.CinemaId).Sum(r => r.ReservedStock) > reservedBefore);
        Assert.Contains(InventoryTransactionDAL.GetAll(), t => t.BillId == bill.Id && t.Type == InventoryTxnType.Reserve);
        Assert.Contains(_hub.Sent, s => s.Group == $"bill_{bill.Id}" && s.Method == RealtimeEvents.PaymentStatusChanged);
        Assert.Contains(_hub.Sent, s => s.Group == $"showtime_{show.ShowtimeId}" && s.Method == RealtimeEvents.SeatMapUpdated);

        // Webhook lặp cùng ProviderTxnId: trả OK, không ghi thêm
        int payments = PaymentDAL.RowsByBill(bill.Id).Count;
        Assert.Equal(200, (await online.HandleWebhook(ok, okSig)).Status);
        Assert.Equal(payments, PaymentDAL.RowsByBill(bill.Id).Count);
    }

    [Fact]
    public async Task Online_FailedWebhook_KeepsWaiting_ThenRetrySucceeds()
    {
        var u = Customer("khach2@fpolycinema.vn");
        ResetWaiting(u.Id);
        var online = Online();
        var (bill, show, _) = await Booked(online, u, 1);
        await online.Checkout(u.Id, show.ShowtimeId, PaymentMethod.VietQrSim);
        var (_, _, fail, failSig) = online.SimPayloads(BillDAL.GetById(bill.Id)!);
        Assert.Equal(200, (await online.HandleWebhook(fail, failSig)).Status);
        Assert.Equal(BillStatus.Waiting, BillDAL.GetById(bill.Id)!.Status);
        Assert.Contains(PaymentDAL.RowsByBill(bill.Id), p => p.Status == PaymentStatus.Failed);
        Assert.Contains(_hub.Sent, s => s.Group == $"bill_{bill.Id}" && s.Method == RealtimeEvents.PaymentStatusChanged);
        // Thử lại: mở trang mô phỏng lần nữa (mã giao dịch mới) và báo thành công
        var (ok, okSig, _, _) = online.SimPayloads(BillDAL.GetById(bill.Id)!);
        Assert.Equal(200, (await online.HandleWebhook(ok, okSig)).Status);
        Assert.Equal(BillStatus.Finish, BillDAL.GetById(bill.Id)!.Status);
    }

    [Fact]
    public async Task Online_CheckoutAfterHoldExpired_PaymentExpiredMessage()
    {
        var u = Customer("khach2@fpolycinema.vn");
        ResetWaiting(u.Id);
        var online = Online();
        var (_, show, _) = await Booked(online, u, 1);
        _clock.Advance(TimeSpan.FromMinutes(_rules.SeatHoldMinutes + 1));
        var ex = await Throws<BusinessRuleException>(() => online.Checkout(u.Id, show.ShowtimeId, PaymentMethod.VietQrSim));
        Assert.Equal(ErrorCodes.PaymentExpired, ex.Code);
        Assert.Equal("Đã hết 10 phút giữ ghế. Bạn chọn lại ghế để tiếp tục nhé.", ex.Message);
    }

    // ------------------------------------------------------------------ webhook đến muộn (SPEC 3.7-04, 05, 06)
    [Fact]
    public async Task Online_LateWebhook_ReholdsSameSeats_ThenFinish()
    {
        var u = Customer();
        ResetWaiting(u.Id);
        var online = Online();
        var (bill, show, seats) = await Booked(online, u, 2);
        await online.Checkout(u.Id, show.ShowtimeId, PaymentMethod.VietQrSim);
        var (ok, okSig, _, _) = online.SimPayloads(BillDAL.GetById(bill.Id)!);
        _clock.Advance(TimeSpan.FromMinutes(_rules.SeatHoldMinutes + 1));
        await Holds().SweepExpired();
        Assert.Equal((BillStatus.Cancel, CancelReason.Expired), (BillDAL.GetById(bill.Id)!.Status, BillDAL.GetById(bill.Id)!.CancelReason));
        Assert.Equal(200, (await online.HandleWebhook(ok, okSig)).Status);
        var b = BillDAL.GetById(bill.Id)!;
        Assert.Equal(BillStatus.Finish, b.Status);
        Assert.Null(b.CancelReason);
        Assert.Equal(seats.OrderBy(x => x), TicketDAL.RowsByBill(bill.Id).Select(t => t.SeatId).OrderBy(x => x));
    }

    /// <summary>
    /// BUG-076: suất bị hủy do sự cố khi khách đang ở màn hình QR (hóa đơn chuyển EXPIRED, ghế về AVAILABLE). Webhook thành công đến sau đó
    /// không được giữ lại ghế và chốt vé cho suất đã hủy; tiền được hoàn về phương thức gốc, khoản PENDING được ghi nhận rồi hoàn.
    /// </summary>
    [Fact]
    public async Task Online_LateWebhook_AfterShowtimeCancelled_RefundsWithoutBooking()
    {
        var u = Customer();
        ResetWaiting(u.Id);
        var online = Online();
        var (bill, show, seats) = await Booked(online, u, 2);
        await online.Checkout(u.Id, show.ShowtimeId, PaymentMethod.VietQrSim);
        var (ok, okSig, _, _) = online.SimPayloads(BillDAL.GetById(bill.Id)!);

        var manager = UserDAL.GetByEmail("manager@fpolycinema.vn")!;
        var shifts = new ShiftService(_clock, Bills());
        var terminal = ShiftService.Terminals().First(t => DBUtil.InTransaction((c, tx) => PosShiftDAL.GetOpenByTerminal(show.CinemaId, t, c, tx)) == null);
        var shift = shifts.Open(manager.Id, show.CinemaId, terminal, 0);
        try
        {
            var mgmt = new OpsManagementService(_clock, _rules, Notifier(), new BillStatusService(_clock), new InventoryService(), new LoyaltyService(_rules));
            await mgmt.CancelShowtime(show.ShowtimeId, "Mất điện toàn rạp", IncidentRules.ConfirmCode(show.ShowtimeId), manager.Id, manager.Role, null);
        }
        finally
        {
            shifts.Close(new PosContext(manager.Id, manager.Role, show.CinemaId, PosShiftDAL.GetOpenByUser(manager.Id)!), new Dictionary<long, int>());
        }
        Assert.Equal((BillStatus.Cancel, CancelReason.Expired), (BillDAL.GetById(bill.Id)!.Status, BillDAL.GetById(bill.Id)!.CancelReason));

        Assert.Equal(200, (await online.HandleWebhook(ok, okSig)).Status);
        var b = BillDAL.GetById(bill.Id)!;
        Assert.Equal((BillStatus.Cancel, CancelReason.Expired), (b.Status, b.CancelReason));
        Assert.Empty(TicketDAL.RowsByBill(bill.Id));
        Assert.All(seats, id => Assert.Equal(ShowtimeSeatStatus.Available, ShowtimeSeatDAL.GetById(show.ShowtimeId, id)!.Status));
        var pays = PaymentDAL.RowsByBill(bill.Id);
        Assert.DoesNotContain(pays, p => p.Status == PaymentStatus.Pending);
        long paidIn = pays.Where(p => p.Status == PaymentStatus.Succeeded && p.Direction == PaymentDirection.In).Sum(p => p.Amount);
        long refunded = pays.Where(p => p.Status == PaymentStatus.Succeeded && p.Direction == PaymentDirection.Refund).Sum(p => p.Amount);
        Assert.True(paidIn > 0);
        Assert.Equal(paidIn, refunded);
        Assert.Contains(NotificationDAL.ForUser(u.Id), n => n.Body.Contains(b.Code) && n.Body.Contains("hoàn"));
        Assert.Contains(_hub.Sent, s => s.Group == $"bill_{bill.Id}" && s.Method == RealtimeEvents.PaymentStatusChanged);
    }

    [Fact]
    public async Task Online_LateWebhook_SeatsResold_OverbookedRefunded_WithVoucherAndNotification()
    {
        var u = Customer();
        var u2 = Customer("khach2@fpolycinema.vn");
        ResetWaiting(u.Id);
        ResetWaiting(u2.Id);
        var online = Online();
        var (bill, show, seats) = await Booked(online, u, 2);
        await online.Checkout(u.Id, show.ShowtimeId, PaymentMethod.VietQrSim);
        var (ok, okSig, _, _) = online.SimPayloads(BillDAL.GetById(bill.Id)!);
        _clock.Advance(TimeSpan.FromMinutes(_rules.SeatHoldMinutes + 1));
        await Holds().SweepExpired();
        // Khách khác giữ đúng các ghế đó
        await online.Hold(u2.Id, show.ShowtimeId, seats);
        int vouchersBefore = VoucherDAL.ForOwner(u.Id).Count;
        Assert.Equal(200, (await online.HandleWebhook(ok, okSig)).Status);
        var b = BillDAL.GetById(bill.Id)!;
        Assert.Equal((BillStatus.Cancel, CancelReason.OverbookedRefunded), (b.Status, b.CancelReason));
        var pays = PaymentDAL.RowsByBill(bill.Id).Where(p => p.Status == PaymentStatus.Succeeded).ToList();
        Assert.Contains(pays, p => p.Direction == PaymentDirection.In && p.Method == PaymentMethod.VietQrSim);
        Assert.Contains(pays, p => p.Direction == PaymentDirection.Refund && p.Method == PaymentMethod.VietQrSim && p.Amount == b.Total);
        var comp = VoucherDAL.ForOwner(u.Id).Single(v => v.Source == VoucherSource.Compensation && v.ValidFrom == _clock.Now);
        Assert.Equal((VoucherType.Amount, _rules.CompensationVoucherAmount), (comp.Type, comp.Value));
        Assert.Equal(vouchersBefore + 1, VoucherDAL.ForOwner(u.Id).Count);
        Assert.Contains(NotificationDAL.ForUser(u.Id), n => n.Body.Contains(comp.Code));
        Assert.Contains(_hub.Sent, s => s.Group == $"bill_{bill.Id}" && s.Method == RealtimeEvents.PaymentStatusChanged);
        // Ghế vẫn thuộc khách thứ hai
        Assert.All(seats, id => Assert.Equal(BillDAL.GetWaitingOnline(u2.Id)!.Id, ShowtimeSeatDAL.GetById(show.ShowtimeId, id)!.HoldBillId));
        ResetWaiting(u2.Id);
    }

    // ------------------------------------------------------------------ ví (SPEC 3.13-04), luật tuổi online (SPEC 3.1-04, 08, 11)
    [Fact]
    public async Task Online_WalletPayment_InsufficientThenOk_WalletTransactionInSameTransaction()
    {
        var u = Customer("khach2@fpolycinema.vn");
        ResetWaiting(u.Id);
        var online = Online();
        var (bill, show, _) = await Booked(online, u, 2);
        DBUtil.ExecuteNonQuery("UPDATE Users SET WalletBalance = @2 WHERE Id = @1", new List<object> { u.Id, 1000L });
        var ex = await Throws<BusinessRuleException>(() => online.Checkout(u.Id, show.ShowtimeId, PaymentMethod.Wallet));
        Assert.Equal(ErrorCodes.WalletInsufficient, ex.Code);
        Assert.Equal(1000, UserDAL.GetById(u.Id)!.WalletBalance);
        Assert.Equal(BillStatus.Waiting, BillDAL.GetById(bill.Id)!.Status);

        var total = BillDAL.GetById(bill.Id)!.Total;
        DBUtil.ExecuteNonQuery("UPDATE Users SET WalletBalance = @2 WHERE Id = @1", new List<object> { u.Id, total + 5000 });
        var res = await online.Checkout(u.Id, show.ShowtimeId, PaymentMethod.Wallet);
        Assert.True(res.Done);
        Assert.Equal(5000, UserDAL.GetById(u.Id)!.WalletBalance);
        var wt = WalletTransactionDAL.ForUser(u.Id).First();
        Assert.Equal((-total, 5000L, bill.Id), (wt.Amount, wt.BalanceAfter, wt.BillId!.Value));
        Assert.Contains(PaymentDAL.RowsByBill(bill.Id), p => p.Method == PaymentMethod.Wallet && p.Direction == PaymentDirection.In && p.Status == PaymentStatus.Succeeded);
    }

    [Fact]
    public async Task Online_AgeRules_UnderageBlocked_T13NeedsConfirmation()
    {
        var u = Customer("khach2@fpolycinema.vn");
        ResetWaiting(u.Id);
        var online = Online();
        var t16 = Show(s => s.AgeRating is AgeRating.T16 or AgeRating.T18);
        DBUtil.ExecuteNonQuery("UPDATE Users SET BirthDate = @2 WHERE Id = @1", new List<object> { u.Id, _clock.Now.AddYears(-14).Date });
        var ex = await Throws<BusinessRuleException>(() => online.Hold(u.Id, t16.ShowtimeId, Seats(t16.ShowtimeId, 1)));
        Assert.Equal(ErrorCodes.AgeRestricted, ex.Code);
        DBUtil.ExecuteNonQuery("UPDATE Users SET BirthDate = @2 WHERE Id = @1", new List<object> { u.Id, new DateTime(1995, 5, 5) });
        await online.Hold(u.Id, t16.ShowtimeId, Seats(t16.ShowtimeId, 1));
        await online.Confirm(u.Id, t16.ShowtimeId, false, false);
        Assert.Equal(ErrorCodes.AgeConfirmRequired, (await Throws<BusinessRuleException>(() => online.Checkout(u.Id, t16.ShowtimeId, PaymentMethod.VietQrSim))).Code);
        await online.Confirm(u.Id, t16.ShowtimeId, true, false);
        Assert.False((await online.Checkout(u.Id, t16.ShowtimeId, PaymentMethod.VietQrSim)).Done);
        ResetWaiting(u.Id);
    }

    // ------------------------------------------------------------------ tự hủy vé (SPEC 3.9)
    [Fact]
    public async Task SelfCancel_RefundsToWallet_ReleasesSeatsAndStock_RestoresPointsAndVoucher()
    {
        var u = Customer();
        ResetWaiting(u.Id);
        var online = Online();
        var (bill, show, seats) = await Booked(online, u, 2, drink: true);
        await online.ApplyPoints(u.Id, show.ShowtimeId, 10);
        var pointsBefore = UserDAL.GetById(u.Id)!.LoyaltyPoints;
        await online.Checkout(u.Id, show.ShowtimeId, PaymentMethod.VietQrSim);
        var (ok, okSig, _, _) = online.SimPayloads(BillDAL.GetById(bill.Id)!);
        await online.HandleWebhook(ok, okSig);
        var paid = BillDAL.GetById(bill.Id)!;
        Assert.Equal(BillStatus.Finish, paid.Status);
        var wallet = UserDAL.GetById(u.Id)!.WalletBalance;
        var reserved = RawMaterialDAL.GetAll().Where(r => r.CinemaId == paid.CinemaId).Sum(r => r.ReservedStock);
        var check = online.SelfCancelCheck(paid, u.Id);
        Assert.True(check.Ok, check.Reason);

        var refund = await online.SelfCancel(u.Id, paid.Code);
        Assert.Equal(paid.Total, refund);
        var b = BillDAL.GetById(bill.Id)!;
        Assert.Equal((BillStatus.Cancel, CancelReason.CancelledByUser, FnbStatus.Voided), (b.Status, b.CancelReason, b.FnbStatus));
        Assert.Equal(wallet + paid.Total, UserDAL.GetById(u.Id)!.WalletBalance);
        Assert.Contains(WalletTransactionDAL.ForUser(u.Id), w => w.BillId == bill.Id && w.Amount == paid.Total);
        Assert.All(seats, id => Assert.Equal(ShowtimeSeatStatus.Available, ShowtimeSeatDAL.GetById(show.ShowtimeId, id)!.Status));
        Assert.All(TicketDAL.RowsByBill(bill.Id), t => Assert.Equal(TicketCheckInStatus.Voided, t.CheckInStatus));
        Assert.True(RawMaterialDAL.GetAll().Where(r => r.CinemaId == paid.CinemaId).Sum(r => r.ReservedStock) < reserved);
        Assert.Contains(InventoryTransactionDAL.GetAll(), t => t.BillId == bill.Id && t.Type == InventoryTxnType.Release);
        // Điểm đã đổi được trả lại, điểm đã cộng bị thu hồi: về đúng số dư trước khi thanh toán
        Assert.Equal(pointsBefore, UserDAL.GetById(u.Id)!.LoyaltyPoints);
        Assert.Contains(_hub.Sent, s => s.Group == $"showtime_{show.ShowtimeId}" && s.Method == RealtimeEvents.SeatMapUpdated);
        // Hủy lần nữa bị chặn với lý do
        var again = await Throws<BusinessRuleException>(() => online.SelfCancel(u.Id, paid.Code));
        Assert.Equal(ErrorCodes.SelfCancelNotAllowed, again.Code);
    }

    [Fact]
    public async Task SelfCancel_TooCloseToStart_MessageWithStartTime()
    {
        var u = Customer("khach2@fpolycinema.vn");
        ResetWaiting(u.Id);
        var online = Online();
        DBUtil.ExecuteNonQuery("UPDATE Users SET WalletBalance = 2000000 WHERE Id = @1", new List<object> { u.Id });
        var (bill, show, _) = await Booked(online, u, 1);
        await online.Checkout(u.Id, show.ShowtimeId, PaymentMethod.Wallet);
        var saved = _clock.Now;
        _clock.Now = show.StartTime.AddMinutes(-60);
        try
        {
            var ex = await Throws<BusinessRuleException>(() => online.SelfCancel(u.Id, bill.Code));
            Assert.Equal(ErrorCodes.SelfCancelNotAllowed, ex.Code);
            Assert.Equal($"Vé chỉ tự hủy được trước giờ chiếu 2 tiếng. Suất này bắt đầu lúc {show.StartTime:HH:mm}.", ex.Message);
        }
        finally { _clock.Now = saved; }
    }

    // ------------------------------------------------------------------ gợi ý ghế AI (SPEC 4.1-09..12)
    [Fact]
    public void Suggest_ThreeClusters_PersonaScores_AndSwitchShowtimeWhenBelowThreshold()
    {
        var svc = new SeatSuggestService(_clock, _rules);
        var show = Show();
        var res = svc.Suggest(show.ShowtimeId, SeatPersona.BestViewAudio, 2, null);
        Assert.InRange(res.Clusters.Count, 1, 3);
        Assert.All(res.Clusters, c => Assert.Equal(2, c.SeatIds.Count));
        Assert.True(res.Scores.Count > 50);
        Assert.Null(res.SwitchTo);

        // Tìm hai suất cùng phim, cùng rạp, cùng ngôn ngữ, lệch nhau trong 60 phút; lấp gần hết suất đầu thì gợi ý chuyển sang suất kia
        var now = _clock.Now;
        var all = ShowtimeDAL.ForCustomer(null, null, now.AddHours(2), now.AddDays(3), now)
            .Where(s => SeatHoldRules.IsSalesOpen(s.FeatureStartTime, s.OperationalStatus, BillChannel.Online, now, _rules)).ToList();
        var pair = all.SelectMany(a => all.Where(b => b.ShowtimeId != a.ShowtimeId && b.MovieId == a.MovieId && b.CinemaId == a.CinemaId && b.LanguageFormat == a.LanguageFormat
                && Math.Abs((b.StartTime - a.StartTime).TotalMinutes) <= _rules.CrossShowtimeWindowMinutes).Select(b => (A: a, B: b))).FirstOrDefault();
        Assert.True(pair.A != null, "Dữ liệu mẫu cần ít nhất hai suất cùng phim, cùng rạp lệch nhau trong 60 phút.");
        var info = SeatMapService.Info(pair.A.ShowtimeId)!;
        // Chỉ còn trống 2 hàng đầu (điểm thấp)
        DBUtil.ExecuteNonQuery("UPDATE ss SET ss.Status = @3 FROM ShowtimeSeats ss JOIN Seats s ON s.Id = ss.SeatId WHERE ss.ShowtimeId = @1 AND s.RowIndex > @2 AND ss.Status = @4",
            new List<object> { pair.A.ShowtimeId, 2, ShowtimeSeatStatus.HouseHold, ShowtimeSeatStatus.Available });
        var low = svc.Suggest(pair.A.ShowtimeId, SeatPersona.BestViewAudio, 2, null);
        Assert.True(low.Clusters.Count == 0 || low.Clusters[0].Score < _rules.GoodClusterScore);
        Assert.NotNull(low.SwitchTo);
        Assert.Contains(all, s => s.ShowtimeId == low.SwitchTo!.ShowtimeId && s.MovieId == pair.A.MovieId && s.LanguageFormat == pair.A.LanguageFormat);
        Assert.Matches(@"^Suất \d{2}:\d{2} còn cụm .+ \d{1,3}/100 điểm\. Chuyển sang suất này\?$", low.SwitchTo.Message);
        Assert.StartsWith($"/booking/{low.SwitchTo.ShowtimeId}", low.SwitchTo.Url);
        Assert.Equal(info.Auditorium.CinemaId, SeatMapService.Info(low.SwitchTo.ShowtimeId)!.Auditorium.CinemaId);
    }

    // ------------------------------------------------------------------ trả bằng ví tại quầy (SPEC 3.6-12)
    [Fact]
    public async Task Pos_WalletPayment_ForLookedUpMember()
    {
        var cashier = UserDAL.GetByEmail("cashier@fpolycinema.vn")!;
        var shift = PosShiftDAL.GetOpenByUser(cashier.Id)!;
        var ctx = new PosContext(cashier.Id, cashier.Role, shift.CinemaId, shift);
        var bills = Bills();
        var drink = DrinkDAL.GetAll().First(d => d.Active && !d.IsSoldOut);
        await bills.UpdateQuantity(ctx, drink.Id, "incr");
        // Chưa tra cứu thành viên thì không trả bằng ví được
        Assert.Equal(ErrorCodes.ValidationFailed, (await Throws<BusinessRuleException>(() => bills.Checkout(ctx, PaymentMethod.Wallet, null))).Code);
        var member = Customer("khach2@fpolycinema.vn");
        DBUtil.ExecuteNonQuery("UPDATE Users SET WalletBalance = 500000 WHERE Id = @1", new List<object> { member.Id });
        await bills.LookupMember(ctx, member.MemberCode);
        var res = await bills.Checkout(ctx, PaymentMethod.Wallet, null);
        var b = BillDAL.GetById(res.BillId)!;
        Assert.Equal(BillStatus.Finish, b.Status);
        Assert.Equal(500000 - b.Total, UserDAL.GetById(member.Id)!.WalletBalance);
        Assert.Contains(WalletTransactionDAL.ForUser(member.Id), w => w.BillId == b.Id && w.Amount == -b.Total);
    }
}
