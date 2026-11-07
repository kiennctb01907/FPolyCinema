using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng WastageLogItems (dòng của phiếu hủy hàng). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class WastageLogItemDAL
{
    public static WastageLogItem FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        WastageLogId = row.GetInt("WastageLogId"),
        RawMaterialId = row.GetInt("RawMaterialId"),
        Quantity = row.GetDecimal("Quantity"),
        Reason = row.GetStringN("Reason"),
    };

    public static List<WastageLogItem> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<WastageLogItem> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, WastageLogId, RawMaterialId, Quantity, Reason FROM WastageLogItems ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static WastageLogItem? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, WastageLogId, RawMaterialId, Quantity, Reason FROM WastageLogItems WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(WastageLogItem item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO WastageLogItems (WastageLogId, RawMaterialId, Quantity, Reason) VALUES (@1, @2, @3, @4); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.WastageLogId, item.RawMaterialId, item.Quantity, DBUtil.Value(item.Reason) };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(WastageLogItem item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE WastageLogItems SET WastageLogId = @1, RawMaterialId = @2, Quantity = @3, Reason = @4 WHERE Id = @5";
        var ps = new List<object> { item.WastageLogId, item.RawMaterialId, item.Quantity, DBUtil.Value(item.Reason), item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM WastageLogItems WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
