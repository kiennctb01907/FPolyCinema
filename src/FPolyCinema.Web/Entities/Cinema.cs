namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng Cinemas: rạp.</summary>
public class Cinema
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool Active { get; set; } = true;
}
