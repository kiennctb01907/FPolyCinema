using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;

namespace FPolyCinema.Tests;

/// <summary>Unit test cho SeatScoring (SPEC 4.1).</summary>
public class SeatScoringTests
{
    [Fact]
    public void IdealSeat_Scores100()
    {
        // R = 20, IdealRowRatio 0,65 → r_ideal = 13; C = 11 → c_ideal = 6
        Assert.Equal(100, SeatScoring.Score(20, 11, 13, 6, SeatType.Standard, SeatPersona.BestViewAudio, 0.65m));
        // NEAR_SCREEN dùng 0,40: R = 10 → r_ideal = 4
        Assert.Equal(100, SeatScoring.Score(10, 11, 4, 6, SeatType.Standard, SeatPersona.NearScreen, 0.65m));
    }

    [Fact]
    public void RawScore_FollowsFormula()
    {
        // R = 10, C = 14, r = 3, c = 2: r_ideal = 6,5; c_ideal = 7,5
        // S_raw = 100 − (0,6 × 3,5 / 10 × 100 + 0,4 × 5,5 / 7 × 100) = 100 − (21 + 31,43) = 47,57
        Assert.Equal(47.571, SeatScoring.RawScore(10, 14, 3, 2, 0.65m, SeatPersona.BestViewAudio), 3);
        Assert.Equal(48, SeatScoring.Score(10, 14, 3, 2, SeatType.Standard, SeatPersona.BestViewAudio));
    }

    [Theory]
    [InlineData(SeatPersona.BestViewAudio)]
    [InlineData(SeatPersona.NearScreen)]
    [InlineData(SeatPersona.PrivateCouple)]
    [InlineData(SeatPersona.NearAisle)]
    public void FrontRow_AlwaysLowerThanMiddleRow(string persona)
    {
        foreach (var (rows, cols) in new[] { (8, 12), (10, 14), (12, 18), (15, 20), (6, 8) })
        {
            int middle = (rows + 1) / 2;
            for (int c = 1; c <= cols; c++)
            {
                int front = SeatScoring.Score(rows, cols, 1, c, SeatType.Standard, persona);
                int mid = SeatScoring.Score(rows, cols, middle, c, SeatType.Standard, persona);
                Assert.True(front < mid, $"{persona} {rows}x{cols} cột {c}: hàng đầu {front} không thấp hơn hàng giữa {mid}");
            }
        }
    }

    [Fact]
    public void Score_AlwaysWithin0And100()
    {
        foreach (var persona in SeatPersona.All)
        foreach (var type in SeatType.All)
        foreach (var (rows, cols) in new[] { (1, 1), (2, 3), (8, 12), (10, 14), (12, 18), (26, 40) })
        foreach (var ratio in new[] { 0.3m, 0.65m, 1m })
        for (int r = 1; r <= rows; r++)
        for (int c = 1; c <= cols; c++)
        foreach (var edge in new[] { false, true })
        {
            int s = SeatScoring.Score(rows, cols, r, c, type, persona, ratio, edge);
            Assert.InRange(s, 0, 100);
        }
    }

    [Fact]
    public void Score_RoundsThenClampsTo0And100()
    {
        // Ghế lý tưởng ở đầu đoạn với NEAR_AISLE: 100 + 15 = 115 → giới hạn 100
        Assert.Equal(100, SeatScoring.Score(20, 11, 13, 6, SeatType.Standard, SeatPersona.NearAisle, 0.65m, true));
        // Góc hàng đầu phòng 26 × 40: S_raw ≈ 24,3; trừ 25 → −0,7 → làm tròn −1 → giới hạn 0
        Assert.True(SeatScoring.RawScore(26, 40, 1, 1, 0.65m, SeatPersona.BestViewAudio) + SeatScoring.Adjustment(26, 1, SeatType.Standard, SeatPersona.BestViewAudio, false) < 0);
        Assert.Equal(0, SeatScoring.Score(26, 40, 1, 1, SeatType.Standard, SeatPersona.BestViewAudio));
        // Làm tròn: 47,57 → 48 (ở RawScore_FollowsFormula); 4 × 0 + … kiểm thêm trường hợp lẻ ,5: R = 10, C = 10, r = 4, c = 3
        // r_ideal 6,5 → 0,6 × 2,5 / 10 × 100 = 15; c_ideal 5,5 → 0,4 × 2,5 / 5 × 100 = 20 → S_raw = 65
        Assert.Equal(65, SeatScoring.Score(10, 10, 4, 3, SeatType.Standard, SeatPersona.BestViewAudio));
    }

    [Fact]
    public void FirstTwoRows_Minus25_ExceptNearScreen()
    {
        Assert.Equal(-25, SeatScoring.Adjustment(10, 1, SeatType.Standard, SeatPersona.BestViewAudio, false));
        Assert.Equal(-25, SeatScoring.Adjustment(10, 2, SeatType.Standard, SeatPersona.NearAisle, false));
        Assert.Equal(0, SeatScoring.Adjustment(10, 3, SeatType.Standard, SeatPersona.BestViewAudio, false));
        // NEAR_SCREEN chỉ trừ 10 điểm ở r = 1
        Assert.Equal(-10, SeatScoring.Adjustment(10, 1, SeatType.Standard, SeatPersona.NearScreen, false));
        Assert.Equal(0, SeatScoring.Adjustment(10, 2, SeatType.Standard, SeatPersona.NearScreen, false));
    }

    [Fact]
    public void PrivateCouple_Plus20_ForBackRowsOrSweetbox()
    {
        // r ≥ 0,85R: R = 10 → từ hàng 9 (8,5)
        Assert.Equal(20, SeatScoring.Adjustment(10, 9, SeatType.Standard, SeatPersona.PrivateCouple, false));
        Assert.Equal(0, SeatScoring.Adjustment(10, 8, SeatType.Standard, SeatPersona.PrivateCouple, false));
        Assert.Equal(20, SeatScoring.Adjustment(10, 6, SeatType.Sweetbox, SeatPersona.PrivateCouple, false));
        Assert.Equal(0, SeatScoring.Adjustment(10, 9, SeatType.Standard, SeatPersona.BestViewAudio, false));
    }

    [Fact]
    public void NearAisle_Plus15_AtSegmentEnd()
    {
        Assert.Equal(15, SeatScoring.Adjustment(10, 6, SeatType.Standard, SeatPersona.NearAisle, true));
        Assert.Equal(0, SeatScoring.Adjustment(10, 6, SeatType.Standard, SeatPersona.NearAisle, false));
        Assert.Equal(0, SeatScoring.Adjustment(10, 6, SeatType.Standard, SeatPersona.BestViewAudio, true));
        int edge = SeatScoring.Score(10, 14, 6, 5, SeatType.Standard, SeatPersona.NearAisle, 0.65m, true);
        int inner = SeatScoring.Score(10, 14, 6, 5, SeatType.Standard, SeatPersona.NearAisle, 0.65m, false);
        Assert.Equal(15, edge - inner);
    }

    [Fact]
    public void IsSegmentEnd_DetectsAisleAndWall()
    {
        // Hàng 14 cột, lối đi ở cột 4 và 11: đoạn 1–3, 5–10, 12–14
        var cols = new HashSet<int>(Enumerable.Range(1, 14).Where(c => c != 4 && c != 11));
        Assert.True(SeatScoring.IsSegmentEnd(cols, 1, 14));   // kề tường trái
        Assert.True(SeatScoring.IsSegmentEnd(cols, 3, 14));   // kề lối đi
        Assert.True(SeatScoring.IsSegmentEnd(cols, 5, 14));
        Assert.True(SeatScoring.IsSegmentEnd(cols, 10, 14));
        Assert.True(SeatScoring.IsSegmentEnd(cols, 14, 14));  // kề tường phải
        Assert.False(SeatScoring.IsSegmentEnd(cols, 7, 14));
        Assert.False(SeatScoring.IsSegmentEnd(cols, 2, 14));
    }

    [Fact]
    public void IdealCoordinates_FollowSpec()
    {
        Assert.Equal(6.5, SeatScoring.IdealRow(10, 0.65m, SeatPersona.BestViewAudio), 6);
        Assert.Equal(4.0, SeatScoring.IdealRow(10, 0.65m, SeatPersona.NearScreen), 6);
        Assert.Equal(7.5, SeatScoring.IdealCol(14), 6);
    }
}
