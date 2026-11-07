using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng Notifications (thông báo cho khách). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class NotificationDAL
{
    public static Notification FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        UserId = row.GetInt("UserId"),
        Title = row.GetString("Title"),
        Body = row.GetString("Body"),
        Link = row.GetStringN("Link"),
        IsRead = row.GetBool("IsRead"),
        CreatedAt = row.GetDateTime("CreatedAt"),
    };

    public static List<Notification> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<Notification> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, UserId, Title, Body, Link, IsRead, CreatedAt FROM Notifications ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static Notification? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, UserId, Title, Body, Link, IsRead, CreatedAt FROM Notifications WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(Notification item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO Notifications (UserId, Title, Body, Link, IsRead, CreatedAt) VALUES (@1, @2, @3, @4, @5, @6); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.UserId, item.Title, item.Body, DBUtil.Value(item.Link), item.IsRead, item.CreatedAt };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(Notification item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Notifications SET UserId = @1, Title = @2, Body = @3, Link = @4, IsRead = @5, CreatedAt = @6 WHERE Id = @7";
        var ps = new List<object> { item.UserId, item.Title, item.Body, DBUtil.Value(item.Link), item.IsRead, item.CreatedAt, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM Notifications WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
