using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng PasswordResetOtps (mã OTP đặt lại mật khẩu). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class PasswordResetOtpDAL
{
    public static PasswordResetOtp FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        UserId = row.GetInt("UserId"),
        OtpHash = row.GetString("OtpHash"),
        ExpiresAt = row.GetDateTime("ExpiresAt"),
        FailedAttempts = row.GetInt("FailedAttempts"),
        UsedAt = row.GetDateTimeN("UsedAt"),
    };

    public static List<PasswordResetOtp> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<PasswordResetOtp> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, UserId, OtpHash, ExpiresAt, FailedAttempts, UsedAt FROM PasswordResetOtps ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static PasswordResetOtp? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, UserId, OtpHash, ExpiresAt, FailedAttempts, UsedAt FROM PasswordResetOtps WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(PasswordResetOtp item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO PasswordResetOtps (UserId, OtpHash, ExpiresAt, FailedAttempts, UsedAt) VALUES (@1, @2, @3, @4, @5); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.UserId, item.OtpHash, item.ExpiresAt, item.FailedAttempts, DBUtil.Value(item.UsedAt) };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(PasswordResetOtp item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE PasswordResetOtps SET UserId = @1, OtpHash = @2, ExpiresAt = @3, FailedAttempts = @4, UsedAt = @5 WHERE Id = @6";
        var ps = new List<object> { item.UserId, item.OtpHash, item.ExpiresAt, item.FailedAttempts, DBUtil.Value(item.UsedAt), item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM PasswordResetOtps WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }
}
