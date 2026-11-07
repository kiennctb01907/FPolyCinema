using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng SchemaVersions (phiên bản script CSDL đã chạy). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class SchemaVersionDAL
{
    public static SchemaVersion FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        Version = row.GetString("Version"),
        AppliedAt = row.GetDateTime("AppliedAt"),
    };

    public static List<SchemaVersion> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<SchemaVersion> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Version, AppliedAt FROM SchemaVersions ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static SchemaVersion? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Version, AppliedAt FROM SchemaVersions WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(SchemaVersion item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO SchemaVersions (Version, AppliedAt) VALUES (@1, @2); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.Version, item.AppliedAt };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(SchemaVersion item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE SchemaVersions SET Version = @1, AppliedAt = @2 WHERE Id = @3";
        var ps = new List<object> { item.Version, item.AppliedAt, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM SchemaVersions WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
