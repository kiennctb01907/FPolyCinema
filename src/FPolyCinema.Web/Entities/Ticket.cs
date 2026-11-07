using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng Tickets: vé.</summary>
public class Ticket
{
    public int Id { get; set; }
    public int BillId { get; set; }
    public int ShowtimeId { get; set; }
    public int SeatId { get; set; }
    public string AudienceType { get; set; } = string.Empty;
    public long ListPrice { get; set; }
    public long FinalPrice { get; set; }
    public decimal VatRate { get; set; }
    public string QrCode { get; set; } = string.Empty;
    public string CheckInStatus { get; set; } = TicketCheckInStatus.NotCheckedIn;
    public DateTime? CheckedInAt { get; set; }
    public int? CheckedInBy { get; set; }
    public int? SwappedFromTicketId { get; set; }
    public DateTime CreatedAt { get; set; }
}
