using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Tests;

/// <summary>
/// Unit test cho quy tắc thuần của Phase 6 (SPEC 7 S7-04): ShowtimeOpsRules (SPEC 3.3 vòng đời vận hành), GateRules (SPEC 3.8 soát vé),
/// CheckInRules (SPEC 3.8-09, CONTRACTS C3-38), KdsRules (SPEC 3.11 bếp, "Tôi đã tới rạp"), InventoryAlertRules (SPEC 3.11-09..11),
/// WastageRules (SPEC 3.11-14, 3.11-15), SeatSwapRules (SPEC 3.10 mục 1), IncidentRules (SPEC 3.10 mục 2), CorporateRules (SPEC 3.10 mục 3),
/// ApprovalRules (SPEC 3.6-21, 3.6-22), HouseSeatRules (SPEC 3.2-07).
/// </summary>
public class Phase6RulesTests
{
    private static readonly CinemaRules Rules = new();
    private static readonly DateTime Start = new(2026, 11, 14, 19, 0, 0);
    private static readonly ShowtimeClock Clock = new(Start, Start.AddMinutes(15), Start.AddMinutes(135));

    // ------------------------------------------------------------------ ShowtimeOpsRules

    [Theory]
    [InlineData(-30, OperationalStatus.Scheduled)]
    [InlineData(-10, OperationalStatus.Boarding)]
    [InlineData(0, OperationalStatus.AdsPlaying)]
    [InlineData(15, OperationalStatus.FeaturePlaying)]
    [InlineData(130, OperationalStatus.CreditsRolling)]
    [InlineData(135, OperationalStatus.CleaningInProgress)]
    public void ShowtimeOps_ByTime_FollowsMilestones(int minutesFromStart, string expected) =>
        Assert.Equal(expected, ShowtimeOpsRules.ByTime(Clock, Start.AddMinutes(minutesFromStart), Rules));

    [Fact]
    public void ShowtimeOps_StaysScheduledWhileRoomNotReady_ThenJumpsToCurrentState()
    {
        var now = Start.AddMinutes(20);
        Assert.Equal(OperationalStatus.Scheduled, ShowtimeOpsRules.Next(OperationalStatus.Scheduled, Clock, now, roomReady: false, Rules));
        Assert.True(ShowtimeOpsRules.IsLate(OperationalStatus.Scheduled, Start, now));
        Assert.Equal(OperationalStatus.FeaturePlaying, ShowtimeOpsRules.Next(OperationalStatus.Scheduled, Clock, now, roomReady: true, Rules));
    }

    [Fact]
    public void ShowtimeOps_CleaningWaitsForStaff_AndNeverGoesBack()
    {
        var late = Start.AddMinutes(300);
        Assert.Equal(OperationalStatus.CleaningInProgress, ShowtimeOpsRules.Next(OperationalStatus.CleaningInProgress, Clock, late, true, Rules));
        Assert.Equal(OperationalStatus.Completed, ShowtimeOpsRules.Next(OperationalStatus.Completed, Clock, Start, true, Rules));
        Assert.Equal(OperationalStatus.Cancelled, ShowtimeOpsRules.Next(OperationalStatus.Cancelled, Clock, Start, true, Rules));
        Assert.Equal(OperationalStatus.FeaturePlaying, ShowtimeOpsRules.Next(OperationalStatus.FeaturePlaying, Clock, Start.AddMinutes(-5), true, Rules));
        Assert.True(ShowtimeOpsRules.CanMarkCleaned(OperationalStatus.CleaningInProgress));
        Assert.False(ShowtimeOpsRules.CanMarkCleaned(OperationalStatus.FeaturePlaying));
    }

    [Fact]
    public void ShowtimeOps_RoomReadyRules()
    {
        Assert.True(ShowtimeOpsRules.IsRoomReady(null));
        Assert.True(ShowtimeOpsRules.IsRoomReady(OperationalStatus.Completed));
        Assert.True(ShowtimeOpsRules.IsRoomReady(OperationalStatus.Cancelled));
        Assert.False(ShowtimeOpsRules.IsRoomReady(OperationalStatus.CleaningInProgress));
        Assert.True(ShowtimeOpsRules.IsRunning(OperationalStatus.Boarding));
        Assert.False(ShowtimeOpsRules.IsRunning(OperationalStatus.CleaningInProgress));
        Assert.True(ShowtimeOpsRules.IsStaleDay(Start.AddDays(-1), Start));
    }

    [Theory]
    [InlineData(OperationalStatus.Boarding, -5, "boarding")]
    [InlineData(OperationalStatus.FeaturePlaying, 20, "playing")]
    [InlineData(OperationalStatus.CleaningInProgress, 150, "cleaning")]
    [InlineData(OperationalStatus.Scheduled, -5, "cleaning")]
    [InlineData(OperationalStatus.Scheduled, -30, "scheduled")]
    public void ShowtimeOps_SignageState(string status, int minutesFromStart, string expected) =>
        Assert.Equal(expected, ShowtimeOpsRules.SignageState(status, Start, Start.AddMinutes(minutesFromStart), Rules.BoardingLeadMinutes));

    // ------------------------------------------------------------------ GateRules

    private static GateTicket T(int id, string status = TicketCheckInStatus.NotCheckedIn, string audience = AudienceType.Adult, bool doc = false) =>
        new(id, "G" + id, audience, audience, status, doc, status == TicketCheckInStatus.CheckedIn ? Start : null);

    private static GateInput Gate(bool found = true, string bill = BillStatus.Finish, int billCinema = 1, int? scanned = null, string ops = OperationalStatus.Boarding,
        string rating = AgeRating.P, bool ready = true, DateTime? now = null, DateTime? end = null, params GateTicket[] tickets) =>
        new(found, bill, billCinema, 1, scanned, tickets.Length == 0 ? new[] { T(1), T(2) } : tickets, ops, Start, end ?? Start.AddMinutes(135), rating, ready,
            now ?? Start.AddMinutes(-5));

    [Fact]
    public void Gate_ChecksInOrder_StopsAtFirstError()
    {
        Assert.Equal(GateReason.NotFound, GateRules.Check(Gate(found: false), Rules).Reason);
        Assert.Equal(GateReason.NotPaid, GateRules.Check(Gate(bill: BillStatus.Waiting, billCinema: 2), Rules).Reason);
        Assert.Equal(GateReason.TicketInvalid, GateRules.Check(Gate(scanned: 1, billCinema: 2, tickets: new[] { T(1, TicketCheckInStatus.Swapped), T(2) }), Rules).Reason);
        Assert.Equal(GateReason.TicketInvalid, GateRules.Check(Gate(scanned: 1, tickets: new[] { T(1, TicketCheckInStatus.Voided) }), Rules).Reason);
        Assert.Equal(GateReason.WrongCinema, GateRules.Check(Gate(billCinema: 2, ops: OperationalStatus.Completed), Rules).Reason);
        Assert.Equal(GateReason.AlreadyCheckedIn, GateRules.Check(Gate(scanned: 1, tickets: new[] { T(1, TicketCheckInStatus.CheckedIn), T(2) }), Rules).Reason);
    }

    [Fact]
    public void Gate_ShowtimeStatus_Messages()
    {
        var cleaning = GateRules.Check(Gate(ops: OperationalStatus.Scheduled, ready: false), Rules);
        Assert.Equal(GateReason.RoomCleaning, cleaning.Reason);
        Assert.Equal("Phòng đang dọn vệ sinh, chưa thể đón khách", cleaning.Message);
        Assert.Equal(GateReason.TooEarly, GateRules.Check(Gate(ops: OperationalStatus.Scheduled, now: Start.AddHours(-2)), Rules).Reason);
        Assert.Equal(GateReason.Ended, GateRules.Check(Gate(ops: OperationalStatus.CleaningInProgress), Rules).Reason);
        Assert.True(GateRules.IsOpenForEntry(OperationalStatus.FeaturePlaying));
        Assert.False(GateRules.IsOpenForEntry(OperationalStatus.CreditsRolling));
    }

    [Fact]
    public void Gate_ValidWithoutWarnings_AdmitsOnlyWaitingTickets()
    {
        var v = GateRules.Check(Gate(tickets: new[] { T(1, TicketCheckInStatus.CheckedIn), T(2), T(3, TicketCheckInStatus.Swapped) }), Rules);
        Assert.Equal(GateVerdict.Ok, v.Kind);
        Assert.Equal(new[] { 2 }, v.AdmittableTicketIds);
    }

    [Fact]
    public void Gate_YellowWarnings_AgeKidsCurfewDocuments()
    {
        Assert.Contains(GateRules.Check(Gate(rating: AgeRating.T16), Rules).Warnings, w => w.Contains("CCCD"));
        var k = GateRules.Check(Gate(rating: AgeRating.K, tickets: new[] { T(1, audience: AudienceType.ChildU13) }), Rules);
        Assert.Equal(GateVerdict.Warn, k.Kind);
        Assert.Contains(k.Warnings, w => w.Contains("giám hộ"));
        var late = GateRules.Check(Gate(end: Start.Date.AddHours(22).AddMinutes(30)), Rules);
        Assert.Equal(GateVerdict.Warn, late.Kind);
        Assert.Contains(GateRules.Check(Gate(tickets: new[] { T(1, doc: true) }), Rules).Warnings, w => w.Contains("giấy tờ"));
        Assert.Empty(GateRules.Check(Gate(rating: AgeRating.P), Rules).Warnings);
    }

    // ------------------------------------------------------------------ CheckInRules (C3-38)

    [Fact]
    public void CheckIn_BillStatus_IgnoresSwappedAndVoided()
    {
        Assert.Equal(BillCheckInStatus.None, CheckInRules.BillStatusOf(new[] { TicketCheckInStatus.Voided }));
        Assert.Equal(BillCheckInStatus.NotCheckedIn, CheckInRules.BillStatusOf(new[] { TicketCheckInStatus.NotCheckedIn, TicketCheckInStatus.Swapped }));
        Assert.Equal(BillCheckInStatus.PartiallyCheckedIn, CheckInRules.BillStatusOf(new[] { TicketCheckInStatus.NotCheckedIn, TicketCheckInStatus.CheckedIn }));
        Assert.Equal(BillCheckInStatus.FullyCheckedIn, CheckInRules.BillStatusOf(new[] { TicketCheckInStatus.CheckedIn, TicketCheckInStatus.Swapped }));
    }

    [Fact]
    public void CheckIn_BillCompletedFlag()
    {
        Assert.True(CheckInRules.IsCompleted(BillStatus.Finish, BillCheckInStatus.FullyCheckedIn, FnbStatus.Fulfilled));
        Assert.True(CheckInRules.IsCompleted(BillStatus.Finish, BillCheckInStatus.None, FnbStatus.None));
        Assert.False(CheckInRules.IsCompleted(BillStatus.Finish, BillCheckInStatus.PartiallyCheckedIn, FnbStatus.None));
        Assert.False(CheckInRules.IsCompleted(BillStatus.Finish, BillCheckInStatus.FullyCheckedIn, FnbStatus.ReadyForPickup));
        Assert.False(CheckInRules.IsCompleted(BillStatus.Cancel, BillCheckInStatus.None, FnbStatus.None));
    }

    // ------------------------------------------------------------------ KdsRules

    [Theory]
    [InlineData(4, KdsRules.WaitOk)]
    [InlineData(5, KdsRules.WaitWarn)]
    [InlineData(10, KdsRules.WaitWarn)]
    [InlineData(11, KdsRules.WaitLate)]
    public void Kds_WaitColour(int minutes, string expected) => Assert.Equal(expected, KdsRules.WaitLevel(TimeSpan.FromMinutes(minutes)));

    [Fact]
    public void Kds_ImHereWindow_StartAndAdvance()
    {
        var end = Start.AddMinutes(135);
        Assert.False(KdsRules.InImHereWindow(Start, end, Start.AddMinutes(-31), 30));
        Assert.True(KdsRules.InImHereWindow(Start, end, Start.AddMinutes(-30), 30));
        Assert.True(KdsRules.InImHereWindow(Start, end, end, 30));
        Assert.False(KdsRules.InImHereWindow(Start, end, end.AddMinutes(1), 30));
        Assert.Null(KdsRules.CannotStartReason(BillStatus.Finish, FnbStatus.PendingPickup));
        Assert.NotNull(KdsRules.CannotStartReason(BillStatus.Finish, FnbStatus.Preparing));
        Assert.NotNull(KdsRules.CannotStartReason(BillStatus.Waiting, FnbStatus.PendingPickup));
        Assert.True(KdsRules.CanAdvance(FnbStatus.Preparing, FnbStatus.ReadyForPickup));
        Assert.True(KdsRules.CanAdvance(FnbStatus.ReadyForPickup, FnbStatus.Fulfilled));
        Assert.False(KdsRules.CanAdvance(FnbStatus.Preparing, FnbStatus.Fulfilled));
    }

    // ------------------------------------------------------------------ InventoryAlertRules

    [Fact]
    public void Inventory_SoldOutAvailableAndLow()
    {
        var a = InventoryAlertRules.Evaluate(
            new Dictionary<int, int> { [1] = 3, [2] = 0, [3] = 5 }, new Dictionary<int, int> { [1] = 0, [2] = 4, [3] = 4 },
            new[] { new MaterialLevel(10, "Bắp", "g", 1000, 0, 500), new MaterialLevel(11, "Đường", "g", 100, 0, 500) },
            new[] { new MaterialLevel(10, "Bắp", "g", 300, 0, 500), new MaterialLevel(11, "Đường", "g", 50, 0, 500) });
        Assert.Equal(new[] { 1 }, a.SoldOut);
        Assert.Equal(new[] { 2 }, a.Available);
        Assert.Equal(new[] { 10 }, a.Low.Select(m => m.Id));
    }

    [Fact]
    public void Inventory_PortionsAndSoldOutEverywhere()
    {
        var p = InventoryAlertRules.Portions(new[] { new RecipeStock(1, 10, 100, 350), new RecipeStock(1, 11, 2, 100) });
        Assert.Equal(3, p[1]);
        Assert.True(InventoryAlertRules.SoldOutEverywhere(new[] { 0, 0 }));
        Assert.False(InventoryAlertRules.SoldOutEverywhere(new[] { 0, 2 }));
        Assert.False(InventoryAlertRules.SoldOutEverywhere(Array.Empty<int>()));
    }

    // ------------------------------------------------------------------ WastageRules

    [Fact]
    public void Wastage_ValidatesLines()
    {
        var known = new HashSet<int> { 1, 2 };
        Assert.True(WastageRules.ValidateLines(new[] { new StockLineInput(1, 0, null) }, known, true).ContainsKey("lines"));
        var e = WastageRules.ValidateLines(new[] { new StockLineInput(1, 5, null), new StockLineInput(3, -1, "x") }, known, needReason: true);
        Assert.True(e.ContainsKey("lines[0].Reason"));
        Assert.True(e.ContainsKey("lines[1].RawMaterialId"));
        Assert.True(e.ContainsKey("lines[1].Quantity"));
        Assert.Empty(WastageRules.ValidateLines(new[] { new StockLineInput(1, 5, null) }, known, needReason: false));
        Assert.True(WastageRules.ValidateLines(new[] { new StockLineInput(1, 5, "a"), new StockLineInput(1, 2, "b") }, known, true).ContainsKey("lines"));
        Assert.True(WastageRules.CanDecide(WastageStatus.Pending));
        Assert.False(WastageRules.CanDecide(WastageStatus.Approved));
        Assert.True(WastageRules.Fits(100, 20, 80));
        Assert.False(WastageRules.Fits(100, 20, 81));
    }

    // ------------------------------------------------------------------ SeatSwapRules

    [Fact]
    public void SeatSwap_OnlyBeforeFeatureAndNotCheckedIn()
    {
        var feature = Start.AddMinutes(15);
        Assert.Null(SeatSwapRules.CannotSwapReason(BillStatus.Finish, TicketCheckInStatus.NotCheckedIn, feature, Start));
        Assert.NotNull(SeatSwapRules.CannotSwapReason(BillStatus.Finish, TicketCheckInStatus.CheckedIn, feature, Start));
        Assert.NotNull(SeatSwapRules.CannotSwapReason(BillStatus.Finish, TicketCheckInStatus.Swapped, feature, Start));
        Assert.NotNull(SeatSwapRules.CannotSwapReason(BillStatus.Finish, TicketCheckInStatus.NotCheckedIn, feature, feature));
        Assert.NotNull(SeatSwapRules.CannotSwapReason(BillStatus.Cancel, TicketCheckInStatus.NotCheckedIn, feature, Start));
    }

    [Fact]
    public void SeatSwap_QuoteChargesOnlyPositiveDifference()
    {
        var up = SeatSwapRules.Quote(new long[] { 90_000, 90_000 }, new long[] { 120_000, 120_000 });
        Assert.True(up.IsUpgrade);
        Assert.Equal(60_000, up.Charge);
        var down = SeatSwapRules.Quote(new long[] { 120_000 }, new long[] { 90_000 });
        Assert.False(down.IsUpgrade);
        Assert.Equal(-30_000, down.Difference);
        Assert.Equal(0, down.Charge);
        Assert.Equal(110_000, SeatSwapRules.NewFinalPrice(80_000, 90_000, 120_000));
        Assert.Equal(80_000, SeatSwapRules.NewFinalPrice(80_000, 120_000, 90_000));
        // Đổi 2 vé: một nâng (+30.000), một hạ (−10.000) → thu 20.000, cả phần thu dồn vào vé nâng hạng
        Assert.Equal(new long[] { 20_000, 0 }, SeatSwapRules.AllocateCharge(20_000, new long[] { 30_000, -10_000 }));
        var split = SeatSwapRules.AllocateCharge(50_000, new long[] { 30_000, 30_000, 0 });
        Assert.Equal(50_000, split.Sum());
        Assert.Equal(0, split[2]);
        Assert.Equal(new long[] { 0, 0 }, SeatSwapRules.AllocateCharge(0, new long[] { -5_000, 5_000 }));
    }

    // ------------------------------------------------------------------ IncidentRules

    [Fact]
    public void Incident_RequiresReasonAndRetypedCode()
    {
        Assert.Equal("S42", IncidentRules.ConfirmCode(42));
        Assert.True(IncidentRules.ConfirmMatches(42, " s42 "));
        var e = IncidentRules.Validate(42, OperationalStatus.Scheduled, "abc", "S41");
        Assert.True(e.ContainsKey("reason"));
        Assert.True(e.ContainsKey("confirmCode"));
        Assert.Empty(IncidentRules.Validate(42, OperationalStatus.FeaturePlaying, "Máy chiếu hỏng", "S42"));
        Assert.True(IncidentRules.Validate(42, OperationalStatus.Cancelled, "Máy chiếu hỏng", "S42").ContainsKey("showtimeId"));
        Assert.True(IncidentRules.Validate(42, OperationalStatus.Completed, "Máy chiếu hỏng", "S42").ContainsKey("showtimeId"));
    }

    // ------------------------------------------------------------------ CorporateRules

    [Fact]
    public void Corporate_PrivateScreeningOnlyUnsoldFutureShowtime()
    {
        Assert.Empty(CorporateRules.ValidatePrivateScreening(OperationalStatus.Scheduled, false, 0, 0, Start, Start.AddHours(-1), 10_000_000));
        Assert.True(CorporateRules.ValidatePrivateScreening(OperationalStatus.Scheduled, false, 1, 0, Start, Start.AddHours(-1), 10_000_000).ContainsKey("ShowtimeId"));
        Assert.True(CorporateRules.ValidatePrivateScreening(OperationalStatus.Scheduled, false, 0, 2, Start, Start.AddHours(-1), 10_000_000).ContainsKey("ShowtimeId"));
        Assert.True(CorporateRules.ValidatePrivateScreening(OperationalStatus.Scheduled, true, 0, 0, Start, Start.AddHours(-1), 10_000_000).ContainsKey("ShowtimeId"));
        Assert.True(CorporateRules.ValidatePrivateScreening(OperationalStatus.Scheduled, false, 0, 0, Start, Start, 10_000_000).ContainsKey("ShowtimeId"));
        Assert.True(CorporateRules.ValidatePrivateScreening(OperationalStatus.Scheduled, false, 0, 0, Start, Start.AddHours(-1), 0).ContainsKey("ContractAmount"));
    }

    [Fact]
    public void Corporate_VoucherBatchAndSplit()
    {
        Assert.Empty(CorporateRules.ValidateVoucherBatch(20, Start.AddDays(30), Start, 0));
        Assert.True(CorporateRules.ValidateVoucherBatch(0, Start.AddDays(30), Start, 0).ContainsKey("VoucherCount"));
        Assert.True(CorporateRules.ValidateVoucherBatch(CorporateRules.MaxVoucherBatch + 1, Start.AddDays(30), Start, 0).ContainsKey("VoucherCount"));
        Assert.True(CorporateRules.ValidateVoucherBatch(5, Start, Start, 0).ContainsKey("ValidUntil"));
        var parts = CorporateRules.SplitContract(1_000_000, 3);
        Assert.Equal(1_000_000, parts.Sum());
        Assert.Equal(333_333, parts[0]);
        Assert.Equal(333_334, parts[2]);
        Assert.True(CorporateRules.FreeTicketApplies(SeatType.Vip));
        Assert.False(CorporateRules.FreeTicketApplies(SeatType.Sweetbox));
    }

    // ------------------------------------------------------------------ ApprovalRules

    [Fact]
    public void Approval_ExpiryAndDecider()
    {
        var created = Start;
        var exp = ApprovalRules.ExpiresAt(created, 10);
        Assert.Equal(created.AddMinutes(10), exp);
        Assert.True(ApprovalRules.IsPending(ApprovalStatus.Pending, exp, created.AddMinutes(9)));
        Assert.Equal(ApprovalStatus.Expired, ApprovalRules.Effective(ApprovalStatus.Pending, exp, exp));
        Assert.Null(ApprovalRules.CannotDecideReason(ApprovalStatus.Pending, exp, created));
        Assert.NotNull(ApprovalRules.CannotDecideReason(ApprovalStatus.Pending, exp, exp));
        Assert.NotNull(ApprovalRules.CannotDecideReason(ApprovalStatus.Approved, exp, created));
        Assert.True(ApprovalRules.CanApprove(Roles.ShiftManager, 1, 1));
        Assert.False(ApprovalRules.CanApprove(Roles.ShiftManager, 2, 1));
        Assert.True(ApprovalRules.CanApprove(Roles.Manager, null, 1));
        Assert.False(ApprovalRules.CanApprove(Roles.POSCashier, 1, 1));
    }

    // ------------------------------------------------------------------ HouseSeatRules

    [Fact]
    public void HouseSeat_ReminderAndRelease()
    {
        Assert.True(HouseSeatRules.NeedsReminder(Start, 2, Start.AddMinutes(-30), 60));
        Assert.False(HouseSeatRules.NeedsReminder(Start, 2, Start.AddMinutes(-90), 60));
        Assert.False(HouseSeatRules.NeedsReminder(Start, 0, Start.AddMinutes(-30), 60));
        Assert.Null(HouseSeatRules.CannotReleaseReason(OperationalStatus.FeaturePlaying, Start.AddMinutes(135), Start.AddMinutes(30)));
        Assert.NotNull(HouseSeatRules.CannotReleaseReason(OperationalStatus.Cancelled, Start.AddMinutes(135), Start));
        Assert.NotNull(HouseSeatRules.CannotReleaseReason(OperationalStatus.CleaningInProgress, Start.AddMinutes(135), Start));
    }
}
