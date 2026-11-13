using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Tests;

/// <summary>
/// Unit test cho quy tắc thuần của Phase 4 (SPEC 7 S7-03): OrphanSeatValidator (đủ 7 tình huống của SPEC 3.5.1), SweetboxRule, SeatHoldRules,
/// LoyaltyCalculator, VoucherRules, DiscountAllocator, CashDropCalculator (đóng ca mù), SettlementCalculator, ManagerPinRules, StockCalculator.
/// </summary>
public class Phase4RulesTests
{
    private static readonly CinemaRules Rules = new();
    private static readonly DateTime Now = new(2026, 11, 13, 19, 0, 0);

    /// <summary>Một hàng G với các ô: ' ' lối đi/khoảng trống, '.' ghế trống, 'x' ghế bị chiếm, 'W' xe lăn, 'S' ghế đôi. Ghế được đánh số liên tục từ cột 1.</summary>
    private static List<SeatSlot> Row(string layout, char row = 'G')
    {
        var list = new List<SeatSlot>();
        int n = 0;
        for (int i = 0; i < layout.Length; i++)
        {
            var ch = layout[i];
            if (ch == ' ') continue;
            n++;
            var type = ch switch { 'W' => SeatType.Wheelchair, 'S' => SeatType.Sweetbox, _ => SeatType.Standard };
            list.Add(new SeatSlot(i + 1, 1, i + 1, $"{row}{n}", type, ch == 'x'));
        }
        return list;
    }

    private static HashSet<int> Pick(List<SeatSlot> seats, params string[] labels) => seats.Where(s => labels.Contains(s.Label)).Select(s => s.SeatId).ToHashSet();

    // ------------------------------------------------------------------ OrphanSeatValidator (SPEC 3.5.1)
    [Fact]
    public void Orphan_SegmentsSplitByAisleWheelchairAndSweetbox()
    {
        var seats = Row("... ..W..SS..");
        var segs = OrphanSeatValidator.Segments(seats);
        Assert.Equal(new[] { 3, 2, 2, 2 }, segs.Select(s => s.Count).ToArray());
    }

    [Fact]
    public void Orphan_SeatLeftBetweenTwoSoldSeats()
    {
        // G2 và G6 đã bán; chọn G4 làm G3 và G5 bị kẹp giữa ghế đã bán và ghế đang chọn
        var seats = Row(".x...x..");
        var v = OrphanSeatValidator.Validate(seats, Pick(seats, "G4"));
        Assert.NotNull(v);
        Assert.Equal(new[] { "G3", "G5" }, v!.Orphans.Select(o => o.Label).ToArray());
        // Chọn G3 thì G4, G5 còn liền nhau, hợp lệ
        Assert.Null(OrphanSeatValidator.Validate(seats, Pick(seats, "G3")));
    }

    [Fact]
    public void Orphan_SeatNextToAisle()
    {
        var seats = Row(".... ....");
        var v = OrphanSeatValidator.Validate(seats, Pick(seats, "G3"));
        Assert.NotNull(v);
        Assert.Equal("G4", Assert.Single(v!.Orphans).Label);
        Assert.Contains("sát lối đi", v.Message);
        Assert.Contains("chọn thêm G4", v.Message);
    }

    [Fact]
    public void Orphan_SeatNextToWall()
    {
        var seats = Row("......");
        var v = OrphanSeatValidator.Validate(seats, Pick(seats, "G2"));
        Assert.NotNull(v);
        Assert.Equal("G1", Assert.Single(v!.Orphans).Label);
        Assert.Contains("sát tường", v.Message);
        Assert.Equal("G1", Assert.Single(v.Suggestion).Label);
    }

    [Fact]
    public void Orphan_SkippingOneSeat_G5AndG7()
    {
        var seats = Row("............");
        var v = OrphanSeatValidator.Validate(seats, Pick(seats, "G5", "G7"));
        Assert.NotNull(v);
        Assert.Equal("G6", Assert.Single(v!.Orphans).Label);
        Assert.Equal(2, v.Suggestion.Count);
        Assert.Contains("G6 ở giữa", v.Message);
        Assert.Contains("dời sang G5–G6", v.Message);
    }

    [Fact]
    public void Orphan_PreExistingSingleSeatIsNotTheSelectorsFault()
    {
        // G2 đã lẻ sẵn giữa hai ghế đã bán (G1, G3); chọn G7, G8 không kề G2 nên hợp lệ
        var seats = Row("x.x.....");
        Assert.Null(OrphanSeatValidator.Validate(seats, Pick(seats, "G7", "G8")));
    }

    [Fact]
    public void Orphan_ShortSegmentException_NoOtherPlacement()
    {
        // Đoạn 3 ghế, khách đi 2 người: mọi vị trí đều để lại 1 ghế nên được phép
        var seats = Row("... ......");
        Assert.Null(OrphanSeatValidator.Validate(seats, Pick(seats, "G1", "G2")));
        Assert.Null(OrphanSeatValidator.Validate(seats, Pick(seats, "G2", "G3")));
        // Đoạn 2 ghế, 1 người
        var pair = Row(".. ....");
        Assert.Null(OrphanSeatValidator.Validate(pair, Pick(pair, "G1")));
    }

    [Fact]
    public void Orphan_WheelchairAndSweetboxNextToSelectionAreBoundaries()
    {
        // W ở cột 1 là biên: chọn G3 làm G2 bị kẹp giữa xe lăn và ghế đang chọn
        var seats = Row("W....SS");
        var v = OrphanSeatValidator.Validate(seats, Pick(seats, "G3"));
        Assert.NotNull(v);
        Assert.Equal("G2", Assert.Single(v!.Orphans).Label);
        // Chọn G5 sát ghế đôi: ghế đôi là biên nên không có ghế lẻ
        Assert.Null(OrphanSeatValidator.Validate(seats, Pick(seats, "G5")));
        // Ghế xe lăn còn trống giữa ghế đang chọn và tường không bị tính là ghế lẻ
        Assert.Null(OrphanSeatValidator.Validate(seats, Pick(seats, "G2", "G3")));
    }

    // ------------------------------------------------------------------ SweetboxRule (SPEC 3.2)
    [Fact]
    public void Sweetbox_PairMustBeChosenTogether()
    {
        var seats = new[] { new PairSeat(10, "M1", 1), new PairSeat(11, "M2", 1), new PairSeat(12, "M3", 2), new PairSeat(13, "M4", 2) };
        var bad = SweetboxRule.IncompletePairs(seats, new HashSet<int> { 10, 12, 13 });
        var pair = Assert.Single(bad);
        Assert.Equal(new[] { "M1", "M2" }, pair.Select(p => p.Label).ToArray());
        Assert.Equal("Ghế đôi M1–M2 được bán theo cặp. Bạn chọn cả hai ghế nhé.", SweetboxRule.Message(pair));
        Assert.Empty(SweetboxRule.IncompletePairs(seats, new HashSet<int> { 10, 11 }));
        Assert.Equal(new HashSet<int> { 10, 11, 5 }, SweetboxRule.ExpandToPairs(seats, new[] { 11, 5 }));
    }

    // ------------------------------------------------------------------ SeatHoldRules (SPEC 3.5)
    [Fact]
    public void SeatHold_EffectiveStatusAndHoldability()
    {
        Assert.Equal(SeatDisplayStatus.Available, SeatHoldRules.EffectiveStatus(ShowtimeSeatStatus.Held, Now, false, Now));
        Assert.Equal(SeatDisplayStatus.Held, SeatHoldRules.EffectiveStatus(ShowtimeSeatStatus.Held, Now.AddSeconds(1), false, Now));
        Assert.Equal(SeatDisplayStatus.Maintenance, SeatHoldRules.EffectiveStatus(ShowtimeSeatStatus.Available, null, true, Now));
        Assert.Equal(SeatDisplayStatus.Booked, SeatHoldRules.EffectiveStatus(ShowtimeSeatStatus.Booked, null, false, Now));
        Assert.True(SeatHoldRules.CanHold(SeatDisplayStatus.Available, null, 5));
        Assert.True(SeatHoldRules.CanHold(SeatDisplayStatus.Held, 5, 5));
        Assert.False(SeatHoldRules.CanHold(SeatDisplayStatus.Held, 6, 5));
        Assert.False(SeatHoldRules.CanHold(SeatDisplayStatus.Maintenance, null, 5));
        Assert.False(SeatHoldRules.CanHold(SeatDisplayStatus.HouseHold, null, 5));
    }

    [Fact]
    public void SeatHold_SalesWindowExpiryAndExtension()
    {
        var feature = Now.AddMinutes(10);
        Assert.True(SeatHoldRules.IsSalesOpen(feature, OperationalStatus.Scheduled, BillChannel.Pos, feature.AddMinutes(14), Rules));
        Assert.False(SeatHoldRules.IsSalesOpen(feature, OperationalStatus.Scheduled, BillChannel.Pos, feature.AddMinutes(15), Rules));
        Assert.False(SeatHoldRules.IsSalesOpen(feature, OperationalStatus.Scheduled, BillChannel.Online, feature, Rules));
        Assert.False(SeatHoldRules.IsSalesOpen(feature.AddHours(2), OperationalStatus.Cancelled, BillChannel.Pos, Now, Rules));
        Assert.Equal(Now.AddMinutes(10), SeatHoldRules.HoldExpiry(null, Now, Rules));
        Assert.Equal(Now.AddMinutes(3), SeatHoldRules.HoldExpiry(Now.AddMinutes(3), Now.AddMinutes(2), Rules));
        Assert.True(SeatHoldRules.CanExtend(Now.AddMinutes(1), false, Now));
        Assert.False(SeatHoldRules.CanExtend(Now.AddMinutes(1), true, Now));
        Assert.False(SeatHoldRules.CanExtend(Now, false, Now));
        Assert.True(SeatHoldRules.IsExpired(Now, Now));
        Assert.False(SeatHoldRules.IsExpired(null, Now));
    }

    // ------------------------------------------------------------------ LoyaltyCalculator (SPEC 3.13)
    [Fact]
    public void Loyalty_EarnTierAndRedeemLimits()
    {
        Assert.Equal(9, LoyaltyCalculator.EarnPoints(193_000, LoyaltyTier.Member, Rules));
        Assert.Equal(13, LoyaltyCalculator.EarnPoints(193_000, LoyaltyTier.Vip, Rules));
        Assert.Equal(19, LoyaltyCalculator.EarnPoints(193_000, LoyaltyTier.Vvip, Rules));
        Assert.Equal(0, LoyaltyCalculator.EarnPoints(0, LoyaltyTier.Vip, Rules));
        Assert.Equal(LoyaltyTier.Member, LoyaltyCalculator.TierFor(3_999_999, Rules));
        Assert.Equal(LoyaltyTier.Vip, LoyaltyCalculator.TierFor(4_000_000, Rules));
        Assert.Equal(LoyaltyTier.Vvip, LoyaltyCalculator.TierFor(8_000_000, Rules));
        Assert.Equal(121, LoyaltyCalculator.MaxRedeemPoints(243_000, 249, Rules));
        Assert.Equal(30, LoyaltyCalculator.MaxRedeemPoints(243_000, 30, Rules));
        Assert.Equal(50_000, LoyaltyCalculator.PointsValue(50, Rules));
        Assert.Null(LoyaltyCalculator.ValidateRedeem(121, 249, 243_000, Rules));
        Assert.Contains("tối đa 121 điểm", LoyaltyCalculator.ValidateRedeem(122, 249, 243_000, Rules));
        Assert.Contains("chỉ còn 10 điểm", LoyaltyCalculator.ValidateRedeem(11, 10, 243_000, Rules));
        Assert.NotNull(LoyaltyCalculator.ValidateRedeem(-1, 10, 243_000, Rules));
    }

    // ------------------------------------------------------------------ VoucherRules (SPEC 3.13)
    private static Voucher V(string type, long value, long min = 0, int used = 0, int limit = 5, int? owner = null, bool active = true) => new()
    {
        Id = 1, Code = "TEST", Type = type, Value = value, MinBillAmount = min, ValidFrom = Now.AddDays(-1), ValidUntil = Now.AddDays(1),
        UsageLimit = limit, UsedCount = used, OwnerUserId = owner, Source = VoucherSource.Promo, Active = active,
    };

    [Fact]
    public void Voucher_ChecksEveryConditionWithSpecificReason()
    {
        var tickets = new[] { (90_000L, SeatType.Vip), (120_000L, SeatType.Sweetbox) };
        Assert.Equal(30_000, VoucherRules.Check(V(VoucherType.Amount, 30_000), Now, 200_000, null, tickets).Discount);
        Assert.Equal(25_000, VoucherRules.Check(V(VoucherType.Percent, 10), Now, 255_000, null, tickets).Discount);
        Assert.Equal(90_000, VoucherRules.Check(V(VoucherType.FreeTicket, 0), Now, 210_000, null, tickets).Discount);
        Assert.Equal(50_000, VoucherRules.Check(V(VoucherType.Amount, 80_000), Now, 50_000, null, tickets).Discount);
        Assert.Contains("ngưng", VoucherRules.Check(V(VoucherType.Amount, 1, active: false), Now, 1, null, tickets).Reason);
        Assert.Contains("hết lượt", VoucherRules.Check(V(VoucherType.Amount, 1, used: 5), Now, 1, null, tickets).Reason);
        Assert.Contains("hóa đơn từ 100.000 ₫", VoucherRules.Check(V(VoucherType.Amount, 1, min: 100_000), Now, 99_000, null, tickets).Reason);
        Assert.Contains("riêng", VoucherRules.Check(V(VoucherType.Amount, 1, owner: 7), Now, 10_000, 8, tickets).Reason);
        Assert.True(VoucherRules.Check(V(VoucherType.Amount, 1, owner: 7), Now, 10_000, 7, tickets).Ok);
        Assert.Contains("hết hạn", VoucherRules.Check(V(VoucherType.Amount, 1), Now.AddDays(2), 10_000, null, tickets).Reason);
        Assert.Contains("chỉ dùng được từ", VoucherRules.Check(V(VoucherType.Amount, 1), Now.AddDays(-2), 10_000, null, tickets).Reason);
        Assert.False(VoucherRules.Check(V(VoucherType.FreeTicket, 0), Now, 120_000, null, new[] { (120_000L, SeatType.Sweetbox) }).Ok);
    }

    // ------------------------------------------------------------------ DiscountAllocator (SPEC 3.4)
    [Fact]
    public void Discount_AllocatedProportionallyRemainderToLastLine()
    {
        var a = DiscountAllocator.Allocate(50_000, new long[] { 85_000, 158_000 });
        Assert.Equal(17_489, a[0]);
        Assert.Equal(32_511, a[1]);
        Assert.Equal(50_000, a.Sum());
        var b = DiscountAllocator.Allocate(10, new long[] { 3, 3, 3 });
        Assert.Equal(9, b.Sum());
        Assert.All(b, x => Assert.True(x <= 3));
        var c = DiscountAllocator.Allocate(100, new long[] { 1000, 1 });
        Assert.Equal(100, c.Sum());
        Assert.True(c[1] <= 1);
        Assert.Equal(new long[] { 0, 0 }, DiscountAllocator.Allocate(0, new long[] { 5, 5 }));
    }

    // ------------------------------------------------------------------ CashDropCalculator (đóng ca mù, SPEC 3.6)
    [Fact]
    public void CashDrop_CountsDenominationsAndComputesDiscrepancyAfterConfirm()
    {
        var counted = CashDropCalculator.CountTotal(new Dictionary<long, int> { [500_000] = 4, [100_000] = 3, [20_000] = 2, [1_000] = 5 });
        Assert.Equal(2_345_000, counted);
        var r = CashDropCalculator.Close(counted, 2_000_000, 500_000, 100_000);
        Assert.Equal(2_400_000, r.Expected);
        Assert.Equal(-55_000, r.Discrepancy);
        Assert.True(r.IsShort);
        Assert.True(CashDropCalculator.Close(2_500_000, 2_000_000, 500_000, 0).Discrepancy == 0);
        Assert.Throws<ArgumentException>(() => CashDropCalculator.CountTotal(new Dictionary<long, int> { [300_000] = 1 }));
        Assert.Throws<ArgumentException>(() => CashDropCalculator.CountTotal(new Dictionary<long, int> { [1_000] = -1 }));
        Assert.Equal(9, CashDropCalculator.Denominations.Length);
    }

    // ------------------------------------------------------------------ SettlementCalculator (SPEC 3.12)
    [Fact]
    public void Settlement_BaseIsMaxOfNetAndFloorAndCinemaAbsorbsTheGap()
    {
        // Giá sàn 54.000 (gồm VAT 8%) → sàn thuần 50.000. Vé 108.000 → thuần 100.000; vé 43.200 → thuần 40.000 (dưới sàn 10.000)
        var r = SettlementCalculator.Compute(new[] { new SettlementLine(108_000, 8, 3), new SettlementLine(43_200, 8, 2) }, 54_000, 50);
        Assert.Equal(5, r.TicketCount);
        Assert.Equal(410_400, r.GrossRevenue);
        Assert.Equal(380_000, r.NetRevenue);
        Assert.Equal(2, r.BelowFloorTickets);
        Assert.Equal(20_000, r.FloorAbsorbedAmount);
        Assert.Equal(400_000, r.BaseTotal);
        Assert.Equal(200_000, r.DistributorAmount);
        Assert.Equal(180_000, r.CinemaRetainedAmount);
    }

    // ------------------------------------------------------------------ ManagerPinRules (SPEC 2.5)
    [Fact]
    public void Pin_LocksAfterMaxAttemptsAndResetsOnSuccess()
    {
        Assert.True(ManagerPinRules.IsWellFormed("246810"));
        Assert.False(ManagerPinRules.IsWellFormed("24681"));
        Assert.False(ManagerPinRules.IsWellFormed("24681a"));
        var a = ManagerPinRules.After(false, 0, Now, Rules);
        Assert.Equal((1, 4), (a.FailedCount, a.Remaining));
        var locked = ManagerPinRules.After(false, 4, Now, Rules);
        Assert.Equal(Now.AddMinutes(5), locked.LockedUntil);
        Assert.Equal(0, locked.FailedCount);
        Assert.True(ManagerPinRules.IsLocked(locked.LockedUntil, Now.AddMinutes(4)));
        Assert.False(ManagerPinRules.IsLocked(locked.LockedUntil, Now.AddMinutes(5)));
        var ok = ManagerPinRules.After(true, 3, Now, Rules);
        Assert.True(ok.Ok);
        Assert.Equal(0, ok.FailedCount);
    }

    // ------------------------------------------------------------------ StockCalculator (SPEC 3.11)
    [Fact]
    public void Stock_PortionsLimitedByScarcestMaterial()
    {
        Assert.Equal(3, StockCalculator.MaxPortions(new[] { (0.2m, 1.0m), (1m, 3.5m) }));
        Assert.Equal(0, StockCalculator.MaxPortions(new[] { (0.2m, 0.1m) }));
        Assert.Equal(int.MaxValue, StockCalculator.MaxPortions(Array.Empty<(decimal, decimal)>()));
        Assert.True(StockCalculator.CanSell(new[] { (0.5m, 1m) }, 2));
        Assert.False(StockCalculator.CanSell(new[] { (0.5m, 1m) }, 3));
    }
}
