using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.Infrastructure;

/// <summary>
/// Phần chốt số liệu của DbSeeder sau khi mô phỏng bán hàng: điểm và hạng thành viên, lượt dùng voucher,
/// đóng các ca đã qua, sổ kho đầu kỳ và tồn kho đang giữ, thông báo cho khách mẫu.
/// </summary>
public sealed partial class DbSeeder
{
    private static readonly System.Globalization.CultureInfo Vi = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>
    /// Cộng điểm theo từng hóa đơn của khách theo thứ tự thời gian: điểm = phần đã trả × PointEarnPercent[hạng] / 100 / PointValueVnd,
    /// làm tròn xuống; hạng xét lại sau mỗi lần cộng theo tổng chi tiêu 12 tháng; hóa đơn bị hủy thì thu hồi điểm.
    /// </summary>
    private void FinalizeCustomers(SqlConnection conn, SqlTransaction tx)
    {
        foreach (var group in _customerBills.GroupBy(b => b.CustomerId))
        {
            var events = new List<(DateTime At, bool IsCancel, CustomerBill Bill)>();
            foreach (var b in group)
            {
                events.Add((b.PaidAt, false, b));
                if (b.CancelledAt is DateTime c) events.Add((c, true, b));
            }
            string tier = LoyaltyTier.Member;
            int points = 0;
            var earned = new Dictionary<int, int>();
            var spends = new List<(DateTime At, long Amount)>();
            foreach (var (at, isCancel, bill) in events.OrderBy(e => e.At).ThenBy(e => e.IsCancel))
            {
                if (!isCancel)
                {
                    int pct = _rules.PointEarnPercent.TryGetValue(tier, out var p) ? p : 0;
                    int earn = (int)(bill.Total * pct / 100 / _rules.PointValueVnd);
                    earned[bill.BillId] = earn;
                    if (earn > 0)
                    {
                        PointTransactionDAL.Insert(new PointTransaction { UserId = group.Key, Points = earn, Reason = $"Tích điểm hóa đơn {bill.Code}", BillId = bill.BillId, CreatedAt = at }, conn, tx);
                        points += earn;
                    }
                    spends.Add((at, bill.Total));
                }
                else
                {
                    int revoke = earned.TryGetValue(bill.BillId, out var e) ? e : 0;
                    if (revoke > 0)
                    {
                        PointTransactionDAL.Insert(new PointTransaction { UserId = group.Key, Points = -revoke, Reason = $"Thu hồi điểm do hủy hóa đơn {bill.Code}", BillId = bill.BillId, CreatedAt = at }, conn, tx);
                        points -= revoke;
                    }
                    spends.Add((at, -bill.Total));
                }
                long spend12 = spends.Where(s => s.At > at.AddMonths(-12)).Sum(s => s.Amount);
                tier = spend12 >= _rules.TierVvipSpend12Months ? LoyaltyTier.Vvip
                    : spend12 >= _rules.TierVipSpend12Months ? LoyaltyTier.Vip
                    : LoyaltyTier.Member;
            }
            var user = UserDAL.GetById(group.Key, conn, tx)!;
            user.LoyaltyPoints = points;
            user.Tier = tier;
            UserDAL.Update(user, conn, tx);
        }
        var khach = UserDAL.GetById(_khachId, conn, tx)!;
        if (khach.Tier != LoyaltyTier.Vip)
            _logger.LogWarning("Khách mẫu {Email} đang ở hạng {Tier}, không phải VIP như CONTRACTS mục 11.", khach.Email, khach.Tier);
    }

    private void FinalizeVouchers(SqlConnection conn, SqlTransaction tx)
    {
        foreach (var v in _vouchers.Values) VoucherDAL.Update(v, conn, tx);
    }

    /// <summary>Đóng các ca đã qua: tiền dự kiến = tiền đầu ca + tiền mặt thu − tiền mặt hoàn; một ca thiếu 20.000đ để có chênh lệch.</summary>
    private void FinalizeShifts(SqlConnection conn, SqlTransaction tx)
    {
        foreach (var ((cinema, date), s) in _shifts)
        {
            if (s.IsOpen) continue;
            var shift = PosShiftDAL.GetById(s.Id, conn, tx)!;
            long expected = shift.OpeningCashFloat + s.CashIn - s.CashRefund;
            long counted = cinema == 0 && date == _today.AddDays(-5) ? expected - 20_000 : expected;
            var closeAt = date.AddHours(23).AddMinutes(55);
            if (s.LastActivity.AddMinutes(10) > closeAt) closeAt = s.LastActivity.AddMinutes(10);
            shift.ClosedAt = closeAt;
            shift.ExpectedCash = expected;
            shift.ActualCashCounted = counted;
            shift.Discrepancy = counted - expected;
            shift.Status = ShiftStatus.Closed;
            PosShiftDAL.Update(shift, conn, tx);
        }
    }

    /// <summary>
    /// Sổ kho: thêm phiếu nhập tồn đầu kỳ (IMPORT) sao cho tồn thực tế cuối cùng đúng giá trị mẫu,
    /// và tính ReservedStock từ các đơn online còn chờ nhận (RESERVE − RELEASE).
    /// </summary>
    private void FinalizeInventory(SqlConnection conn, SqlTransaction tx)
    {
        var importAt = _today.AddDays(-HistoryDays - 1).AddHours(7);
        InventoryTransactionDAL.InsertOpeningBalances("Tồn đầu kỳ khi đưa hệ thống vào sử dụng", _managerId, importAt, conn, tx);
        RawMaterialDAL.RecalculateReservedStock(conn, tx);
    }

    private void SeedNotifications(SqlConnection conn, SqlTransaction tx)
    {
        var compensation = _vouchers["BUTHUONG-HA01"];
        NotificationDAL.Insert(new Notification
        {
            UserId = _khachId,
            Title = "Bạn nhận được voucher bồi thường",
            Body = $"FPoly Cinema gửi bạn voucher {compensation.Code} trị giá {compensation.Value.ToString("N0", Vi)}đ, dùng được đến {compensation.ValidUntil:dd/MM/yyyy}.",
            Link = null,
            IsRead = false,
            CreatedAt = compensation.ValidFrom,
        }, conn, tx);
        NotificationDAL.Insert(new Notification
        {
            UserId = _khachId,
            Title = "Đặt vé thành công",
            Body = "Vé điện tử của bạn đã sẵn sàng. Vui lòng xuất trình mã QR khi vào phòng chiếu.",
            Link = null,
            IsRead = true,
            CreatedAt = Trim(_now.AddHours(-26)),
        }, conn, tx);
    }
}
