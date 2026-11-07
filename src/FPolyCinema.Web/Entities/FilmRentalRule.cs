namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng FilmRentalRules: tỷ lệ chia doanh thu theo tuần.</summary>
public class FilmRentalRule
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public int WeekNumber { get; set; }
    public decimal DistributorSharePercent { get; set; }
}
