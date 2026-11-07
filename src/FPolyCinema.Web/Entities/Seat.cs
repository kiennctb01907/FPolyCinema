namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng Seats: ghế trong phòng.</summary>
public class Seat
{
    public int Id { get; set; }
    public int AuditoriumId { get; set; }
    public int RowIndex { get; set; }
    public int ColIndex { get; set; }
    public string Label { get; set; } = string.Empty;
    public string SeatType { get; set; } = string.Empty;
    public int? PairGroupId { get; set; }
    public bool IsHouseSeat { get; set; }
    public bool IsMaintenance { get; set; }
    public int BaseViewScore { get; set; }
}
