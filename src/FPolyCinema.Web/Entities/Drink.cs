namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng Drinks: đồ uống, món ăn và combo.</summary>
public class Drink
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long Price { get; set; }
    public string? Image { get; set; }
    public string? Description { get; set; }
    public bool Active { get; set; } = true;
    public bool IsCombo { get; set; }
    public bool IsSoldOut { get; set; }
    public DateTime CreatedAt { get; set; }
}
