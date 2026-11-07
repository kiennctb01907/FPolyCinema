using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>
/// Máy trạng thái của hóa đơn (CONTRACTS mục 5). Logic thuần, không truy cập DB.
/// Mọi thay đổi Bills.Status đều phải qua CanTransition: BillStatusService.ChangeStatus gọi hàm này (qua EnsureTransition)
/// rồi mới ghi bằng BillDAL.UpdateStatus.
///
/// Bảng chuyển trạng thái hợp lệ:
/// | Từ               | Sang                          | Ai thực hiện (actorRole)                         |
/// |------------------|-------------------------------|--------------------------------------------------|
/// | waiting          | finish                        | PosStaff (quầy); System (webhook thành công)    |
/// | waiting          | cancel, CANCELLED_BEFORE_PAYMENT | PosStaff; Customer (hóa đơn online của mình)  |
/// | waiting          | cancel, EXPIRED               | System                                           |
/// | finish           | cancel, VOIDED_BY_POS         | ShiftManager hoặc Manager duyệt                  |
/// | finish           | cancel, CANCELLED_BY_USER     | Customer                                         |
/// | finish           | cancel, INCIDENT_COMPENSATED  | SupervisorOrManager                              |
/// | cancel (EXPIRED) | finish                        | System (webhook đến muộn, giữ lại được ghế cũ)   |
/// | cancel (EXPIRED) | cancel, OVERBOOKED_REFUNDED   | System (webhook đến muộn, ghế đã bán)            |
///
/// Điều kiện nghiệp vụ ở cột "Điều kiện" (đã thu đủ tiền, chưa có vé vào cửa, đủ điều kiện tự hủy…)
/// và quyền sở hữu hóa đơn do service kiểm tra trước khi gọi, vì cần dữ liệu trong DB.
/// </summary>
public static class BillStateMachine
{
    private sealed record Rule(string From, string? FromReason, string To, string? ToReason, string[] Actors);

    private static readonly string[] PosStaff = Roles.PosStaff;
    private static readonly string[] Supervisors = Roles.SupervisorOrManager;
    private static readonly string[] SystemOnly = { Actors.System };

    private static readonly Rule[] Table =
    {
        new(BillStatus.Waiting, null, BillStatus.Finish, null, PosStaff.Append(Actors.System).ToArray()),
        new(BillStatus.Waiting, null, BillStatus.Cancel, CancelReason.CancelledBeforePayment, PosStaff.Append(Roles.Customer).ToArray()),
        new(BillStatus.Waiting, null, BillStatus.Cancel, CancelReason.Expired, SystemOnly),
        new(BillStatus.Finish, null, BillStatus.Cancel, CancelReason.VoidedByPos, Supervisors),
        new(BillStatus.Finish, null, BillStatus.Cancel, CancelReason.CancelledByUser, new[] { Roles.Customer }),
        new(BillStatus.Finish, null, BillStatus.Cancel, CancelReason.IncidentCompensated, Supervisors),
        new(BillStatus.Cancel, CancelReason.Expired, BillStatus.Finish, null, SystemOnly),
        new(BillStatus.Cancel, CancelReason.Expired, BillStatus.Cancel, CancelReason.OverbookedRefunded, SystemOnly),
    };

    /// <summary>
    /// Kiểm tra một lần chuyển trạng thái.
    /// </summary>
    /// <param name="from">Trạng thái hiện tại (waiting, finish, cancel).</param>
    /// <param name="to">Trạng thái muốn chuyển sang.</param>
    /// <param name="reason">CancelReason mới khi chuyển sang cancel; null khi chuyển sang finish.</param>
    /// <param name="actorRole">Vai trò người thực hiện (Roles) hoặc Actors.System.</param>
    /// <param name="errorCode">INVALID_TRANSITION khi bị từ chối, null khi hợp lệ.</param>
    /// <param name="currentReason">CancelReason hiện tại của hóa đơn, bắt buộc khi from là cancel.</param>
    /// <returns>true nếu chuyển được.</returns>
    public static bool CanTransition(string from, string to, string? reason, string actorRole, out string? errorCode, string? currentReason = null)
    {
        foreach (var r in Table)
        {
            if (r.From != from || r.To != to) continue;
            if (r.FromReason != null && r.FromReason != currentReason) continue;
            if (r.ToReason != reason) continue;
            if (Array.IndexOf(r.Actors, actorRole) < 0) continue;
            errorCode = null;
            return true;
        }
        errorCode = ErrorCodes.InvalidTransition;
        return false;
    }

    /// <summary>Như CanTransition nhưng ném BusinessRuleException(INVALID_TRANSITION) khi không hợp lệ.</summary>
    public static void EnsureTransition(string from, string to, string? reason, string actorRole, string? currentReason = null)
    {
        if (!CanTransition(from, to, reason, actorRole, out var code, currentReason))
        {
            var target = reason == null ? to : $"{to} ({reason})";
            throw new BusinessRuleException(code!,
                "Không thể chuyển trạng thái hóa đơn này. Vui lòng tải lại trang để xem trạng thái mới nhất.",
                new { from, to = target, actorRole });
        }
    }
}
