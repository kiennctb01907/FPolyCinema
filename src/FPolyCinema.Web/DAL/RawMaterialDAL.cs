using System.Data;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng RawMaterials (nguyên liệu và bao bì theo rạp). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class RawMaterialDAL
{
    public static RawMaterial FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        CinemaId = row.GetInt("CinemaId"),
        Name = row.GetString("Name"),
        Unit = row.GetString("Unit"),
        ActualStock = row.GetDecimal("ActualStock"),
        ReservedStock = row.GetDecimal("ReservedStock"),
        MinThreshold = row.GetDecimal("MinThreshold"),
        Active = row.GetBool("Active"),
    };

    public static List<RawMaterial> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<RawMaterial> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, CinemaId, Name, Unit, ActualStock, ReservedStock, MinThreshold, Active FROM RawMaterials ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static RawMaterial? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, CinemaId, Name, Unit, ActualStock, ReservedStock, MinThreshold, Active FROM RawMaterials WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(RawMaterial item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO RawMaterials (CinemaId, Name, Unit, ActualStock, ReservedStock, MinThreshold, Active) VALUES (@1, @2, @3, @4, @5, @6, @7); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.CinemaId, item.Name, item.Unit, item.ActualStock, item.ReservedStock, item.MinThreshold, item.Active };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(RawMaterial item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE RawMaterials SET CinemaId = @1, Name = @2, Unit = @3, ActualStock = @4, ReservedStock = @5, MinThreshold = @6, Active = @7 WHERE Id = @8";
        var ps = new List<object> { item.CinemaId, item.Name, item.Unit, item.ActualStock, item.ReservedStock, item.MinThreshold, item.Active, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM RawMaterials WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }

    /// <summary>
    /// Tính lại ReservedStock của mọi nguyên liệu từ sổ kho: tổng RESERVE − tổng RELEASE (phần đang giữ cho đơn online chờ nhận).
    /// Trả về số dòng đã cập nhật.
    /// </summary>
    public static int RecalculateReservedStock(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE rm SET ReservedStock = ISNULL(r.Held, 0) FROM RawMaterials rm " +
                           "LEFT JOIN (SELECT RawMaterialId, SUM(CASE WHEN Type = @1 THEN Quantity WHEN Type = @2 THEN -Quantity ELSE 0 END) AS Held " +
                           "FROM InventoryTransactions GROUP BY RawMaterialId) r ON r.RawMaterialId = rm.Id";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { InventoryTxnType.Reserve, InventoryTxnType.Release }, conn, tx);
    }
}
