namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng Auditoriums: phòng chiếu.</summary>
public class Auditorium
{
    public int Id { get; set; }
    public int CinemaId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int TotalCols { get; set; }
    public decimal IdealRowRatio { get; set; } = 0.65m;
    public bool Active { get; set; } = true;
}
