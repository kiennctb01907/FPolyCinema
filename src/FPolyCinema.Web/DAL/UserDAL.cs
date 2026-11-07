using System.Data;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy cập bảng Users (người dùng (khách và nhân viên)). Các hàm nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
/// </summary>
public static partial class UserDAL
{
    public static User FromDataRow(DataRow row) => new()
    {
        Id = row.GetInt("Id"),
        Email = row.GetString("Email"),
        PasswordHash = row.GetString("PasswordHash"),
        FullName = row.GetString("FullName"),
        Phone = row.GetStringN("Phone"),
        BirthDate = row.GetDateTimeN("BirthDate"),
        Role = row.GetString("Role"),
        CinemaId = row.GetIntN("CinemaId"),
        Active = row.GetBool("Active"),
        MustChangePassword = row.GetBool("MustChangePassword"),
        MemberCode = row.GetStringN("MemberCode"),
        LoyaltyPoints = row.GetInt("LoyaltyPoints"),
        Tier = row.GetString("Tier"),
        WalletBalance = row.GetLong("WalletBalance"),
        ManagerPinHash = row.GetStringN("ManagerPinHash"),
        PinFailedCount = row.GetInt("PinFailedCount"),
        PinLockedUntil = row.GetDateTimeN("PinLockedUntil"),
        CreatedAt = row.GetDateTime("CreatedAt"),
    };

    public static List<User> FromDataTable(DataTable table) => table.Rows.Cast<DataRow>().Select(FromDataRow).ToList();

    public static List<User> GetAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Email, PasswordHash, FullName, Phone, BirthDate, Role, CinemaId, Active, MustChangePassword, MemberCode, LoyaltyPoints, Tier, WalletBalance, ManagerPinHash, PinFailedCount, PinLockedUntil, CreatedAt FROM Users ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static User? GetById(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Email, PasswordHash, FullName, Phone, BirthDate, Role, CinemaId, Active, MustChangePassword, MemberCode, LoyaltyPoints, Tier, WalletBalance, ManagerPinHash, PinFailedCount, PinLockedUntil, CreatedAt FROM Users WHERE Id = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Thêm dòng mới, gán và trả về Id vừa tạo.</summary>
    public static int Insert(User item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO Users (Email, PasswordHash, FullName, Phone, BirthDate, Role, CinemaId, Active, MustChangePassword, MemberCode, LoyaltyPoints, Tier, WalletBalance, ManagerPinHash, PinFailedCount, PinLockedUntil, CreatedAt) VALUES (@1, @2, @3, @4, @5, @6, @7, @8, @9, @10, @11, @12, @13, @14, @15, @16, @17); SELECT CAST(SCOPE_IDENTITY() AS INT);";
        var ps = new List<object> { item.Email, item.PasswordHash, item.FullName, DBUtil.Value(item.Phone), DBUtil.Value(item.BirthDate), item.Role, DBUtil.Value(item.CinemaId), item.Active, item.MustChangePassword, DBUtil.Value(item.MemberCode), item.LoyaltyPoints, item.Tier, item.WalletBalance, DBUtil.Value(item.ManagerPinHash), item.PinFailedCount, DBUtil.Value(item.PinLockedUntil), item.CreatedAt };
        item.Id = Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
        return item.Id;
    }

    /// <summary>Cập nhật theo khóa chính; trả về số dòng bị ảnh hưởng.</summary>
    public static int Update(User item, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Users SET Email = @1, PasswordHash = @2, FullName = @3, Phone = @4, BirthDate = @5, Role = @6, CinemaId = @7, Active = @8, MustChangePassword = @9, MemberCode = @10, LoyaltyPoints = @11, Tier = @12, WalletBalance = @13, ManagerPinHash = @14, PinFailedCount = @15, PinLockedUntil = @16, CreatedAt = @17 WHERE Id = @18";
        var ps = new List<object> { item.Email, item.PasswordHash, item.FullName, DBUtil.Value(item.Phone), DBUtil.Value(item.BirthDate), item.Role, DBUtil.Value(item.CinemaId), item.Active, item.MustChangePassword, DBUtil.Value(item.MemberCode), item.LoyaltyPoints, item.Tier, item.WalletBalance, DBUtil.Value(item.ManagerPinHash), item.PinFailedCount, DBUtil.Value(item.PinLockedUntil), item.CreatedAt, item.Id };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Xóa theo khóa chính; trả về số dòng đã xóa.</summary>
    public static int Delete(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM Users WHERE Id = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id }, conn, tx);
    }

    public static User? GetByEmail(string email, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Email, PasswordHash, FullName, Phone, BirthDate, Role, CinemaId, Active, MustChangePassword, MemberCode, LoyaltyPoints, Tier, WalletBalance, ManagerPinHash, PinFailedCount, PinLockedUntil, CreatedAt FROM Users WHERE Email = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { email.Trim() }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>
    /// Đăng nhập theo slide bài 4: lấy người dùng theo email và Active = 1, rồi kiểm tra mật khẩu bằng PasswordHasher
    /// (slide so sánh mật khẩu trong SQL; ở đây băm để an toàn nhưng giữ nguyên tên hàm). Sai thì trả về null.
    /// </summary>
    public static User? Authenticate(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)) return null;
        const string sql = "SELECT Id, Email, PasswordHash, FullName, Phone, BirthDate, Role, CinemaId, Active, MustChangePassword, MemberCode, LoyaltyPoints, Tier, WalletBalance, ManagerPinHash, PinFailedCount, PinLockedUntil, CreatedAt FROM Users WHERE Email = @1 AND Active = 1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { email.Trim() });
        if (table.Rows.Count == 0) return null;
        var user = FromDataRow(table.Rows[0]);
        return PasswordUtil.Verify(user.PasswordHash, password) ? user : null;
    }

    /// <summary>Khóa (active = false) hoặc mở (active = true) tài khoản; trả về số dòng bị ảnh hưởng.</summary>
    public static int SetActive(int id, bool active, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Users SET Active = @1 WHERE Id = @2";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { active, id }, conn, tx);
    }

    /// <summary>Số người dùng hiện có (DbSeeder chỉ nạp dữ liệu mẫu khi bằng 0).</summary>
    public static int Count(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM Users";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, null, conn, tx));
    }
}
