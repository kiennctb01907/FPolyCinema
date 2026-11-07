using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng WastageLogs: phiếu hủy hàng hỏng.</summary>
public class WastageLog
{
    public int Id { get; set; }
    public int CinemaId { get; set; }
    public string Status { get; set; } = WastageStatus.Pending;
    public string? Note { get; set; }
    public int CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
}
