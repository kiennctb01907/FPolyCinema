using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng ProductRecipes (định mức nguyên liệu của món). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class ProductRecipeDAL
{
    public static ProductRecipe FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        DrinkId = row.GetInt("DrinkId"),
        RawMaterialId = row.GetInt("RawMaterialId"),
        Quantity = row.GetDecimal("Quantity"),
    };

    public static List<ProductRecipe> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<ProductRecipe> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, DrinkId, RawMaterialId, Quantity FROM ProductRecipes ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static ProductRecipe? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, DrinkId, RawMaterialId, Quantity FROM ProductRecipes WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(ProductRecipe item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO ProductRecipes (DrinkId, RawMaterialId, Quantity) VALUES (@1, @2, @3); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.DrinkId, item.RawMaterialId, item.Quantity };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(ProductRecipe item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE ProductRecipes SET DrinkId = @1, RawMaterialId = @2, Quantity = @3 WHERE Id = @4";
        var ps = new List<object> { item.DrinkId, item.RawMaterialId, item.Quantity, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM ProductRecipes WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
