using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Tests;

/// <summary>Unit test cho các mốc thời gian của suất chiếu (SPEC 3.3), nhãn ghế (SPEC 3.2) và mã sinh tự động (CONTRACTS mục 9).</summary>
public class ShowtimeRulesTests
{
    private static readonly CinemaRules Rules = new();
    private static readonly DateTime Day = new(2026, 11, 6);

    [Fact]
    public void Timeline_StartRoundedFeatureEndCleaning()
    {
        // Yêu cầu 09:02, phim 112 phút: Start 09:00; Feature 09:15; End 11:07; Cleaning 11:22 → làm tròn lên 11:25
        var t = ShowtimeTimeCalculator.Compute(Day.AddHours(9).AddMinutes(2), 112, Rules);
        Assert.Equal(Day.AddHours(9), t.StartTime);
        Assert.Equal(Day.AddHours(9).AddMinutes(15), t.FeatureStartTime);
        Assert.Equal(Day.AddHours(11).AddMinutes(7), t.EndTime);
        Assert.Equal(Day.AddHours(11).AddMinutes(25), t.CleaningEndTime);
    }

    [Fact]
    public void Timeline_StartRoundsToNearestMultiple()
    {
        Assert.Equal(Day.AddHours(9).AddMinutes(5), ShowtimeTimeCalculator.Compute(Day.AddHours(9).AddMinutes(3), 90, Rules).StartTime);
        Assert.Equal(Day.AddHours(9).AddMinutes(5), ShowtimeTimeCalculator.RoundToNearest(Day.AddHours(9).AddMinutes(2).AddSeconds(30), 5));
        Assert.Equal(Day.AddHours(9), ShowtimeTimeCalculator.RoundToNearest(Day.AddHours(9).AddMinutes(2).AddSeconds(29), 5));
    }

    [Fact]
    public void Timeline_CleaningEndAlreadyMultiple_NotChanged()
    {
        // Feature 10:15 + 100 phút = 11:55; + 15 = 12:10 (đã là bội số 5)
        var t = ShowtimeTimeCalculator.Compute(Day.AddHours(10), 100, Rules);
        Assert.Equal(Day.AddHours(12).AddMinutes(10), t.CleaningEndTime);
        Assert.Equal(Day.AddHours(12).AddMinutes(15), ShowtimeTimeCalculator.RoundUp(Day.AddHours(12).AddMinutes(10).AddSeconds(1), 5));
    }

    [Fact]
    public void Rounding_IsMeasuredFromMidnightForAnyStep()
    {
        // Bước 7 phút không chia hết 1440: mốc tính từ 00:00 của ngày (09:05 = 545 phút; 77 × 7 = 539 → 08:59, 78 × 7 = 546 → 09:06)
        Assert.Equal(Day.AddMinutes(546), ShowtimeTimeCalculator.RoundToNearest(Day.AddHours(9).AddMinutes(5), 7));
        Assert.Equal(Day.AddMinutes(539), ShowtimeTimeCalculator.RoundToNearest(Day.AddHours(9).AddMinutes(1), 7));
        Assert.Equal(Day.AddMinutes(546), ShowtimeTimeCalculator.RoundUp(Day.AddHours(9).AddMinutes(1), 7));
        Assert.Equal(Day.AddMinutes(539), ShowtimeTimeCalculator.RoundUp(Day.AddMinutes(539), 7));
        // Làm tròn lên qua nửa đêm sang ngày hôm sau
        Assert.Equal(Day.AddDays(1), ShowtimeTimeCalculator.RoundUp(Day.AddHours(23).AddMinutes(58), 5));
    }

    [Fact]
    public void Timeline_UsesConfiguredMinutes()
    {
        var rules = new CinemaRules { AdsMinutes = 10, CleaningMinutes = 20, ScheduleRoundMinutes = 10 };
        var t = ShowtimeTimeCalculator.Compute(Day.AddHours(20).AddMinutes(4), 95, rules);
        Assert.Equal(Day.AddHours(20), t.StartTime);
        Assert.Equal(Day.AddHours(20).AddMinutes(10), t.FeatureStartTime);
        Assert.Equal(Day.AddHours(21).AddMinutes(45), t.EndTime);
        Assert.Equal(Day.AddHours(22).AddMinutes(10), t.CleaningEndTime);
    }

    [Fact]
    public void SeatLabels_RowLetterAndOrdinalSkippingEmptyCells()
    {
        Assert.Equal('A', SeatLabeler.RowLetter(1));
        Assert.Equal('G', SeatLabeler.RowLetter(7));
        Assert.Equal('Z', SeatLabeler.RowLetter(26));
        Assert.Throws<ArgumentOutOfRangeException>(() => SeatLabeler.RowLetter(27));
        // Hàng 7 có lối đi ở cột 4: cột 1, 2, 3, 5, 6, 7, 8 → G1, G2, G3, G4, G5, G6, G7
        var cells = new[] { 1, 2, 3, 5, 6, 7, 8 }.Select(c => (7, c));
        var labels = SeatLabeler.AssignLabels(cells);
        Assert.Equal("G3", labels[(7, 3)]);
        Assert.Equal("G4", labels[(7, 5)]);
        Assert.Equal("G7", labels[(7, 8)]);
    }

    [Fact]
    public void BillCode_HdTimestampAnd4UpperLetters()
    {
        var code = CodeGenerator.BillCode(new DateTime(2026, 11, 6, 21, 5, 9));
        Assert.Matches("^HD20261106210509[A-Z]{4}$", code);
    }

    [Fact]
    public void TicketQrCode_Tk12RandomCharsAndUnique()
    {
        var codes = Enumerable.Range(0, 5000).Select(_ => CodeGenerator.TicketQrCode()).ToList();
        Assert.All(codes, c => Assert.Matches("^TK[A-Z0-9]{12}$", c));
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public void MemberCode_PickupNumber_TerminalCode()
    {
        Assert.Equal("TV00000001", CodeGenerator.MemberCode(1));
        Assert.Equal("TV12345678", CodeGenerator.MemberCode(12_345_678));
        Assert.Equal(1, CodeGenerator.NextPickupNumber(null));
        Assert.Equal(8, CodeGenerator.NextPickupNumber(7));
        Assert.Equal(1, CodeGenerator.NextPickupNumber(999));
        Assert.Equal("007", CodeGenerator.FormatPickupNumber(7));
        Assert.Equal("POS-01", CodeGenerator.TerminalCode(1));
        Assert.Equal("POS-02", CodeGenerator.TerminalCode(2));
    }
}
