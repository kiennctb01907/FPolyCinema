namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng Categories: loại đồ uống (danh mục bắp nước).</summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
}
