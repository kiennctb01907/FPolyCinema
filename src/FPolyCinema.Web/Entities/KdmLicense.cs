namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng KdmLicenses: khóa KDM của phim tại rạp.</summary>
public class KdmLicense
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public int CinemaId { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidUntil { get; set; }
    public string? Note { get; set; }
}
