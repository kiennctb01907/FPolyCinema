using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng PointTransactions (giao dịch điểm). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class PointTransactionDAL
{
    public static PointTransaction FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        UserId = row.GetInt("UserId"),
        Points = row.GetInt("Points"),
        Reason = row.GetString("Reason"),
        BillId = row.GetIntN("BillId"),
        CreatedAt = row.GetDateTime("CreatedAt"),
    };

    public static List<PointTransaction> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<PointTransaction> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, UserId, Points, Reason, BillId, CreatedAt FROM PointTransactions ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static PointTransaction? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, UserId, Points, Reason, BillId, CreatedAt FROM PointTransactions WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(PointTransaction item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO PointTransactions (UserId, Points, Reason, BillId, CreatedAt) VALUES (@1, @2, @3, @4, @5); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.UserId, item.Points, item.Reason, DBUtil.Value(item.BillId), item.CreatedAt };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(PointTransaction item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE PointTransactions SET UserId = @1, Points = @2, Reason = @3, BillId = @4, CreatedAt = @5 WHERE Id = @6";
        var ps = new List<object> { item.UserId, item.Points, item.Reason, DBUtil.Value(item.BillId), item.CreatedAt, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM PointTransactions WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
