using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng Drinks (đồ uống, món ăn và combo). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class DrinkDAL
{
    public static Drink FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        CategoryId = row.GetInt("CategoryId"),
        Name = row.GetString("Name"),
        Price = row.GetLong("Price"),
        Image = row.GetStringN("Image"),
        Description = row.GetStringN("Description"),
        Active = row.GetBool("Active"),
        IsCombo = row.GetBool("IsCombo"),
        IsSoldOut = row.GetBool("IsSoldOut"),
        CreatedAt = row.GetDateTime("CreatedAt"),
    };

    public static List<Drink> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<Drink> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, CategoryId, Name, Price, Image, Description, Active, IsCombo, IsSoldOut, CreatedAt FROM Drinks ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static Drink? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, CategoryId, Name, Price, Image, Description, Active, IsCombo, IsSoldOut, CreatedAt FROM Drinks WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(Drink item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO Drinks (CategoryId, Name, Price, Image, Description, Active, IsCombo, IsSoldOut, CreatedAt) VALUES (@1, @2, @3, @4, @5, @6, @7, @8, @9); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.CategoryId, item.Name, item.Price, DBUtil.Value(item.Image), DBUtil.Value(item.Description), item.Active, item.IsCombo, item.IsSoldOut, item.CreatedAt };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(Drink item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Drinks SET CategoryId = @1, Name = @2, Price = @3, Image = @4, Description = @5, Active = @6, IsCombo = @7, IsSoldOut = @8, CreatedAt = @9 WHERE Id = @10";
        var ps = new List<object> { item.CategoryId, item.Name, item.Price, DBUtil.Value(item.Image), DBUtil.Value(item.Description), item.Active, item.IsCombo, item.IsSoldOut, item.CreatedAt, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM Drinks WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
