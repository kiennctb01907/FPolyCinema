using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>Các hàm thêm của UserDAL cho Phase 2: tìm nhân viên, đổi mật khẩu, hồ sơ, đếm dữ liệu liên quan trước khi xóa.</summary>
public static partial class UserDAL
{
    /// <summary>Tìm nhân viên (mọi vai trò trừ khách) theo tên hoặc email, lọc vai trò, rạp, trạng thái; trả một trang và tổng số dòng.</summary>
    public static List<User> SearchStaff(string? query, string? role, int? cinemaId, bool? active, int offset, int limit, out int total)
    {
        var ps = new List<object> { Roles.Customer, DBUtil.Value(DalText.LikeContains(query)), DBUtil.Value(role), DBUtil.Value(cinemaId), DBUtil.Value(active) };
        const string countSql = "SELECT COUNT(*) FROM Users WHERE Role <> @1 AND (@2 IS NULL OR FullName LIKE @2 ESCAPE '!' OR Email LIKE @2 ESCAPE '!') AND (@3 IS NULL OR Role = @3) AND (@4 IS NULL OR CinemaId = @4) AND (@5 IS NULL OR Active = @5)";
        const string sql = "SELECT Id, Email, PasswordHash, FullName, Phone, BirthDate, Role, CinemaId, Active, MustChangePassword, MemberCode, LoyaltyPoints, Tier, WalletBalance, ManagerPinHash, PinFailedCount, PinLockedUntil, CreatedAt FROM Users" +
                           " WHERE Role <> @1 AND (@2 IS NULL OR FullName LIKE @2 ESCAPE '!' OR Email LIKE @2 ESCAPE '!') AND (@3 IS NULL OR Role = @3) AND (@4 IS NULL OR CinemaId = @4) AND (@5 IS NULL OR Active = @5)" +
                           " ORDER BY FullName, Id OFFSET @6 ROWS FETCH NEXT @7 ROWS ONLY";
        total = Convert.ToInt32(DBUtil.ExecuteScalar(countSql, ps));
        var pagePs = new List<object>(ps) { offset, limit };
        return FromDataTable(DBUtil.QueryDataTable(sql, pagePs));
    }

    /// <summary>Email đã có người dùng khác dùng chưa (bỏ qua exceptId khi sửa).</summary>
    public static bool EmailExists(string email, int? exceptId = null, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM Users WHERE Email = @1 AND (@2 IS NULL OR Id <> @2)";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { email.Trim(), DBUtil.Value(exceptId) }, conn, tx)) > 0;
    }

    /// <summary>Đặt mật khẩu mới (đã băm) và cờ MustChangePassword.</summary>
    public static int UpdatePassword(int id, string passwordHash, bool mustChangePassword, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Users SET PasswordHash = @1, MustChangePassword = @2 WHERE Id = @3";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { passwordHash, mustChangePassword, id }, conn, tx);
    }

    /// <summary>Cập nhật thông tin cá nhân (họ tên, số điện thoại, ngày sinh).</summary>
    public static int UpdateProfile(int id, string fullName, string? phone, DateTime? birthDate, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Users SET FullName = @1, Phone = @2, BirthDate = @3 WHERE Id = @4";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { fullName, DBUtil.Value(phone), DBUtil.Value(birthDate), id }, conn, tx);
    }

    /// <summary>Quản lý sửa hồ sơ nhân viên: email, họ tên, số điện thoại, vai trò, rạp.</summary>
    public static int UpdateStaff(int id, string email, string fullName, string? phone, string role, int? cinemaId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Users SET Email = @1, FullName = @2, Phone = @3, Role = @4, CinemaId = @5 WHERE Id = @6";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { email.Trim(), fullName, DBUtil.Value(phone), role, DBUtil.Value(cinemaId), id }, conn, tx);
    }

    /// <summary>Số hóa đơn nhân viên đã lập (Bills.UserId) – căn cứ của quy tắc xóa nhân viên (SPEC 3.14).</summary>
    public static int CountBillsCreated(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM Bills WHERE UserId = @1";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { id }, conn, tx));
    }

    /// <summary>Số dòng dữ liệu vận hành khác trỏ tới người dùng (ca, phê duyệt, nhật ký, kho, vé đã soát, ví, điểm…).</summary>
    public static int CountOperationalReferences(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql =
            "SELECT (SELECT COUNT(*) FROM PosShifts WHERE UserId = @1)" +
            " + (SELECT COUNT(*) FROM SupervisorApprovals WHERE RequestedBy = @1 OR DecidedBy = @1)" +
            " + (SELECT COUNT(*) FROM AuditLogs WHERE UserId = @1)" +
            " + (SELECT COUNT(*) FROM WastageLogs WHERE CreatedBy = @1 OR DecidedBy = @1)" +
            " + (SELECT COUNT(*) FROM InventoryTransactions WHERE CreatedBy = @1)" +
            " + (SELECT COUNT(*) FROM Tickets WHERE CheckedInBy = @1)" +
            " + (SELECT COUNT(*) FROM CorporateBookings WHERE CreatedBy = @1)" +
            " + (SELECT COUNT(*) FROM Vouchers WHERE OwnerUserId = @1)" +
            " + (SELECT COUNT(*) FROM WalletTransactions WHERE UserId = @1)" +
            " + (SELECT COUNT(*) FROM PointTransactions WHERE UserId = @1)" +
            " + (SELECT COUNT(*) FROM Bills WHERE CustomerId = @1)";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { id }, conn, tx));
    }

    /// <summary>Xóa các dòng phụ không mang lịch sử nghiệp vụ (OTP, thông báo) trước khi xóa cứng người dùng.</summary>
    public static int DeleteDependents(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM PasswordResetOtps WHERE UserId = @1; DELETE FROM Notifications WHERE UserId = @1; SELECT @@ROWCOUNT;";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { id }, conn, tx));
    }

    /// <summary>Số thứ tự lớn nhất trong mã thành viên TV + 8 chữ số; 0 nếu chưa có.</summary>
    public static int MaxMemberSequence(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT ISNULL(MAX(CAST(SUBSTRING(MemberCode, 3, 8) AS INT)), 0) FROM Users WHERE MemberCode LIKE @1";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { "TV%" }, conn, tx));
    }
}
