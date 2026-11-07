using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng WastageLogs (phiếu hủy hàng hỏng). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class WastageLogDAL
{
    public static WastageLog FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        CinemaId = row.GetInt("CinemaId"),
        Status = row.GetString("Status"),
        Note = row.GetStringN("Note"),
        CreatedBy = row.GetInt("CreatedBy"),
        CreatedAt = row.GetDateTime("CreatedAt"),
        DecidedBy = row.GetIntN("DecidedBy"),
        DecidedAt = row.GetDateTimeN("DecidedAt"),
    };

    public static List<WastageLog> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<WastageLog> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, CinemaId, Status, Note, CreatedBy, CreatedAt, DecidedBy, DecidedAt FROM WastageLogs ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static WastageLog? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, CinemaId, Status, Note, CreatedBy, CreatedAt, DecidedBy, DecidedAt FROM WastageLogs WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(WastageLog item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO WastageLogs (CinemaId, Status, Note, CreatedBy, CreatedAt, DecidedBy, DecidedAt) VALUES (@1, @2, @3, @4, @5, @6, @7); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.CinemaId, item.Status, DBUtil.Value(item.Note), item.CreatedBy, item.CreatedAt, DBUtil.Value(item.DecidedBy), DBUtil.Value(item.DecidedAt) };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(WastageLog item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE WastageLogs SET CinemaId = @1, Status = @2, Note = @3, CreatedBy = @4, CreatedAt = @5, DecidedBy = @6, DecidedAt = @7 WHERE Id = @8";
        var ps = new List<object> { item.CinemaId, item.Status, DBUtil.Value(item.Note), item.CreatedBy, item.CreatedAt, DBUtil.Value(item.DecidedBy), DBUtil.Value(item.DecidedAt), item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM WastageLogs WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
