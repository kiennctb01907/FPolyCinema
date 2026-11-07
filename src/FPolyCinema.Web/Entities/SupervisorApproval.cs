using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Entities;

/// <summary>Một dòng của bảng SupervisorApprovals: yêu cầu phê duyệt của quản lý.</summary>
public class SupervisorApproval
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public int BillId { get; set; }
    public int RequestedBy { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = ApprovalStatus.Pending;
    public string? Method { get; set; }
    public int? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
