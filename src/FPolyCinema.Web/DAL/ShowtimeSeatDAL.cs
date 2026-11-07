using System.Data;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng ShowtimeSeats (trạng thái ghế theo suất). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class ShowtimeSeatDAL
{
    public static ShowtimeSeat FromDataRow(DataRow row) => new()
    {
        ShowtimeId = row.GetInt("ShowtimeId"),
        SeatId = row.GetInt("SeatId"),
        Status = row.GetString("Status"),
        HoldBillId = row.GetIntN("HoldBillId"),
        HoldExpiresAt = row.GetDateTimeN("HoldExpiresAt"),
        TicketId = row.GetIntN("TicketId"),
    };

    public static List<ShowtimeSeat> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<ShowtimeSeat> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT ShowtimeId, SeatId, Status, HoldBillId, HoldExpiresAt, TicketId FROM ShowtimeSeats ORDER BY ShowtimeId, SeatId";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static ShowtimeSeat? GetById(int showtimeId, int seatId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT ShowtimeId, SeatId, Status, HoldBillId, HoldExpiresAt, TicketId FROM ShowtimeSeats WHERE ShowtimeId = @1 AND SeatId = @2";
        var table = DBUtil.QueryDataTable(sql, new List<object> { showtimeId, seatId }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới; trả về số dòng đã thêm.</summary>
    public static int Insert(ShowtimeSeat item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO ShowtimeSeats (ShowtimeId, SeatId, Status, HoldBillId, HoldExpiresAt, TicketId) VALUES (@1, @2, @3, @4, @5, @6)";
        var ps = new List<object> { item.ShowtimeId, item.SeatId, item.Status, DBUtil.Value(item.HoldBillId), DBUtil.Value(item.HoldExpiresAt), DBUtil.Value(item.TicketId) };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(ShowtimeSeat item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE ShowtimeSeats SET Status = @1, HoldBillId = @2, HoldExpiresAt = @3, TicketId = @4 WHERE ShowtimeId = @5 AND SeatId = @6";
        var ps = new List<object> { item.Status, DBUtil.Value(item.HoldBillId), DBUtil.Value(item.HoldExpiresAt), DBUtil.Value(item.TicketId), item.ShowtimeId, item.SeatId };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int showtimeId, int seatId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM ShowtimeSeats WHERE ShowtimeId = @1 AND SeatId = @2";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { showtimeId, seatId }, conn, tx);
    }

    /// <summary>
    /// Sinh trạng thái ghế cho mọi ghế của phòng khi tạo suất (SPEC 3.3): ghế nội bộ nhận HOUSE_HOLD,
    /// các ghế khác AVAILABLE. Trả về số dòng đã thêm.
    /// </summary>
    public static int CreateForShowtime(int showtimeId, int auditoriumId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO ShowtimeSeats (ShowtimeId, SeatId, Status) SELECT @1, s.Id, CASE WHEN s.IsHouseSeat = 1 THEN @3 ELSE @4 END FROM Seats s WHERE s.AuditoriumId = @2";
        var ps = new List<object> { showtimeId, auditoriumId, ShowtimeSeatStatus.HouseHold, ShowtimeSeatStatus.Available };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    public static List<ShowtimeSeat> GetByShowtime(int showtimeId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT ShowtimeId, SeatId, Status, HoldBillId, HoldExpiresAt, TicketId FROM ShowtimeSeats WHERE ShowtimeId = @1 ORDER BY SeatId";
        return FromDataTable(DBUtil.QueryDataTable(sql, new List<object> { showtimeId }, conn, tx));
    }

    /// <summary>
    /// Trả các ghế đang gắn với vé của hóa đơn về AVAILABLE (hủy hóa đơn đã thanh toán). Trả về số ghế đã trả.
    /// Nối theo khóa chính (ShowtimeId, SeatId) thay vì cột TicketId (không có index).
    /// </summary>
    public static int ReleaseForBill(int billId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE ss SET ss.Status = @2, ss.TicketId = NULL, ss.HoldBillId = NULL, ss.HoldExpiresAt = NULL FROM Tickets t JOIN ShowtimeSeats ss ON ss.ShowtimeId = t.ShowtimeId AND ss.SeatId = t.SeatId AND ss.TicketId = t.Id WHERE t.BillId = @1";
        var ps = new List<object> { billId, ShowtimeSeatStatus.Available };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }
}
