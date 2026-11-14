using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Utils;

namespace FPolyCinema.Web.Services;

/// <summary>
/// Duyệt hủy hóa đơn từ xa (SPEC 3.6-21, 3.6-22; UI P1-12, A7): thu ngân gửi yêu cầu thay cho nhập PIN → APPROVAL_REQUESTED tới manager_alerts_{cinemaId};
/// quản lý duyệt hoặc từ chối trên popup hoặc dashboard → APPROVAL_DECIDED gửi về máy POS (pos_terminal_{key}) và tới các quản lý khác.
/// Yêu cầu hết hạn sau ApprovalRequestExpireMinutes (EXPIRED, cũng báo về máy POS).
/// </summary>
public class ApprovalService
{
    private readonly IClock _clock;
    private readonly CinemaRules _rules;
    private readonly RealtimeNotifier _notifier;
    private readonly BillService _bills;

    public ApprovalService(IClock clock, CinemaRules rules, RealtimeNotifier notifier, BillService bills)
    {
        _clock = clock;
        _rules = rules;
        _notifier = notifier;
        _bills = bills;
    }

    /// <summary>Nội dung popup của một yêu cầu (UI A7): hóa đơn, lý do, người yêu cầu.</summary>
    public static object Payload(ApprovalDetailRow a) => new
    {
        id = a.Id, billId = a.BillId, billCode = a.BillCode, total = a.Total, totalText = Fmt.Money(a.Total), reason = a.Reason,
        requester = a.RequesterName, createdAt = a.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"), expiresAt = a.ExpiresAt.ToString("yyyy-MM-ddTHH:mm:ss"),
        createdText = Fmt.Time(a.CreatedAt),
    };

    private static string? TerminalOf(int requestedBy) =>
        PosShiftDAL.GetOpenByUser(requestedBy) is { } s ? ShiftService.TerminalKey(s.CinemaId, s.TerminalCode) : null;

    /// <summary>
    /// Thu ngân gửi yêu cầu duyệt từ xa: hóa đơn finish của rạp, chưa có vé vào cửa, chưa có yêu cầu đang chờ. Ghi SupervisorApprovals (PENDING, REMOTE)
    /// rồi phát APPROVAL_REQUESTED. Trả yêu cầu vừa tạo.
    /// </summary>
    public async Task<ApprovalDetailRow> Request(int billId, string? reason, int requestedBy, int cinemaId)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5) throw FieldErrors.Of("reason", "Bạn ghi lý do hủy (ít nhất 5 ký tự).");
        var now = _clock.Now;
        int id = DBUtil.InTransaction((conn, tx) =>
        {
            var b = BillDAL.GetForUpdate(billId, conn, tx) ?? throw CategoryService.NotFound("Không tìm thấy hóa đơn.");
            if (b.CinemaId != cinemaId) throw FieldErrors.Of("billCode", "Hóa đơn thuộc rạp khác.");
            if (b.Status != BillStatus.Finish)
                throw new BusinessRuleException(ErrorCodes.InvalidTransition, "Chỉ hủy được hóa đơn đã thanh toán. Hóa đơn chưa thanh toán thì dùng Hủy hóa đơn.");
            if (TicketDAL.CountCheckedIn(b.Id, conn, tx) > 0)
                throw new BusinessRuleException(ErrorCodes.InvalidTransition, "Hóa đơn đã có vé vào cửa nên không hủy được.");
            if (SupervisorApprovalDAL.CountPendingForBill(b.Id, now, conn, tx) > 0)
                throw new BusinessRuleException(ErrorCodes.ApprovalRequired, "Hóa đơn này đang có yêu cầu chờ quản lý duyệt.");
            return SupervisorApprovalDAL.Insert(new SupervisorApproval
            {
                Type = ApprovalType.VoidBill, BillId = b.Id, RequestedBy = requestedBy, Reason = reason.Trim(), Status = ApprovalStatus.Pending,
                Method = ApprovalMethod.Remote, ExpiresAt = ApprovalRules.ExpiresAt(now, _rules.ApprovalRequestExpireMinutes), CreatedAt = now,
            }, conn, tx);
        });
        var row = SupervisorApprovalDAL.GetDetail(id)!;
        await _notifier.ApprovalRequested(row.CinemaId, Payload(row));
        return row;
    }

    /// <summary>
    /// Quản lý quyết định yêu cầu từ xa: duyệt thì hủy hóa đơn như khi nhập PIN (BillService.VoidPaid, tiền mặt hoàn vào ca đang mở của thu ngân);
    /// từ chối thì REJECTED. Phát APPROVAL_DECIDED về máy POS đã gửi và tới các quản lý của rạp.
    /// </summary>
    public async Task<string> Decide(int approvalId, bool approve, int deciderId)
    {
        var now = _clock.Now;
        var a = SupervisorApprovalDAL.GetDetail(approvalId) ?? throw CategoryService.NotFound("Không tìm thấy yêu cầu phê duyệt.");
        var decider = UserDAL.GetById(deciderId) ?? throw CategoryService.NotFound("Không tìm thấy người duyệt.");
        if (!ApprovalRules.CanApprove(decider.Role, decider.CinemaId, a.CinemaId)) throw new BusinessRuleException(ErrorCodes.AccessDenied, "Bạn không duyệt được yêu cầu của rạp này.");
        var reason = ApprovalRules.CannotDecideReason(a.Status, a.ExpiresAt, now);
        if (reason != null) throw new BusinessRuleException(ErrorCodes.InvalidTransition, reason);
        var terminal = TerminalOf(a.RequestedBy);
        string message;
        if (approve)
        {
            var shift = PosShiftDAL.GetOpenByUser(a.RequestedBy);
            await _bills.VoidPaid(a.BillId, a.Reason, decider, a.RequestedBy, a.CinemaId, shift?.CinemaId == a.CinemaId ? shift.Id : null, a.Id);
            message = $"{decider.FullName} đã duyệt hủy hóa đơn {a.BillCode}. Đã hoàn tiền.";
        }
        else
        {
            if (DBUtil.InTransaction((conn, tx) => SupervisorApprovalDAL.Decide(a.Id, ApprovalStatus.Rejected, decider.Id, now, conn, tx)) != 1)
                throw new BusinessRuleException(ErrorCodes.InvalidTransition, "Yêu cầu đã được xử lý trước đó.");
            message = $"{decider.FullName} đã từ chối hủy hóa đơn {a.BillCode}.";
        }
        await _notifier.ApprovalDecided(terminal, a.CinemaId, new
        {
            id = a.Id, billId = a.BillId, billCode = a.BillCode, status = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected, decider = decider.FullName, message,
        });
        return message;
    }

    /// <summary>Yêu cầu quá hạn chuyển EXPIRED và báo về máy POS (gọi từ ShowtimeClockService).</summary>
    public async Task<int> ExpireOverdue()
    {
        var now = _clock.Now;
        var rows = DBUtil.InTransaction((conn, tx) => SupervisorApprovalDAL.ExpireOverdue(now, conn, tx));
        foreach (var a in rows)
            await _notifier.ApprovalDecided(TerminalOf(a.RequestedBy), a.CinemaId, new
            {
                id = a.Id, billId = a.BillId, billCode = a.BillCode, status = ApprovalStatus.Expired, decider = (string?)null,
                message = $"Yêu cầu hủy hóa đơn {a.BillCode} đã hết hạn sau {_rules.ApprovalRequestExpireMinutes} phút mà chưa có quản lý duyệt.",
            });
        return rows.Count;
    }

    /// <summary>Yêu cầu đang chờ của rạp (null là mọi rạp) cho dashboard và popup.</summary>
    public List<ApprovalDetailRow> Pending(int? cinemaId) => SupervisorApprovalDAL.PendingDetails(cinemaId, _clock.Now);
}
