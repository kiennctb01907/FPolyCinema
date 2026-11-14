using System.Data;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

// Truy vấn của web khách và đặt vé online (Phase 5). Mọi hàm ghi nhận conn và tx để service gom vào DBUtil.InTransaction.

public static partial class ShowtimeDAL
{
    /// <summary>
    /// Suất cho web khách: bắt đầu trong [from, to), chưa bị hủy, không phải suất thuê trọn (IsPrivate, SPEC 3.10-08), lọc tùy chọn theo rạp và phim; kèm rạp, số ghế trống hiệu lực (ghế bảo trì không tính).
    /// </summary>
    public static List<CustomerShowtimeRow> ForCustomer(int? cinemaId, int? movieId, DateTime from, DateTime to, DateTime now, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        var ps = new List<object> { from, to, now, ShowtimeSeatStatus.Available, ShowtimeSeatStatus.Held, OperationalStatus.Cancelled };
        var sql = "SELECT t.Id, t.MovieId, m.Title, m.AgeRating, a.CinemaId, c.Name AS CinemaName, a.Name AS AuditoriumName, a.Format, t.LanguageFormat," +
                  " t.StartTime, t.FeatureStartTime, t.EndTime, t.OperationalStatus," +
                  " (SELECT COUNT(*) FROM ShowtimeSeats ss JOIN Seats s ON s.Id = ss.SeatId WHERE ss.ShowtimeId = t.Id AND s.IsMaintenance = 0" +
                  "   AND (ss.Status = @4 OR (ss.Status = @5 AND ss.HoldExpiresAt <= @3))) AS FreeSeats" +
                  " FROM Showtimes t JOIN Movies m ON m.Id = t.MovieId JOIN Auditoriums a ON a.Id = t.AuditoriumId JOIN Cinemas c ON c.Id = a.CinemaId" +
                  " WHERE t.StartTime >= @1 AND t.StartTime < @2 AND t.OperationalStatus <> @6 AND t.IsPrivate = 0 AND m.Active = 1 AND c.Active = 1";
        if (cinemaId is int cid)
        {
            ps.Add(cid);
            sql += " AND a.CinemaId = @" + ps.Count;
        }
        if (movieId is int mid)
        {
            ps.Add(mid);
            sql += " AND t.MovieId = @" + ps.Count;
        }
        sql += " ORDER BY c.Name, m.Title, t.StartTime";
        return DBUtil.QueryDataTable(sql, ps, conn, tx).Rows.Cast<DataRow>().Select(r => new CustomerShowtimeRow(r.GetInt("Id"), r.GetInt("MovieId"), r.GetString("Title"),
            r.GetString("AgeRating"), r.GetInt("CinemaId"), r.GetString("CinemaName"), r.GetString("AuditoriumName"), r.GetString("Format"), r.GetString("LanguageFormat"),
            r.GetDateTime("StartTime"), r.GetDateTime("FeatureStartTime"), r.GetDateTime("EndTime"), r.GetString("OperationalStatus"), r.GetInt("FreeSeats"))).ToList();
    }

    /// <summary>Phim đang có suất chưa bắt đầu trong [from, to) (đang chiếu) – Id phim theo số suất giảm dần.</summary>
    public static List<int> MoviesWithShowtimes(DateTime from, DateTime to, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT t.MovieId FROM Showtimes t JOIN Movies m ON m.Id = t.MovieId WHERE t.StartTime >= @1 AND t.StartTime < @2 AND t.OperationalStatus <> @3 AND t.IsPrivate = 0 AND m.Active = 1" +
                           " GROUP BY t.MovieId ORDER BY COUNT(*) DESC, t.MovieId";
        return DBUtil.QueryDataTable(sql, new List<object> { from, to, OperationalStatus.Cancelled }, conn, tx).Rows.Cast<DataRow>().Select(r => r.GetInt("MovieId")).ToList();
    }
}

public static partial class BillDAL
{
    /// <summary>Hóa đơn ONLINE waiting mới nhất của khách (mỗi khách đặt online một hóa đơn tại một thời điểm).</summary>
    public static Bill? GetWaitingOnline(int customerId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = SelectCols + " FROM Bills WHERE CustomerId = @1 AND Channel = @2 AND Status = @3 ORDER BY Id DESC";
        var t = DBUtil.QueryDataTable(sql, new List<object> { customerId, BillChannel.Online, BillStatus.Waiting }, conn, tx);
        return t.Rows.Count == 0 ? null : FromDataRow(t.Rows[0]);
    }

    /// <summary>Hóa đơn đã thanh toán hoặc đã hủy sau thanh toán của khách (vé của tôi), kèm suất và phim.</summary>
    public static List<CustomerBillRow> ForCustomer(int customerId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT b.Id, b.Code, b.Channel, b.Status, b.CancelReason, b.Total, b.CreatedAt, b.FnbStatus, b.CheckInStatus," +
                           " st.Id AS ShowtimeId, st.StartTime, st.EndTime, m.Title, m.AgeRating, m.PosterUrl, a.Name AS AuditoriumName, c.Name AS CinemaName," +
                           " (SELECT COUNT(*) FROM Tickets tk WHERE tk.BillId = b.Id) AS TicketCount" +
                           " FROM Bills b LEFT JOIN Showtimes st ON st.Id = b.ShowtimeId LEFT JOIN Movies m ON m.Id = st.MovieId" +
                           " LEFT JOIN Auditoriums a ON a.Id = st.AuditoriumId JOIN Cinemas c ON c.Id = b.CinemaId" +
                           " WHERE b.CustomerId = @1 AND (b.Status = @2 OR (b.Status = @3 AND b.PaidAt IS NOT NULL) OR b.CancelReason = @4)" +
                           " ORDER BY COALESCE(st.StartTime, b.CreatedAt) DESC";
        return DBUtil.QueryDataTable(sql, new List<object> { customerId, BillStatus.Finish, BillStatus.Cancel, CancelReason.OverbookedRefunded }, conn, tx)
            .Rows.Cast<DataRow>().Select(r => new CustomerBillRow(r.GetInt("Id"), r.GetString("Code"), r.GetString("Channel"), r.GetString("Status"), r.GetStringN("CancelReason"),
                r.GetLong("Total"), r.GetDateTime("CreatedAt"), r.GetString("FnbStatus"), r.GetString("CheckInStatus"), r.GetIntN("ShowtimeId"), r.GetDateTimeN("StartTime"),
                r.GetDateTimeN("EndTime"), r.GetStringN("Title"), r.GetStringN("AgeRating"), r.GetStringN("PosterUrl"), r.GetStringN("AuditoriumName"), r.GetString("CinemaName"),
                r.GetInt("TicketCount"))).ToList();
    }

    /// <summary>Số lần khách tự hủy (CANCELLED_BY_USER) có thời điểm hủy trong [from, to).</summary>
    public static int CountSelfCancels(int customerId, DateTime from, DateTime to, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        Convert.ToInt32(DBUtil.ExecuteScalar("SELECT COUNT(*) FROM Bills WHERE CustomerId = @1 AND CancelReason = @2 AND CancelledAt >= @3 AND CancelledAt < @4",
            new List<object> { customerId, CancelReason.CancelledByUser, from, to }, conn, tx));
}

public static partial class UserDAL
{
    /// <summary>
    /// Cộng (delta dương) hoặc trừ (delta âm) ví; không cho số dư âm. Trả số dư mới, null khi không đủ tiền.
    /// Chỉ WalletService gọi, luôn kèm một bản ghi WalletTransactions trong cùng transaction (SPEC 3.13).
    /// </summary>
    public static long? ChangeWallet(int id, long delta, SqlConnection conn, SqlTransaction tx)
    {
        var v = DBUtil.ExecuteScalar("UPDATE Users SET WalletBalance = WalletBalance + @2 OUTPUT inserted.WalletBalance WHERE Id = @1 AND WalletBalance + @2 >= 0",
            new List<object> { id, delta }, conn, tx);
        return v == null || v == DBNull.Value ? null : Convert.ToInt64(v);
    }
}

public static partial class PaymentDAL
{
    /// <summary>Khoản thanh toán theo mã giao dịch của cổng (chống webhook lặp).</summary>
    public static Payment? GetByProviderTxnId(string txnId, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        FromDataTable(DBUtil.QueryDataTable("SELECT * FROM Payments WHERE ProviderTxnId = @1", new List<object> { txnId }, conn, tx)).FirstOrDefault();

    /// <summary>Yêu cầu thanh toán PENDING mới nhất của hóa đơn (lưu ảnh chụp ghế lúc bắt đầu thanh toán).</summary>
    public static Payment? LatestPending(int billId, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        FromDataTable(DBUtil.QueryDataTable("SELECT TOP 1 * FROM Payments WHERE BillId = @1 AND Status = @2 ORDER BY Id DESC",
            new List<object> { billId, PaymentStatus.Pending }, conn, tx)).FirstOrDefault();

    /// <summary>Đánh dấu các yêu cầu PENDING cũ của hóa đơn là FAILED (khi khách bắt đầu thanh toán lại).</summary>
    public static int FailPending(int billId, SqlConnection conn, SqlTransaction tx) =>
        DBUtil.ExecuteNonQuery("UPDATE Payments SET Status = @3 WHERE BillId = @1 AND Status = @2", new List<object> { billId, PaymentStatus.Pending, PaymentStatus.Failed }, conn, tx);
}

public static partial class NotificationDAL
{
    public static List<Notification> ForUser(int userId, int limit = 50, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        FromDataTable(DBUtil.QueryDataTable("SELECT TOP (@2) * FROM Notifications WHERE UserId = @1 ORDER BY CreatedAt DESC, Id DESC", new List<object> { userId, limit }, conn, tx));

    public static int CountUnread(int userId) =>
        Convert.ToInt32(DBUtil.ExecuteScalar("SELECT COUNT(*) FROM Notifications WHERE UserId = @1 AND IsRead = 0", new List<object> { userId }));

    public static int MarkAllRead(int userId) =>
        DBUtil.ExecuteNonQuery("UPDATE Notifications SET IsRead = 1 WHERE UserId = @1 AND IsRead = 0", new List<object> { userId });
}

public static partial class VoucherDAL
{
    /// <summary>Voucher riêng của khách (mọi trạng thái), mới nhất trước.</summary>
    public static List<Voucher> ForOwner(int userId) =>
        FromDataTable(DBUtil.QueryDataTable("SELECT * FROM Vouchers WHERE OwnerUserId = @1 ORDER BY ValidUntil DESC", new List<object> { userId }));

    /// <summary>Ưu đãi công khai còn dùng được (voucher PROMO không gắn chủ, còn hạn và còn lượt).</summary>
    public static List<Voucher> PublicPromos(DateTime now) =>
        FromDataTable(DBUtil.QueryDataTable("SELECT * FROM Vouchers WHERE OwnerUserId IS NULL AND Source = @1 AND Active = 1 AND ValidFrom <= @2 AND ValidUntil > @2 AND UsedCount < UsageLimit ORDER BY ValidUntil",
            new List<object> { VoucherSource.Promo, now }));

    public static bool CodeExists(string code, SqlConnection conn, SqlTransaction tx) =>
        Convert.ToInt32(DBUtil.ExecuteScalar("SELECT COUNT(*) FROM Vouchers WHERE Code = @1", new List<object> { code }, conn, tx)) > 0;
}

public static partial class WalletTransactionDAL
{
    public static List<WalletTransaction> ForUser(int userId, int limit = 50) =>
        FromDataTable(DBUtil.QueryDataTable("SELECT TOP (@2) * FROM WalletTransactions WHERE UserId = @1 ORDER BY CreatedAt DESC, Id DESC", new List<object> { userId, limit }));
}

public static partial class PointTransactionDAL
{
    public static List<PointTransaction> ForUser(int userId, int limit = 50) =>
        FromDataTable(DBUtil.QueryDataTable("SELECT TOP (@2) * FROM PointTransactions WHERE UserId = @1 ORDER BY CreatedAt DESC, Id DESC", new List<object> { userId, limit }));
}
