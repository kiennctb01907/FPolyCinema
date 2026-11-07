namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng PointTransactions: giao dịch điểm.</summary>
public class PointTransaction
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int Points { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int? BillId { get; set; }
    public DateTime CreatedAt { get; set; }
}
