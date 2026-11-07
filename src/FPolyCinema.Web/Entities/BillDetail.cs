namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng BillDetails: dòng bắp nước của hóa đơn.</summary>
public class BillDetail
{
    public int Id { get; set; }
    public int BillId { get; set; }
    public int DrinkId { get; set; }
    public int Quantity { get; set; }
    public long Price { get; set; }
    public decimal VatRate { get; set; }
    public long LineFinalAmount { get; set; }
}
