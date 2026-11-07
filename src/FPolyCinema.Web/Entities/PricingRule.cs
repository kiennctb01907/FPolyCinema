namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng PricingRules: bảng giá 6 chiều theo rạp.</summary>
public class PricingRule
{
    public int Id { get; set; }
    public int CinemaId { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public string RuleKey { get; set; } = string.Empty;
    public long Amount { get; set; }
}
