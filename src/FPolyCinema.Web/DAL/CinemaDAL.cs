using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng Cinemas (rạp). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class CinemaDAL
{
    public static Cinema FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        Name = row.GetString("Name"),
        Address = row.GetString("Address"),
        Phone = row.GetStringN("Phone"),
        Active = row.GetBool("Active"),
    };

    public static List<Cinema> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<Cinema> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Name, Address, Phone, Active FROM Cinemas ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static Cinema? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Name, Address, Phone, Active FROM Cinemas WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(Cinema item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO Cinemas (Name, Address, Phone, Active) VALUES (@1, @2, @3, @4); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.Name, item.Address, DBUtil.Value(item.Phone), item.Active };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(Cinema item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Cinemas SET Name = @1, Address = @2, Phone = @3, Active = @4 WHERE Id = @5";
        var ps = new List<object> { item.Name, item.Address, DBUtil.Value(item.Phone), item.Active, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM Cinemas WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
