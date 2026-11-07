using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng Vouchers (voucher). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class VoucherDAL
{
    public static Voucher FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        Code = row.GetString("Code"),
        Type = row.GetString("Type"),
        Value = row.GetLong("Value"),
        MinBillAmount = row.GetLong("MinBillAmount"),
        ValidFrom = row.GetDateTime("ValidFrom"),
        ValidUntil = row.GetDateTime("ValidUntil"),
        UsageLimit = row.GetInt("UsageLimit"),
        UsedCount = row.GetInt("UsedCount"),
        OwnerUserId = row.GetIntN("OwnerUserId"),
        Source = row.GetString("Source"),
        CorporateBookingId = row.GetIntN("CorporateBookingId"),
        Active = row.GetBool("Active"),
    };

    public static List<Voucher> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<Voucher> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Code, Type, Value, MinBillAmount, ValidFrom, ValidUntil, UsageLimit, UsedCount, OwnerUserId, Source, CorporateBookingId, Active FROM Vouchers ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static Voucher? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Code, Type, Value, MinBillAmount, ValidFrom, ValidUntil, UsageLimit, UsedCount, OwnerUserId, Source, CorporateBookingId, Active FROM Vouchers WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(Voucher item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO Vouchers (Code, Type, Value, MinBillAmount, ValidFrom, ValidUntil, UsageLimit, UsedCount, OwnerUserId, Source, CorporateBookingId, Active) VALUES (@1, @2, @3, @4, @5, @6, @7, @8, @9, @10, @11, @12); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.Code, item.Type, item.Value, item.MinBillAmount, item.ValidFrom, item.ValidUntil, item.UsageLimit, item.UsedCount, DBUtil.Value(item.OwnerUserId), item.Source, DBUtil.Value(item.CorporateBookingId), item.Active };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(Voucher item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Vouchers SET Code = @1, Type = @2, Value = @3, MinBillAmount = @4, ValidFrom = @5, ValidUntil = @6, UsageLimit = @7, UsedCount = @8, OwnerUserId = @9, Source = @10, CorporateBookingId = @11, Active = @12 WHERE Id = @13";
        var ps = new List<object> { item.Code, item.Type, item.Value, item.MinBillAmount, item.ValidFrom, item.ValidUntil, item.UsageLimit, item.UsedCount, DBUtil.Value(item.OwnerUserId), item.Source, DBUtil.Value(item.CorporateBookingId), item.Active, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM Vouchers WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
