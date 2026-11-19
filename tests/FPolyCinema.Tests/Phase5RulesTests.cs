using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Tests;

/// <summary>
/// Unit test cho quy tắc thuần của Phase 5 (SPEC 7 S7-04): ClusterFinder (SPEC 4.1 tìm cụm K ghế), SelfCancelRules (SPEC 3.9),
/// OnlineAgeRules (SPEC 3.1 phần online), WebhookSignature (SPEC 3.7), TierProgressCalculator (UI C5).
/// </summary>
public class Phase5RulesTests
{
    private static readonly CinemaRules Rules = new();
    private static readonly DateTime Now = new(2026, 11, 13, 15, 0, 0);

    /// <summary>
    /// Phòng R hàng; mỗi chuỗi là một hàng từ hàng 1 (gần màn hình): ' ' lối đi, '.' ghế trống, 'x' ghế đã bán, 'S' ghế đôi (cặp liên tiếp), 'W' xe lăn.
    /// </summary>
    private static List<ClusterSeat> Room(params string[] rows)
    {
        var list = new List<ClusterSeat>();
        int id = 0, pair = 0;
        for (int r = 0; r < rows.Length; r++)
        {
            int n = 0, sweetInPair = 0;
            for (int c = 0; c < rows[r].Length; c++)
            {
                var ch = rows[r][c];
                if (ch == ' ') continue;
                n++;
                id++;
                var type = ch switch { 'S' => SeatType.Sweetbox, 'W' => SeatType.Wheelchair, _ => SeatType.Standard };
                int? pg = null;
                if (type == SeatType.Sweetbox)
                {
                    if (sweetInPair % 2 == 0) pair++;
                    sweetInPair++;
                    pg = pair;
                }
                list.Add(new ClusterSeat(id, r + 1, c + 1, $"{(char)('A' + r)}{n}", type, pg, ch != 'x'));
            }
        }
        return list;
    }

    // ------------------------------------------------------------------ ClusterFinder (SPEC 4.1)
    [Fact]
    public void Cluster_SlidingWindowFindsContiguousFreeSeats_BestNearIdealRow()
    {
        // 10 hàng × 10 cột, r_ideal = 6,5: cụm 3 ghế tốt nhất nằm hàng 6 hoặc 7, ở giữa phòng
        var rows = Enumerable.Repeat("..........", 10).ToArray();
        var seats = Room(rows);
        var res = ClusterFinder.Find(seats, 10, 10, SeatScoring.DefaultIdealRowRatio, SeatPersona.BestViewAudio, 3);
        Assert.Equal(3, res.Count);
        var best = res[0];
        Assert.Equal(3, best.Seats.Count);
        Assert.Contains(best.Seats[0].Row, new[] { 6, 7 });
        Assert.All(best.Seats, s => Assert.InRange(s.Col, 4, 7));
        Assert.True(res[0].Score >= res[1].Score && res[1].Score >= res[2].Score);
        // Ba phương án không dùng chung ghế
        Assert.Equal(9, res.SelectMany(c => c.SeatIds).Distinct().Count());
        Assert.Matches(@"^[A-J]\d+–[A-J]\d+$", best.Range);
    }

    [Fact]
    public void Cluster_SkipsTakenSeatsAndWindowsLeavingAnOrphan()
    {
        // Hàng duy nhất ". . . x . . . ." (8 ghế): chọn 2 ghế ở đoạn 3 ghế đầu sẽ để lẻ 1 ghế → bị bỏ
        var seats = Room("...x....");
        var res = ClusterFinder.Find(seats, 1, 8, SeatScoring.DefaultIdealRowRatio, SeatPersona.BestViewAudio, 2, 10);
        Assert.DoesNotContain(res, c => c.Seats.Any(s => !s.Free));
        foreach (var c in res)
        {
            var slots = seats.Select(s => new SeatSlot(s.SeatId, s.Row, s.Col, s.Label, s.SeatType, !s.Free)).ToList();
            Assert.Null(OrphanSeatValidator.Validate(slots, c.SeatIds.ToHashSet()));
        }
        // Không có cụm nào gồm A2–A3 (để lẻ A1) hoặc A1–A2 (để lẻ A3)
        Assert.DoesNotContain(res, c => c.Range is "A2–A3" or "A1–A2");
    }

    [Fact]
    public void Cluster_SweetboxOnlyWholePairs_WheelchairExcluded()
    {
        // Hàng cuối: W . . S S S S ; tìm 2 ghế với persona cặp đôi
        var seats = Room("........", "W..SSSS");
        var res = ClusterFinder.Find(seats, 2, 7, SeatScoring.DefaultIdealRowRatio, SeatPersona.PrivateCouple, 2, 10);
        Assert.DoesNotContain(res, c => c.Seats.Any(s => s.SeatType == SeatType.Wheelchair));
        foreach (var c in res.Where(c => c.Seats.Any(s => s.SeatType == SeatType.Sweetbox)))
        {
            Assert.All(c.Seats, s => Assert.Equal(SeatType.Sweetbox, s.SeatType));
            Assert.Single(c.Seats.Select(s => s.PairGroupId).Distinct());
        }
        // 3 ghế không thể cắt đôi một cặp ghế đôi
        var three = ClusterFinder.Find(seats, 2, 7, SeatScoring.DefaultIdealRowRatio, SeatPersona.PrivateCouple, 3, 10);
        Assert.DoesNotContain(three, c => c.Seats.Count(s => s.SeatType == SeatType.Sweetbox) % 2 == 1);
    }

    [Fact]
    public void Cluster_TieBrokenByDistanceToIdealRow_MaxThreeResults()
    {
        var seats = Room(Enumerable.Repeat("....", 8).ToArray());
        var res = ClusterFinder.Find(seats, 8, 4, SeatScoring.DefaultIdealRowRatio, SeatPersona.BestViewAudio, 1, 5);
        Assert.Equal(5, res.Count);
        Assert.Equal(3, ClusterFinder.Find(seats, 8, 4, SeatScoring.DefaultIdealRowRatio, SeatPersona.BestViewAudio, 1).Count);
        for (int i = 1; i < res.Count; i++)
            Assert.True(res[i - 1].Score > res[i].Score || (res[i - 1].Score == res[i].Score && res[i - 1].RowDistance <= res[i].RowDistance));
        Assert.Empty(ClusterFinder.Find(seats, 8, 4, SeatScoring.DefaultIdealRowRatio, SeatPersona.BestViewAudio, 5));
    }

    [Fact]
    public void Cluster_PersonaChangesScores()
    {
        var seats = Room(Enumerable.Repeat("..........", 10).ToArray());
        var near = ClusterFinder.Find(seats, 10, 10, SeatScoring.DefaultIdealRowRatio, SeatPersona.NearScreen, 2)[0];
        var best = ClusterFinder.Find(seats, 10, 10, SeatScoring.DefaultIdealRowRatio, SeatPersona.BestViewAudio, 2)[0];
        Assert.True(near.Seats[0].Row < best.Seats[0].Row);
        var scores = ClusterFinder.ScoreAll(seats, 10, 10, SeatScoring.DefaultIdealRowRatio, SeatPersona.NearAisle);
        Assert.All(scores.Values, v => Assert.InRange(v, 0, 100));
    }

    // ------------------------------------------------------------------ SelfCancelRules (SPEC 3.9)
    private static SelfCancelFacts Facts(DateTime start, int cancels = 0, string channel = BillChannel.Online, string status = BillStatus.Finish,
        bool owner = true, bool corporate = false, string fnb = FnbStatus.None, params string[] tickets) =>
        new(channel, status, owner, corporate, start, tickets.Length == 0 ? new[] { TicketCheckInStatus.NotCheckedIn } : tickets, fnb, cancels);

    [Fact]
    public void SelfCancel_AllowedTwoHoursBefore_WithRemainingCount()
    {
        var ok = SelfCancelRules.Check(Facts(Now.AddHours(3), cancels: 1, fnb: FnbStatus.PendingPickup), Now, Rules.SelfCancelMinMinutesBeforeStart, Rules.SelfCancelMaxPerMonth);
        Assert.True(ok.Ok);
        Assert.Equal(1, ok.RemainingThisMonth);
        Assert.Equal(Now.AddHours(1), ok.Deadline);
        // Đúng mốc StartTime − 120 phút vẫn được
        Assert.True(SelfCancelRules.Check(Facts(Now.AddMinutes(120)), Now, 120, 2).Ok);
    }

    [Fact]
    public void SelfCancel_TooLate_MessageMatchesUiText()
    {
        var start = new DateTime(2026, 11, 13, 16, 30, 0);
        var r = SelfCancelRules.Check(Facts(start), Now, 120, 2);
        Assert.False(r.Ok);
        Assert.Equal("Vé chỉ tự hủy được trước giờ chiếu 2 tiếng. Suất này bắt đầu lúc 16:30.", r.Reason);
    }

    [Fact]
    public void SelfCancel_BlockedByChannelOwnerCheckInFnbAndMonthlyLimit()
    {
        var later = Now.AddDays(1);
        Assert.False(SelfCancelRules.Check(Facts(later, channel: BillChannel.Pos), Now, 120, 2).Ok);
        Assert.False(SelfCancelRules.Check(Facts(later, corporate: true), Now, 120, 2).Ok);
        Assert.False(SelfCancelRules.Check(Facts(later, owner: false), Now, 120, 2).Ok);
        Assert.False(SelfCancelRules.Check(Facts(later, status: BillStatus.Cancel), Now, 120, 2).Ok);
        Assert.False(SelfCancelRules.Check(Facts(later, tickets: new[] { TicketCheckInStatus.NotCheckedIn, TicketCheckInStatus.CheckedIn }), Now, 120, 2).Ok);
        Assert.False(SelfCancelRules.Check(Facts(later, fnb: FnbStatus.Preparing), Now, 120, 2).Ok);
        var limit = SelfCancelRules.Check(Facts(later, cancels: 2), Now, 120, 2);
        Assert.False(limit.Ok);
        Assert.Equal(0, limit.RemainingThisMonth);
        Assert.Contains("tối đa 2 lần", limit.Reason);
        Assert.Equal(new DateTime(2026, 11, 1), SelfCancelRules.MonthStart(Now));
    }

    // ------------------------------------------------------------------ OnlineAgeRules (SPEC 3.1)
    [Fact]
    public void OnlineAge_UnderageAccountBlocked_T13RequiresConfirm()
    {
        var start = new DateTime(2026, 11, 13, 19, 0, 0);
        var end = start.AddHours(2);
        var kid = new DateTime(2014, 1, 1); // 12 tuổi
        var r = OnlineAgeRules.Check(AgeRating.T13, kid, Now, start, end, true, false, Rules.ChildU13LatestEnd, Rules.ChildU16LatestEnd);
        Assert.Equal(ErrorCodes.AgeRestricted, r.ErrorCode);
        var adult = new DateTime(1995, 1, 1);
        Assert.Equal(ErrorCodes.AgeConfirmRequired, OnlineAgeRules.Check(AgeRating.T13, adult, Now, start, end, false, false, Rules.ChildU13LatestEnd, Rules.ChildU16LatestEnd).ErrorCode);
        Assert.True(OnlineAgeRules.Check(AgeRating.T13, adult, Now, start, end, true, false, Rules.ChildU13LatestEnd, Rules.ChildU16LatestEnd).Ok);
        // Không có ngày sinh: chỉ cần tích xác nhận
        Assert.True(OnlineAgeRules.Check(AgeRating.T18, null, Now, start, end, true, false, Rules.ChildU13LatestEnd, Rules.ChildU16LatestEnd).Ok);
        // Phim P suất sớm: không cần tích gì
        Assert.Empty(OnlineAgeRules.Confirmations(AgeRating.P, start, end, Rules.ChildU13LatestEnd, Rules.ChildU16LatestEnd));
        Assert.True(OnlineAgeRules.Check(AgeRating.P, null, Now, start, end, false, false, Rules.ChildU13LatestEnd, Rules.ChildU16LatestEnd).Ok);
    }

    [Fact]
    public void OnlineAge_KNeedsGuardianConfirm_LateShowNotices()
    {
        var start = new DateTime(2026, 11, 13, 20, 30, 0);
        Assert.Equal(ErrorCodes.GuardianRequired, OnlineAgeRules.Check(AgeRating.K, null, Now, new DateTime(2026, 11, 13, 9, 0, 0), new DateTime(2026, 11, 13, 10, 40, 0), false, false, Rules.ChildU13LatestEnd, Rules.ChildU16LatestEnd).ErrorCode);
        Assert.Equal("Trẻ em dưới 13 tuổi không vào được suất này", OnlineAgeRules.CurfewNotice(start, start.AddMinutes(100), Rules.ChildU13LatestEnd, Rules.ChildU16LatestEnd));
        Assert.Equal("Trẻ em dưới 16 tuổi không vào được suất này", OnlineAgeRules.CurfewNotice(start, start.AddMinutes(160), Rules.ChildU13LatestEnd, Rules.ChildU16LatestEnd));
        Assert.Null(OnlineAgeRules.CurfewNotice(start, start.AddMinutes(60), Rules.ChildU13LatestEnd, Rules.ChildU16LatestEnd));
        // Phim P nhưng suất muộn: vẫn phải tích xác nhận, câu có lưu ý giờ chiếu
        var c = OnlineAgeRules.Confirmations(AgeRating.P, start, start.AddMinutes(100), Rules.ChildU13LatestEnd, Rules.ChildU16LatestEnd);
        Assert.Contains(c, x => x.Code == ErrorCodes.AgeConfirmRequired && x.Text.Contains("Trẻ em dưới 13 tuổi không vào được suất này"));
    }

    // ------------------------------------------------------------------ WebhookSignature (SPEC 3.7)
    [Fact]
    public void Webhook_SignatureIsHmacSha256Hex_AndVerifiesExactly()
    {
        var sig = WebhookSignature.Compute("bi-mat", "{\"billId\":1}");
        Assert.Matches("^[0-9a-f]{64}$", sig);
        Assert.True(WebhookSignature.Verify("bi-mat", "{\"billId\":1}", sig));
        Assert.True(WebhookSignature.Verify("bi-mat", "{\"billId\":1}", sig.ToUpperInvariant()));
        Assert.False(WebhookSignature.Verify("bi-mat", "{\"billId\":2}", sig));
        Assert.False(WebhookSignature.Verify("khac", "{\"billId\":1}", sig));
        Assert.False(WebhookSignature.Verify("bi-mat", "{\"billId\":1}", null));
        Assert.False(WebhookSignature.Verify("", "{\"billId\":1}", sig));
    }

    // ------------------------------------------------------------------ TierProgressCalculator (UI C5)
    [Fact]
    public void TierProgress_TowardsNextTier()
    {
        var m = TierProgressCalculator.Of(1_000_000, Rules.TierVipSpend12Months, Rules.TierVvipSpend12Months);
        Assert.Equal((LoyaltyTier.Member, LoyaltyTier.Vip, 25), (m.Tier, m.NextTier, m.Percent));
        var v = TierProgressCalculator.Of(6_000_000, Rules.TierVipSpend12Months, Rules.TierVvipSpend12Months);
        Assert.Equal((LoyaltyTier.Vip, LoyaltyTier.Vvip, 75), (v.Tier, v.NextTier, v.Percent));
        var top = TierProgressCalculator.Of(9_000_000, Rules.TierVipSpend12Months, Rules.TierVvipSpend12Months);
        Assert.Equal((LoyaltyTier.Vvip, (string?)null, 100), (top.Tier, top.NextTier, top.Percent));
    }

    /// <summary>Trang đặt vé báo trước khi tài khoản chưa đủ tuổi (SPEC 3.1-04); cùng câu với lỗi khi giữ ghế.</summary>
    [Fact]
    public void UnderAgeMessage_ByBirthDateAndRating()
    {
        var now = new DateTime(2026, 11, 19, 19, 0, 0);
        var teen = new FPolyCinema.Web.Entities.User { BirthDate = new DateTime(2011, 3, 10) };   // 15 tuổi
        Assert.Equal("Phim T18 dành cho khán giả từ đủ 18 tuổi. Theo ngày sinh trong tài khoản, bạn chưa đủ tuổi xem phim này.",
            FPolyCinema.Web.Services.OnlineBookingService.UnderAgeMessage(teen, AgeRating.T18, now));
        Assert.NotNull(FPolyCinema.Web.Services.OnlineBookingService.UnderAgeMessage(teen, AgeRating.T16, now));
        Assert.Null(FPolyCinema.Web.Services.OnlineBookingService.UnderAgeMessage(teen, AgeRating.T13, now));
        Assert.Null(FPolyCinema.Web.Services.OnlineBookingService.UnderAgeMessage(new FPolyCinema.Web.Entities.User(), AgeRating.T18, now));   // không có ngày sinh
        Assert.Null(FPolyCinema.Web.Services.OnlineBookingService.UnderAgeMessage(null, AgeRating.T18, now));
    }
}
