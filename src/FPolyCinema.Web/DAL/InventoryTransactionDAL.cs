using System.Data;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng InventoryTransactions (giao dịch kho). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class InventoryTransactionDAL
{
    public static InventoryTransaction FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        RawMaterialId = row.GetInt("RawMaterialId"),
        Type = row.GetString("Type"),
        Quantity = row.GetDecimal("Quantity"),
        BillId = row.GetIntN("BillId"),
        WastageLogId = row.GetIntN("WastageLogId"),
        Note = row.GetStringN("Note"),
        CreatedBy = row.GetIntN("CreatedBy"),
        CreatedAt = row.GetDateTime("CreatedAt"),
    };

    public static List<InventoryTransaction> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<InventoryTransaction> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, RawMaterialId, Type, Quantity, BillId, WastageLogId, Note, CreatedBy, CreatedAt FROM InventoryTransactions ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static InventoryTransaction? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, RawMaterialId, Type, Quantity, BillId, WastageLogId, Note, CreatedBy, CreatedAt FROM InventoryTransactions WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(InventoryTransaction item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO InventoryTransactions (RawMaterialId, Type, Quantity, BillId, WastageLogId, Note, CreatedBy, CreatedAt) VALUES (@1, @2, @3, @4, @5, @6, @7, @8); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.RawMaterialId, item.Type, item.Quantity, DBUtil.Value(item.BillId), DBUtil.Value(item.WastageLogId), DBUtil.Value(item.Note), DBUtil.Value(item.CreatedBy), item.CreatedAt };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(InventoryTransaction item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE InventoryTransactions SET RawMaterialId = @1, Type = @2, Quantity = @3, BillId = @4, WastageLogId = @5, Note = @6, CreatedBy = @7, CreatedAt = @8 WHERE Id = @9";
        var ps = new List<object> { item.RawMaterialId, item.Type, item.Quantity, DBUtil.Value(item.BillId), DBUtil.Value(item.WastageLogId), DBUtil.Value(item.Note), DBUtil.Value(item.CreatedBy), item.CreatedAt, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM InventoryTransactions WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }

    /// <summary>
    /// Ghi giao dịch kho cho mọi dòng bắp nước của hóa đơn theo định mức (ProductRecipes × số lượng), gộp theo nguyên liệu
    /// của đúng rạp. Chỉ ghi sổ, không đổi tồn kho (service tự cập nhật RawMaterials). Trả về số dòng đã ghi.
    /// </summary>
    public static int InsertForBill(int billId, int cinemaId, string type, string? note, int? createdBy, DateTime at,
        SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO InventoryTransactions (RawMaterialId, Type, Quantity, BillId, WastageLogId, Note, CreatedBy, CreatedAt) " +
                           "SELECT pr.RawMaterialId, @2, SUM(pr.Quantity * bd.Quantity), @1, NULL, @3, @4, @5 " +
                           "FROM BillDetails bd JOIN ProductRecipes pr ON pr.DrinkId = bd.DrinkId JOIN RawMaterials rm ON rm.Id = pr.RawMaterialId " +
                           "WHERE bd.BillId = @1 AND rm.CinemaId = @6 GROUP BY pr.RawMaterialId";
        var ps = new List<object> { billId, type, DBUtil.Value(note), DBUtil.Value(createdBy), at, cinemaId };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>
    /// Ghi phiếu nhập tồn đầu kỳ (IMPORT) cho mọi nguyên liệu, số lượng = tồn thực tế hiện tại + tổng đã xuất (CONSUME, WASTAGE),
    /// để sổ kho khớp với ActualStock. Dùng khi đưa dữ liệu có sẵn vào hệ thống. Trả về số dòng đã ghi.
    /// </summary>
    public static int InsertOpeningBalances(string note, int? createdBy, DateTime at, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO InventoryTransactions (RawMaterialId, Type, Quantity, BillId, WastageLogId, Note, CreatedBy, CreatedAt) " +
                           "SELECT rm.Id, @1, rm.ActualStock + ISNULL(c.Used, 0), NULL, NULL, @2, @3, @4 FROM RawMaterials rm " +
                           "LEFT JOIN (SELECT RawMaterialId, SUM(Quantity) AS Used FROM InventoryTransactions WHERE Type IN (@5, @6) GROUP BY RawMaterialId) c ON c.RawMaterialId = rm.Id";
        var ps = new List<object> { InventoryTxnType.Import, note, DBUtil.Value(createdBy), at, InventoryTxnType.Consume, InventoryTxnType.Wastage };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }
}
