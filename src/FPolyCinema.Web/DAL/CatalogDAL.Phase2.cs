using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>Các hàm thêm của CategoryDAL cho màn hình danh sách (Lab 5) và quy tắc xóa.</summary>
public static partial class CategoryDAL
{
    public static List<CategoryRow> Search(string? query, bool? active, int offset, int limit, out int total)
    {
        var ps = new List<object> { DBUtil.Value(DalText.LikeContains(query)), DBUtil.Value(active) };
        const string countSql = "SELECT COUNT(*) FROM Categories c WHERE (@1 IS NULL OR c.Name LIKE @1 ESCAPE '!') AND (@2 IS NULL OR c.Active = @2)";
        const string sql = "SELECT c.Id, c.Name, c.Active, (SELECT COUNT(*) FROM Drinks d WHERE d.CategoryId = c.Id) AS DrinkCount FROM Categories c" +
                           " WHERE (@1 IS NULL OR c.Name LIKE @1 ESCAPE '!') AND (@2 IS NULL OR c.Active = @2)" +
                           " ORDER BY c.Name, c.Id OFFSET @3 ROWS FETCH NEXT @4 ROWS ONLY";
        total = Convert.ToInt32(DBUtil.ExecuteScalar(countSql, ps));
        var table = DBUtil.QueryDataTable(sql, new List<object>(ps) { offset, limit });
        return table.Rows.Cast<DataRow>().Select(r => new CategoryRow(FromDataRow(r), r.GetInt("DrinkCount"))).ToList();
    }

    /// <summary>Các loại đang hoạt động, sắp theo tên (cho ô chọn loại).</summary>
    public static List<Category> GetActive(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Name, Active FROM Categories WHERE Active = 1 ORDER BY Name";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static int CountDrinks(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM Drinks WHERE CategoryId = @1";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { id }, conn, tx));
    }

    public static bool NameExists(string name, int? exceptId = null, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM Categories WHERE Name = @1 AND (@2 IS NULL OR Id <> @2)";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { name.Trim(), DBUtil.Value(exceptId) }, conn, tx)) > 0;
    }

    public static int SetActive(int id, bool active, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Categories SET Active = @1 WHERE Id = @2";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { active, id }, conn, tx);
    }
}

/// <summary>Các hàm thêm của DrinkDAL: lọc theo loại và tìm theo tên (Lab 5 bài 3), quy tắc xóa, ảnh đang dùng.</summary>
public static partial class DrinkDAL
{
    public static List<DrinkRow> Search(string? query, int? categoryId, bool? active, int offset, int limit, out int total)
    {
        var ps = new List<object> { DBUtil.Value(DalText.LikeContains(query)), DBUtil.Value(categoryId), DBUtil.Value(active) };
        const string countSql = "SELECT COUNT(*) FROM Drinks d WHERE (@1 IS NULL OR d.Name LIKE @1 ESCAPE '!') AND (@2 IS NULL OR d.CategoryId = @2) AND (@3 IS NULL OR d.Active = @3)";
        const string sql = "SELECT d.Id, d.CategoryId, d.Name, d.Price, d.Image, d.Description, d.Active, d.IsCombo, d.IsSoldOut, d.CreatedAt, c.Name AS CategoryName," +
                           " (SELECT COUNT(*) FROM ProductRecipes r WHERE r.DrinkId = d.Id) AS RecipeCount" +
                           " FROM Drinks d JOIN Categories c ON c.Id = d.CategoryId" +
                           " WHERE (@1 IS NULL OR d.Name LIKE @1 ESCAPE '!') AND (@2 IS NULL OR d.CategoryId = @2) AND (@3 IS NULL OR d.Active = @3)" +
                           " ORDER BY d.Name, d.Id OFFSET @4 ROWS FETCH NEXT @5 ROWS ONLY";
        total = Convert.ToInt32(DBUtil.ExecuteScalar(countSql, ps));
        var table = DBUtil.QueryDataTable(sql, new List<object>(ps) { offset, limit });
        return table.Rows.Cast<DataRow>().Select(r => new DrinkRow(FromDataRow(r), r.GetString("CategoryName"), r.GetInt("RecipeCount"))).ToList();
    }

    /// <summary>Số dòng hóa đơn có món này – căn cứ của quy tắc xóa đồ uống (SPEC 3.14).</summary>
    public static int CountBillLines(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM BillDetails WHERE DrinkId = @1";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { id }, conn, tx));
    }

    public static int SetActive(int id, bool active, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Drinks SET Active = @1 WHERE Id = @2";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { active, id }, conn, tx);
    }

    /// <summary>Số món đang dùng ảnh này (để không xóa ảnh còn được tham chiếu).</summary>
    public static int CountByImage(string imagePath, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM Drinks WHERE Image = @1";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { imagePath }, conn, tx));
    }
}

/// <summary>Các hàm thêm của ProductRecipeDAL: định mức theo món (SPEC 3.11).</summary>
public static partial class ProductRecipeDAL
{
    public static List<RecipeLine> GetByDrink(int drinkId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT r.Id, r.DrinkId, r.RawMaterialId, r.Quantity, m.CinemaId, m.Name, m.Unit FROM ProductRecipes r JOIN RawMaterials m ON m.Id = r.RawMaterialId WHERE r.DrinkId = @1 ORDER BY m.CinemaId, m.Name";
        var table = DBUtil.QueryDataTable(sql, new List<object> { drinkId }, conn, tx);
        return table.Rows.Cast<DataRow>().Select(r => new RecipeLine(r.GetInt("Id"), r.GetInt("DrinkId"), r.GetInt("RawMaterialId"), r.GetDecimal("Quantity"), r.GetInt("CinemaId"), r.GetString("Name"), r.GetString("Unit"))).ToList();
    }

    public static int DeleteByDrink(int drinkId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM ProductRecipes WHERE DrinkId = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { drinkId }, conn, tx);
    }

    public static int CountByDrink(int drinkId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM ProductRecipes WHERE DrinkId = @1";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { drinkId }, conn, tx));
    }
}

/// <summary>Các hàm thêm của RawMaterialDAL: nguyên liệu theo rạp (RawMaterials quản lý theo từng rạp, SPEC 3.11).</summary>
public static partial class RawMaterialDAL
{
    public static List<RawMaterial> GetActiveByCinema(int cinemaId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, CinemaId, Name, Unit, ActualStock, ReservedStock, MinThreshold, Active FROM RawMaterials WHERE CinemaId = @1 AND Active = 1 ORDER BY Name";
        return FromDataTable(DBUtil.QueryDataTable(sql, new List<object> { cinemaId }, conn, tx));
    }
}

/// <summary>Các hàm thêm của CinemaDAL.</summary>
public static partial class CinemaDAL
{
    public static List<Cinema> GetActive(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Name, Address, Phone, Active FROM Cinemas WHERE Active = 1 ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }
}

/// <summary>Các hàm thêm của MovieDAL dùng ở Phase 2 (ảnh đang dùng, ví dụ cho style guide).</summary>
public static partial class MovieDAL
{
    public static int CountByPoster(string posterPath, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM Movies WHERE PosterUrl = @1";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { posterPath }, conn, tx));
    }

    public static List<Movie> GetActive(int limit, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT TOP (@1) Id, Title, OriginalTitle, Genres, DurationMinutes, AgeRating, ClassificationLicenseNo, ContentWarnings, IsVietnameseFilm, LanguageFormats, ReleaseDate, DistributorId, MinimumTicketPrice, IsBlockbuster, PosterUrl, TrailerUrl, Description, AiDemandScore, Active, CreatedAt FROM Movies WHERE Active = 1 ORDER BY ReleaseDate DESC, Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, new List<object> { limit }, conn, tx));
    }
}
