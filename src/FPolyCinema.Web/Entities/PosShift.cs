namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng PosShifts: ca bán hàng tại quầy.</summary>
public class PosShift
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CinemaId { get; set; }
    public string TerminalCode { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; }
    public long OpeningCashFloat { get; set; }
    public DateTime? ClosedAt { get; set; }
    public long? ActualCashCounted { get; set; }
    public long? ExpectedCash { get; set; }
    public long? Discrepancy { get; set; }
    public string Status { get; set; } = string.Empty;
}
