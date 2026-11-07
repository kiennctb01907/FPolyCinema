using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng PricingRules (bảng giá 6 chiều theo rạp). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class PricingRuleDAL
{
    public static PricingRule FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        CinemaId = row.GetInt("CinemaId"),
        RuleType = row.GetString("RuleType"),
        RuleKey = row.GetString("RuleKey"),
        Amount = row.GetLong("Amount"),
    };

    public static List<PricingRule> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<PricingRule> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, CinemaId, RuleType, RuleKey, Amount FROM PricingRules ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static PricingRule? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, CinemaId, RuleType, RuleKey, Amount FROM PricingRules WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(PricingRule item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO PricingRules (CinemaId, RuleType, RuleKey, Amount) VALUES (@1, @2, @3, @4); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.CinemaId, item.RuleType, item.RuleKey, item.Amount };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(PricingRule item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE PricingRules SET CinemaId = @1, RuleType = @2, RuleKey = @3, Amount = @4 WHERE Id = @5";
        var ps = new List<object> { item.CinemaId, item.RuleType, item.RuleKey, item.Amount, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM PricingRules WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
