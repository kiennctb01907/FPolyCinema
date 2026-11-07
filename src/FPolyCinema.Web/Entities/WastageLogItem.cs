namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng WastageLogItems: dòng của phiếu hủy hàng.</summary>
public class WastageLogItem
{
    public int Id { get; set; }
    public int WastageLogId { get; set; }
    public int RawMaterialId { get; set; }
    public decimal Quantity { get; set; }
    public string? Reason { get; set; }
}
