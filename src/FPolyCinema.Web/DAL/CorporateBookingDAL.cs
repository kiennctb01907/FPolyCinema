using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng CorporateBookings (hợp đồng doanh nghiệp). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class CorporateBookingDAL
{
    public static CorporateBooking FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        CompanyName = row.GetString("CompanyName"),
        ContactName = row.GetString("ContactName"),
        Phone = row.GetStringN("Phone"),
        Email = row.GetStringN("Email"),
        Type = row.GetString("Type"),
        ShowtimeId = row.GetIntN("ShowtimeId"),
        VoucherCount = row.GetIntN("VoucherCount"),
        ContractAmount = row.GetLong("ContractAmount"),
        BillId = row.GetIntN("BillId"),
        CreatedBy = row.GetInt("CreatedBy"),
        CreatedAt = row.GetDateTime("CreatedAt"),
    };

    public static List<CorporateBooking> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<CorporateBooking> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, CompanyName, ContactName, Phone, Email, Type, ShowtimeId, VoucherCount, ContractAmount, BillId, CreatedBy, CreatedAt FROM CorporateBookings ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static CorporateBooking? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, CompanyName, ContactName, Phone, Email, Type, ShowtimeId, VoucherCount, ContractAmount, BillId, CreatedBy, CreatedAt FROM CorporateBookings WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(CorporateBooking item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO CorporateBookings (CompanyName, ContactName, Phone, Email, Type, ShowtimeId, VoucherCount, ContractAmount, BillId, CreatedBy, CreatedAt) VALUES (@1, @2, @3, @4, @5, @6, @7, @8, @9, @10, @11); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.CompanyName, item.ContactName, DBUtil.Value(item.Phone), DBUtil.Value(item.Email), item.Type, DBUtil.Value(item.ShowtimeId), DBUtil.Value(item.VoucherCount), item.ContractAmount, DBUtil.Value(item.BillId), item.CreatedBy, item.CreatedAt };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(CorporateBooking item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE CorporateBookings SET CompanyName = @1, ContactName = @2, Phone = @3, Email = @4, Type = @5, ShowtimeId = @6, VoucherCount = @7, ContractAmount = @8, BillId = @9, CreatedBy = @10, CreatedAt = @11 WHERE Id = @12";
        var ps = new List<object> { item.CompanyName, item.ContactName, DBUtil.Value(item.Phone), DBUtil.Value(item.Email), item.Type, DBUtil.Value(item.ShowtimeId), DBUtil.Value(item.VoucherCount), item.ContractAmount, DBUtil.Value(item.BillId), item.CreatedBy, item.CreatedAt, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM CorporateBookings WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
