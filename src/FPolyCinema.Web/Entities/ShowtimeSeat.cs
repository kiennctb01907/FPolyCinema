using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng ShowtimeSeats: trạng thái ghế theo suất.</summary>
public class ShowtimeSeat
{
    public int ShowtimeId { get; set; }
    public int SeatId { get; set; }
    public string Status { get; set; } = ShowtimeSeatStatus.Available;
    public int? HoldBillId { get; set; }
    public DateTime? HoldExpiresAt { get; set; }
    /// <summary>Đối tượng vé đã chọn cho ghế đang giữ (vé thật tạo lúc thanh toán).</summary>
    public string? HoldAudienceType { get; set; }
    public int? TicketId { get; set; }
}
