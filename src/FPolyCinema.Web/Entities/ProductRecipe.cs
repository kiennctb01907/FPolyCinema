namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng ProductRecipes: định mức nguyên liệu của món.</summary>
public class ProductRecipe
{
    public int Id { get; set; }
    public int DrinkId { get; set; }
    public int RawMaterialId { get; set; }
    public decimal Quantity { get; set; }
}
