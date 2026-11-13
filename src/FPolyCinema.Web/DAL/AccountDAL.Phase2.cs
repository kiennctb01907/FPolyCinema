using System.Data;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>Các hàm thêm của PasswordResetOtpDAL cho quên mật khẩu bằng OTP (SPEC 2.5).</summary>
public static partial class PasswordResetOtpDAL
{
    /// <summary>Mã gần nhất của người dùng (theo hạn dùng mới nhất).</summary>
    public static PasswordResetOtp? GetLatestForUser(int userId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT TOP 1 Id, UserId, OtpHash, ExpiresAt, FailedAttempts, UsedAt FROM PasswordResetOtps WHERE UserId = @1 ORDER BY ExpiresAt DESC, Id DESC";
        var table = DBUtil.QueryDataTable(sql, new List<object> { userId }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }

    /// <summary>Vô hiệu mọi mã chưa dùng của người dùng (khi gửi mã mới hoặc đã đặt lại mật khẩu).</summary>
    public static int ExpireAllForUser(int userId, DateTime now, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE PasswordResetOtps SET ExpiresAt = @2 WHERE UserId = @1 AND UsedAt IS NULL AND ExpiresAt > @2";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { userId, now }, conn, tx);
    }

    /// <summary>Tăng số lần nhập sai; trả số lần sai sau khi tăng.</summary>
    public static int IncrementFailed(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE PasswordResetOtps SET FailedAttempts = FailedAttempts + 1 WHERE Id = @1; SELECT FailedAttempts FROM PasswordResetOtps WHERE Id = @1;";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { id }, conn, tx));
    }

    /// <summary>Đánh dấu mã đã dùng, chỉ khi mã chưa dùng (trả 0 nếu mã đã bị dùng ở yêu cầu khác).</summary>
    public static int MarkUsed(int id, DateTime now, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE PasswordResetOtps SET UsedAt = @2 WHERE Id = @1 AND UsedAt IS NULL";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id, now }, conn, tx);
    }
}

/// <summary>Các hàm thêm của EmailOutboxDAL cho hộp thư thử /dev/mailbox (UI D2).</summary>
public static partial class EmailOutboxDAL
{
    /// <summary>Thư mới nhất ở trên.</summary>
    public static List<EmailOutbox> GetLatest(int limit, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT TOP (@1) Id, ToEmail, Subject, Body, SentAt, CreatedAt FROM EmailOutbox ORDER BY CreatedAt DESC, Id DESC";
        return FromDataTable(DBUtil.QueryDataTable(sql, new List<object> { limit }, conn, tx));
    }

    public static int Count(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM EmailOutbox";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, null, conn, tx));
    }

    public static int DeleteAll(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM EmailOutbox";
        return DBUtil.ExecuteNonQuery(sql, null, conn, tx);
    }
}

/// <summary>Các hàm thêm của SupervisorApprovalDAL: đếm và liệt kê yêu cầu đang chờ (chuông phê duyệt).</summary>
public static partial class SupervisorApprovalDAL
{
    public static int CountPending(int? cinemaId, DateTime now, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM SupervisorApprovals a JOIN Bills b ON b.Id = a.BillId WHERE a.Status = @1 AND a.ExpiresAt > @2 AND (@3 IS NULL OR b.CinemaId = @3)";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { ApprovalStatus.Pending, now, DBUtil.Value(cinemaId) }, conn, tx));
    }

    public static List<PendingApprovalRow> ListPending(int? cinemaId, DateTime now, int limit, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT TOP (@4) a.Id, b.Code, u.FullName, a.Reason, a.CreatedAt FROM SupervisorApprovals a JOIN Bills b ON b.Id = a.BillId JOIN Users u ON u.Id = a.RequestedBy" +
                           " WHERE a.Status = @1 AND a.ExpiresAt > @2 AND (@3 IS NULL OR b.CinemaId = @3) ORDER BY a.CreatedAt DESC";
        var table = DBUtil.QueryDataTable(sql, new List<object> { ApprovalStatus.Pending, now, DBUtil.Value(cinemaId), limit }, conn, tx);
        return table.Rows.Cast<DataRow>().Select(r => new PendingApprovalRow(r.GetInt("Id"), r.GetString("Code"), r.GetString("FullName"), r.GetString("Reason"), r.GetDateTime("CreatedAt"))).ToList();
    }
}

/// <summary>Các hàm thêm của PosShiftDAL: ca đang mở của thu ngân (topbar POS).</summary>
public static partial class PosShiftDAL
{
    public static PosShift? GetOpenByUser(int userId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT TOP 1 Id, UserId, CinemaId, TerminalCode, OpenedAt, OpeningCashFloat, ClosedAt, ActualCashCounted, ExpectedCash, Discrepancy, Status FROM PosShifts WHERE UserId = @1 AND Status = @2 ORDER BY OpenedAt DESC";
        var table = DBUtil.QueryDataTable(sql, new List<object> { userId, ShiftStatus.Open }, conn, tx);
        return table.Rows.Count == 0 ? null : FromDataRow(table.Rows[0]);
    }
}
