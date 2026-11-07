namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng Holidays: ngày lễ.</summary>
public class Holiday
{
    public DateTime HolidayDate { get; set; }
    public string Name { get; set; } = string.Empty;
}
