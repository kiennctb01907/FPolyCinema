using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng Payments (giao dịch thanh toán và hoàn tiền). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class PaymentDAL
{
    public static Payment FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        BillId = row.GetInt("BillId"),
        PosShiftId = row.GetIntN("PosShiftId"),
        Method = row.GetString("Method"),
        Direction = row.GetString("Direction"),
        Amount = row.GetLong("Amount"),
        Status = row.GetString("Status"),
        ProviderTxnId = row.GetStringN("ProviderTxnId"),
        RawPayload = row.GetStringN("RawPayload"),
        CreatedAt = row.GetDateTime("CreatedAt"),
    };

    public static List<Payment> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<Payment> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, BillId, PosShiftId, Method, Direction, Amount, Status, ProviderTxnId, RawPayload, CreatedAt FROM Payments ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static Payment? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, BillId, PosShiftId, Method, Direction, Amount, Status, ProviderTxnId, RawPayload, CreatedAt FROM Payments WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(Payment item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO Payments (BillId, PosShiftId, Method, Direction, Amount, Status, ProviderTxnId, RawPayload, CreatedAt) VALUES (@1, @2, @3, @4, @5, @6, @7, @8, @9); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.BillId, DBUtil.Value(item.PosShiftId), item.Method, item.Direction, item.Amount, item.Status, DBUtil.Value(item.ProviderTxnId), DBUtil.Value(item.RawPayload), item.CreatedAt };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(Payment item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Payments SET BillId = @1, PosShiftId = @2, Method = @3, Direction = @4, Amount = @5, Status = @6, ProviderTxnId = @7, RawPayload = @8, CreatedAt = @9 WHERE Id = @10";
        var ps = new List<object> { item.BillId, DBUtil.Value(item.PosShiftId), item.Method, item.Direction, item.Amount, item.Status, DBUtil.Value(item.ProviderTxnId), DBUtil.Value(item.RawPayload), item.CreatedAt, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM Payments WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
