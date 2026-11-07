namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng WalletTransactions: giao dịch ví.</summary>
public class WalletTransaction
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public long Amount { get; set; }
    public long BalanceAfter { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int? BillId { get; set; }
    public DateTime CreatedAt { get; set; }
}
