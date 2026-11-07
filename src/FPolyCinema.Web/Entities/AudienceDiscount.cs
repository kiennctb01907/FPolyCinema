namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng AudienceDiscounts: mức giảm theo đối tượng vé.</summary>
public class AudienceDiscount
{
    public string AudienceType { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; }
    public decimal LegalMinPercent { get; set; }
    public bool PosOnly { get; set; }
    public bool RequiresDocument { get; set; }
}
