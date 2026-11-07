using System.Data;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng Bills (hóa đơn). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class BillDAL
{
    public static Bill FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        Code = row.GetString("Code"),
        Channel = row.GetString("Channel"),
        CinemaId = row.GetInt("CinemaId"),
        ShowtimeId = row.GetIntN("ShowtimeId"),
        UserId = row.GetIntN("UserId"),
        CustomerId = row.GetIntN("CustomerId"),
        PosShiftId = row.GetIntN("PosShiftId"),
        TerminalCode = row.GetStringN("TerminalCode"),
        Status = row.GetString("Status"),
        CancelReason = row.GetStringN("CancelReason"),
        CreatedAt = row.GetDateTime("CreatedAt"),
        PaidAt = row.GetDateTimeN("PaidAt"),
        CancelledAt = row.GetDateTimeN("CancelledAt"),
        TicketSubtotal = row.GetLong("TicketSubtotal"),
        FnbSubtotal = row.GetLong("FnbSubtotal"),
        DiscountTotal = row.GetLong("DiscountTotal"),
        Total = row.GetLong("Total"),
        VoucherId = row.GetIntN("VoucherId"),
        PointsUsed = row.GetInt("PointsUsed"),
        HoldExpiresAt = row.GetDateTimeN("HoldExpiresAt"),
        CheckInStatus = row.GetString("CheckInStatus"),
        FnbStatus = row.GetString("FnbStatus"),
        PickupNumber = row.GetIntN("PickupNumber"),
        GuardianConfirmed = row.GetBool("GuardianConfirmed"),
        AgeConfirmed = row.GetBool("AgeConfirmed"),
        CorporateBookingId = row.GetIntN("CorporateBookingId"),
    };

    public static List<Bill> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<Bill> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Code, Channel, CinemaId, ShowtimeId, UserId, CustomerId, PosShiftId, TerminalCode, Status, CancelReason, CreatedAt, PaidAt, CancelledAt, TicketSubtotal, FnbSubtotal, DiscountTotal, Total, VoucherId, PointsUsed, HoldExpiresAt, CheckInStatus, FnbStatus, PickupNumber, GuardianConfirmed, AgeConfirmed, CorporateBookingId FROM Bills ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static Bill? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Code, Channel, CinemaId, ShowtimeId, UserId, CustomerId, PosShiftId, TerminalCode, Status, CancelReason, CreatedAt, PaidAt, CancelledAt, TicketSubtotal, FnbSubtotal, DiscountTotal, Total, VoucherId, PointsUsed, HoldExpiresAt, CheckInStatus, FnbStatus, PickupNumber, GuardianConfirmed, AgeConfirmed, CorporateBookingId FROM Bills WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>
    /// Thêm hóa đơn mới, gán và trả về Id vừa tạo. Hóa đơn luôn bắt đầu ở waiting; mọi trạng thái sau đó
    /// chỉ đổi qua BillStatusService.ChangeStatus.
    /// </summary>
    public static int Insert(Bill item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        if (item.Status != BillStatus.Waiting || item.CancelReason != null || item.PaidAt != null || item.CancelledAt != null)
            throw new InvalidOperationException("Hóa đơn mới phải ở trạng thái waiting, chưa có PaidAt, CancelReason, CancelledAt.");
        const string sql = "INSERT INTO Bills (Code, Channel, CinemaId, ShowtimeId, UserId, CustomerId, PosShiftId, TerminalCode, Status, CancelReason, CreatedAt, PaidAt, CancelledAt, TicketSubtotal, FnbSubtotal, DiscountTotal, Total, VoucherId, PointsUsed, HoldExpiresAt, CheckInStatus, FnbStatus, PickupNumber, GuardianConfirmed, AgeConfirmed, CorporateBookingId) VALUES (@1, @2, @3, @4, @5, @6, @7, @8, @9, @10, @11, @12, @13, @14, @15, @16, @17, @18, @19, @20, @21, @22, @23, @24, @25, @26); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.Code, item.Channel, item.CinemaId, DBUtil.Value(item.ShowtimeId), DBUtil.Value(item.UserId), DBUtil.Value(item.CustomerId), DBUtil.Value(item.PosShiftId), DBUtil.Value(item.TerminalCode), item.Status, DBUtil.Value(item.CancelReason), item.CreatedAt, DBUtil.Value(item.PaidAt), DBUtil.Value(item.CancelledAt), item.TicketSubtotal, item.FnbSubtotal, item.DiscountTotal, item.Total, DBUtil.Value(item.VoucherId), item.PointsUsed, DBUtil.Value(item.HoldExpiresAt), item.CheckInStatus, item.FnbStatus, DBUtil.Value(item.PickupNumber), item.GuardianConfirmed, item.AgeConfirmed, DBUtil.Value(item.CorporateBookingId) };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng. Không đổi Status, CancelReason, PaidAt, CancelledAt (dùng ChangeStatus).</summary>
    public static int Update(Bill item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Bills SET Code = @1, Channel = @2, CinemaId = @3, ShowtimeId = @4, UserId = @5, CustomerId = @6, PosShiftId = @7, TerminalCode = @8, CreatedAt = @9, TicketSubtotal = @10, FnbSubtotal = @11, DiscountTotal = @12, Total = @13, VoucherId = @14, PointsUsed = @15, HoldExpiresAt = @16, CheckInStatus = @17, FnbStatus = @18, PickupNumber = @19, GuardianConfirmed = @20, AgeConfirmed = @21, CorporateBookingId = @22 WHERE Id = @23";
        var ps = new List<object> { item.Code, item.Channel, item.CinemaId, DBUtil.Value(item.ShowtimeId), DBUtil.Value(item.UserId), DBUtil.Value(item.CustomerId), DBUtil.Value(item.PosShiftId), DBUtil.Value(item.TerminalCode), item.CreatedAt, item.TicketSubtotal, item.FnbSubtotal, item.DiscountTotal, item.Total, DBUtil.Value(item.VoucherId), item.PointsUsed, DBUtil.Value(item.HoldExpiresAt), item.CheckInStatus, item.FnbStatus, DBUtil.Value(item.PickupNumber), item.GuardianConfirmed, item.AgeConfirmed, DBUtil.Value(item.CorporateBookingId), item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM Bills WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }

    public static Bill? GetByCode(string code, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Code, Channel, CinemaId, ShowtimeId, UserId, CustomerId, PosShiftId, TerminalCode, Status, CancelReason, CreatedAt, PaidAt, CancelledAt, TicketSubtotal, FnbSubtotal, DiscountTotal, Total, VoucherId, PointsUsed, HoldExpiresAt, CheckInStatus, FnbStatus, PickupNumber, GuardianConfirmed, AgeConfirmed, CorporateBookingId FROM Bills WHERE Code = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { code }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>
    /// Ghi trạng thái mới của hóa đơn, chỉ khi trạng thái và lý do hủy hiện tại vẫn đúng như đã đọc
    /// (chống hai người cùng đổi một hóa đơn). Trả về số dòng đã đổi (0 hoặc 1).
    /// Chỉ BillStatusService.ChangeStatus gọi hàm này, sau khi BillStateMachine.CanTransition cho phép (CONTRACTS mục 5).
    /// </summary>
    public static int UpdateStatus(int billId, string fromStatus, string? fromReason, string toStatus, string? toReason,
        DateTime? paidAt, DateTime? cancelledAt, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Bills SET Status = @1, CancelReason = @2, PaidAt = @3, CancelledAt = @4 WHERE Id = @5 AND Status = @6 AND ISNULL(CancelReason, '') = @7";
        var ps = new List<object> { toStatus, DBUtil.Value(toReason), DBUtil.Value(paidAt), DBUtil.Value(cancelledAt), billId, fromStatus, fromReason ?? string.Empty };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }
}
