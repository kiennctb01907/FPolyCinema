using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng Showtimes (suất chiếu). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class ShowtimeDAL
{
    public static Showtime FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        MovieId = row.GetInt("MovieId"),
        AuditoriumId = row.GetInt("AuditoriumId"),
        StartTime = row.GetDateTime("StartTime"),
        FeatureStartTime = row.GetDateTime("FeatureStartTime"),
        EndTime = row.GetDateTime("EndTime"),
        CleaningEndTime = row.GetDateTime("CleaningEndTime"),
        LanguageFormat = row.GetString("LanguageFormat"),
        OperationalStatus = row.GetString("OperationalStatus"),
        IsAiGenerated = row.GetBool("IsAiGenerated"),
        IsPrivate = row.GetBool("IsPrivate"),
        CancelNote = row.GetStringN("CancelNote"),
        CreatedAt = row.GetDateTime("CreatedAt"),
    };

    public static List<Showtime> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<Showtime> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, MovieId, AuditoriumId, StartTime, FeatureStartTime, EndTime, CleaningEndTime, LanguageFormat, OperationalStatus, IsAiGenerated, IsPrivate, CancelNote, CreatedAt FROM Showtimes ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static Showtime? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, MovieId, AuditoriumId, StartTime, FeatureStartTime, EndTime, CleaningEndTime, LanguageFormat, OperationalStatus, IsAiGenerated, IsPrivate, CancelNote, CreatedAt FROM Showtimes WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(Showtime item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO Showtimes (MovieId, AuditoriumId, StartTime, FeatureStartTime, EndTime, CleaningEndTime, LanguageFormat, OperationalStatus, IsAiGenerated, IsPrivate, CancelNote, CreatedAt) VALUES (@1, @2, @3, @4, @5, @6, @7, @8, @9, @10, @11, @12); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.MovieId, item.AuditoriumId, item.StartTime, item.FeatureStartTime, item.EndTime, item.CleaningEndTime, item.LanguageFormat, item.OperationalStatus, item.IsAiGenerated, item.IsPrivate, DBUtil.Value(item.CancelNote), item.CreatedAt };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(Showtime item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Showtimes SET MovieId = @1, AuditoriumId = @2, StartTime = @3, FeatureStartTime = @4, EndTime = @5, CleaningEndTime = @6, LanguageFormat = @7, OperationalStatus = @8, IsAiGenerated = @9, IsPrivate = @10, CancelNote = @11, CreatedAt = @12 WHERE Id = @13";
        var ps = new List<object> { item.MovieId, item.AuditoriumId, item.StartTime, item.FeatureStartTime, item.EndTime, item.CleaningEndTime, item.LanguageFormat, item.OperationalStatus, item.IsAiGenerated, item.IsPrivate, DBUtil.Value(item.CancelNote), item.CreatedAt, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM Showtimes WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
