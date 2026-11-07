using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Tests;

/// <summary>Unit test cho PricingCalculator: cả 6 chiều của bảng giá, các đối tượng vé, làm tròn và VAT (SPEC 3.4).</summary>
public class PricingCalculatorTests
{
    private static readonly CinemaRules Rules = new();

    // Thứ Tư 04/11/2026 là ngày thường; thứ Ba 03/11; thứ Bảy 07/11; Chủ nhật 08/11
    private static readonly DateTime Wednesday = new(2026, 11, 4);
    private static readonly DateTime Tuesday = new(2026, 11, 3);
    private static readonly DateTime Saturday = new(2026, 11, 7);
    private static readonly DateTime Sunday = new(2026, 11, 8);

    private static PriceTable Table() => new PriceTable()
        .Set(PricingRuleType.Base, PricingRuleKey.Default, 70_000)
        .Set(PricingRuleType.DayType, DayType.Weekday, 0)
        .Set(PricingRuleType.DayType, DayType.Weekend, 15_000)
        .Set(PricingRuleType.DayType, DayType.Holiday, 20_000)
        .Set(PricingRuleType.DayType, DayType.HappyTuesday, -20_000)
        .Set(PricingRuleType.TimeSlot, TimeSlot.Morning, -10_000)
        .Set(PricingRuleType.TimeSlot, TimeSlot.Afternoon, 0)
        .Set(PricingRuleType.TimeSlot, TimeSlot.Golden, 10_000)
        .Set(PricingRuleType.TimeSlot, TimeSlot.Late, -5_000)
        .Set(PricingRuleType.SeatType, SeatType.Standard, 0)
        .Set(PricingRuleType.SeatType, SeatType.Vip, 15_000)
        .Set(PricingRuleType.SeatType, SeatType.Sweetbox, 30_000)
        .Set(PricingRuleType.SeatType, SeatType.Wheelchair, 0)
        .Set(PricingRuleType.Format, AuditoriumFormat.F2D, 0)
        .Set(PricingRuleType.Format, AuditoriumFormat.F3D, 20_000)
        .Set(PricingRuleType.Format, AuditoriumFormat.IMAX, 50_000)
        .Set(PricingRuleType.Format, AuditoriumFormat.F4DX, 70_000)
        .Set(PricingRuleType.Blockbuster, PricingRuleKey.Yes, 10_000);

    private static PriceBreakdown Price(DateTime start, string format = AuditoriumFormat.F2D, string seat = SeatType.Standard,
        bool blockbuster = false, decimal discount = 0, IEnumerable<DateTime>? holidays = null)
        => PricingCalculator.Calculate(new PriceInput(start, start.AddMinutes(15), format, seat, blockbuster, discount),
            Table(), holidays ?? Array.Empty<DateTime>(), Rules);

    [Fact]
    public void Base_WeekdayAfternoon2DStandard_IsBaseOnly()
    {
        var p = Price(Wednesday.AddHours(14));
        Assert.Equal(DayType.Weekday, p.DayType);
        Assert.Equal(TimeSlot.Afternoon, p.TimeSlot);
        Assert.Equal(70_000, p.ListPrice);
    }

    [Fact]
    public void Base_MissingBaseRule_Throws()
        => Assert.Throws<InvalidOperationException>(() =>
            PricingCalculator.Calculate(new PriceInput(Wednesday.AddHours(14), Wednesday.AddHours(14).AddMinutes(15), AuditoriumFormat.F2D, SeatType.Standard, false, 0),
                new PriceTable(), Array.Empty<DateTime>(), Rules));

    [Fact]
    public void DayType_TuesdayIsHappyTuesday_WithNegativeAmount()
    {
        var p = Price(Tuesday.AddHours(14));
        Assert.Equal(DayType.HappyTuesday, p.DayType);
        Assert.Equal(-20_000, p.DayTypeAmount);
        Assert.Equal(50_000, p.ListPrice);
    }

    [Fact]
    public void DayType_SaturdayAndSundayAreWeekend()
    {
        Assert.Equal(DayType.Weekend, Price(Saturday.AddHours(14)).DayType);
        Assert.Equal(DayType.Weekend, Price(Sunday.AddHours(14)).DayType);
        Assert.Equal(85_000, Price(Sunday.AddHours(14)).ListPrice);
    }

    [Fact]
    public void DayType_HolidayWinsOverTuesdayAndWeekend()
    {
        Assert.Equal(DayType.Holiday, Price(Tuesday.AddHours(14), holidays: new[] { Tuesday }).DayType);
        var p = Price(Saturday.AddHours(14), holidays: new[] { Saturday });
        Assert.Equal(DayType.Holiday, p.DayType);
        Assert.Equal(90_000, p.ListPrice);
    }

    [Fact]
    public void DayType_UsesOperatingDay_ShowBefore6amBelongsToPreviousDay()
    {
        // 00:30 sáng thứ Tư thuộc ngày vận hành thứ Ba → HAPPY_TUESDAY, khung LATE
        var p = Price(Wednesday.AddMinutes(30));
        Assert.Equal(Tuesday, p.OperatingDate);
        Assert.Equal(DayType.HappyTuesday, p.DayType);
        Assert.Equal(TimeSlot.Late, p.TimeSlot);
        Assert.Equal(Tuesday, PricingCalculator.OperatingDate(Wednesday.AddHours(5).AddMinutes(59)));
        Assert.Equal(Wednesday, PricingCalculator.OperatingDate(Wednesday.AddHours(6)));
    }

    [Theory]
    [InlineData(6, 0, TimeSlot.Morning)]
    [InlineData(11, 59, TimeSlot.Morning)]
    [InlineData(12, 0, TimeSlot.Afternoon)]
    [InlineData(16, 59, TimeSlot.Afternoon)]
    [InlineData(17, 0, TimeSlot.Golden)]
    [InlineData(21, 59, TimeSlot.Golden)]
    [InlineData(22, 0, TimeSlot.Late)]
    [InlineData(23, 45, TimeSlot.Late)]
    [InlineData(1, 0, TimeSlot.Late)]
    [InlineData(5, 59, TimeSlot.Late)]
    public void TimeSlot_ByFeatureStartTime(int hour, int minute, string expected)
        => Assert.Equal(expected, PricingCalculator.TimeSlotOf(Wednesday.AddHours(hour).AddMinutes(minute), Rules));

    [Fact]
    public void TimeSlot_UsesFeatureStartNotStartTime()
    {
        // Bắt đầu quảng cáo 16:50, phim chiếu từ 17:05 → GOLDEN
        var start = Wednesday.AddHours(16).AddMinutes(50);
        var p = PricingCalculator.Calculate(new PriceInput(start, start.AddMinutes(15), AuditoriumFormat.F2D, SeatType.Standard, false, 0), Table(), Array.Empty<DateTime>(), Rules);
        Assert.Equal(TimeSlot.Golden, p.TimeSlot);
        Assert.Equal(80_000, p.ListPrice);
    }

    [Theory]
    [InlineData(SeatType.Standard, 70_000)]
    [InlineData(SeatType.Vip, 85_000)]
    [InlineData(SeatType.Sweetbox, 100_000)]
    [InlineData(SeatType.Wheelchair, 70_000)]
    public void SeatType_AddsAmountPerSeat(string seat, long expected)
        => Assert.Equal(expected, Price(Wednesday.AddHours(14), seat: seat).ListPrice);

    [Theory]
    [InlineData(AuditoriumFormat.F2D, 70_000)]
    [InlineData(AuditoriumFormat.F3D, 90_000)]
    [InlineData(AuditoriumFormat.IMAX, 120_000)]
    [InlineData(AuditoriumFormat.F4DX, 140_000)]
    public void Format_AddsAmount(string format, long expected)
        => Assert.Equal(expected, Price(Wednesday.AddHours(14), format: format).ListPrice);

    [Fact]
    public void Blockbuster_AddsAmountOnlyForBlockbuster()
    {
        Assert.Equal(80_000, Price(Wednesday.AddHours(14), blockbuster: true).ListPrice);
        Assert.Equal(10_000, Price(Wednesday.AddHours(14), blockbuster: true).BlockbusterAmount);
        Assert.Equal(0, Price(Wednesday.AddHours(14), blockbuster: false).BlockbusterAmount);
    }

    [Fact]
    public void AllSixDimensions_AreSummedThenDiscounted()
    {
        // Chủ nhật, 19:00, IMAX, VIP, bom tấn: 70 + 15 + 10 + 15 + 50 + 10 = 170.000; U22 giảm 10% → 153.000
        var p = Price(Sunday.AddHours(18).AddMinutes(45), AuditoriumFormat.IMAX, SeatType.Vip, true, 10);
        Assert.Equal(170_000, p.GrossAmount);
        Assert.Equal(153_000, p.ListPrice);
    }

    [Theory]
    [InlineData(AudienceType.Adult, 0, 100_000)]
    [InlineData(AudienceType.U22, 10, 90_000)]
    [InlineData(AudienceType.ChildU13, 20, 80_000)]
    [InlineData(AudienceType.Child13To15, 20, 80_000)]
    [InlineData(AudienceType.Senior, 20, 80_000)]
    [InlineData(AudienceType.Merit, 20, 80_000)]
    [InlineData(AudienceType.Hardship, 20, 80_000)]
    [InlineData(AudienceType.DisabledSevere, 50, 50_000)]
    [InlineData(AudienceType.DisabledExtreme, 100, 0)]
    public void AudienceTypes_ApplyDiscountOfContracts(string audience, int discount, long expected)
    {
        Assert.Contains(audience, AudienceType.All);
        // Gốc 100.000: thứ Tư 14:00, 3D (+20.000), bom tấn (+10.000)
        var p = Price(Wednesday.AddHours(13).AddMinutes(45), AuditoriumFormat.F3D, SeatType.Standard, true, discount);
        Assert.Equal(100_000, p.GrossAmount);
        Assert.Equal(expected, p.ListPrice);
    }

    [Theory]
    [InlineData(85_500, 86_000)]
    [InlineData(85_499, 85_000)]
    [InlineData(85_000, 85_000)]
    [InlineData(500, 1_000)]
    [InlineData(499, 0)]
    [InlineData(0, 0)]
    [InlineData(-5_000, 0)]
    public void RoundPrice_ToNearestThousand_HalfUp_NotNegative(int amount, long expected)
        => Assert.Equal(expected, PricingCalculator.RoundPrice(amount, 1000));

    [Fact]
    public void ListPrice_RoundedAfterDiscount()
    {
        // Gốc 95.000 (thứ Tư 19:00, 2D, VIP: 70 + 10 + 15) × 90% = 85.500 → 86.000
        var p = Price(Wednesday.AddHours(18).AddMinutes(45), AuditoriumFormat.F2D, SeatType.Vip, false, 10);
        Assert.Equal(95_000, p.GrossAmount);
        Assert.Equal(86_000, p.ListPrice);
    }

    [Fact]
    public void ListPrice_NeverNegative()
    {
        var table = new PriceTable().Set(PricingRuleType.Base, PricingRuleKey.Default, 10_000).Set(PricingRuleType.DayType, DayType.HappyTuesday, -50_000);
        var start = Tuesday.AddHours(14);
        var p = PricingCalculator.Calculate(new PriceInput(start, start.AddMinutes(15), AuditoriumFormat.F2D, SeatType.Standard, false, 0), table, Array.Empty<DateTime>(), Rules);
        Assert.Equal(0, p.ListPrice);
    }

    [Theory]
    [InlineData(108_000, 8, 100_000)]
    [InlineData(90_000, 8, 83_333)]
    [InlineData(50_000, 8, 46_296)]
    [InlineData(110_000, 10, 100_000)]
    public void PreTaxAmount_FromVatInclusivePrice(long price, int vat, long expected)
    {
        Assert.Equal(expected, PricingCalculator.PreTaxAmount(price, vat));
        Assert.Equal(price - expected, PricingCalculator.VatAmount(price, vat));
    }
}
