using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.Services;

/// <summary>
/// Đổi trạng thái hóa đơn – đường duy nhất để thay Bills.Status (CONTRACTS mục 5).
/// Mọi lần đổi đều gọi BillStateMachine.CanTransition trước khi ghi DB.
/// </summary>
public class BillStatusService
{
    private readonly IClock _clock;

    public BillStatusService(IClock clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// Chuyển hóa đơn sang trạng thái mới.
    /// - Sang finish: PaidAt = bây giờ (giữ nguyên nếu đã có), xóa CancelReason và CancelledAt.
    /// - Sang cancel: CancelReason = reason, CancelledAt = bây giờ, giữ PaidAt để tra lịch sử.
    /// Ném BusinessRuleException(INVALID_TRANSITION) nếu bảng chuyển trạng thái không cho phép hoặc hóa đơn
    /// vừa bị người khác đổi. Điều kiện nghiệp vụ của từng dòng (đã thu đủ tiền, chưa vào cửa…) do service gọi hàm này kiểm trước.
    /// Truyền conn và tx để chạy chung transaction với các bước khác.
    /// </summary>
    public Bill ChangeStatus(int billId, string to, string? reason, string actorRole, SqlConnection? conn = null, SqlTransaction? tx = null)
        => ChangeStatus(billId, to, reason, actorRole, _clock.Now, conn, tx);

    /// <summary>Như trên nhưng ghi thời điểm cho trước (dùng khi nạp dữ liệu mẫu của các ngày đã qua).</summary>
    public Bill ChangeStatus(int billId, string to, string? reason, string actorRole, DateTime at, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        var bill = BillDAL.GetById(billId, conn, tx) ?? throw new KeyNotFoundException("Không tìm thấy hóa đơn.");
        BillStateMachine.EnsureTransition(bill.Status, to, reason, actorRole, bill.CancelReason);

        DateTime? paidAt = bill.PaidAt;
        DateTime? cancelledAt = null;
        string? cancelReason = null;
        if (to == BillStatus.Finish)
        {
            paidAt ??= at;
        }
        else
        {
            cancelReason = reason;
            cancelledAt = at;
        }

        var changed = BillDAL.UpdateStatus(billId, bill.Status, bill.CancelReason, to, cancelReason, paidAt, cancelledAt, conn, tx);
        if (changed != 1)
            throw new BusinessRuleException(ErrorCodes.InvalidTransition, "Hóa đơn vừa được cập nhật ở nơi khác. Vui lòng tải lại trang.");

        bill.Status = to;
        bill.CancelReason = cancelReason;
        bill.PaidAt = paidAt;
        bill.CancelledAt = cancelledAt;
        return bill;
    }
}
