using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng PosShifts (ca bán hàng tại quầy). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class PosShiftDAL
{
    public static PosShift FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        UserId = row.GetInt("UserId"),
        CinemaId = row.GetInt("CinemaId"),
        TerminalCode = row.GetString("TerminalCode"),
        OpenedAt = row.GetDateTime("OpenedAt"),
        OpeningCashFloat = row.GetLong("OpeningCashFloat"),
        ClosedAt = row.GetDateTimeN("ClosedAt"),
        ActualCashCounted = row.GetLongN("ActualCashCounted"),
        ExpectedCash = row.GetLongN("ExpectedCash"),
        Discrepancy = row.GetLongN("Discrepancy"),
        Status = row.GetString("Status"),
    };

    public static List<PosShift> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<PosShift> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, UserId, CinemaId, TerminalCode, OpenedAt, OpeningCashFloat, ClosedAt, ActualCashCounted, ExpectedCash, Discrepancy, Status FROM PosShifts ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static PosShift? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, UserId, CinemaId, TerminalCode, OpenedAt, OpeningCashFloat, ClosedAt, ActualCashCounted, ExpectedCash, Discrepancy, Status FROM PosShifts WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(PosShift item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO PosShifts (UserId, CinemaId, TerminalCode, OpenedAt, OpeningCashFloat, ClosedAt, ActualCashCounted, ExpectedCash, Discrepancy, Status) VALUES (@1, @2, @3, @4, @5, @6, @7, @8, @9, @10); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.UserId, item.CinemaId, item.TerminalCode, item.OpenedAt, item.OpeningCashFloat, DBUtil.Value(item.ClosedAt), DBUtil.Value(item.ActualCashCounted), DBUtil.Value(item.ExpectedCash), DBUtil.Value(item.Discrepancy), item.Status };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(PosShift item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE PosShifts SET UserId = @1, CinemaId = @2, TerminalCode = @3, OpenedAt = @4, OpeningCashFloat = @5, ClosedAt = @6, ActualCashCounted = @7, ExpectedCash = @8, Discrepancy = @9, Status = @10 WHERE Id = @11";
        var ps = new List<object> { item.UserId, item.CinemaId, item.TerminalCode, item.OpenedAt, item.OpeningCashFloat, DBUtil.Value(item.ClosedAt), DBUtil.Value(item.ActualCashCounted), DBUtil.Value(item.ExpectedCash), DBUtil.Value(item.Discrepancy), item.Status, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM PosShifts WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
