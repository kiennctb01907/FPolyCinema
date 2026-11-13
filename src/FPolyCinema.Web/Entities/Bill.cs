using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng Bills: hóa đơn.</summary>
public class Bill
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public int CinemaId { get; set; }
    public int? ShowtimeId { get; set; }
    public int? UserId { get; set; }
    public int? CustomerId { get; set; }
    public int? PosShiftId { get; set; }
    public string? TerminalCode { get; set; }
    public string Status { get; set; } = BillStatus.Waiting;
    public string? CancelReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public long TicketSubtotal { get; set; }
    public long FnbSubtotal { get; set; }
    public long DiscountTotal { get; set; }
    public long Total { get; set; }
    public int? VoucherId { get; set; }
    public int PointsUsed { get; set; }
    public DateTime? HoldExpiresAt { get; set; }
    /// <summary>Thu ngân đã gia hạn giữ ghế một lần (SPEC 3.5).</summary>
    public bool HoldExtended { get; set; }
    public string CheckInStatus { get; set; } = BillCheckInStatus.None;
    public string FnbStatus { get; set; } = Domain.Constants.FnbStatus.None;
    public int? PickupNumber { get; set; }
    public bool GuardianConfirmed { get; set; }
    public bool AgeConfirmed { get; set; }
    public int? CorporateBookingId { get; set; }
}
