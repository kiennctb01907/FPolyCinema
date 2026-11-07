using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng AuditLogs (nhật ký thao tác). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class AuditLogDAL
{
    public static AuditLog FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        UserId = row.GetIntN("UserId"),
        Action = row.GetString("Action"),
        EntityType = row.GetString("EntityType"),
        EntityId = row.GetStringN("EntityId"),
        Detail = row.GetStringN("Detail"),
        CreatedAt = row.GetDateTime("CreatedAt"),
    };

    public static List<AuditLog> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<AuditLog> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, UserId, Action, EntityType, EntityId, Detail, CreatedAt FROM AuditLogs ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static AuditLog? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, UserId, Action, EntityType, EntityId, Detail, CreatedAt FROM AuditLogs WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(AuditLog item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO AuditLogs (UserId, Action, EntityType, EntityId, Detail, CreatedAt) VALUES (@1, @2, @3, @4, @5, @6); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { DBUtil.Value(item.UserId), item.Action, item.EntityType, DBUtil.Value(item.EntityId), DBUtil.Value(item.Detail), item.CreatedAt };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(AuditLog item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE AuditLogs SET UserId = @1, Action = @2, EntityType = @3, EntityId = @4, Detail = @5, CreatedAt = @6 WHERE Id = @7";
        var ps = new List<object> { DBUtil.Value(item.UserId), item.Action, item.EntityType, DBUtil.Value(item.EntityId), DBUtil.Value(item.Detail), item.CreatedAt, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM AuditLogs WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
