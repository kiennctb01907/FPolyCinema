namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng InventoryTransactions: giao dịch kho.</summary>
public class InventoryTransaction
{
    public int Id { get; set; }
    public int RawMaterialId { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public int? BillId { get; set; }
    public int? WastageLogId { get; set; }
    public string? Note { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
