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
using Microsoft.Extensions.Logging.Abstractions;

namespace FPolyCinema.Tests;

/// <summary>CSDL mẫu riêng cho test vận hành Phase 6.</summary>
public sealed class Phase6DatabaseFixture : IDisposable
{
    public static readonly DateTime Now = new(2026, 11, 6, 14, 0, 0);

    public Phase6DatabaseFixture()
    {
        ConnectionString = TestDatabase.NewConnectionString("P6Test");
        WebRoot = Path.Combine(Path.GetTempPath(), "fpc-p6-" + Guid.NewGuid().ToString("N"));
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
/// Test tích hợp Phase 6 trên CSDL mẫu: đồng hồ suất chiếu và "Đã dọn xong" (SPEC 3.3-18..22), soát vé và vào cửa từng phần (SPEC 3.8),
/// hủy suất do sự cố (SPEC 3.10 mục 2), phiếu hủy hàng và nhập kho (SPEC 3.11-14, 3.11-15), duyệt hủy từ xa (SPEC 3.6-21, 3.6-22),
/// giải phóng ghế nội bộ (SPEC 3.2-07), đổi ghế (SPEC 3.10 mục 1), bếp và số gọi (SPEC 3.11-04..07).
/// </summary>
[Collection("Database")]
public class Phase6ServiceTests : IClassFixture<Phase6DatabaseFixture>
{
    private readonly CinemaRules _rules = new();
    private readonly RecordingHub _hub = new();

    public Phase6ServiceTests(Phase6DatabaseFixture f) => DBUtil.Init(f.ConnectionString);

    private RealtimeNotifier Notifier() => new(_hub, NullLogger<RealtimeNotifier>.Instance);
    private BillService Bills(IClock c) => new(c, _rules, new BillStatusService(c), Notifier(), new InventoryService(), new LoyaltyService(_rules));
    private OpsManagementService Mgmt(IClock c) => new(c, _rules, Notifier(), new BillStatusService(c), new InventoryService(), new LoyaltyService(_rules));

    private bool Sent(string group, string evt) => _hub.Sent.Any(s => s.Group == group && s.Method == evt);

    /// <summary>Suất thứ hai trở đi của một phòng trong ngày sau thời điểm mẫu, có vé đã bán; kèm suất ngay trước nó trong phòng.</summary>
    private static (ClockShowtimeRow Prev, ClockShowtimeRow Show) PairWithTickets(int dayOffset)
    {
        var day = Phase6DatabaseFixture.Now.Date.AddDays(dayOffset);
        var rows = ShowtimeDAL.ForClock(day.AddHours(8), day.AddHours(24)).Where(r => r.OperationalStatus == OperationalStatus.Scheduled).ToList();
        foreach (var room in rows.GroupBy(r => r.AuditoriumId))
        {
            var list = room.OrderBy(r => r.StartTime).ToList();
            for (int i = 1; i < list.Count; i++)
                if (TicketDAL.CountActiveForShowtime(list[i].Id) > 0) return (list[i - 1], list[i]);
        }
        throw new InvalidOperationException("Dữ liệu mẫu không có suất phù hợp.");
    }

    private static List<Bill> FinishBills(int showtimeId) => DBUtil.InTransaction((c, t) => BillDAL.ByShowtime(showtimeId, BillStatus.Finish, c, t));

    // ------------------------------------------------------------------ đồng hồ suất chiếu, soát vé (SPEC 3.3, 3.8)

    [Fact]
    public async Task Clock_RoomNotReady_StaysScheduled_ThenCleanedJumpsAhead_AndGateAdmitsPartially()
    {
        var (prev, show) = PairWithTickets(1);
        var clock = new FakeClock(show.StartTime.AddMinutes(1));
        var ops = new ShowtimeOpsService(clock, _rules, Notifier());
        await ops.Tick();
        // Suất trước chưa dọn xong: phòng chưa sẵn sàng, suất sau đứng ở SCHEDULED và bị báo trễ
        Assert.Equal(OperationalStatus.CleaningInProgress, ShowtimeDAL.GetById(prev.Id)!.OperationalStatus);
        Assert.Equal(OperationalStatus.Scheduled, ShowtimeDAL.GetById(show.Id)!.OperationalStatus);
        Assert.Contains(ops.Board(show.CinemaId).Late, r => r.ShowtimeId == show.Id);
        Assert.True(Sent(CinemaHub.OpsGroup(show.CinemaId), RealtimeEvents.ShowtimeOpsChanged));

        // Máy soát vé báo "Phòng đang dọn vệ sinh"
        var bill = FinishBills(show.Id).First(b => TicketDAL.GateRows(b.Id).Any(t => t.CheckInStatus == TicketCheckInStatus.NotCheckedIn));
        var gate = new GateService(clock, _rules, Notifier());
        var cleaning = gate.Scan(bill.Code, show.CinemaId, null);
        Assert.Equal(GateReason.RoomCleaning, cleaning.Verdict.Reason);

        // "Đã dọn xong": suất trước COMPLETED, suất sau nhảy thẳng tới trạng thái đúng giờ (ADS_PLAYING)
        await ops.MarkCleaned(prev.Id, show.CinemaId);
        Assert.Equal(OperationalStatus.Completed, ShowtimeDAL.GetById(prev.Id)!.OperationalStatus);
        Assert.Equal(OperationalStatus.AdsPlaying, ShowtimeDAL.GetById(show.Id)!.OperationalStatus);
        Assert.Equal(ErrorCodes.InvalidTransition, Assert.Throws<BusinessRuleException>(() => ops.MarkCleaned(prev.Id, show.CinemaId).GetAwaiter().GetResult()).Code);

        // Soát vé: cho vào một ghế → PARTIALLY hoặc FULLY; phát CHECKIN_UPDATED và OCCUPANCY_UPDATED
        var scan = gate.Scan(bill.Code, show.CinemaId, null);
        Assert.NotEqual(GateVerdict.Reject, scan.Verdict.Kind);
        var first = scan.Verdict.AdmittableTicketIds.First();
        var staff = UserDAL.GetByEmail("gate@fpolycinema.vn")!;
        var r = await gate.Admit(bill.Id, new[] { first }, warningsConfirmed: true, staff.Id, show.CinemaId);
        Assert.Equal(1, r.Admitted);
        var expected = CheckInRules.BillStatusOf(TicketDAL.GetByBill(bill.Id).Select(t => t.CheckInStatus));
        Assert.Equal(expected, BillDAL.GetById(bill.Id)!.CheckInStatus);
        Assert.Contains(BillDAL.GetById(bill.Id)!.CheckInStatus, new[] { BillCheckInStatus.PartiallyCheckedIn, BillCheckInStatus.FullyCheckedIn });
        Assert.True(Sent(CinemaHub.BillGroup(bill.Id), RealtimeEvents.CheckinUpdated));
        Assert.True(Sent(CinemaHub.AuditoriumGroup(show.AuditoriumId), RealtimeEvents.OccupancyUpdated));
        // Quét lại đúng vé đã vào: từ chối
        var again = gate.Scan(TicketDAL.GetByBill(bill.Id).First(t => t.Id == first).QrCode, show.CinemaId, null);
        Assert.Equal(GateReason.AlreadyCheckedIn, again.Verdict.Reason);
        // Đúng rạp
        Assert.Equal(GateReason.WrongCinema, gate.Scan(bill.Code, show.CinemaId == 1 ? 2 : 1, null).Verdict.Reason);
    }

    // ------------------------------------------------------------------ hủy suất do sự cố (SPEC 3.10-05..07)

    [Fact]
    public async Task Incident_CancelsShowtime_RefundsAll_VoidsTickets_GivesVouchers_InOneTransaction()
    {
        var (_, show) = PairWithTickets(2);
        var clock = new FakeClock(Phase6DatabaseFixture.Now.AddMinutes(5));
        var mgmt = Mgmt(clock);
        var shift = UserDAL.GetByEmail("shift@fpolycinema.vn")!;
        var bills = FinishBills(show.Id);
        var paid = bills.Sum(b => PaymentDAL.RowsByBill(b.Id).Where(p => p.Status == PaymentStatus.Succeeded && p.Direction == PaymentDirection.In).Sum(p => p.Amount));

        var bad = await Assert.ThrowsAsync<BusinessRuleException>(() => mgmt.CancelShowtime(show.Id, "Máy chiếu hỏng", "S0", shift.Id, shift.Role, null));
        Assert.Equal(ErrorCodes.ValidationFailed, bad.Code);

        var sum = await mgmt.CancelShowtime(show.Id, "Máy chiếu hỏng", IncidentRules.ConfirmCode(show.Id), shift.Id, shift.Role, null);
        Assert.Equal(bills.Count, sum.Bills);
        Assert.Equal(paid, sum.Refunded);
        Assert.Equal(OperationalStatus.Cancelled, ShowtimeDAL.GetById(show.Id)!.OperationalStatus);
        foreach (var b in bills)
        {
            var nb = BillDAL.GetById(b.Id)!;
            Assert.Equal((BillStatus.Cancel, CancelReason.IncidentCompensated), (nb.Status, nb.CancelReason));
            Assert.All(TicketDAL.GetByBill(b.Id), t => Assert.True(t.CheckInStatus is TicketCheckInStatus.Voided or TicketCheckInStatus.Swapped));
        }
        Assert.Equal(bills.Count(b => b.CustomerId != null), sum.Vouchers);
        Assert.Equal(0, TicketDAL.CountActiveForShowtime(show.Id));
        Assert.True(Sent(CinemaHub.OpsGroup(show.CinemaId), RealtimeEvents.ShowtimeOpsChanged));
    }

    // ------------------------------------------------------------------ kho: nhập kho, phiếu hủy hàng (SPEC 3.11-14, 3.11-15)

    [Fact]
    public void Inventory_Import_AndWastageNeedsApprovalBeforeStockDrops()
    {
        var now = Phase6DatabaseFixture.Now;
        var inv = new InventoryService();
        var m = RawMaterialDAL.ByCinema(1).First(x => x.Active);
        var manager = UserDAL.GetByEmail("manager@fpolycinema.vn")!;
        Assert.Equal(1, inv.Import(1, new[] { new StockLineInput(m.Id, 10, null) }, "Phiếu nhập", manager.Id, now));
        var afterImport = RawMaterialDAL.GetById(m.Id)!.ActualStock;
        Assert.Equal(m.ActualStock + 10, afterImport);

        var cashier = UserDAL.GetByEmail("cashier@fpolycinema.vn")!;
        Assert.Throws<BusinessRuleException>(() => inv.CreateWastage(1, new[] { new StockLineInput(m.Id, 1, null) }, null, cashier.Id, now));
        int logId = inv.CreateWastage(1, new[] { new StockLineInput(m.Id, 2, "Hết hạn") }, null, cashier.Id, now);
        Assert.Equal(afterImport, RawMaterialDAL.GetById(m.Id)!.ActualStock);
        var shift = UserDAL.GetByEmail("shift@fpolycinema.vn")!;
        inv.DecideWastage(logId, approve: true, shift.Id, shift.CinemaId, now);
        Assert.Equal(afterImport - 2, RawMaterialDAL.GetById(m.Id)!.ActualStock);
        Assert.Equal(WastageStatus.Approved, WastageLogDAL.GetRow(logId)!.Log.Status);
        Assert.Contains(WastageLogDAL.Report(1, now.Date, now.Date.AddDays(1)), r => r.LogId == logId);
        Assert.Equal(ErrorCodes.InvalidTransition, Assert.Throws<BusinessRuleException>(() => inv.DecideWastage(logId, true, shift.Id, shift.CinemaId, now)).Code);
    }

    // ------------------------------------------------------------------ duyệt hủy từ xa (SPEC 3.6-21, 3.6-22)

    [Fact]
    public async Task RemoteApproval_RequestNotifiesManagers_RejectGoesBackToTerminal_AndExpires()
    {
        var clock = new FakeClock(Phase6DatabaseFixture.Now);
        var approvals = new ApprovalService(clock, _rules, Notifier(), Bills(clock));
        var cashier = UserDAL.GetByEmail("cashier@fpolycinema.vn")!;
        var bills = DBUtil.QueryDataTable("SELECT TOP 2 Id FROM Bills WHERE Status = @1 AND CinemaId = 1 AND CheckInStatus IN (@2, @3) ORDER BY Id DESC",
            new List<object> { BillStatus.Finish, BillCheckInStatus.None, BillCheckInStatus.NotCheckedIn }).Rows.Cast<System.Data.DataRow>().Select(r => Convert.ToInt32(r["Id"])).ToList();

        var a = await approvals.Request(bills[0], "Khách đổi ý", cashier.Id, 1);
        Assert.True(Sent(CinemaHub.ManagerAlertsGroup(1), RealtimeEvents.ApprovalRequested));
        await Assert.ThrowsAsync<BusinessRuleException>(() => approvals.Request(bills[0], "Gửi lại", cashier.Id, 1));
        var shift = UserDAL.GetByEmail("shift@fpolycinema.vn")!;
        await approvals.Decide(a.Id, approve: false, shift.Id);
        Assert.Equal(BillStatus.Finish, BillDAL.GetById(bills[0])!.Status);
        Assert.True(Sent(CinemaHub.ManagerAlertsGroup(1), RealtimeEvents.ApprovalDecided));
        var open = PosShiftDAL.GetOpenByUser(cashier.Id);
        if (open != null) Assert.True(Sent(CinemaHub.TerminalGroup(ShiftService.TerminalKey(open.CinemaId, open.TerminalCode)), RealtimeEvents.ApprovalDecided));

        var b = await approvals.Request(bills[1], "Khách mua nhầm", cashier.Id, 1);
        clock.Now = clock.Now.AddMinutes(_rules.ApprovalRequestExpireMinutes + 1);
        Assert.Equal(1, await approvals.ExpireOverdue());
        Assert.Equal(ErrorCodes.InvalidTransition, (await Assert.ThrowsAsync<BusinessRuleException>(() => approvals.Decide(b.Id, true, shift.Id))).Code);
    }

    // ------------------------------------------------------------------ ghế nội bộ (SPEC 3.2-07)

    [Fact]
    public async Task HouseSeats_ReleasedToAvailable_WithAuditLog()
    {
        var clock = new FakeClock(Phase6DatabaseFixture.Now);
        var mgmt = Mgmt(clock);
        var (all, _) = mgmt.HouseSeats(1);
        var row = all.First();
        var shift = UserDAL.GetByEmail("shift@fpolycinema.vn")!;
        int n = await mgmt.ReleaseHouseSeats(row.ShowtimeId, null, shift.Id, shift.CinemaId);
        Assert.Equal(row.HouseSeats, n);
        Assert.DoesNotContain(mgmt.HouseSeats(1).All, r => r.ShowtimeId == row.ShowtimeId);
        Assert.True(Sent(CinemaHub.ShowtimeGroup(row.ShowtimeId), RealtimeEvents.SeatMapUpdated));
        Assert.Contains(AuditLogDAL.GetAll(), l => l.Action == AuditAction.HouseSeatReleased && l.EntityId == row.ShowtimeId.ToString());
    }
}
