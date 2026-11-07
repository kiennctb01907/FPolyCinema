namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng Vouchers: voucher.</summary>
public class Voucher
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public long Value { get; set; }
    public long MinBillAmount { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidUntil { get; set; }
    public int UsageLimit { get; set; } = 1;
    public int UsedCount { get; set; }
    public int? OwnerUserId { get; set; }
    public string Source { get; set; } = string.Empty;
    public int? CorporateBookingId { get; set; }
    public bool Active { get; set; } = true;
}
