using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng Tickets (vé). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class TicketDAL
{
    public static Ticket FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        BillId = row.GetInt("BillId"),
        ShowtimeId = row.GetInt("ShowtimeId"),
        SeatId = row.GetInt("SeatId"),
        AudienceType = row.GetString("AudienceType"),
        ListPrice = row.GetLong("ListPrice"),
        FinalPrice = row.GetLong("FinalPrice"),
        VatRate = row.GetDecimal("VatRate"),
        QrCode = row.GetString("QrCode"),
        CheckInStatus = row.GetString("CheckInStatus"),
        CheckedInAt = row.GetDateTimeN("CheckedInAt"),
        CheckedInBy = row.GetIntN("CheckedInBy"),
        SwappedFromTicketId = row.GetIntN("SwappedFromTicketId"),
        CreatedAt = row.GetDateTime("CreatedAt"),
    };

    public static List<Ticket> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<Ticket> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, BillId, ShowtimeId, SeatId, AudienceType, ListPrice, FinalPrice, VatRate, QrCode, CheckInStatus, CheckedInAt, CheckedInBy, SwappedFromTicketId, CreatedAt FROM Tickets ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static Ticket? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, BillId, ShowtimeId, SeatId, AudienceType, ListPrice, FinalPrice, VatRate, QrCode, CheckInStatus, CheckedInAt, CheckedInBy, SwappedFromTicketId, CreatedAt FROM Tickets WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(Ticket item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO Tickets (BillId, ShowtimeId, SeatId, AudienceType, ListPrice, FinalPrice, VatRate, QrCode, CheckInStatus, CheckedInAt, CheckedInBy, SwappedFromTicketId, CreatedAt) VALUES (@1, @2, @3, @4, @5, @6, @7, @8, @9, @10, @11, @12, @13); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.BillId, item.ShowtimeId, item.SeatId, item.AudienceType, item.ListPrice, item.FinalPrice, item.VatRate, item.QrCode, item.CheckInStatus, DBUtil.Value(item.CheckedInAt), DBUtil.Value(item.CheckedInBy), DBUtil.Value(item.SwappedFromTicketId), item.CreatedAt };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(Ticket item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Tickets SET BillId = @1, ShowtimeId = @2, SeatId = @3, AudienceType = @4, ListPrice = @5, FinalPrice = @6, VatRate = @7, QrCode = @8, CheckInStatus = @9, CheckedInAt = @10, CheckedInBy = @11, SwappedFromTicketId = @12, CreatedAt = @13 WHERE Id = @14";
        var ps = new List<object> { item.BillId, item.ShowtimeId, item.SeatId, item.AudienceType, item.ListPrice, item.FinalPrice, item.VatRate, item.QrCode, item.CheckInStatus, DBUtil.Value(item.CheckedInAt), DBUtil.Value(item.CheckedInBy), DBUtil.Value(item.SwappedFromTicketId), item.CreatedAt, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM Tickets WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }

    public static List<Ticket> GetByBill(int billId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, BillId, ShowtimeId, SeatId, AudienceType, ListPrice, FinalPrice, VatRate, QrCode, CheckInStatus, CheckedInAt, CheckedInBy, SwappedFromTicketId, CreatedAt FROM Tickets WHERE BillId = @1 ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, new List<object> { billId }, conn, tx));
    }

    /// <summary>Đổi trạng thái vào cửa của mọi vé trong hóa đơn (ví dụ VOIDED khi hủy). Trả về số vé đã đổi.</summary>
    public static int SetCheckInStatusForBill(int billId, string checkInStatus, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Tickets SET CheckInStatus = @2 WHERE BillId = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { billId, checkInStatus }, conn, tx);
    }
}
