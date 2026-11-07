namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng Payments: giao dịch thanh toán và hoàn tiền.</summary>
public class Payment
{
    public int Id { get; set; }
    public int BillId { get; set; }
    public int? PosShiftId { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ProviderTxnId { get; set; }
    public string? RawPayload { get; set; }
    public DateTime CreatedAt { get; set; }
}
