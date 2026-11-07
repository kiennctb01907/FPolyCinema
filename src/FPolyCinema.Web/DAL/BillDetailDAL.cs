using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng BillDetails (dòng bắp nước của hóa đơn). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class BillDetailDAL
{
    public static BillDetail FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        BillId = row.GetInt("BillId"),
        DrinkId = row.GetInt("DrinkId"),
        Quantity = row.GetInt("Quantity"),
        Price = row.GetLong("Price"),
        VatRate = row.GetDecimal("VatRate"),
        LineFinalAmount = row.GetLong("LineFinalAmount"),
    };

    public static List<BillDetail> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<BillDetail> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, BillId, DrinkId, Quantity, Price, VatRate, LineFinalAmount FROM BillDetails ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static BillDetail? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, BillId, DrinkId, Quantity, Price, VatRate, LineFinalAmount FROM BillDetails WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(BillDetail item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO BillDetails (BillId, DrinkId, Quantity, Price, VatRate, LineFinalAmount) VALUES (@1, @2, @3, @4, @5, @6); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.BillId, item.DrinkId, item.Quantity, item.Price, item.VatRate, item.LineFinalAmount };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(BillDetail item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE BillDetails SET BillId = @1, DrinkId = @2, Quantity = @3, Price = @4, VatRate = @5, LineFinalAmount = @6 WHERE Id = @7";
        var ps = new List<object> { item.BillId, item.DrinkId, item.Quantity, item.Price, item.VatRate, item.LineFinalAmount, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM BillDetails WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
