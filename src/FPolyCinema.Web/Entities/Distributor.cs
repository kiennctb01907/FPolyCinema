namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng Distributors: nhà phát hành phim.</summary>
public class Distributor
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool Active { get; set; } = true;
}
