using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng WalletTransactions (giao dịch ví). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class WalletTransactionDAL
{
    public static WalletTransaction FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        UserId = row.GetInt("UserId"),
        Amount = row.GetLong("Amount"),
        BalanceAfter = row.GetLong("BalanceAfter"),
        Reason = row.GetString("Reason"),
        BillId = row.GetIntN("BillId"),
        CreatedAt = row.GetDateTime("CreatedAt"),
    };

    public static List<WalletTransaction> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<WalletTransaction> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, UserId, Amount, BalanceAfter, Reason, BillId, CreatedAt FROM WalletTransactions ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static WalletTransaction? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, UserId, Amount, BalanceAfter, Reason, BillId, CreatedAt FROM WalletTransactions WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(WalletTransaction item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO WalletTransactions (UserId, Amount, BalanceAfter, Reason, BillId, CreatedAt) VALUES (@1, @2, @3, @4, @5, @6); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.UserId, item.Amount, item.BalanceAfter, item.Reason, DBUtil.Value(item.BillId), item.CreatedAt };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(WalletTransaction item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE WalletTransactions SET UserId = @1, Amount = @2, BalanceAfter = @3, Reason = @4, BillId = @5, CreatedAt = @6 WHERE Id = @7";
        var ps = new List<object> { item.UserId, item.Amount, item.BalanceAfter, item.Reason, DBUtil.Value(item.BillId), item.CreatedAt, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM WalletTransactions WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
