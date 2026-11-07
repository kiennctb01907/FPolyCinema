using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng EmailOutbox (thư đã gửi hoặc nằm trong hộp thư thử). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class EmailOutboxDAL
{
    public static EmailOutbox FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        ToEmail = row.GetString("ToEmail"),
        Subject = row.GetString("Subject"),
        Body = row.GetString("Body"),
        SentAt = row.GetDateTimeN("SentAt"),
        CreatedAt = row.GetDateTime("CreatedAt"),
    };

    public static List<EmailOutbox> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<EmailOutbox> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, ToEmail, Subject, Body, SentAt, CreatedAt FROM EmailOutbox ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static EmailOutbox? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, ToEmail, Subject, Body, SentAt, CreatedAt FROM EmailOutbox WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(EmailOutbox item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO EmailOutbox (ToEmail, Subject, Body, SentAt, CreatedAt) VALUES (@1, @2, @3, @4, @5); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.ToEmail, item.Subject, item.Body, DBUtil.Value(item.SentAt), item.CreatedAt };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(EmailOutbox item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE EmailOutbox SET ToEmail = @1, Subject = @2, Body = @3, SentAt = @4, CreatedAt = @5 WHERE Id = @6";
        var ps = new List<object> { item.ToEmail, item.Subject, item.Body, DBUtil.Value(item.SentAt), item.CreatedAt, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM EmailOutbox WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
