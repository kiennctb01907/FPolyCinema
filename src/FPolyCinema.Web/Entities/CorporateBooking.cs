namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng CorporateBookings: hợp đồng doanh nghiệp.</summary>
public class CorporateBooking
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string Type { get; set; } = string.Empty;
    public int? ShowtimeId { get; set; }
    public int? VoucherCount { get; set; }
    public long ContractAmount { get; set; }
    public int? BillId { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
