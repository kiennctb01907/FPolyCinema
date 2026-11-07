using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng KdmLicenses (khóa KDM của phim tại rạp). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class KdmLicenseDAL
{
    public static KdmLicense FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        MovieId = row.GetInt("MovieId"),
        CinemaId = row.GetInt("CinemaId"),
        ValidFrom = row.GetDateTime("ValidFrom"),
        ValidUntil = row.GetDateTime("ValidUntil"),
        Note = row.GetStringN("Note"),
    };

    public static List<KdmLicense> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<KdmLicense> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, MovieId, CinemaId, ValidFrom, ValidUntil, Note FROM KdmLicenses ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static KdmLicense? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, MovieId, CinemaId, ValidFrom, ValidUntil, Note FROM KdmLicenses WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(KdmLicense item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO KdmLicenses (MovieId, CinemaId, ValidFrom, ValidUntil, Note) VALUES (@1, @2, @3, @4, @5); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.MovieId, item.CinemaId, item.ValidFrom, item.ValidUntil, DBUtil.Value(item.Note) };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(KdmLicense item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE KdmLicenses SET MovieId = @1, CinemaId = @2, ValidFrom = @3, ValidUntil = @4, Note = @5 WHERE Id = @6";
        var ps = new List<object> { item.MovieId, item.CinemaId, item.ValidFrom, item.ValidUntil, DBUtil.Value(item.Note), item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM KdmLicenses WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
