using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng AudienceDiscounts (mức giảm theo đối tượng vé). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class AudienceDiscountDAL
{
    public static AudienceDiscount FromDataRow(DataRow row) => new()
    {
        AudienceType = row.GetString("AudienceType"),
        DiscountPercent = row.GetDecimal("DiscountPercent"),
        LegalMinPercent = row.GetDecimal("LegalMinPercent"),
        PosOnly = row.GetBool("PosOnly"),
        RequiresDocument = row.GetBool("RequiresDocument"),
    };

    public static List<AudienceDiscount> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<AudienceDiscount> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT AudienceType, DiscountPercent, LegalMinPercent, PosOnly, RequiresDocument FROM AudienceDiscounts ORDER BY AudienceType";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static AudienceDiscount? GetById(string audienceType, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT AudienceType, DiscountPercent, LegalMinPercent, PosOnly, RequiresDocument FROM AudienceDiscounts WHERE AudienceType = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { audienceType }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới; trả về số dòng đã thêm.</summary>
    public static int Insert(AudienceDiscount item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO AudienceDiscounts (AudienceType, DiscountPercent, LegalMinPercent, PosOnly, RequiresDocument) VALUES (@1, @2, @3, @4, @5)";
        var ps = new List<object> { item.AudienceType, item.DiscountPercent, item.LegalMinPercent, item.PosOnly, item.RequiresDocument };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(AudienceDiscount item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE AudienceDiscounts SET DiscountPercent = @1, LegalMinPercent = @2, PosOnly = @3, RequiresDocument = @4 WHERE AudienceType = @5";
        var ps = new List<object> { item.DiscountPercent, item.LegalMinPercent, item.PosOnly, item.RequiresDocument, item.AudienceType };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(string audienceType, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM AudienceDiscounts WHERE AudienceType = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { audienceType }, conn, tx);
    }
}
