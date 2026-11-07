using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng FilmRentalRules (tỷ lệ chia doanh thu theo tuần). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class FilmRentalRuleDAL
{
    public static FilmRentalRule FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        MovieId = row.GetInt("MovieId"),
        WeekNumber = row.GetInt("WeekNumber"),
        DistributorSharePercent = row.GetDecimal("DistributorSharePercent"),
    };

    public static List<FilmRentalRule> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<FilmRentalRule> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, MovieId, WeekNumber, DistributorSharePercent FROM FilmRentalRules ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static FilmRentalRule? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, MovieId, WeekNumber, DistributorSharePercent FROM FilmRentalRules WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(FilmRentalRule item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO FilmRentalRules (MovieId, WeekNumber, DistributorSharePercent) VALUES (@1, @2, @3); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.MovieId, item.WeekNumber, item.DistributorSharePercent };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(FilmRentalRule item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE FilmRentalRules SET MovieId = @1, WeekNumber = @2, DistributorSharePercent = @3 WHERE Id = @4";
        var ps = new List<object> { item.MovieId, item.WeekNumber, item.DistributorSharePercent, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM FilmRentalRules WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
