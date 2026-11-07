using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Bảng giá 6 chiều của một rạp: (RuleType, RuleKey) → số tiền (có thể âm).</summary>
public sealed class PriceTable
{
    private readonly Dictionary<(string Type, string Key), long> _amounts = new();

    public PriceTable Set(string ruleType, string ruleKey, long amount)
    {
        _amounts[(ruleType, ruleKey)] = amount;
        return this;
    }

    /// <summary>Số tiền của một dòng; dòng chưa khai báo coi là 0.</summary>
    public long Amount(string ruleType, string ruleKey) => _amounts.TryGetValue((ruleType, ruleKey), out var v) ? v : 0;

    public bool Has(string ruleType, string ruleKey) => _amounts.ContainsKey((ruleType, ruleKey));
}

/// <summary>Dữ liệu cần để tính giá một vé.</summary>
public sealed record PriceInput(
    DateTime StartTime,
    DateTime FeatureStartTime,
    string AuditoriumFormat,
    string SeatType,
    bool IsBlockbuster,
    decimal AudienceDiscountPercent);

/// <summary>Kết quả tính giá, kèm từng thành phần để hiển thị ở trang thử tính giá.</summary>
public sealed record PriceBreakdown(
    DateTime OperatingDate,
    string DayType,
    string TimeSlot,
    long BaseAmount,
    long DayTypeAmount,
    long TimeSlotAmount,
    long SeatTypeAmount,
    long FormatAmount,
    long BlockbusterAmount,
    long GrossAmount,
    decimal AudienceDiscountPercent,
    long ListPrice);

/// <summary>
/// Tính giá vé (SPEC 3.4). Logic thuần, có unit test.
///   Gốc      = BASE + DAY_TYPE + TIME_SLOT + SEAT_TYPE + FORMAT + (phim bom tấn ? BLOCKBUSTER : 0)
///   ListPrice = làm tròn theo PriceRoundTo( Gốc × (100 − % giảm của AudienceType) / 100 ), không âm
/// Giá đã gồm VAT; tiền trước thuế = làm tròn(FinalPrice × 100 / (100 + VatRate)).
/// </summary>
public static class PricingCalculator
{
    /// <summary>Suất bắt đầu trước giờ này thuộc ngày vận hành hôm trước và tính khung giờ LATE.</summary>
    public static readonly TimeSpan NightCutoff = new(6, 0, 0);

    /// <summary>Ngày vận hành của suất: bắt đầu trước 06:00 thì thuộc ngày hôm trước.</summary>
    public static DateTime OperatingDate(DateTime startTime) =>
        startTime.TimeOfDay < NightCutoff ? startTime.Date.AddDays(-1) : startTime.Date;

    /// <summary>
    /// DAY_TYPE theo ngày vận hành: HOLIDAY nếu ngày có trong Holidays; nếu không thì HAPPY_TUESDAY vào thứ Ba,
    /// WEEKEND vào thứ Bảy và Chủ nhật, còn lại là WEEKDAY.
    /// </summary>
    public static string DayTypeOf(DateTime operatingDate, IEnumerable<DateTime> holidays)
    {
        var d = operatingDate.Date;
        if (holidays.Any(h => h.Date == d)) return DayType.Holiday;
        return d.DayOfWeek switch
        {
            DayOfWeek.Tuesday => DayType.HappyTuesday,
            DayOfWeek.Saturday or DayOfWeek.Sunday => DayType.Weekend,
            _ => DayType.Weekday,
        };
    }

    /// <summary>
    /// TIME_SLOT theo FeatureStartTime: trước PriceSlotAfternoonStart là MORNING, trước PriceSlotGoldenStart là AFTERNOON,
    /// trước PriceSlotLateStart là GOLDEN, còn lại là LATE. Suất trước 06:00 là LATE.
    /// </summary>
    public static string TimeSlotOf(DateTime featureStartTime, CinemaRules rules)
    {
        var t = featureStartTime.TimeOfDay;
        if (t < NightCutoff) return TimeSlot.Late;
        if (t < rules.PriceSlotAfternoonStart) return TimeSlot.Morning;
        if (t < rules.PriceSlotGoldenStart) return TimeSlot.Afternoon;
        if (t < rules.PriceSlotLateStart) return TimeSlot.Golden;
        return TimeSlot.Late;
    }

    /// <summary>Làm tròn đến bội số roundTo gần nhất; đúng nửa (ví dụ 500 khi roundTo = 1000) thì làm tròn lên. Không âm.</summary>
    public static long RoundPrice(decimal amount, int roundTo)
    {
        if (roundTo <= 0) roundTo = 1;
        if (amount <= 0) return 0;
        var units = Math.Floor(amount / roundTo + 0.5m);
        return (long)(units * roundTo);
    }

    /// <summary>Tính giá niêm yết của một vé theo bảng giá của rạp.</summary>
    public static PriceBreakdown Calculate(PriceInput input, PriceTable table, IEnumerable<DateTime> holidays, CinemaRules rules)
    {
        if (!table.Has(PricingRuleType.Base, PricingRuleKey.Default))
            throw new InvalidOperationException("Bảng giá của rạp chưa có dòng BASE/DEFAULT.");
        if (input.AudienceDiscountPercent < 0 || input.AudienceDiscountPercent > 100)
            throw new ArgumentOutOfRangeException(nameof(input), "Phần trăm giảm phải nằm trong 0–100.");

        var operatingDate = OperatingDate(input.StartTime);
        var dayType = DayTypeOf(operatingDate, holidays);
        var timeSlot = TimeSlotOf(input.FeatureStartTime, rules);

        long baseAmount = table.Amount(PricingRuleType.Base, PricingRuleKey.Default);
        long dayAmount = table.Amount(PricingRuleType.DayType, dayType);
        long slotAmount = table.Amount(PricingRuleType.TimeSlot, timeSlot);
        long seatAmount = table.Amount(PricingRuleType.SeatType, input.SeatType);
        long formatAmount = table.Amount(PricingRuleType.Format, input.AuditoriumFormat);
        long blockbusterAmount = input.IsBlockbuster ? table.Amount(PricingRuleType.Blockbuster, PricingRuleKey.Yes) : 0;

        long gross = baseAmount + dayAmount + slotAmount + seatAmount + formatAmount + blockbusterAmount;
        var discounted = (decimal)gross * (100m - input.AudienceDiscountPercent) / 100m;
        var listPrice = RoundPrice(discounted, rules.PriceRoundTo);

        return new PriceBreakdown(operatingDate, dayType, timeSlot, baseAmount, dayAmount, slotAmount, seatAmount,
            formatAmount, blockbusterAmount, gross, input.AudienceDiscountPercent, listPrice);
    }

    /// <summary>Tiền trước thuế của một khoản đã gồm VAT: làm tròn(amount × 100 / (100 + vatRate)).</summary>
    public static long PreTaxAmount(long amountIncludingVat, decimal vatRate)
    {
        if (vatRate < 0) throw new ArgumentOutOfRangeException(nameof(vatRate));
        return (long)Math.Round(amountIncludingVat * 100m / (100m + vatRate), MidpointRounding.AwayFromZero);
    }

    /// <summary>Tiền VAT nằm trong một khoản đã gồm VAT.</summary>
    public static long VatAmount(long amountIncludingVat, decimal vatRate) => amountIncludingVat - PreTaxAmount(amountIncludingVat, vatRate);
}
