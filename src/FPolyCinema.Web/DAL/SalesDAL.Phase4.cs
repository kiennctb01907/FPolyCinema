using System.Data;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

// Truy vấn bán hàng của Phase 4 (giữ ghế, hóa đơn, ca, kho). Mọi hàm ghi nhận conn và tx để service gom vào DBUtil.InTransaction.

public static partial class ShowtimeSeatDAL
{
    private const string StateSql = "SELECT ss.SeatId, s.RowIndex, s.ColIndex, s.Label, s.SeatType, s.PairGroupId, s.IsHouseSeat, s.IsMaintenance, s.BaseViewScore," +
                                    " ss.Status, ss.HoldBillId, ss.HoldExpiresAt, ss.HoldAudienceType, ss.TicketId";

    private static SeatStateRow StateOf(DataRow r) => new(r.GetInt("SeatId"), r.GetInt("RowIndex"), r.GetInt("ColIndex"), r.GetString("Label"),
        r.GetString("SeatType"), r.GetIntN("PairGroupId"), r.GetBool("IsHouseSeat"), r.GetBool("IsMaintenance"), r.GetInt("BaseViewScore"),
        r.GetString("Status"), r.GetIntN("HoldBillId"), r.GetDateTimeN("HoldExpiresAt"), r.GetStringN("HoldAudienceType"), r.GetIntN("TicketId"));

    /// <summary>
    /// Khóa mọi ghế của suất (SPEC 3.5 bước 2): SELECT … FROM ShowtimeSeats WITH (UPDLOCK, HOLDLOCK) WHERE ShowtimeId = @1.
    /// Hai yêu cầu giữ ghế cùng suất đến cùng lúc vì thế được xử lý lần lượt. Chỉ gọi trong transaction.
    /// </summary>
    public static List<SeatStateRow> LockForShowtime(int showtimeId, SqlConnection conn, SqlTransaction tx)
    {
        const string sql = StateSql + " FROM ShowtimeSeats ss WITH (UPDLOCK, HOLDLOCK) JOIN Seats s ON s.Id = ss.SeatId WHERE ss.ShowtimeId = @1 ORDER BY s.RowIndex, s.ColIndex";
        return DBUtil.QueryDataTable(sql, new List<object> { showtimeId }, conn, tx).Rows.Cast<DataRow>().Select(StateOf).ToList();
    }

    /// <summary>Đọc sơ đồ ghế của suất (không khóa); service tự tính trạng thái hiệu lực.</summary>
    public static List<SeatStateRow> GetStates(int showtimeId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = StateSql + " FROM ShowtimeSeats ss JOIN Seats s ON s.Id = ss.SeatId WHERE ss.ShowtimeId = @1 ORDER BY s.RowIndex, s.ColIndex";
        return DBUtil.QueryDataTable(sql, new List<object> { showtimeId }, conn, tx).Rows.Cast<DataRow>().Select(StateOf).ToList();
    }

    /// <summary>
    /// Giữ các ghế cho hóa đơn (SPEC 3.5 bước 6): chỉ đổi ghế đang AVAILABLE, HELD đã hết hạn hoặc HELD bởi chính hóa đơn.
    /// Trả số dòng đã đổi để service so với số ghế yêu cầu.
    /// </summary>
    public static int Hold(int showtimeId, int billId, DateTime expiresAt, DateTime now, IReadOnlyCollection<int> seatIds, SqlConnection conn, SqlTransaction tx)
    {
        if (seatIds.Count == 0) return 0;
        var ps = new List<object> { showtimeId, billId, expiresAt, now, ShowtimeSeatStatus.Held, ShowtimeSeatStatus.Available };
        var sql = "UPDATE ShowtimeSeats SET Status = @5, HoldAudienceType = CASE WHEN HoldBillId = @2 THEN HoldAudienceType ELSE NULL END, HoldBillId = @2, HoldExpiresAt = @3 WHERE ShowtimeId = @1 AND TicketId IS NULL AND (Status = @6 OR (Status = @5 AND (HoldExpiresAt <= @4 OR HoldBillId = @2))) AND SeatId IN (" + DBUtil.InParams(seatIds.Cast<object>(), ps) + ")";
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Trả các ghế của hóa đơn trong suất về AVAILABLE (ghế bị bỏ khỏi lựa chọn, hủy, hết hạn).</summary>
    public static int Release(int showtimeId, int billId, IReadOnlyCollection<int> seatIds, SqlConnection conn, SqlTransaction tx)
    {
        if (seatIds.Count == 0) return 0;
        var ps = new List<object> { showtimeId, billId, ShowtimeSeatStatus.Available, ShowtimeSeatStatus.Held };
        var sql = "UPDATE ShowtimeSeats SET Status = @3, HoldBillId = NULL, HoldExpiresAt = NULL, HoldAudienceType = NULL WHERE ShowtimeId = @1 AND HoldBillId = @2 AND Status = @4 AND SeatId IN (" + DBUtil.InParams(seatIds.Cast<object>(), ps) + ")";
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Ghế đang HELD bởi hóa đơn (mọi suất).</summary>
    public static List<ShowtimeSeat> HeldByBill(int billId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT ShowtimeId, SeatId, Status, HoldBillId, HoldExpiresAt, HoldAudienceType, TicketId FROM ShowtimeSeats WHERE HoldBillId = @1 AND Status = @2 ORDER BY ShowtimeId, SeatId";
        return FromDataTable(DBUtil.QueryDataTable(sql, new List<object> { billId, ShowtimeSeatStatus.Held }, conn, tx));
    }

    /// <summary>Đặt đối tượng vé cho một ghế đang giữ, hoặc mọi ghế đang giữ của hóa đơn khi seatId null. Trả số ghế đã đổi.</summary>
    public static int SetAudience(int billId, int? seatId, string audienceType, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE ShowtimeSeats SET HoldAudienceType = @3 WHERE HoldBillId = @1 AND Status = @4 AND (@2 IS NULL OR SeatId = @2)";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { billId, DBUtil.Value(seatId), audienceType, ShowtimeSeatStatus.Held }, conn, tx);
    }

    /// <summary>Đổi hạn giữ của mọi ghế đang giữ của hóa đơn (gia hạn một lần).</summary>
    public static int SetHoldExpiry(int billId, DateTime expiresAt, SqlConnection conn, SqlTransaction tx)
    {
        const string sql = "UPDATE ShowtimeSeats SET HoldExpiresAt = @2 WHERE HoldBillId = @1 AND Status = @3";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { billId, expiresAt, ShowtimeSeatStatus.Held }, conn, tx);
    }

    /// <summary>Ghế đang giữ của hóa đơn chuyển BOOKED và gắn vé (khi hóa đơn finish).</summary>
    public static int Book(int showtimeId, int seatId, int billId, int ticketId, SqlConnection conn, SqlTransaction tx)
    {
        const string sql = "UPDATE ShowtimeSeats SET Status = @5, TicketId = @4, HoldBillId = NULL, HoldExpiresAt = NULL, HoldAudienceType = NULL WHERE ShowtimeId = @1 AND SeatId = @2 AND HoldBillId = @3 AND Status = @6";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { showtimeId, seatId, billId, ticketId, ShowtimeSeatStatus.Booked, ShowtimeSeatStatus.Held }, conn, tx);
    }

    /// <summary>
    /// Trả các ghế HELD đã hết hạn mà hóa đơn giữ không còn waiting (hoặc không có hóa đơn) về AVAILABLE; trả danh sách (suất, ghế) đã đổi.
    /// Ghế của hóa đơn waiting hết hạn do bộ quét xử lý cùng hóa đơn.
    /// </summary>
    public static List<(int ShowtimeId, int SeatId)> ReleaseStrayExpired(DateTime now, SqlConnection conn, SqlTransaction tx)
    {
        const string sql = "UPDATE ss SET ss.Status = @2, ss.HoldBillId = NULL, ss.HoldExpiresAt = NULL, ss.HoldAudienceType = NULL" +
                           " OUTPUT inserted.ShowtimeId, inserted.SeatId FROM ShowtimeSeats ss LEFT JOIN Bills b ON b.Id = ss.HoldBillId" +
                           " WHERE ss.Status = @3 AND ss.HoldExpiresAt <= @1 AND (b.Id IS NULL OR b.Status <> @4)";
        var t = DBUtil.QueryDataTable(sql, new List<object> { now, ShowtimeSeatStatus.Available, ShowtimeSeatStatus.Held, BillStatus.Waiting }, conn, tx);
        return t.Rows.Cast<DataRow>().Select(r => (r.GetInt("ShowtimeId"), r.GetInt("SeatId"))).ToList();
    }

    /// <summary>Ghế gắn với vé của hóa đơn (để phát sự kiện sau khi hủy).</summary>
    public static List<(int ShowtimeId, int SeatId)> SeatsOfBillTickets(int billId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT ShowtimeId, SeatId FROM Tickets WHERE BillId = @1 AND CheckInStatus NOT IN (@2, @3)";
        var t = DBUtil.QueryDataTable(sql, new List<object> { billId, TicketCheckInStatus.Swapped, TicketCheckInStatus.Voided }, conn, tx);
        return t.Rows.Cast<DataRow>().Select(r => (r.GetInt("ShowtimeId"), r.GetInt("SeatId"))).ToList();
    }
}

public static partial class ShowtimeDAL
{
    /// <summary>Các suất không bị hủy của rạp bắt đầu trong [from, to), kèm số ghế trống hiệu lực (danh sách bán của POS).</summary>
    public static List<PosShowtimeRow> ForSale(int cinemaId, DateTime from, DateTime to, DateTime now, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT t.Id, t.MovieId, m.Title, m.AgeRating, m.PosterUrl, m.DurationMinutes, a.Name AS AuditoriumName, a.Format, t.LanguageFormat," +
                           " t.StartTime, t.FeatureStartTime, t.EndTime, t.OperationalStatus," +
                           " (SELECT COUNT(*) FROM ShowtimeSeats ss WHERE ss.ShowtimeId = t.Id) AS TotalSeats," +
                           " (SELECT COUNT(*) FROM ShowtimeSeats ss JOIN Seats s ON s.Id = ss.SeatId WHERE ss.ShowtimeId = t.Id AND s.IsMaintenance = 0" +
                           "   AND (ss.Status = @5 OR (ss.Status = @6 AND ss.HoldExpiresAt <= @4))) AS FreeSeats" +
                           " FROM Showtimes t JOIN Movies m ON m.Id = t.MovieId JOIN Auditoriums a ON a.Id = t.AuditoriumId" +
                           " WHERE a.CinemaId = @1 AND t.StartTime >= @2 AND t.StartTime < @3 AND t.OperationalStatus <> @7 ORDER BY m.Title, t.StartTime";
        var ps = new List<object> { cinemaId, from, to, now, ShowtimeSeatStatus.Available, ShowtimeSeatStatus.Held, OperationalStatus.Cancelled };
        return DBUtil.QueryDataTable(sql, ps, conn, tx).Rows.Cast<DataRow>().Select(r => new PosShowtimeRow(r.GetInt("Id"), r.GetInt("MovieId"), r.GetString("Title"),
            r.GetString("AgeRating"), r.GetStringN("PosterUrl"), r.GetInt("DurationMinutes"), r.GetString("AuditoriumName"), r.GetString("Format"), r.GetString("LanguageFormat"),
            r.GetDateTime("StartTime"), r.GetDateTime("FeatureStartTime"), r.GetDateTime("EndTime"), r.GetString("OperationalStatus"), r.GetInt("TotalSeats"), r.GetInt("FreeSeats"))).ToList();
    }
}

public static partial class BillDAL
{
    private const string SelectCols = "SELECT Id, Code, Channel, CinemaId, ShowtimeId, UserId, CustomerId, PosShiftId, TerminalCode, Status, CancelReason, CreatedAt, PaidAt, CancelledAt, TicketSubtotal, FnbSubtotal, DiscountTotal, Total, VoucherId, PointsUsed, HoldExpiresAt, HoldExtended, CheckInStatus, FnbStatus, PickupNumber, GuardianConfirmed, AgeConfirmed, CorporateBookingId";

    /// <summary>Đọc hóa đơn và khóa dòng (UPDLOCK) để hai thao tác trên cùng hóa đơn chạy lần lượt.</summary>
    public static Bill? GetForUpdate(int id, SqlConnection conn, SqlTransaction tx)
    {
        const string sql = SelectCols + " FROM Bills WITH (UPDLOCK, ROWLOCK) WHERE Id = @1";
        var t = DBUtil.QueryDataTable(sql, new List<object> { id }, conn, tx);
        return t.Rows.Count == 0 ? null : FromDataRow(t.Rows[0]);
    }

    /// <summary>Hóa đơn waiting mới nhất của ca (hóa đơn tạm tính của thu ngân trên máy đó).</summary>
    public static Bill? GetWaitingForShift(int shiftId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = SelectCols + " FROM Bills WHERE PosShiftId = @1 AND Status = @2 ORDER BY Id DESC";
        var t = DBUtil.QueryDataTable(sql, new List<object> { shiftId, BillStatus.Waiting }, conn, tx);
        return t.Rows.Count == 0 ? null : FromDataRow(t.Rows[0]);
    }

    public static List<Bill> WaitingForShift(int shiftId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = SelectCols + " FROM Bills WHERE PosShiftId = @1 AND Status = @2 ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, new List<object> { shiftId, BillStatus.Waiting }, conn, tx));
    }

    /// <summary>Id các hóa đơn waiting đã hết hạn giữ ghế (bộ quét hủy EXPIRED).</summary>
    public static List<int> ExpiredWaitingIds(DateTime now, int limit, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT TOP (@3) Id FROM Bills WHERE Status = @1 AND HoldExpiresAt IS NOT NULL AND HoldExpiresAt <= @2 ORDER BY HoldExpiresAt";
        var t = DBUtil.QueryDataTable(sql, new List<object> { BillStatus.Waiting, now, limit }, conn, tx);
        return t.Rows.Cast<DataRow>().Select(r => r.GetInt("Id")).ToList();
    }

    private const string ListSql =
        "SELECT b.Id, b.Code, b.CreatedAt, b.PaidAt, b.Channel, b.CinemaId, c.Name AS CinemaName, u.FullName AS StaffName, cu.FullName AS CustomerName," +
        " (SELECT COUNT(*) FROM Tickets t WHERE t.BillId = b.Id AND t.CheckInStatus <> 'SWAPPED') AS TicketCount," +
        " (SELECT COALESCE(SUM(bd.Quantity), 0) FROM BillDetails bd WHERE bd.BillId = b.Id) AS ItemCount," +
        " b.Total, b.Status, b.CancelReason, m.Title AS MovieTitle, s.StartTime" +
        " FROM Bills b JOIN Cinemas c ON c.Id = b.CinemaId LEFT JOIN Users u ON u.Id = b.UserId LEFT JOIN Users cu ON cu.Id = b.CustomerId" +
        " LEFT JOIN Showtimes s ON s.Id = b.ShowtimeId LEFT JOIN Movies m ON m.Id = s.MovieId";

    private const string CountSql = "SELECT COUNT(*) FROM Bills b";

    private const string ListWhere =
        " WHERE b.CreatedAt >= @1 AND b.CreatedAt < @2 AND (@3 IS NULL OR b.Status = @3) AND (@4 IS NULL OR b.Channel = @4)" +
        " AND (@5 IS NULL OR b.CinemaId = @5) AND (@6 IS NULL OR (b.UserId = @6 AND b.Channel <> 'ONLINE')) AND (@7 IS NULL OR b.Code LIKE @7)";

    private static BillListRow ListRowOf(DataRow r) => new(r.GetInt("Id"), r.GetString("Code"), r.GetDateTime("CreatedAt"), r.GetDateTimeN("PaidAt"), r.GetString("Channel"),
        r.GetInt("CinemaId"), r.GetString("CinemaName"), r.GetStringN("StaffName"), r.GetStringN("CustomerName"), r.GetInt("TicketCount"), r.GetInt("ItemCount"),
        r.GetLong("Total"), r.GetString("Status"), r.GetStringN("CancelReason"), r.GetStringN("MovieTitle"), r.GetDateTimeN("StartTime"));

    private static List<object> ListParams(DateTime from, DateTime toExclusive, string? status, string? channel, int? cinemaId, int? staffId, string? code) =>
        new() { from, toExclusive, DBUtil.Value(status), DBUtil.Value(channel), DBUtil.Value(cinemaId), DBUtil.Value(staffId),
            string.IsNullOrWhiteSpace(code) ? DBNull.Value : "%" + code.Trim().Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%" };

    /// <summary>Tìm hóa đơn theo khoảng ngày tạo, trạng thái, kênh, rạp, nhân viên, mã; có phân trang (limit ≤ 0 là lấy hết, dùng khi xuất CSV).</summary>
    public static List<BillListRow> Search(DateTime from, DateTime toExclusive, string? status, string? channel, int? cinemaId, int? staffId, string? code,
        int offset, int limit, out int total)
    {
        var ps = ListParams(from, toExclusive, status, channel, cinemaId, staffId, code);
        total = Convert.ToInt32(DBUtil.ExecuteScalar(CountSql + ListWhere, ps));
        var sql = ListSql + ListWhere + " ORDER BY b.CreatedAt DESC, b.Id DESC" + (limit > 0 ? " OFFSET @8 ROWS FETCH NEXT @9 ROWS ONLY" : string.Empty);
        var ps2 = ListParams(from, toExclusive, status, channel, cinemaId, staffId, code);
        if (limit > 0) { ps2.Add(offset); ps2.Add(limit); }
        return DBUtil.QueryDataTable(sql, ps2).Rows.Cast<DataRow>().Select(ListRowOf).ToList();
    }

    /// <summary>Một dòng danh sách theo Id (đầu trang chi tiết).</summary>
    public static BillListRow? GetListRow(int id)
    {
        const string sql = ListSql + " WHERE b.Id = @1";
        var t = DBUtil.QueryDataTable(sql, new List<object> { id });
        return t.Rows.Count == 0 ? null : ListRowOf(t.Rows[0]);
    }

    /// <summary>Số hóa đơn waiting của ca.</summary>
    public static int CountWaitingForShift(int shiftId, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        Convert.ToInt32(DBUtil.ExecuteScalar("SELECT COUNT(*) FROM Bills WHERE PosShiftId = @1 AND Status = @2", new List<object> { shiftId, BillStatus.Waiting }, conn, tx));

    /// <summary>Số gọi bắp nước lớn nhất đã cấp trong ngày vận hành của rạp (để cấp số tiếp theo).</summary>
    public static int? LastPickupNumber(int cinemaId, DateTime dayStart, DateTime dayEnd, SqlConnection conn, SqlTransaction tx)
    {
        const string sql = "SELECT TOP 1 PickupNumber FROM Bills WITH (UPDLOCK, HOLDLOCK) WHERE CinemaId = @1 AND PickupNumber IS NOT NULL AND PaidAt >= @2 AND PaidAt < @3 ORDER BY PaidAt DESC, Id DESC";
        var v = DBUtil.ExecuteScalar(sql, new List<object> { cinemaId, dayStart, dayEnd }, conn, tx);
        return v == null || v == DBNull.Value ? null : Convert.ToInt32(v);
    }

    /// <summary>Tổng chi tiêu của khách trong khoảng thời gian (hóa đơn finish theo PaidAt) để xét hạng.</summary>
    public static long CustomerSpend(int customerId, DateTime from, DateTime to, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COALESCE(SUM(Total), 0) FROM Bills WHERE CustomerId = @1 AND Status = @2 AND PaidAt >= @3 AND PaidAt <= @4";
        return Convert.ToInt64(DBUtil.ExecuteScalar(sql, new List<object> { customerId, BillStatus.Finish, from, to }, conn, tx));
    }
}

public static partial class BillDetailDAL
{
    public static List<BillDetail> GetByBill(int billId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, BillId, DrinkId, Quantity, Price, VatRate, LineFinalAmount FROM BillDetails WHERE BillId = @1 ORDER BY Id";
        return FromDataTable(DBUtil.QueryDataTable(sql, new List<object> { billId }, conn, tx));
    }

    public static List<BillLineRow> RowsByBill(int billId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT bd.Id, bd.DrinkId, d.Name, d.Image, d.IsCombo, bd.Quantity, bd.Price, bd.VatRate, bd.LineFinalAmount FROM BillDetails bd JOIN Drinks d ON d.Id = bd.DrinkId WHERE bd.BillId = @1 ORDER BY bd.Id";
        return DBUtil.QueryDataTable(sql, new List<object> { billId }, conn, tx).Rows.Cast<DataRow>().Select(r => new BillLineRow(r.GetInt("Id"), r.GetInt("DrinkId"),
            r.GetString("Name"), r.GetStringN("Image"), r.GetBool("IsCombo"), r.GetInt("Quantity"), r.GetLong("Price"), r.GetDecimal("VatRate"), r.GetLong("LineFinalAmount"))).ToList();
    }
}

public static partial class TicketDAL
{
    /// <summary>Vé của hóa đơn kèm ghế, suất, phim, phòng.</summary>
    public static List<TicketRow> RowsByBill(int billId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT t.Id, t.SeatId, s.Label, s.SeatType, t.AudienceType, t.ListPrice, t.FinalPrice, t.VatRate, t.QrCode, t.CheckInStatus," +
                           " m.Title, m.AgeRating, st.StartTime, st.EndTime, a.Name AS AuditoriumName, st.LanguageFormat, a.Format" +
                           " FROM Tickets t JOIN Seats s ON s.Id = t.SeatId JOIN Showtimes st ON st.Id = t.ShowtimeId JOIN Movies m ON m.Id = st.MovieId JOIN Auditoriums a ON a.Id = st.AuditoriumId" +
                           " WHERE t.BillId = @1 ORDER BY s.RowIndex, s.ColIndex";
        return DBUtil.QueryDataTable(sql, new List<object> { billId }, conn, tx).Rows.Cast<DataRow>().Select(r => new TicketRow(r.GetInt("Id"), r.GetInt("SeatId"),
            r.GetString("Label"), r.GetString("SeatType"), r.GetString("AudienceType"), r.GetLong("ListPrice"), r.GetLong("FinalPrice"), r.GetDecimal("VatRate"),
            r.GetString("QrCode"), r.GetString("CheckInStatus"), r.GetString("Title"), r.GetString("AgeRating"), r.GetDateTime("StartTime"), r.GetDateTime("EndTime"),
            r.GetString("AuditoriumName"), r.GetString("LanguageFormat"), r.GetString("Format"))).ToList();
    }

    /// <summary>Số vé đã vào cửa của hóa đơn (không cho hủy nếu khác 0).</summary>
    public static int CountCheckedIn(int billId, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        Convert.ToInt32(DBUtil.ExecuteScalar("SELECT COUNT(*) FROM Tickets WHERE BillId = @1 AND CheckInStatus = @2", new List<object> { billId, TicketCheckInStatus.CheckedIn }, conn, tx));

    public static bool QrExists(string qr, SqlConnection conn, SqlTransaction tx) =>
        Convert.ToInt32(DBUtil.ExecuteScalar("SELECT COUNT(*) FROM Tickets WHERE QrCode = @1", new List<object> { qr }, conn, tx)) > 0;

    /// <summary>Hóa đơn của vé có mã QR (quét QR tại quầy).</summary>
    public static int? BillIdByQr(string qr)
    {
        var v = DBUtil.ExecuteScalar("SELECT BillId FROM Tickets WHERE QrCode = @1", new List<object> { qr });
        return v == null || v == DBNull.Value ? null : Convert.ToInt32(v);
    }
}

public static partial class PaymentDAL
{
    public static List<PaymentRow> RowsByBill(int billId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Method, Direction, Amount, Status, CreatedAt, PosShiftId FROM Payments WHERE BillId = @1 ORDER BY CreatedAt, Id";
        return DBUtil.QueryDataTable(sql, new List<object> { billId }, conn, tx).Rows.Cast<DataRow>().Select(r => new PaymentRow(r.GetInt("Id"), r.GetString("Method"),
            r.GetString("Direction"), r.GetLong("Amount"), r.GetString("Status"), r.GetDateTime("CreatedAt"), r.GetIntN("PosShiftId"))).ToList();
    }

    /// <summary>Tiền mặt thu và hoàn trong ca (thanh toán thành công).</summary>
    public static (long CashIn, long CashRefund) CashOfShift(int shiftId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COALESCE(SUM(CASE WHEN Direction = @3 THEN Amount ELSE 0 END), 0) AS CashIn, COALESCE(SUM(CASE WHEN Direction = @4 THEN Amount ELSE 0 END), 0) AS CashRefund" +
                           " FROM Payments WHERE PosShiftId = @1 AND Method = @2 AND Status = @5";
        var r = DBUtil.QueryDataTable(sql, new List<object> { shiftId, PaymentMethod.Cash, PaymentDirection.In, PaymentDirection.Refund, PaymentStatus.Succeeded }, conn, tx).Rows[0];
        return (r.GetLong("CashIn"), r.GetLong("CashRefund"));
    }
}

public static partial class PosShiftDAL
{
    /// <summary>Ca đang mở tại một máy của rạp.</summary>
    public static PosShift? GetOpenByTerminal(int cinemaId, string terminalCode, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, UserId, CinemaId, TerminalCode, OpenedAt, OpeningCashFloat, ClosedAt, ActualCashCounted, ExpectedCash, Discrepancy, Status FROM PosShifts WHERE CinemaId = @1 AND TerminalCode = @2 AND Status = @3";
        var t = DBUtil.QueryDataTable(sql, new List<object> { cinemaId, terminalCode, ShiftStatus.Open }, conn, tx);
        return t.Rows.Count == 0 ? null : FromDataRow(t.Rows[0]);
    }

    private const string ShiftRowSql =
        "SELECT p.Id, p.UserId, p.CinemaId, p.TerminalCode, p.OpenedAt, p.OpeningCashFloat, p.ClosedAt, p.ActualCashCounted, p.ExpectedCash, p.Discrepancy, p.Status," +
        " u.FullName AS UserName, c.Name AS CinemaName," +
        " (SELECT COALESCE(SUM(Amount), 0) FROM Payments x WHERE x.PosShiftId = p.Id AND x.Method = 'CASH' AND x.Direction = 'IN' AND x.Status = 'SUCCEEDED') AS CashIn," +
        " (SELECT COALESCE(SUM(Amount), 0) FROM Payments x WHERE x.PosShiftId = p.Id AND x.Method = 'CASH' AND x.Direction = 'REFUND' AND x.Status = 'SUCCEEDED') AS CashRefund," +
        " (SELECT COUNT(*) FROM Bills b WHERE b.PosShiftId = p.Id AND b.Status = 'finish') AS BillCount" +
        " FROM PosShifts p JOIN Users u ON u.Id = p.UserId JOIN Cinemas c ON c.Id = p.CinemaId";

    private static ShiftRow ShiftRowOf(DataRow r) => new(FromDataRow(r), r.GetString("UserName"), r.GetString("CinemaName"), r.GetLong("CashIn"), r.GetLong("CashRefund"), r.GetInt("BillCount"));

    public static ShiftRow? GetRow(int id)
    {
        var t = DBUtil.QueryDataTable(ShiftRowSql + " WHERE p.Id = @1", new List<object> { id });
        return t.Rows.Count == 0 ? null : ShiftRowOf(t.Rows[0]);
    }

    /// <summary>Các ca đã đóng trong khoảng ngày mở ca (báo cáo biên bản ca).</summary>
    public static List<ShiftRow> ClosedRows(DateTime from, DateTime toExclusive, int? cinemaId)
    {
        const string sql = ShiftRowSql + " WHERE p.Status = @4 AND p.OpenedAt >= @1 AND p.OpenedAt < @2 AND (@3 IS NULL OR p.CinemaId = @3) ORDER BY p.OpenedAt DESC";
        return DBUtil.QueryDataTable(sql, new List<object> { from, toExclusive, DBUtil.Value(cinemaId), ShiftStatus.Closed }).Rows.Cast<DataRow>().Select(ShiftRowOf).ToList();
    }

    /// <summary>Chốt ca: chỉ đổi khi ca còn OPEN (biên bản không sửa được sau khi chốt).</summary>
    public static int Close(int id, DateTime closedAt, long counted, long expected, long discrepancy, SqlConnection conn, SqlTransaction tx)
    {
        const string sql = "UPDATE PosShifts SET Status = @2, ClosedAt = @3, ActualCashCounted = @4, ExpectedCash = @5, Discrepancy = @6 WHERE Id = @1 AND Status = @7";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { id, ShiftStatus.Closed, closedAt, counted, expected, discrepancy, ShiftStatus.Open }, conn, tx);
    }
}

public static partial class UserDAL
{
    /// <summary>Tìm khách theo mã thành viên, số điện thoại hoặc email (đúng toàn bộ chuỗi).</summary>
    public static User? FindCustomer(string query)
    {
        const string sql = "SELECT TOP 1 Id, Email, PasswordHash, FullName, Phone, BirthDate, Role, CinemaId, Active, MustChangePassword, MemberCode, LoyaltyPoints, Tier, WalletBalance, ManagerPinHash, PinFailedCount, PinLockedUntil, CreatedAt" +
                           " FROM Users WHERE Role = @2 AND Active = 1 AND (MemberCode = @1 OR Phone = @1 OR Email = @1) ORDER BY Id";
        var t = DBUtil.QueryDataTable(sql, new List<object> { query.Trim(), Roles.Customer });
        return t.Rows.Count == 0 ? null : FromDataRow(t.Rows[0]);
    }

    /// <summary>Người duyệt đang hoạt động của rạp: ShiftManager của rạp và Manager (của rạp hoặc toàn chuỗi) có PIN.</summary>
    public static List<SupervisorRow> Supervisors(int cinemaId)
    {
        const string sql = "SELECT Id, FullName, Role, CinemaId FROM Users WHERE Active = 1 AND ManagerPinHash IS NOT NULL AND ((Role = @2 AND CinemaId = @1) OR (Role = @3 AND (CinemaId IS NULL OR CinemaId = @1))) ORDER BY Role DESC, FullName";
        return DBUtil.QueryDataTable(sql, new List<object> { cinemaId, Roles.ShiftManager, Roles.Manager }).Rows.Cast<DataRow>()
            .Select(r => new SupervisorRow(r.GetInt("Id"), r.GetString("FullName"), r.GetString("Role"), r.GetIntN("CinemaId"))).ToList();
    }

    /// <summary>Đặt (hoặc gỡ khi null) PIN quản lý đã băm; xóa đếm sai và thời gian khóa.</summary>
    public static int SetManagerPin(int id, string? pinHash, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        DBUtil.ExecuteNonQuery("UPDATE Users SET ManagerPinHash = @2, PinFailedCount = 0, PinLockedUntil = NULL WHERE Id = @1", new List<object> { id, DBUtil.Value(pinHash) }, conn, tx);

    public static int UpdatePinState(int id, int failedCount, DateTime? lockedUntil, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        DBUtil.ExecuteNonQuery("UPDATE Users SET PinFailedCount = @2, PinLockedUntil = @3 WHERE Id = @1", new List<object> { id, failedCount, DBUtil.Value(lockedUntil) }, conn, tx);

    /// <summary>Cộng hoặc trừ điểm (không âm) và đặt hạng mới. Trả số dòng đã đổi (0 nếu số dư không đủ).</summary>
    public static int AddPoints(int id, int delta, string tier, SqlConnection conn, SqlTransaction tx) =>
        DBUtil.ExecuteNonQuery("UPDATE Users SET LoyaltyPoints = LoyaltyPoints + @2, Tier = @3 WHERE Id = @1 AND LoyaltyPoints + @2 >= 0", new List<object> { id, delta, tier }, conn, tx);
    // Ví chỉ đổi qua WalletService.Debit, Credit (luôn ghi WalletTransactions trong cùng transaction, SPEC 3.13).
}

public static partial class VoucherDAL
{
    public static Voucher? GetByCode(string code, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Code, Type, Value, MinBillAmount, ValidFrom, ValidUntil, UsageLimit, UsedCount, OwnerUserId, Source, CorporateBookingId, Active FROM Vouchers WHERE Code = @1";
        var t = DBUtil.QueryDataTable(sql, new List<object> { code.Trim().ToUpperInvariant() }, conn, tx);
        return t.Rows.Count == 0 ? null : FromDataRow(t.Rows[0]);
    }

    /// <summary>Tăng (delta = 1) hoặc trả lại (delta = −1) một lượt dùng, trong giới hạn UsageLimit. Trả số dòng đã đổi.</summary>
    public static int ChangeUsed(int id, int delta, SqlConnection conn, SqlTransaction tx) =>
        DBUtil.ExecuteNonQuery("UPDATE Vouchers SET UsedCount = UsedCount + @2 WHERE Id = @1 AND UsedCount + @2 >= 0 AND UsedCount + @2 <= UsageLimit", new List<object> { id, delta }, conn, tx);
}

public static partial class PointTransactionDAL
{
    /// <summary>Tổng điểm đã ghi cho hóa đơn theo dấu (cộng: Points &gt; 0, trừ: Points &lt; 0).</summary>
    public static (int Earned, int Redeemed) SumForBill(int billId, int userId, SqlConnection conn, SqlTransaction tx)
    {
        const string sql = "SELECT COALESCE(SUM(CASE WHEN Points > 0 THEN Points ELSE 0 END), 0) AS E, COALESCE(SUM(CASE WHEN Points < 0 THEN -Points ELSE 0 END), 0) AS R FROM PointTransactions WHERE BillId = @1 AND UserId = @2";
        var r = DBUtil.QueryDataTable(sql, new List<object> { billId, userId }, conn, tx).Rows[0];
        return (r.GetInt("E"), r.GetInt("R"));
    }
}

public static partial class RawMaterialDAL
{
    private const string StockSql = "SELECT pr.DrinkId, rm.Id AS RawMaterialId, rm.Name, pr.Quantity, rm.ActualStock - rm.ReservedStock AS Available FROM ProductRecipes pr JOIN RawMaterials rm ON rm.Id = pr.RawMaterialId WHERE rm.CinemaId = @1 AND rm.Active = 1";

    /// <summary>Định mức của các món tại rạp kèm khả dụng của từng nguyên liệu (ActualStock − ReservedStock).</summary>
    public static List<DrinkStockLine> StockLines(int cinemaId, IReadOnlyCollection<int>? drinkIds = null, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        var ps = new List<object> { cinemaId };
        var sql = StockSql + (drinkIds == null ? string.Empty : " AND pr.DrinkId IN (" + DBUtil.InParams(drinkIds.Cast<object>(), ps) + ")");
        return DBUtil.QueryDataTable(sql, ps, conn, tx).Rows.Cast<DataRow>().Select(r => new DrinkStockLine(r.GetInt("DrinkId"), r.GetInt("RawMaterialId"),
            r.GetString("Name"), r.GetDecimal("Quantity"), r.GetDecimal("Available"))).ToList();
    }

    /// <summary>Khóa các nguyên liệu dùng cho hóa đơn (UPDLOCK) rồi trả khả dụng, để kiểm và trừ kho trong cùng transaction.</summary>
    public static List<DrinkStockLine> LockForBill(int billId, int cinemaId, SqlConnection conn, SqlTransaction tx)
    {
        const string sql = "SELECT pr.DrinkId, rm.Id AS RawMaterialId, rm.Name, pr.Quantity, rm.ActualStock - rm.ReservedStock AS Available" +
                           " FROM BillDetails bd JOIN ProductRecipes pr ON pr.DrinkId = bd.DrinkId JOIN RawMaterials rm WITH (UPDLOCK, ROWLOCK) ON rm.Id = pr.RawMaterialId" +
                           " WHERE bd.BillId = @1 AND rm.CinemaId = @2";
        return DBUtil.QueryDataTable(sql, new List<object> { billId, cinemaId }, conn, tx).Rows.Cast<DataRow>().Select(r => new DrinkStockLine(r.GetInt("DrinkId"),
            r.GetInt("RawMaterialId"), r.GetString("Name"), r.GetDecimal("Quantity"), r.GetDecimal("Available"))).ToList();
    }

    /// <summary>
    /// Đổi tồn kho theo định mức × số lượng của mọi dòng bắp nước của hóa đơn: actualSign −1 trừ ActualStock (CONSUME), +1 cộng lại;
    /// reservedSign −1 giảm ReservedStock (RELEASE). Trả số nguyên liệu đã đổi.
    /// </summary>
    public static int ApplyForBill(int billId, int cinemaId, int actualSign, int reservedSign, SqlConnection conn, SqlTransaction tx)
    {
        const string sql = "UPDATE rm SET rm.ActualStock = rm.ActualStock + @3 * x.Qty, rm.ReservedStock = rm.ReservedStock + @4 * x.Qty FROM RawMaterials rm" +
                           " JOIN (SELECT pr.RawMaterialId, SUM(pr.Quantity * bd.Quantity) AS Qty FROM BillDetails bd JOIN ProductRecipes pr ON pr.DrinkId = bd.DrinkId WHERE bd.BillId = @1 GROUP BY pr.RawMaterialId) x" +
                           " ON x.RawMaterialId = rm.Id WHERE rm.CinemaId = @2";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { billId, cinemaId, actualSign, reservedSign }, conn, tx);
    }
}

public static partial class SupervisorApprovalDAL
{
    public static List<ApprovalRow> RowsByBill(int billId)
    {
        const string sql = "SELECT a.Id, a.Status, a.Method, a.Reason, r.FullName AS Requester, d.FullName AS Decider, a.CreatedAt, a.DecidedAt" +
                           " FROM SupervisorApprovals a JOIN Users r ON r.Id = a.RequestedBy LEFT JOIN Users d ON d.Id = a.DecidedBy WHERE a.BillId = @1 ORDER BY a.CreatedAt";
        return DBUtil.QueryDataTable(sql, new List<object> { billId }).Rows.Cast<DataRow>().Select(r => new ApprovalRow(r.GetInt("Id"), r.GetString("Status"),
            r.GetStringN("Method"), r.GetString("Reason"), r.GetString("Requester"), r.GetStringN("Decider"), r.GetDateTime("CreatedAt"), r.GetDateTimeN("DecidedAt"))).ToList();
    }
}
