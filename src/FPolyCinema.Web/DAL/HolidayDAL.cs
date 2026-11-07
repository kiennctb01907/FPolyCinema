using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng Holidays (ngày lễ). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class HolidayDAL
{
    public static Holiday FromDataRow(DataRow row) => new()
    {
        HolidayDate = row.GetDateTime("HolidayDate"),
        Name = row.GetString("Name"),
    };

    public static List<Holiday> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<Holiday> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT HolidayDate, Name FROM Holidays ORDER BY HolidayDate";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static Holiday? GetById(DateTime holidayDate, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT HolidayDate, Name FROM Holidays WHERE HolidayDate = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { holidayDate }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới; trả về số dòng đã thêm.</summary>
    public static int Insert(Holiday item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO Holidays (HolidayDate, Name) VALUES (@1, @2)";
        var ps = new List<object> { item.HolidayDate, item.Name };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(Holiday item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Holidays SET Name = @1 WHERE HolidayDate = @2";
        var ps = new List<object> { item.Name, item.HolidayDate };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(DateTime holidayDate, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM Holidays WHERE HolidayDate = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { holidayDate }, conn, tx);
    }
}
