using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng Movies (phim). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class MovieDAL
{
    public static Movie FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        Title = row.GetString("Title"),
        OriginalTitle = row.GetStringN("OriginalTitle"),
        Genres = row.GetString("Genres"),
        DurationMinutes = row.GetInt("DurationMinutes"),
        AgeRating = row.GetString("AgeRating"),
        ClassificationLicenseNo = row.GetStringN("ClassificationLicenseNo"),
        ContentWarnings = row.GetStringN("ContentWarnings"),
        IsVietnameseFilm = row.GetBool("IsVietnameseFilm"),
        LanguageFormats = row.GetString("LanguageFormats"),
        ReleaseDate = row.GetDateTime("ReleaseDate"),
        DistributorId = row.GetIntN("DistributorId"),
        MinimumTicketPrice = row.GetLong("MinimumTicketPrice"),
        IsBlockbuster = row.GetBool("IsBlockbuster"),
        PosterUrl = row.GetStringN("PosterUrl"),
        TrailerUrl = row.GetStringN("TrailerUrl"),
        Description = row.GetStringN("Description"),
        AiDemandScore = row.GetDecimalN("AiDemandScore"),
        Active = row.GetBool("Active"),
        CreatedAt = row.GetDateTime("CreatedAt"),
    };

    public static List<Movie> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<Movie> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Title, OriginalTitle, Genres, DurationMinutes, AgeRating, ClassificationLicenseNo, ContentWarnings, IsVietnameseFilm, LanguageFormats, ReleaseDate, DistributorId, MinimumTicketPrice, IsBlockbuster, PosterUrl, TrailerUrl, Description, AiDemandScore, Active, CreatedAt FROM Movies ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static Movie? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Title, OriginalTitle, Genres, DurationMinutes, AgeRating, ClassificationLicenseNo, ContentWarnings, IsVietnameseFilm, LanguageFormats, ReleaseDate, DistributorId, MinimumTicketPrice, IsBlockbuster, PosterUrl, TrailerUrl, Description, AiDemandScore, Active, CreatedAt FROM Movies WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(Movie item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO Movies (Title, OriginalTitle, Genres, DurationMinutes, AgeRating, ClassificationLicenseNo, ContentWarnings, IsVietnameseFilm, LanguageFormats, ReleaseDate, DistributorId, MinimumTicketPrice, IsBlockbuster, PosterUrl, TrailerUrl, Description, AiDemandScore, Active, CreatedAt) VALUES (@1, @2, @3, @4, @5, @6, @7, @8, @9, @10, @11, @12, @13, @14, @15, @16, @17, @18, @19); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.Title, DBUtil.Value(item.OriginalTitle), item.Genres, item.DurationMinutes, item.AgeRating, DBUtil.Value(item.ClassificationLicenseNo), DBUtil.Value(item.ContentWarnings), item.IsVietnameseFilm, item.LanguageFormats, item.ReleaseDate, DBUtil.Value(item.DistributorId), item.MinimumTicketPrice, item.IsBlockbuster, DBUtil.Value(item.PosterUrl), DBUtil.Value(item.TrailerUrl), DBUtil.Value(item.Description), DBUtil.Value(item.AiDemandScore), item.Active, item.CreatedAt };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(Movie item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Movies SET Title = @1, OriginalTitle = @2, Genres = @3, DurationMinutes = @4, AgeRating = @5, ClassificationLicenseNo = @6, ContentWarnings = @7, IsVietnameseFilm = @8, LanguageFormats = @9, ReleaseDate = @10, DistributorId = @11, MinimumTicketPrice = @12, IsBlockbuster = @13, PosterUrl = @14, TrailerUrl = @15, Description = @16, AiDemandScore = @17, Active = @18, CreatedAt = @19 WHERE Id = @20";
        var ps = new List<object> { item.Title, DBUtil.Value(item.OriginalTitle), item.Genres, item.DurationMinutes, item.AgeRating, DBUtil.Value(item.ClassificationLicenseNo), DBUtil.Value(item.ContentWarnings), item.IsVietnameseFilm, item.LanguageFormats, item.ReleaseDate, DBUtil.Value(item.DistributorId), item.MinimumTicketPrice, item.IsBlockbuster, DBUtil.Value(item.PosterUrl), DBUtil.Value(item.TrailerUrl), DBUtil.Value(item.Description), DBUtil.Value(item.AiDemandScore), item.Active, item.CreatedAt, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM Movies WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
