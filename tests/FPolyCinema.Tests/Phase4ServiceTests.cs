using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Hubs;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.Utils;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FPolyCinema.Tests;

/// <summary>Hub giả: ghi lại mọi sự kiện đã phát để kiểm SEAT_MAP_UPDATED chỉ gồm các ghế thay đổi.</summary>
public sealed class RecordingHub : IHubContext<CinemaHub>
{
    public List<(string Group, string Method, object? Payload)> Sent { get; } = new();
    public IHubClients Clients => new HubClients(this);
    public IGroupManager Groups => throw new NotSupportedException();

    private sealed class HubClients : IHubClients
    {
        private readonly RecordingHub _hub;
        public HubClients(RecordingHub hub) => _hub = hub;
        public IClientProxy Group(string groupName) => new Proxy(_hub, groupName);
        public IClientProxy All => new Proxy(_hub, "*");
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => All;
        public IClientProxy Client(string connectionId) => All;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => All;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Group(groupName);
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => All;
        public IClientProxy User(string userId) => All;
        public IClientProxy Users(IReadOnlyList<string> userIds) => All;
    }

    private sealed class Proxy : IClientProxy
    {
        private readonly RecordingHub _hub;
        private readonly string _group;
        public Proxy(RecordingHub hub, string group) { _hub = hub; _group = group; }
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            lock (_hub.Sent) _hub.Sent.Add((_group, method, args.FirstOrDefault()));
            return Task.CompletedTask;
        }
    }
}

/// <summary>CSDL mẫu riêng cho test bán hàng Phase 4 (test ghi nhiều hóa đơn nên không dùng chung CSDL mẫu của các test kiểm dữ liệu).</summary>
public sealed class Phase4DatabaseFixture : IDisposable
{
    public static readonly DateTime Now = new(2026, 11, 6, 14, 0, 0);

    public Phase4DatabaseFixture()
    {
        ConnectionString = TestDatabase.NewConnectionString("P4Test");
        WebRoot = Path.Combine(Path.GetTempPath(), "fpc-p4-" + Guid.NewGuid().ToString("N"));
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
/// Test tích hợp Phase 4 trên CSDL mẫu: giữ ghế (khóa theo suất, tranh chấp, hết hạn, bộ quét), luật ghế đôi, xe lăn, bảo trì, ghế mồ côi kèm nhật ký;
/// hóa đơn tạm tính (đối tượng vé theo luật, bắp nước và kho, voucher, điểm); thanh toán tại quầy; hủy hóa đơn đã thanh toán bằng PIN; đóng ca mù;
/// thống kê và đối soát (SettlementCalculator khớp stored procedure).
/// </summary>
[Collection("Database")]
public class Phase4ServiceTests : IClassFixture<Phase4DatabaseFixture>
{
    private readonly FakeClock _clock = new(Phase4DatabaseFixture.Now.AddMinutes(1));
    private readonly CinemaRules _rules = new();
    private readonly RecordingHub _hub = new();

    public Phase4ServiceTests(Phase4DatabaseFixture f) => DBUtil.Init(f.ConnectionString);

    private RealtimeNotifier Notifier() => new(_hub, NullLogger<RealtimeNotifier>.Instance);
    private BillService Bills() => new(_clock, _rules, new BillStatusService(_clock), Notifier(), new InventoryService(), new LoyaltyService(_rules));
    private SeatHoldService Holds() => new(_clock, _rules, Bills(), Notifier());
    private ShiftService Shifts() => new(_clock, Bills());

    private static int Cinema1 => CinemaDAL.GetActive().OrderBy(c => c.Id).First().Id;

    private PosContext Cashier()
    {
        var u = UserDAL.GetByEmail("cashier@fpolycinema.vn")!;
        return Shifts().Require(u.Id, u.Role, PosShiftDAL.GetOpenByUser(u.Id)?.TerminalCode);
    }

    /// <summary>Mở ca mới cho một nhân viên khác ở máy trống (mỗi test tự có hóa đơn riêng).</summary>
    private PosContext NewShift(string email)
    {
        var u = UserDAL.GetByEmail(email)!;
        var open = PosShiftDAL.GetOpenByUser(u.Id);
        if (open != null) return new PosContext(u.Id, u.Role, open.CinemaId, open);
        var used = PosShiftDAL.GetAll().Where(s => s.Status == ShiftStatus.Open && s.CinemaId == Cinema1).Select(s => s.TerminalCode).ToHashSet();
        var terminal = ShiftService.Terminals().First(t => !used.Contains(t));
        var shift = Shifts().Open(u.Id, Cinema1, terminal, 1_000_000);
        return new PosContext(u.Id, u.Role, Cinema1, shift);
    }

    private PosShowtimeRow Show(Func<PosShowtimeRow, bool>? where = null, int minFree = 30)
    {
        var now = _clock.Now;
        return ShowtimeDAL.ForSale(Cinema1, now, now.AddDays(2), now)
            .Where(s => s.FeatureStartTime > now.AddHours(1) && s.FreeSeats >= minFree && (where == null || where(s)))
            .OrderBy(s => s.StartTime).First();
    }

    /// <summary>k ghế thường hoặc VIP liền nhau ở đầu một đoạn còn trống hoàn toàn và dài hơn k + 1 (không tạo ghế lẻ).</summary>
    private static List<SeatStateRow> FreeRun(int showtimeId, int k, int skipRuns = 0)
    {
        var states = ShowtimeSeatDAL.GetStates(showtimeId);
        var slots = states.Select(s => new SeatSlot(s.SeatId, s.RowIndex, s.ColIndex, s.Label, s.SeatType, false)).ToList();
        var byId = states.ToDictionary(s => s.SeatId);
        var runs = OrphanSeatValidator.Segments(slots)
            .Where(seg => seg.Count >= k + 2 && seg.All(x => byId[x.SeatId].Status == ShowtimeSeatStatus.Available && !byId[x.SeatId].IsMaintenance))
            .ToList();
        return runs[skipRuns % runs.Count].Take(k).Select(x => byId[x.SeatId]).ToList();
    }

    private static HoldOptions Pos(PosContext ctx, bool wheelchair = false) => new(BillChannel.Pos, ctx.Role, ctx.UserId, WheelchairConfirmed: wheelchair);

    private static async Task<T> Throws<T>(Func<Task> act) where T : Exception => await Assert.ThrowsAsync<T>(act);

    // ------------------------------------------------------------------ giữ ghế (SPEC 3.5)
    [Fact]
    public async Task Hold_FirstHoldCreatesWaitingBill_SeatsPointToBill_EventHasOnlyChangedSeats()
    {
        var ctx = NewShift("shift@fpolycinema.vn");
        var bills = Bills();
        var show = Show();
        var seats = FreeRun(show.ShowtimeId, 2);
        var bill = await bills.Ensure(ctx);
        Assert.Equal(BillStatus.Waiting, bill.Status);
        Assert.Equal(ctx.Shift.Id, bill.PosShiftId);

        _hub.Sent.Clear();
        var r = await Holds().Hold(bill.Id, show.ShowtimeId, seats.Select(s => s.SeatId).ToList(), Pos(ctx));
        Assert.Equal(_clock.Now.AddMinutes(_rules.SeatHoldMinutes), r.Bill.HoldExpiresAt);
        Assert.Equal(show.ShowtimeId, r.Bill.ShowtimeId);
        var held = ShowtimeSeatDAL.HeldByBill(bill.Id);
        Assert.Equal(seats.Select(s => s.SeatId).OrderBy(x => x), held.Select(h => h.SeatId).OrderBy(x => x));
        Assert.All(held, h => Assert.Equal(r.Bill.HoldExpiresAt, h.HoldExpiresAt));
        var ev = Assert.Single(_hub.Sent);
        Assert.Equal(("showtime_" + show.ShowtimeId, RealtimeEvents.SeatMapUpdated), (ev.Group, ev.Method));
        Assert.Contains("changes", System.Text.Json.JsonSerializer.Serialize(ev.Payload));
        Assert.Equal(2, System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(ev.Payload)).RootElement.GetProperty("changes").GetArrayLength());
        Assert.True(r.Bill.TicketSubtotal > 0);

        // Đổi ghế không gia hạn; gia hạn được đúng một lần
        _clock.Advance(TimeSpan.FromMinutes(2));
        var r2 = await Holds().Hold(bill.Id, show.ShowtimeId, new[] { seats[0].SeatId }, Pos(ctx));
        Assert.Equal(r.Bill.HoldExpiresAt, r2.Bill.HoldExpiresAt);
        await bills.ExtendHold(ctx);
        Assert.Equal(r.Bill.HoldExpiresAt!.Value.AddMinutes(_rules.SeatHoldExtendMinutes), BillDAL.GetById(bill.Id)!.HoldExpiresAt);
        await Throws<BusinessRuleException>(() => bills.ExtendHold(ctx));
        await bills.CancelWaiting(bill.Id, ctx.Role, ctx.CinemaId);
        Assert.Empty(ShowtimeSeatDAL.HeldByBill(bill.Id));
        Assert.Equal(CancelReason.CancelledBeforePayment, BillDAL.GetById(bill.Id)!.CancelReason);
    }

    [Fact]
    public async Task Hold_ConcurrentRequestsForSameSeat_OnlyOneWins_OtherGetsSeatTaken()
    {
        var a = NewShift("shift@fpolycinema.vn");
        var b = NewShift("manager@fpolycinema.vn");
        var show = Show();
        var seats = FreeRun(show.ShowtimeId, 2, 1).Select(s => s.SeatId).ToList();
        var billA = await Bills().Ensure(a);
        var billB = await Bills().Ensure(b);
        var results = await Task.WhenAll(
            Task.Run(async () => { try { await Holds().Hold(billA.Id, show.ShowtimeId, seats, Pos(a)); return (string?)null; } catch (BusinessRuleException ex) { return ex.Code; } }),
            Task.Run(async () => { try { await Holds().Hold(billB.Id, show.ShowtimeId, seats, Pos(b)); return (string?)null; } catch (BusinessRuleException ex) { return ex.Code; } }));
        Assert.Single(results, r => r == null);
        Assert.Single(results, r => r == ErrorCodes.SeatTaken);
        var owners = seats.Select(id => ShowtimeSeatDAL.GetById(show.ShowtimeId, id)!.HoldBillId).Distinct().ToList();
        Assert.Single(owners);
        await Bills().CancelWaiting(billA.Id, a.Role, a.CinemaId);
        await Bills().CancelWaiting(billB.Id, b.Role, b.CinemaId);
    }

    [Fact]
    public async Task Hold_ExpiredHoldIsAvailableToOthers_SweeperCancelsExpired()
    {
        var a = NewShift("shift@fpolycinema.vn");
        var show = Show();
        var seats = FreeRun(show.ShowtimeId, 2, 2).Select(s => s.SeatId).ToList();
        var billA = await Bills().Ensure(a);
        await Holds().Hold(billA.Id, show.ShowtimeId, seats, Pos(a));

        _clock.Advance(TimeSpan.FromMinutes(_rules.SeatHoldMinutes) + TimeSpan.FromSeconds(1));
        var states = ShowtimeSeatDAL.GetStates(show.ShowtimeId).Where(s => seats.Contains(s.SeatId)).ToList();
        Assert.All(states, s => Assert.Equal(SeatDisplayStatus.Available, SeatMapService.Effective(s, _clock.Now)));
        var b = NewShift("manager@fpolycinema.vn");
        var billB = await Bills().Ensure(b);
        await Holds().Hold(billB.Id, show.ShowtimeId, seats, Pos(b));
        Assert.All(seats, id => Assert.Equal(billB.Id, ShowtimeSeatDAL.GetById(show.ShowtimeId, id)!.HoldBillId));

        // Hóa đơn A hết hạn: bộ quét hủy EXPIRED (tác nhân hệ thống)
        int n = await Holds().SweepExpired();
        Assert.True(n >= 1);
        var expired = BillDAL.GetById(billA.Id)!;
        Assert.Equal((BillStatus.Cancel, CancelReason.Expired), (expired.Status, expired.CancelReason));
        await Bills().CancelWaiting(billB.Id, b.Role, b.CinemaId);
    }

    [Fact]
    public async Task Hold_SalesClosedSweetboxWheelchairMaintenance()
    {
        var ctx = NewShift("shift@fpolycinema.vn");
        var bills = Bills();
        var bill = await bills.Ensure(ctx);
        var holds = Holds();

        // Hết giờ bán tại quầy: FeatureStartTime + PosSalesCutoffMinutes
        var show = Show();
        var free = FreeRun(show.ShowtimeId, 1, 3);
        var saved = _clock.Now;
        _clock.Now = show.FeatureStartTime.AddMinutes(_rules.PosSalesCutoffMinutes);
        var closed = await Throws<BusinessRuleException>(() => holds.Hold(bill.Id, show.ShowtimeId, free.Select(s => s.SeatId).ToList(), Pos(ctx)));
        Assert.Equal(ErrorCodes.SalesClosed, closed.Code);
        _clock.Now = saved;

        // Suất có ghế đôi và chỗ xe lăn
        var withPairs = ShowtimeDAL.ForSale(Cinema1, saved, saved.AddDays(2), saved).Where(s => s.FeatureStartTime > saved.AddHours(1))
            .First(s => ShowtimeSeatDAL.GetStates(s.ShowtimeId).Any(x => x.SeatType == SeatType.Sweetbox && x.Status == ShowtimeSeatStatus.Available)
                     && ShowtimeSeatDAL.GetStates(s.ShowtimeId).Any(x => x.SeatType == SeatType.Wheelchair && x.Status == ShowtimeSeatStatus.Available));
        var states = ShowtimeSeatDAL.GetStates(withPairs.ShowtimeId);
        var sb = states.Where(x => x.SeatType == SeatType.Sweetbox && x.Status == ShowtimeSeatStatus.Available).GroupBy(x => x.PairGroupId).First(g => g.Count() == 2).ToList();
        var pair = await Throws<BusinessRuleException>(() => holds.Hold(bill.Id, withPairs.ShowtimeId, new[] { sb[0].SeatId }, Pos(ctx)));
        Assert.Equal(ErrorCodes.SweetboxPair, pair.Code);
        Assert.Contains("được bán theo cặp", pair.Message);
        await holds.Hold(bill.Id, withPairs.ShowtimeId, sb.Select(x => x.SeatId).ToList(), Pos(ctx));

        var wc = states.First(x => x.SeatType == SeatType.Wheelchair && x.Status == ShowtimeSeatStatus.Available);
        var need = await Throws<BusinessRuleException>(() => holds.Hold(bill.Id, withPairs.ShowtimeId, new[] { wc.SeatId }, Pos(ctx)));
        Assert.Equal(ErrorCodes.WheelchairConfirmRequired, need.Code);
        await holds.Hold(bill.Id, withPairs.ShowtimeId, new[] { wc.SeatId }, Pos(ctx, wheelchair: true));

        // Ghế bảo trì không giữ được ở mọi suất
        var maint = FreeRun(withPairs.ShowtimeId, 1, 0)[0];
        SeatDAL.SetMaintenance(maint.SeatId, true);
        try
        {
            var m = await Throws<BusinessRuleException>(() => holds.Hold(bill.Id, withPairs.ShowtimeId, new[] { maint.SeatId }, Pos(ctx)));
            Assert.Equal(ErrorCodes.SeatTaken, m.Code);
            Assert.Contains("bảo trì", m.Message);
        }
        finally { SeatDAL.SetMaintenance(maint.SeatId, false); }
        await bills.CancelWaiting(bill.Id, ctx.Role, ctx.CinemaId);
    }

    [Fact]
    public async Task Hold_OrphanSeatBlocked_PosStaffOverrideWithReasonIsAudited()
    {
        var ctx = NewShift("shift@fpolycinema.vn");
        var bill = await Bills().Ensure(ctx);
        var show = Show();
        var run = FreeRun(show.ShowtimeId, 3, 4);
        var middle = new[] { run[1].SeatId };
        var err = await Throws<BusinessRuleException>(() => Holds().Hold(bill.Id, show.ShowtimeId, middle, Pos(ctx)));
        Assert.Equal(ErrorCodes.OrphanSeat, err.Code);
        Assert.Contains(run[0].Label, err.Message);
        var noReason = await Throws<BusinessRuleException>(() => Holds().Hold(bill.Id, show.ShowtimeId, middle, Pos(ctx) with { OverrideOrphan = true }));
        Assert.Equal(ErrorCodes.OrphanSeat, noReason.Code);
        var r = await Holds().Hold(bill.Id, show.ShowtimeId, middle, Pos(ctx) with { OverrideOrphan = true, OverrideReason = "Khách cần ngồi đúng ghế này" });
        Assert.True(r.OrphanOverridden);
        Assert.Contains(AuditLogDAL.GetAll(), l => l.Action == AuditAction.OrphanSeatOverride && l.EntityId == bill.Id.ToString() && l.Detail!.Contains("Khách cần ngồi"));
        // Kênh online (khách) không được bỏ qua luật dù gửi kèm lý do
        await Holds().Hold(bill.Id, show.ShowtimeId, Array.Empty<int>(), Pos(ctx));
        var online = await Throws<BusinessRuleException>(() => Holds().Hold(bill.Id, show.ShowtimeId, middle,
            new HoldOptions(BillChannel.Online, Roles.Customer, null, OverrideOrphan: true, OverrideReason: "Thử bỏ qua luật")));
        Assert.Equal(ErrorCodes.OrphanSeat, online.Code);
        await Bills().CancelWaiting(bill.Id, ctx.Role, ctx.CinemaId);
    }

    // ------------------------------------------------------------------ hóa đơn tạm tính và thanh toán (SPEC 3.1, 3.4, 3.6, 3.11, 3.13)
    [Fact]
    public async Task Cart_AgeRules_GuardianRequired_ForChildTicketsAtPos()
    {
        var ctx = NewShift("shift@fpolycinema.vn");
        var bills = Bills();
        var bill = await bills.Ensure(ctx);
        var t18 = Show(s => s.AgeRating is AgeRating.T16 or AgeRating.T18, 10);
        var seat = FreeRun(t18.ShowtimeId, 1, 5)[0];
        await Holds().Hold(bill.Id, t18.ShowtimeId, new[] { seat.SeatId }, Pos(ctx));
        var age = await Throws<BusinessRuleException>(() => bills.SetAudience(ctx, seat.SeatId, AudienceType.Child13To15));
        Assert.Equal(ErrorCodes.AgeRestricted, age.Code);
        var senior = await bills.SetAudience(ctx, seat.SeatId, AudienceType.Senior);
        Assert.Equal(AudienceType.Senior, senior.Tickets.Single().AudienceType);
        Assert.True(senior.Tickets.Single().ListPrice < senior.TicketSubtotal + 1);

        var k = ShowtimeDAL.ForSale(Cinema1, _clock.Now, _clock.Now.AddDays(3), _clock.Now)
            .FirstOrDefault(s => s.AgeRating == AgeRating.K && s.FeatureStartTime > _clock.Now.AddHours(1) && s.EndTime.TimeOfDay < _rules.ChildU13LatestEnd && s.EndTime.Date == s.StartTime.Date && s.FreeSeats > 10);
        if (k != null)
        {
            var kSeat = FreeRun(k.ShowtimeId, 1, 0)[0];
            await Holds().Hold(bill.Id, k.ShowtimeId, new[] { kSeat.SeatId }, Pos(ctx));
            var cart = await bills.SetAudience(ctx, kSeat.SeatId, AudienceType.ChildU13);
            Assert.True(cart.GuardianRequired);
            Assert.Equal(ErrorCodes.GuardianRequired, cart.Tickets.Single().WarningCode);
            var g = await Throws<BusinessRuleException>(() => bills.Checkout(ctx, PaymentMethod.Cash, 1_000_000));
            Assert.Equal(ErrorCodes.GuardianRequired, g.Code);
            cart = await bills.SetGuardian(ctx, true);
            Assert.Null(cart.Tickets.Single().WarningCode);
        }
        await bills.CancelWaiting(bill.Id, ctx.Role, ctx.CinemaId);
    }

    [Fact]
    public async Task Checkout_Cash_CreatesTicketsBooksSeatsConsumesStockEarnsPoints_ThenVoidWithPin()
    {
        var ctx = Cashier();
        var bills = Bills();
        var show = Show(s => s.AgeRating is AgeRating.P or AgeRating.T13 or AgeRating.T16 or AgeRating.T18, 20);
        var seats = FreeRun(show.ShowtimeId, 2, 6).Select(s => s.SeatId).ToList();
        var bill = await bills.Ensure(ctx);
        await Holds().Hold(bill.Id, show.ShowtimeId, seats, Pos(ctx));

        var cinemaDrinks = RawMaterialDAL.StockLines(ctx.CinemaId).Select(l => l.DrinkId).Distinct().ToList();
        var drink = DrinkDAL.GetAll().First(d => d.Active && !d.IsSoldOut && cinemaDrinks.Contains(d.Id));
        await bills.UpdateQuantity(ctx, drink.Id, "incr");
        await bills.UpdateQuantity(ctx, drink.Id, "incr");
        var khach = UserDAL.GetByEmail("khach@fpolycinema.vn")!;
        await bills.LookupMember(ctx, khach.MemberCode);
        var bad = await Throws<BusinessRuleException>(() => bills.ApplyPoints(ctx, khach.LoyaltyPoints + 1));
        Assert.Equal(ErrorCodes.PointsInvalid, bad.Code);
        var cart = await bills.ApplyPoints(ctx, 10);
        Assert.Equal(10_000, cart.PointsDiscount);
        var voucher = await Throws<BusinessRuleException>(() => bills.ApplyVoucher(ctx, "KHONG-CO-MA"));
        Assert.Equal(ErrorCodes.VoucherInvalid, voucher.Code);

        var stockBefore = RawMaterialDAL.StockLines(ctx.CinemaId, new[] { drink.Id }).ToDictionary(l => l.RawMaterialId, l => l.Available);
        var low = await Throws<BusinessRuleException>(() => bills.Checkout(ctx, PaymentMethod.Cash, cart.Total - 1));
        Assert.Equal(ErrorCodes.ValidationFailed, low.Code);
        var res = await bills.Checkout(ctx, PaymentMethod.Cash, cart.Total + 7_000);
        Assert.Equal(7_000, res.Change);

        var paid = BillDAL.GetById(bill.Id)!;
        Assert.Equal((BillStatus.Finish, FnbStatus.Preparing, BillCheckInStatus.NotCheckedIn), (paid.Status, paid.FnbStatus, paid.CheckInStatus));
        Assert.NotNull(paid.PickupNumber);
        var tickets = TicketDAL.GetByBill(bill.Id);
        Assert.Equal(2, tickets.Count);
        Assert.Equal(2, tickets.Select(t => t.QrCode).Distinct().Count());
        Assert.All(tickets, t => Assert.Matches("^TK[A-Z0-9]{12}$", t.QrCode));
        Assert.All(seats, id => Assert.Equal(ShowtimeSeatStatus.Booked, ShowtimeSeatDAL.GetById(show.ShowtimeId, id)!.Status));
        var details = BillDetailDAL.GetByBill(bill.Id);
        Assert.Equal(paid.Total, tickets.Sum(t => t.FinalPrice) + details.Sum(d => d.LineFinalAmount));
        Assert.Equal(paid.DiscountTotal, tickets.Sum(t => t.ListPrice - t.FinalPrice) + details.Sum(d => d.Price * d.Quantity - d.LineFinalAmount));
        var stockAfter = RawMaterialDAL.StockLines(ctx.CinemaId, new[] { drink.Id }).ToDictionary(l => l.RawMaterialId, l => l.Available);
        foreach (var line in RawMaterialDAL.StockLines(ctx.CinemaId, new[] { drink.Id }))
            Assert.Equal(stockBefore[line.RawMaterialId] - line.PerPortion * 2, stockAfter[line.RawMaterialId]);
        Assert.Contains(InventoryTransactionDAL.GetAll(), t => t.BillId == bill.Id && t.Type == InventoryTxnType.Consume);
        var pay = Assert.Single(PaymentDAL.RowsByBill(bill.Id));
        Assert.Equal((PaymentMethod.Cash, ctx.Shift.Id, paid.Total), (pay.Method, pay.PosShiftId!.Value, pay.Amount));
        var khachAfter = UserDAL.GetById(khach.Id)!;
        var earned = LoyaltyCalculator.EarnPoints(paid.Total, khach.Tier, _rules);
        Assert.Equal(khach.LoyaltyPoints - 10 + earned, khachAfter.LoyaltyPoints);

        // Hủy hóa đơn đã thanh toán: không có PIN thì APPROVAL_REQUIRED (controller), PIN sai đếm lượt, đủ lượt thì khóa
        var pins = new ManagerPinService(_clock, _rules);
        var sup = UserDAL.GetByEmail("shift@fpolycinema.vn")!;
        var wrong = Assert.Throws<BusinessRuleException>(() => pins.Verify(sup.Id, "000000", ctx.CinemaId));
        Assert.Equal(ErrorCodes.PinInvalid, wrong.Code);
        Assert.Contains("Còn 4 lần thử", wrong.Message);
        Assert.Equal(ErrorCodes.ApprovalRequired, (await Throws<BusinessRuleException>(() => bills.VoidPaid(bill.Id, "Khách đổi ý", UserDAL.GetById(ctx.UserId)!, ctx.UserId, ctx.CinemaId, ctx.Shift.Id))).Code);
        var approver = pins.Verify(sup.Id, "246810", ctx.CinemaId);
        Assert.Equal(0, UserDAL.GetById(sup.Id)!.PinFailedCount);
        // Hoàn tiền mặt phải trừ vào két của một ca đang mở (hủy từ /admin/bills khi quản lý không có ca)
        Assert.Equal(ErrorCodes.ShiftNotOpen, (await Throws<BusinessRuleException>(() => bills.VoidPaid(bill.Id, "Khách đổi ý trước giờ chiếu", approver, ctx.UserId, ctx.CinemaId, null))).Code);
        Assert.Equal(BillStatus.Finish, BillDAL.GetById(bill.Id)!.Status);
        await bills.VoidPaid(bill.Id, "Khách đổi ý trước giờ chiếu", approver, ctx.UserId, ctx.CinemaId, ctx.Shift.Id);

        var voided = BillDAL.GetById(bill.Id)!;
        Assert.Equal((BillStatus.Cancel, CancelReason.VoidedByPos, BillCheckInStatus.Voided), (voided.Status, voided.CancelReason, voided.CheckInStatus));
        Assert.All(TicketDAL.GetByBill(bill.Id), t => Assert.Equal(TicketCheckInStatus.Voided, t.CheckInStatus));
        Assert.All(seats, id => Assert.Equal(ShowtimeSeatStatus.Available, ShowtimeSeatDAL.GetById(show.ShowtimeId, id)!.Status));
        var refund = PaymentDAL.RowsByBill(bill.Id).Single(p => p.Direction == PaymentDirection.Refund);
        Assert.Equal((paid.Total, ctx.Shift.Id), (refund.Amount, refund.PosShiftId!.Value));
        Assert.Equal(khach.LoyaltyPoints, UserDAL.GetById(khach.Id)!.LoyaltyPoints);
        Assert.Contains(AuditLogDAL.GetAll(), l => l.Action == AuditAction.VoidBill && l.EntityId == bill.Id.ToString());
        var approval = SupervisorApprovalDAL.RowsByBill(bill.Id).Single();
        Assert.Equal((ApprovalStatus.Approved, ApprovalMethod.Pin), (approval.Status, approval.Method));
        // Hủy lần hai bị máy trạng thái chặn
        Assert.Equal(ErrorCodes.InvalidTransition, (await Throws<BusinessRuleException>(() => bills.VoidPaid(bill.Id, "Hủy lần nữa", approver, ctx.UserId, ctx.CinemaId, ctx.Shift.Id))).Code);
    }

    [Fact]
    public async Task Void_BlockedWhenATicketCheckedIn_PinLocksAfterFiveFailures()
    {
        var ctx = Cashier();
        var bills = Bills();
        var show = Show(s => s.AgeRating is AgeRating.P or AgeRating.T13, 10);
        var seat = FreeRun(show.ShowtimeId, 1, 7)[0];
        var bill = await bills.Ensure(ctx);
        await Holds().Hold(bill.Id, show.ShowtimeId, new[] { seat.SeatId }, Pos(ctx));
        await bills.Checkout(ctx, PaymentMethod.VietQrSim, null);
        Assert.Equal(PaymentMethod.VietQrSim, PaymentDAL.RowsByBill(bill.Id).Single().Method);
        var t = TicketDAL.GetByBill(bill.Id).Single();
        t.CheckInStatus = TicketCheckInStatus.CheckedIn;
        TicketDAL.Update(t);
        var mgr = UserDAL.GetByEmail("manager@fpolycinema.vn")!;
        var blocked = await Throws<BusinessRuleException>(() => bills.VoidPaid(bill.Id, "Khách muốn hủy", mgr, ctx.UserId, ctx.CinemaId, ctx.Shift.Id));
        Assert.Contains("vào cửa", blocked.Message);

        var pins = new ManagerPinService(_clock, _rules);
        BusinessRuleException? last = null;
        for (int i = 0; i < _rules.ManagerPinMaxAttempts; i++) last = Assert.Throws<BusinessRuleException>(() => pins.Verify(mgr.Id, "999999", ctx.CinemaId));
        Assert.Equal(ErrorCodes.PinLocked, last!.Code);
        Assert.Equal(ErrorCodes.PinLocked, Assert.Throws<BusinessRuleException>(() => pins.Verify(mgr.Id, "246810", ctx.CinemaId)).Code);
        _clock.Advance(TimeSpan.FromMinutes(_rules.ManagerPinLockMinutes));
        Assert.Equal(mgr.Id, pins.Verify(mgr.Id, "246810", ctx.CinemaId).Id);
    }

    [Fact]
    public async Task Stock_InsufficientMaterialBlocksAddingDrink()
    {
        var ctx = NewShift("shift@fpolycinema.vn");
        var bills = Bills();
        var line = RawMaterialDAL.StockLines(ctx.CinemaId).OrderBy(l => l.DrinkId).First();
        var drink = DrinkDAL.GetById(line.DrinkId)!;
        var raw = RawMaterialDAL.GetById(line.RawMaterialId)!;
        var keep = raw.ActualStock;
        raw.ActualStock = raw.ReservedStock + line.PerPortion * 1.5m;
        RawMaterialDAL.Update(raw);
        try
        {
            var portions = new InventoryService().Portions(ctx.CinemaId, new[] { drink });
            Assert.True(portions[drink.Id] <= 1);
            await bills.UpdateQuantity(ctx, drink.Id, "incr");
            var err = await Throws<BusinessRuleException>(() => bills.UpdateQuantity(ctx, drink.Id, "incr"));
            Assert.Equal(ErrorCodes.StockInsufficient, err.Code);
            Assert.Contains("tạm hết nguyên liệu", err.Message);
        }
        finally
        {
            raw = RawMaterialDAL.GetById(line.RawMaterialId)!;
            raw.ActualStock = keep;
            RawMaterialDAL.Update(raw);
            var b = await bills.Current(ctx);
            if (b != null) await bills.CancelWaiting(b.Id, ctx.Role, ctx.CinemaId);
        }
    }


    // ------------------------------------------------------------------ mở ca tại máy (SPEC 3.6, S3.6-08)
    [Fact]
    public void Shift_RequiresTerminalCookieAndOpeningFloat()
    {
        var u = UserDAL.GetByEmail("cashier@fpolycinema.vn")!;
        var open = PosShiftDAL.GetOpenByUser(u.Id)!;
        // Trình duyệt chưa có cookie mã máy hoặc mang mã máy khác thì không bán được
        Assert.Null(Shifts().Resolve(u.Id, u.Role, null));
        var other = ShiftService.Terminals().First(t => t != open.TerminalCode);
        Assert.Equal(ErrorCodes.ShiftNotOpen, Assert.Throws<BusinessRuleException>(() => Shifts().Require(u.Id, u.Role, other)).Code);
        Assert.Equal(open.Id, Shifts().Require(u.Id, u.Role, open.TerminalCode).Shift.Id);
        // Đóng ca vẫn tìm được ca của chính mình
        Assert.Equal(open.Id, ShiftService.ResolveOwn(u.Id, u.Role)!.Shift.Id);
        // Mở ca phải nhập tiền đầu ca
        var supervisor = UserDAL.GetByEmail("shift@fpolycinema.vn")!;
        var ex = Assert.Throws<BusinessRuleException>(() => Shifts().Open(supervisor.Id, Cinema1, other, null));
        Assert.Equal(ErrorCodes.ValidationFailed, ex.Code);
        Assert.Contains("OpeningCashFloat", ((Dictionary<string, string>)ex.Details!).Keys);
    }

    // ------------------------------------------------------------------ đóng ca mù (SPEC 3.6)
    [Fact]
    public async Task CloseShift_BlockedByWaitingBill_ThenBlindCountComputesExpectedAndDiscrepancy()
    {
        var u = UserDAL.GetByEmail("cashier2@fpolycinema.vn")!;
        var open = PosShiftDAL.GetOpenByUser(u.Id)!;
        var ctx = new PosContext(u.Id, u.Role, open.CinemaId, open);
        var bills = Bills();
        var drink = DrinkDAL.GetAll().First(d => d.Active && !d.IsSoldOut);
        await bills.UpdateQuantity(ctx, drink.Id, "incr");
        var blocked = Assert.Throws<BusinessRuleException>(() => Shifts().Close(ctx, new Dictionary<long, int>()));
        Assert.Equal(ErrorCodes.ShiftHasWaitingBills, blocked.Code);
        await bills.Checkout(ctx, PaymentMethod.Cash, 1_000_000);

        var (cashIn, cashRefund) = PaymentDAL.CashOfShift(open.Id);
        var counts = new Dictionary<long, int> { [500_000] = 2, [10_000] = 3 };
        var r = Shifts().Close(ctx, counts);
        Assert.Equal(1_030_000, r.Counted);
        Assert.Equal(open.OpeningCashFloat + cashIn - cashRefund, r.Expected);
        Assert.Equal(r.Counted - r.Expected, r.Discrepancy);
        var closed = PosShiftDAL.GetById(open.Id)!;
        Assert.Equal((ShiftStatus.Closed, r.Expected, r.Discrepancy), (closed.Status, closed.ExpectedCash!.Value, closed.Discrepancy!.Value));
        // Biên bản đã chốt không sửa được
        Assert.Equal(ErrorCodes.ShiftNotOpen, Assert.Throws<BusinessRuleException>(() => Shifts().Close(ctx, counts)).Code);
        Assert.Null(Shifts().Resolve(u.Id, u.Role, null));
    }

    // ------------------------------------------------------------------ thống kê, đối soát, quản lý hóa đơn (SPEC 3.12, 3.6)
    [Fact]
    public void Statistics_SettlementCalculatorMatchesStoredProcedure()
    {
        var stats = new StatisticsService(_clock, _rules);
        // Tuần của một vé đã bán (tuần n là [ngày khởi chiếu + 7(n − 1), ngày khởi chiếu + 7n) theo ngày vận hành)
        var ticket = TicketDAL.GetAll().First(t => t.CheckInStatus == TicketCheckInStatus.CheckedIn);
        var showtime = ShowtimeDAL.GetById(ticket.ShowtimeId)!;
        var movie = MovieDAL.GetById(showtime.MovieId)!;
        int week = (int)((PricingCalculator.OperatingDate(showtime.StartTime) - movie.ReleaseDate.Date).TotalDays / 7) + 1;
        var report = stats.Settlement(movie.Id, week, null);
        Assert.Equal(report.Result.TicketCount, report.Check.TicketCount);
        Assert.Equal(report.Result.GrossRevenue, report.Check.GrossRevenue);
        Assert.Equal(report.Result.BelowFloorTickets, report.Check.BelowFloorTickets);
        Assert.InRange(report.Check.NetRevenue - report.Result.NetRevenue, -2, 2);
        Assert.InRange(report.Check.DistributorAmount - report.Result.DistributorAmount, -2, 2);
        Assert.InRange(report.Check.FloorAbsorbedAmount - report.Result.FloorAbsorbedAmount, -2, 2);
        Assert.True(report.Result.TicketCount > 0);
    }

    [Fact]
    public void Statistics_DashboardSharesOccupancyAndSeatsNeedingChange()
    {
        var stats = new StatisticsService(_clock, _rules);
        var d = stats.Dashboard(null, "Toàn chuỗi");
        Assert.Equal(30, d.Revenue30.Count);
        Assert.Equal(2, d.VietnameseShares.Count);
        Assert.Equal(d.VietnameseShares.Sum(s => s.Total), d.ChainShare.Total);
        Assert.True(d.ChainShare.Vietnamese * 100 >= d.ChainShare.Total * 20);
        Assert.True(d.TodayKpi.TotalSeats > 0);
        var occ = stats.Occupancy(stats.Range(StatisticsService.Last7, null, null), Cinema1);
        Assert.All(occ, o => Assert.InRange(o.Sold, 0, o.Capacity));

        // Ghế đã bán ở suất tương lai chuyển sang bảo trì thì vào danh sách "Cần đổi ghế"
        var booked = ShowtimeSeatDAL.GetAll().First(s => s.Status == ShowtimeSeatStatus.Booked && ShowtimeDAL.GetById(s.ShowtimeId)!.StartTime > _clock.Now);
        SeatDAL.SetMaintenance(booked.SeatId, true);
        try
        {
            var seat = SeatDAL.GetById(booked.SeatId)!;
            Assert.Contains(StatisticDAL.SeatsNeedingChange(_clock.Now, null), a => a.Kind == "seat" && a.Title.Contains(seat.Label));
        }
        finally { SeatDAL.SetMaintenance(booked.SeatId, false); }
    }

    [Fact]
    public void Bills_SearchFiltersByStatusChannelStaffAndCode()
    {
        var q = new BillQueryService();
        var stats = new StatisticsService(_clock, _rules);
        var range = stats.Range(StatisticsService.Last30, null, null);
        var all = q.Search(range, null, null, null, null, null, 1, 20, out var total);
        Assert.True(total > 100);
        Assert.Equal(20, all.Count);
        q.Search(range, BillStatus.Cancel, null, null, null, null, 1, 50, out var cancelled);
        Assert.InRange(cancelled, 1, total - 1);
        var online = q.Search(range, null, BillChannel.Online, null, null, null, 1, 50, out _);
        Assert.All(online, b => Assert.Equal(BillChannel.Online, b.Channel));
        var cashier = UserDAL.GetByEmail("cashier@fpolycinema.vn")!;
        var mine = q.All(range, null, null, null, cashier.Id, null);
        Assert.NotEmpty(mine);
        Assert.All(mine, b => Assert.Equal(BillChannel.Pos, b.Channel));
        var one = all[0];
        var byCode = q.Search(range, null, null, null, null, one.Code[..12], 1, 50, out _);
        Assert.Contains(byCode, b => b.Id == one.Id);
        var detail = q.Detail(one.Id)!;
        Assert.Equal(one.Code, detail.Bill.Code);
        Assert.NotNull(q.FindByScan(one.Code.ToLowerInvariant()));
        var csv = System.Text.Encoding.UTF8.GetString(StatisticsService.Csv(new[] { "Mã", "Tên" }, new[] { new object?[] { "=1+1", "a,b" } }));
        Assert.Contains("'=1+1", csv);
        Assert.Contains("\"a,b\"", csv);
    }

    // ------------------------------------------------------------------ PIN quản lý (SPEC 2.5)
    [Fact]
    public void Staff_SupervisorNeedsSixDigitPin_StoredHashed_RoleChangeRemovesPin()
    {
        var svc = new StaffService(_clock, Options.Create(new AuthOptions()), new CapturingEmailSender());
        var noPin = Assert.Throws<BusinessRuleException>(() => svc.Create(new StaffInput("ca.test@fpolycinema.vn", "Quản Lý Ca Thử", null, Roles.ShiftManager, Cinema1)));
        Assert.Contains("ManagerPin", ((IDictionary<string, string>)noPin.Details!).Keys);
        Assert.Throws<BusinessRuleException>(() => svc.Create(new StaffInput("ca.test@fpolycinema.vn", "Quản Lý Ca Thử", null, Roles.ShiftManager, Cinema1, "12ab56")));
        Assert.Throws<BusinessRuleException>(() => svc.Create(new StaffInput("tn.test@fpolycinema.vn", "Thu Ngân Thử", null, Roles.POSCashier, Cinema1, "123456")));
        var u = svc.Create(new StaffInput("ca.test@fpolycinema.vn", "Quản Lý Ca Thử", null, Roles.ShiftManager, Cinema1, "135790"));
        Assert.NotEqual("135790", u.ManagerPinHash);
        Assert.True(PasswordUtil.Verify(u.ManagerPinHash, "135790"));
        Assert.Equal(u.Id, new ManagerPinService(_clock, _rules).Verify(u.Id, "135790", Cinema1).Id);
        // Sửa mà để trống PIN thì giữ PIN cũ; đổi sang thu ngân thì gỡ PIN
        svc.Update(u.Id, new StaffInput(u.Email, u.FullName, null, Roles.ShiftManager, Cinema1), 0);
        Assert.True(PasswordUtil.Verify(UserDAL.GetById(u.Id)!.ManagerPinHash, "135790"));
        svc.Update(u.Id, new StaffInput(u.Email, u.FullName, null, Roles.POSCashier, Cinema1), 0);
        Assert.Null(UserDAL.GetById(u.Id)!.ManagerPinHash);
    }
}
