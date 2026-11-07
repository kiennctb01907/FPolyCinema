namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng RawMaterials: nguyên liệu và bao bì theo rạp.</summary>
public class RawMaterial
{
    public int Id { get; set; }
    public int CinemaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal ActualStock { get; set; }
    public decimal ReservedStock { get; set; }
    public decimal MinThreshold { get; set; }
    public bool Active { get; set; } = true;
}
