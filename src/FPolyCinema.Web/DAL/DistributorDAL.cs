using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng Distributors (nhà phát hành phim). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class DistributorDAL
{
    public static Distributor FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        Name = row.GetString("Name"),
        Email = row.GetStringN("Email"),
        Phone = row.GetStringN("Phone"),
        Active = row.GetBool("Active"),
    };

    public static List<Distributor> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<Distributor> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Name, Email, Phone, Active FROM Distributors ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static Distributor? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Name, Email, Phone, Active FROM Distributors WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(Distributor item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO Distributors (Name, Email, Phone, Active) VALUES (@1, @2, @3, @4); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.Name, DBUtil.Value(item.Email), DBUtil.Value(item.Phone), item.Active };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(Distributor item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Distributors SET Name = @1, Email = @2, Phone = @3, Active = @4 WHERE Id = @5";
        var ps = new List<object> { item.Name, DBUtil.Value(item.Email), DBUtil.Value(item.Phone), item.Active, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM Distributors WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
