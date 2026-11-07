namespace FPolyCinema.Web.Domain.Constants;

/// <summary>Sáu chiều của bảng giá (cột PricingRules.RuleType).</summary>
public static class PricingRuleType
{
    public const string Base = "BASE";
    public const string DayType = "DAY_TYPE";
    public const string TimeSlot = "TIME_SLOT";
    public const string SeatType = "SEAT_TYPE";
    public const string Format = "FORMAT";
    public const string Blockbuster = "BLOCKBUSTER";

    public static readonly string[] All = { Base, DayType, TimeSlot, SeatType, Format, Blockbuster };
}

/// <summary>Loại ngày tính giá (RuleKey của DAY_TYPE).</summary>
public static class DayType
{
    public const string Weekday = "WEEKDAY";
    public const string Weekend = "WEEKEND";
    public const string Holiday = "HOLIDAY";
    public const string HappyTuesday = "HAPPY_TUESDAY";

    public static readonly string[] All = { Weekday, Weekend, Holiday, HappyTuesday };
}

/// <summary>Khung giờ tính giá (RuleKey của TIME_SLOT).</summary>
public static class TimeSlot
{
    public const string Morning = "MORNING";
    public const string Afternoon = "AFTERNOON";
    public const string Golden = "GOLDEN";
    public const string Late = "LATE";

    public static readonly string[] All = { Morning, Afternoon, Golden, Late };
}

/// <summary>RuleKey hợp lệ theo từng RuleType (CONTRACTS mục 3).</summary>
public static class PricingRuleKey
{
    /// <summary>RuleKey duy nhất của BASE.</summary>
    public const string Default = "DEFAULT";

    /// <summary>RuleKey duy nhất của BLOCKBUSTER (áp cho phim bom tấn).</summary>
    public const string Yes = "YES";

    /// <summary>Danh sách RuleKey hợp lệ của một RuleType; RuleType lạ trả mảng rỗng.</summary>
    public static IReadOnlyList<string> For(string ruleType) => ruleType switch
    {
        PricingRuleType.Base => new[] { Default },
        PricingRuleType.DayType => DayType.All,
        PricingRuleType.TimeSlot => TimeSlot.All,
        PricingRuleType.SeatType => Constants.SeatType.All,
        PricingRuleType.Format => AuditoriumFormat.All,
        PricingRuleType.Blockbuster => new[] { Yes },
        _ => Array.Empty<string>(),
    };

    public static bool IsValid(string ruleType, string ruleKey) => For(ruleType).Contains(ruleKey);
}
