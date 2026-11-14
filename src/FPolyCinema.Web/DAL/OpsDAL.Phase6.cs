using System.Data;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

// Truy vấn vận hành của Phase 6: đồng hồ suất, soát vé, sĩ số, bảng sảnh, bếp, ghế nội bộ, kho, hủy hàng, doanh nghiệp, phê duyệt từ xa.
// Mọi hàm ghi nhận conn và tx để service gom vào DBUtil.InTransaction.

public static partial class ShowtimeDAL
{
    /// <summary>Các suất bắt đầu trong [from, to) kèm phòng và rạp (kể cả suất đã dọn xong, đã hủy để biết suất trước của phòng).</summary>
    public static List<ClockShowtimeRow> ForClock(DateTime from, DateTime to, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT t.Id, t.AuditoriumId, a.CinemaId, t.StartTime, t.FeatureStartTime, t.EndTime, t.OperationalStatus FROM Showtimes t" +
                           " JOIN Auditoriums a ON a.Id = t.AuditoriumId WHERE t.StartTime >= @1 AND t.StartTime < @2 ORDER BY t.AuditoriumId, t.StartTime";
        return DBUtil.QueryDataTable(sql, new List<object> { from, to }, conn, tx).Rows.Cast<DataRow>().Select(r => new ClockShowtimeRow(r.GetInt("Id"),
            r.GetInt("AuditoriumId"), r.GetInt("CinemaId"), r.GetDateTime("StartTime"), r.GetDateTime("FeatureStartTime"), r.GetDateTime("EndTime"),
            r.GetString("OperationalStatus"))).ToList();
    }

    /// <summary>Suất còn chưa dọn xong (không COMPLETED, không CANCELLED) bắt đầu trước mốc: ngày vận hành đã qua.</summary>
    public static List<ClockShowtimeRow> UnfinishedBefore(DateTime before, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT t.Id, t.AuditoriumId, a.CinemaId, t.StartTime, t.FeatureStartTime, t.EndTime, t.OperationalStatus FROM Showtimes t" +
                           " JOIN Auditoriums a ON a.Id = t.AuditoriumId WHERE t.StartTime < @1 AND t.OperationalStatus NOT IN (@2, @3)";
        return DBUtil.QueryDataTable(sql, new List<object> { before, OperationalStatus.Completed, OperationalStatus.Cancelled }, conn, tx).Rows.Cast<DataRow>()
            .Select(r => new ClockShowtimeRow(r.GetInt("Id"), r.GetInt("AuditoriumId"), r.GetInt("CinemaId"), r.GetDateTime("StartTime"), r.GetDateTime("FeatureStartTime"),
                r.GetDateTime("EndTime"), r.GetString("OperationalStatus"))).ToList();
    }

    /// <summary>Trạng thái của suất liền trước (không tính suất đã hủy) trong cùng phòng; null nếu không có suất trước.</summary>
    public static string? PreviousStatus(int auditoriumId, DateTime startTime, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        DBUtil.ExecuteScalar("SELECT TOP 1 OperationalStatus FROM Showtimes WHERE AuditoriumId = @1 AND StartTime < @2 AND OperationalStatus <> @3 ORDER BY StartTime DESC",
            new List<object> { auditoriumId, startTime, OperationalStatus.Cancelled }, conn, tx) as string;

    /// <summary>Đổi OperationalStatus có điều kiện trạng thái cũ (chống ghi đè khi hai tiến trình cùng đổi). Trả số dòng đã đổi.</summary>
    public static int SetOperationalStatus(int id, string from, string to, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        DBUtil.ExecuteNonQuery("UPDATE Showtimes SET OperationalStatus = @3 WHERE Id = @1 AND OperationalStatus = @2", new List<object> { id, from, to }, conn, tx);

    /// <summary>Hủy suất do sự cố: OperationalStatus = CANCELLED kèm ghi chú lý do.</summary>
    public static int Cancel(int id, string note, SqlConnection conn, SqlTransaction tx) =>
        DBUtil.ExecuteNonQuery("UPDATE Showtimes SET OperationalStatus = @2, CancelNote = @3 WHERE Id = @1 AND OperationalStatus <> @2",
            new List<object> { id, OperationalStatus.Cancelled, note }, conn, tx);

    /// <summary>Đánh dấu suất thuê trọn (ẩn khỏi web khách).</summary>
    public static int SetPrivate(int id, bool isPrivate, SqlConnection conn, SqlTransaction tx) =>
        DBUtil.ExecuteNonQuery("UPDATE Showtimes SET IsPrivate = @2 WHERE Id = @1", new List<object> { id, isPrivate }, conn, tx);

    /// <summary>Khóa dòng suất (UPDLOCK) để hủy suất hoặc thuê trọn chạy lần lượt.</summary>
    public static Showtime? GetForUpdate(int id, SqlConnection conn, SqlTransaction tx)
    {
        var t = DBUtil.QueryDataTable("SELECT * FROM Showtimes WITH (UPDLOCK, ROWLOCK) WHERE Id = @1", new List<object> { id }, conn, tx);
        return t.Rows.Count == 0 ? null : FromDataRow(t.Rows[0]);
    }

    private const string OccupancySql =
        "SELECT t.Id AS ShowtimeId, t.AuditoriumId, a.Name AS AuditoriumName, a.CinemaId, c.Name AS CinemaName, m.Title, m.AgeRating, t.StartTime, t.FeatureStartTime, t.EndTime, t.OperationalStatus," +
        " (SELECT COUNT(*) FROM Tickets k JOIN Bills b ON b.Id = k.BillId WHERE k.ShowtimeId = t.Id AND b.Status = @9 AND k.CheckInStatus IN (@10, @11)) AS Sold," +
        " (SELECT COUNT(*) FROM Tickets k JOIN Bills b ON b.Id = k.BillId WHERE k.ShowtimeId = t.Id AND b.Status = @9 AND k.CheckInStatus = @11) AS CheckedIn," +
        " (SELECT COUNT(*) FROM ShowtimeSeats ss WHERE ss.ShowtimeId = t.Id) AS Capacity" +
        " FROM Showtimes t JOIN Auditoriums a ON a.Id = t.AuditoriumId JOIN Cinemas c ON c.Id = a.CinemaId JOIN Movies m ON m.Id = t.MovieId";

    private static LiveOccupancyRow OccupancyOf(DataRow r) => new(r.GetInt("ShowtimeId"), r.GetInt("AuditoriumId"), r.GetString("AuditoriumName"), r.GetInt("CinemaId"),
        r.GetString("CinemaName"), r.GetString("Title"), r.GetString("AgeRating"), r.GetDateTime("StartTime"), r.GetDateTime("FeatureStartTime"), r.GetDateTime("EndTime"),
        r.GetString("OperationalStatus"), r.GetInt("Sold"), r.GetInt("CheckedIn"), r.GetInt("Capacity"));

    /// <summary>
    /// Suất cho dashboard vận hành trong [from, to): đang mở cửa, đang chiếu, đang dọn, hoặc còn SCHEDULED (để báo đỏ suất trễ), kèm sĩ số.
    /// </summary>
    public static List<LiveOccupancyRow> Operations(int? cinemaId, DateTime from, DateTime to, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = OccupancySql + " WHERE (@1 IS NULL OR a.CinemaId = @1) AND t.StartTime < @3 AND t.EndTime >= @2 AND t.OperationalStatus NOT IN (@4, @5) ORDER BY t.StartTime, a.Name";
        var ps = new List<object> { DBUtil.Value(cinemaId), from, to, OperationalStatus.Completed, OperationalStatus.Cancelled, 0, 0, 0,
            BillStatus.Finish, TicketCheckInStatus.NotCheckedIn, TicketCheckInStatus.CheckedIn };
        return DBUtil.QueryDataTable(sql, ps, conn, tx).Rows.Cast<DataRow>().Select(OccupancyOf).ToList();
    }

    /// <summary>Sĩ số một suất.</summary>
    public static LiveOccupancyRow? Occupancy(int showtimeId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = OccupancySql + " WHERE t.Id = @1";
        var ps = new List<object> { showtimeId, 0, 0, 0, 0, 0, 0, 0, BillStatus.Finish, TicketCheckInStatus.NotCheckedIn, TicketCheckInStatus.CheckedIn };
        var t = DBUtil.QueryDataTable(sql, ps, conn, tx);
        return t.Rows.Count == 0 ? null : OccupancyOf(t.Rows[0]);
    }

    /// <summary>Bảng sảnh: suất không riêng tư, không hủy của rạp, kết thúc sau now (hoặc đang dọn phòng trong 6 giờ qua) và bắt đầu trước mốc to, sắp theo giờ.</summary>
    public static List<SignageRow> Signage(int cinemaId, DateTime now, DateTime to, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT t.Id, t.StartTime, t.EndTime, m.Title, m.AgeRating, a.Format, t.LanguageFormat, a.Name AS AuditoriumName, t.OperationalStatus" +
                           " FROM Showtimes t JOIN Auditoriums a ON a.Id = t.AuditoriumId JOIN Movies m ON m.Id = t.MovieId" +
                           " WHERE a.CinemaId = @1 AND t.IsPrivate = 0 AND t.OperationalStatus NOT IN (@4, @5) AND (t.EndTime > @2 OR (t.OperationalStatus = @6 AND t.EndTime > @7)) AND t.StartTime < @3 ORDER BY t.StartTime, a.Name";
        var ps = new List<object> { cinemaId, now, to, OperationalStatus.Cancelled, OperationalStatus.Completed, OperationalStatus.CleaningInProgress, now.AddHours(-6) };
        return DBUtil.QueryDataTable(sql, ps, conn, tx).Rows.Cast<DataRow>().Select(r => new SignageRow(r.GetInt("Id"), r.GetDateTime("StartTime"), r.GetDateTime("EndTime"),
            r.GetString("Title"), r.GetString("AgeRating"), r.GetString("Format"), r.GetString("LanguageFormat"), r.GetString("AuditoriumName"), r.GetString("OperationalStatus"))).ToList();
    }

    /// <summary>Suất của rạp trong [from, to) dùng cho chọn suất (hủy suất, thuê trọn), kèm số vé đã bán.</summary>
    public static List<LiveOccupancyRow> ForCinemaRange(int? cinemaId, DateTime from, DateTime to, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = OccupancySql + " WHERE (@1 IS NULL OR a.CinemaId = @1) AND t.StartTime >= @2 AND t.StartTime < @3 AND t.OperationalStatus NOT IN (@4, @5) ORDER BY t.StartTime, a.Name";
        var ps = new List<object> { DBUtil.Value(cinemaId), from, to, OperationalStatus.Completed, OperationalStatus.Cancelled, 0, 0, 0,
            BillStatus.Finish, TicketCheckInStatus.NotCheckedIn, TicketCheckInStatus.CheckedIn };
        return DBUtil.QueryDataTable(sql, ps, conn, tx).Rows.Cast<DataRow>().Select(OccupancyOf).ToList();
    }
}

public static partial class TicketDAL
{
    private const string GateSql = "SELECT t.Id, t.SeatId, s.Label, s.SeatType, s.PairGroupId, t.AudienceType, t.CheckInStatus, t.CheckedInAt, ad.RequiresDocument, t.ListPrice, t.FinalPrice, t.QrCode" +
                                   " FROM Tickets t JOIN Seats s ON s.Id = t.SeatId JOIN AudienceDiscounts ad ON ad.AudienceType = t.AudienceType";

    private static GateTicketRow GateOf(DataRow r) => new(r.GetInt("Id"), r.GetInt("SeatId"), r.GetString("Label"), r.GetString("SeatType"), r.GetIntN("PairGroupId"),
        r.GetString("AudienceType"), r.GetString("CheckInStatus"), r.GetDateTimeN("CheckedInAt"), r.GetBool("RequiresDocument"), r.GetLong("ListPrice"),
        r.GetLong("FinalPrice"), r.GetString("QrCode"));

    /// <summary>Mọi vé của hóa đơn kèm nhãn ghế, loại ghế và cờ cần giấy tờ, sắp theo hàng rồi cột.</summary>
    public static List<GateTicketRow> GateRows(int billId, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        DBUtil.QueryDataTable(GateSql + " WHERE t.BillId = @1 ORDER BY s.RowIndex, s.ColIndex", new List<object> { billId }, conn, tx).Rows.Cast<DataRow>().Select(GateOf).ToList();

    /// <summary>Vé theo mã QR.</summary>
    public static Ticket? GetByQr(string qr, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        var t = DBUtil.QueryDataTable("SELECT * FROM Tickets WHERE QrCode = @1", new List<object> { qr }, conn, tx);
        return t.Rows.Count == 0 ? null : FromDataRow(t.Rows[0]);
    }

    /// <summary>Cho các vé NOT_CHECKED_IN của hóa đơn vào cửa. Trả số vé đã đổi.</summary>
    public static int CheckIn(int billId, IReadOnlyCollection<int> ticketIds, DateTime at, int userId, SqlConnection conn, SqlTransaction tx)
    {
        if (ticketIds.Count == 0) return 0;
        var ps = new List<object> { billId, TicketCheckInStatus.CheckedIn, at, userId, TicketCheckInStatus.NotCheckedIn };
        var sql = "UPDATE Tickets SET CheckInStatus = @2, CheckedInAt = @3, CheckedInBy = @4 WHERE BillId = @1 AND CheckInStatus = @5 AND Id IN (" +
                  DBUtil.InParams(ticketIds.Cast<object>(), ps) + ")";
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Đổi trạng thái vào cửa của một vé có điều kiện trạng thái cũ (đổi ghế: NOT_CHECKED_IN → SWAPPED).</summary>
    public static int SetCheckInStatus(int ticketId, string from, string to, SqlConnection conn, SqlTransaction tx) =>
        DBUtil.ExecuteNonQuery("UPDATE Tickets SET CheckInStatus = @3 WHERE Id = @1 AND CheckInStatus = @2", new List<object> { ticketId, from, to }, conn, tx);

    /// <summary>Số vé còn hiệu lực (đã bán) của suất.</summary>
    public static int CountActiveForShowtime(int showtimeId, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        Convert.ToInt32(DBUtil.ExecuteScalar("SELECT COUNT(*) FROM Tickets t JOIN Bills b ON b.Id = t.BillId WHERE t.ShowtimeId = @1 AND b.Status <> @2 AND t.CheckInStatus IN (@3, @4)",
            new List<object> { showtimeId, BillStatus.Cancel, TicketCheckInStatus.NotCheckedIn, TicketCheckInStatus.CheckedIn }, conn, tx));
}

public static partial class ShowtimeSeatDAL
{
    /// <summary>Ghế nội bộ (HOUSE_HOLD) của suất chuyển AVAILABLE; seatIds rỗng là mọi ghế nội bộ. Trả các ghế đã đổi.</summary>
    public static List<int> ReleaseHouse(int showtimeId, IReadOnlyCollection<int>? seatIds, SqlConnection conn, SqlTransaction tx)
    {
        var ps = new List<object> { showtimeId, ShowtimeSeatStatus.Available, ShowtimeSeatStatus.HouseHold };
        var sql = "UPDATE ShowtimeSeats SET Status = @2 OUTPUT inserted.SeatId WHERE ShowtimeId = @1 AND Status = @3" +
                  (seatIds is { Count: > 0 } ? " AND SeatId IN (" + DBUtil.InParams(seatIds.Cast<object>(), ps) + ")" : string.Empty);
        return DBUtil.QueryDataTable(sql, ps, conn, tx).Rows.Cast<DataRow>().Select(r => r.GetInt("SeatId")).ToList();
    }

    /// <summary>Suất còn ghế nội bộ, bắt đầu trong [from, to), kèm danh sách nhãn ghế.</summary>
    public static List<HouseSeatRow> HouseSeats(int? cinemaId, DateTime from, DateTime to, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT t.Id, m.Title, a.Name AS AuditoriumName, t.StartTime, t.EndTime, t.OperationalStatus, COUNT(*) AS N, STRING_AGG(s.Label, ', ') WITHIN GROUP (ORDER BY s.RowIndex, s.ColIndex) AS Labels" +
                           " FROM ShowtimeSeats ss JOIN Seats s ON s.Id = ss.SeatId JOIN Showtimes t ON t.Id = ss.ShowtimeId JOIN Auditoriums a ON a.Id = t.AuditoriumId JOIN Movies m ON m.Id = t.MovieId" +
                           " WHERE ss.Status = @1 AND (@2 IS NULL OR a.CinemaId = @2) AND t.StartTime >= @3 AND t.StartTime < @4 AND t.OperationalStatus <> @5" +
                           " GROUP BY t.Id, m.Title, a.Name, t.StartTime, t.EndTime, t.OperationalStatus ORDER BY t.StartTime";
        var ps = new List<object> { ShowtimeSeatStatus.HouseHold, DBUtil.Value(cinemaId), from, to, OperationalStatus.Cancelled };
        return DBUtil.QueryDataTable(sql, ps, conn, tx).Rows.Cast<DataRow>().Select(r => new HouseSeatRow(r.GetInt("Id"), r.GetString("Title"), r.GetString("AuditoriumName"),
            r.GetDateTime("StartTime"), r.GetDateTime("EndTime"), r.GetString("OperationalStatus"), r.GetInt("N"), r.GetString("Labels"))).ToList();
    }

    /// <summary>Mọi ghế đang giữ hoặc đã bán của suất chuyển AVAILABLE (hủy suất). Trả các ghế đã đổi.</summary>
    public static List<int> ReleaseAll(int showtimeId, SqlConnection conn, SqlTransaction tx)
    {
        const string sql = "UPDATE ShowtimeSeats SET Status = @2, HoldBillId = NULL, HoldExpiresAt = NULL, HoldAudienceType = NULL, TicketId = NULL OUTPUT inserted.SeatId" +
                           " WHERE ShowtimeId = @1 AND Status IN (@3, @4)";
        return DBUtil.QueryDataTable(sql, new List<object> { showtimeId, ShowtimeSeatStatus.Available, ShowtimeSeatStatus.Held, ShowtimeSeatStatus.Booked }, conn, tx)
            .Rows.Cast<DataRow>().Select(r => r.GetInt("SeatId")).ToList();
    }

    /// <summary>Đổi ghế: ghế cũ của vé về AVAILABLE (bỏ TicketId). Trả số dòng đã đổi.</summary>
    public static int Unbook(int showtimeId, int seatId, int ticketId, SqlConnection conn, SqlTransaction tx) =>
        DBUtil.ExecuteNonQuery("UPDATE ShowtimeSeats SET Status = @4, TicketId = NULL WHERE ShowtimeId = @1 AND SeatId = @2 AND TicketId = @3",
            new List<object> { showtimeId, seatId, ticketId, ShowtimeSeatStatus.Available }, conn, tx);

    /// <summary>Thuê trọn suất: mọi ghế AVAILABLE hoặc HOUSE_HOLD không bảo trì của suất (để tạo vé). Không khóa – gọi sau LockForShowtime.</summary>
    public static List<SeatStateRow> SellableForPrivate(int showtimeId, SqlConnection conn, SqlTransaction tx) =>
        GetStates(showtimeId, conn, tx).Where(s => !s.IsMaintenance && s.Status is ShowtimeSeatStatus.Available or ShowtimeSeatStatus.HouseHold).ToList();

    /// <summary>Gắn một ghế AVAILABLE hoặc HOUSE_HOLD vào vé (BOOKED) khi thuê trọn suất.</summary>
    public static int BookDirect(int showtimeId, int seatId, int ticketId, SqlConnection conn, SqlTransaction tx) =>
        DBUtil.ExecuteNonQuery("UPDATE ShowtimeSeats SET Status = @4, TicketId = @3, HoldBillId = NULL, HoldExpiresAt = NULL WHERE ShowtimeId = @1 AND SeatId = @2 AND Status IN (@5, @6)",
            new List<object> { showtimeId, seatId, ticketId, ShowtimeSeatStatus.Booked, ShowtimeSeatStatus.Available, ShowtimeSeatStatus.HouseHold }, conn, tx);
}

public static partial class BillDAL
{
    /// <summary>Hóa đơn theo trạng thái của một suất (hủy suất).</summary>
    public static List<Bill> ByShowtime(int showtimeId, string status, SqlConnection conn, SqlTransaction tx) =>
        FromDataTable(DBUtil.QueryDataTable(SelectCols + " FROM Bills WITH (UPDLOCK) WHERE ShowtimeId = @1 AND Status = @2", new List<object> { showtimeId, status }, conn, tx));

    /// <summary>Đơn trên màn hình bếp của rạp: bắp nước PREPARING hoặc READY_FOR_PICKUP; thời điểm vào bếp là lúc trừ kho (CONSUME), không có thì lúc thanh toán.</summary>
    public static List<KdsOrderRow> KdsOrders(int cinemaId, int? billId = null, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT b.Id, b.Code, b.Channel, b.PickupNumber, b.FnbStatus," +
                           " COALESCE((SELECT MAX(it.CreatedAt) FROM InventoryTransactions it WHERE it.BillId = b.Id AND it.Type = @4), b.PaidAt, b.CreatedAt) AS QueuedAt" +
                           " FROM Bills b WHERE b.CinemaId = @1 AND b.Status = @5 AND b.FnbStatus IN (@2, @3) AND (@6 IS NULL OR b.Id = @6) ORDER BY QueuedAt";
        var ps = new List<object> { cinemaId, FnbStatus.Preparing, FnbStatus.ReadyForPickup, InventoryTxnType.Consume, BillStatus.Finish, DBUtil.Value(billId) };
        return DBUtil.QueryDataTable(sql, ps, conn, tx).Rows.Cast<DataRow>().Select(r => new KdsOrderRow(r.GetInt("Id"), r.GetString("Code"), r.GetString("Channel"),
            r.GetIntN("PickupNumber"), r.GetString("FnbStatus"), r.GetDateTime("QueuedAt"))).ToList();
    }

    /// <summary>Đổi FnbStatus có điều kiện trạng thái cũ. Trả số dòng đã đổi.</summary>
    public static int SetFnbStatus(int billId, string from, string to, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        DBUtil.ExecuteNonQuery("UPDATE Bills SET FnbStatus = @3 WHERE Id = @1 AND FnbStatus = @2", new List<object> { billId, from, to }, conn, tx);

    /// <summary>Đặt CheckInStatus đã tính lại của hóa đơn.</summary>
    public static int SetCheckInStatus(int billId, string status, SqlConnection conn, SqlTransaction tx) =>
        DBUtil.ExecuteNonQuery("UPDATE Bills SET CheckInStatus = @2 WHERE Id = @1", new List<object> { billId, status }, conn, tx);
}

public static partial class BillDetailDAL
{
    /// <summary>Món của các đơn bếp.</summary>
    public static List<KdsLineRow> KdsLines(IReadOnlyCollection<int> billIds, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        if (billIds.Count == 0) return new List<KdsLineRow>();
        var ps = new List<object>();
        var sql = "SELECT bd.BillId, d.Name, bd.Quantity FROM BillDetails bd JOIN Drinks d ON d.Id = bd.DrinkId WHERE bd.BillId IN (" +
                  DBUtil.InParams(billIds.Cast<object>(), ps) + ") ORDER BY bd.BillId, bd.Id";
        return DBUtil.QueryDataTable(sql, ps, conn, tx).Rows.Cast<DataRow>().Select(r => new KdsLineRow(r.GetInt("BillId"), r.GetString("Name"), r.GetInt("Quantity"))).ToList();
    }
}

public static partial class RawMaterialDAL
{
    /// <summary>Nguyên liệu của rạp (hoặc mọi rạp), lọc theo tên và trạng thái, kèm tên rạp.</summary>
    public static List<MaterialRow> Search(int? cinemaId, string? q, bool? active, bool lowOnly, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT rm.*, c.Name AS CinemaName FROM RawMaterials rm JOIN Cinemas c ON c.Id = rm.CinemaId WHERE (@1 IS NULL OR rm.CinemaId = @1)" +
                           " AND (@2 IS NULL OR rm.Name LIKE @2) AND (@3 IS NULL OR rm.Active = @3) AND (@4 = 0 OR rm.ActualStock - rm.ReservedStock < rm.MinThreshold) ORDER BY c.Name, rm.Name";
        var ps = new List<object> { DBUtil.Value(cinemaId), DBUtil.Value(string.IsNullOrWhiteSpace(q) ? null : "%" + q.Trim() + "%"), DBUtil.Value(active), lowOnly ? 1 : 0 };
        return DBUtil.QueryDataTable(sql, ps, conn, tx).Rows.Cast<DataRow>().Select(r => new MaterialRow(FromDataRow(r), r.GetString("CinemaName"))).ToList();
    }

    /// <summary>Nguyên liệu của rạp kèm tồn (dùng cho cảnh báo kho).</summary>
    public static List<RawMaterial> ByCinema(int cinemaId, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        FromDataTable(DBUtil.QueryDataTable("SELECT * FROM RawMaterials WHERE CinemaId = @1 AND Active = 1", new List<object> { cinemaId }, conn, tx));

    /// <summary>Khóa một nguyên liệu (UPDLOCK) trước khi đổi tồn.</summary>
    public static RawMaterial? GetForUpdate(int id, SqlConnection conn, SqlTransaction tx)
    {
        var t = DBUtil.QueryDataTable("SELECT * FROM RawMaterials WITH (UPDLOCK, ROWLOCK) WHERE Id = @1", new List<object> { id }, conn, tx);
        return t.Rows.Count == 0 ? null : FromDataRow(t.Rows[0]);
    }

    /// <summary>Cộng (hoặc trừ khi delta âm) ActualStock; không cho tồn thực tế thấp hơn phần đã giữ. Trả số dòng đã đổi.</summary>
    public static int ChangeActual(int id, decimal delta, SqlConnection conn, SqlTransaction tx) =>
        DBUtil.ExecuteNonQuery("UPDATE RawMaterials SET ActualStock = ActualStock + @2 WHERE Id = @1 AND ActualStock + @2 >= ReservedStock",
            new List<object> { id, delta }, conn, tx);

    /// <summary>Tên đã có ở rạp (trừ chính nó).</summary>
    public static bool NameExists(int cinemaId, string name, int? exceptId, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        Convert.ToInt32(DBUtil.ExecuteScalar("SELECT COUNT(*) FROM RawMaterials WHERE CinemaId = @1 AND Name = @2 AND (@3 IS NULL OR Id <> @3)",
            new List<object> { cinemaId, name, DBUtil.Value(exceptId) }, conn, tx)) > 0;

    /// <summary>Ngưng hoặc dùng lại nguyên liệu.</summary>
    public static int SetActive(int id, bool active) =>
        DBUtil.ExecuteNonQuery("UPDATE RawMaterials SET Active = @2 WHERE Id = @1", new List<object> { id, active });
}

public static partial class InventoryTransactionDAL
{
    /// <summary>Lịch sử giao dịch kho của một nguyên liệu (mới nhất trước).</summary>
    public static List<InventoryTransaction> ByMaterial(int rawMaterialId, int limit, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        FromDataTable(DBUtil.QueryDataTable("SELECT TOP (@2) * FROM InventoryTransactions WHERE RawMaterialId = @1 ORDER BY CreatedAt DESC, Id DESC",
            new List<object> { rawMaterialId, limit }, conn, tx));
}

public static partial class DrinkDAL
{
    /// <summary>Đặt cờ IsSoldOut cho các món. Trả số dòng đã đổi.</summary>
    public static int SetSoldOut(IReadOnlyCollection<int> drinkIds, bool soldOut, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        if (drinkIds.Count == 0) return 0;
        var ps = new List<object> { soldOut };
        var sql = "UPDATE Drinks SET IsSoldOut = @1 WHERE Id IN (" + DBUtil.InParams(drinkIds.Cast<object>(), ps) + ")";
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Các món có định mức dùng ít nhất một nguyên liệu của rạp.</summary>
    public static List<Drink> WithRecipeAtCinema(int cinemaId, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        FromDataTable(DBUtil.QueryDataTable("SELECT d.* FROM Drinks d WHERE d.Active = 1 AND EXISTS (SELECT 1 FROM ProductRecipes pr JOIN RawMaterials rm ON rm.Id = pr.RawMaterialId WHERE pr.DrinkId = d.Id AND rm.CinemaId = @1)",
            new List<object> { cinemaId }, conn, tx));
}

public static partial class WastageLogDAL
{
    private const string RowSql = "SELECT w.*, c.Name AS CinemaName, cu.FullName AS CreatorName, du.FullName AS DeciderName," +
                                  " (SELECT COUNT(*) FROM WastageLogItems i WHERE i.WastageLogId = w.Id) AS ItemCount" +
                                  " FROM WastageLogs w JOIN Cinemas c ON c.Id = w.CinemaId JOIN Users cu ON cu.Id = w.CreatedBy LEFT JOIN Users du ON du.Id = w.DecidedBy";

    private static WastageRow RowOf(DataRow r) => new(FromDataRow(r), r.GetString("CinemaName"), r.GetString("CreatorName"), r.GetStringN("DeciderName"), r.GetInt("ItemCount"));

    /// <summary>Phiếu hủy hàng của rạp theo trạng thái (null là mọi trạng thái), mới nhất trước.</summary>
    public static List<WastageRow> Search(int? cinemaId, string? status, int limit, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        DBUtil.QueryDataTable(RowSql + " WHERE (@1 IS NULL OR w.CinemaId = @1) AND (@2 IS NULL OR w.Status = @2) ORDER BY w.CreatedAt DESC OFFSET 0 ROWS FETCH NEXT @3 ROWS ONLY",
            new List<object> { DBUtil.Value(cinemaId), DBUtil.Value(status), limit }, conn, tx).Rows.Cast<DataRow>().Select(RowOf).ToList();

    public static WastageRow? GetRow(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        var t = DBUtil.QueryDataTable(RowSql + " WHERE w.Id = @1", new List<object> { id }, conn, tx);
        return t.Rows.Count == 0 ? null : RowOf(t.Rows[0]);
    }

    /// <summary>Khóa phiếu (UPDLOCK) trước khi duyệt hoặc từ chối.</summary>
    public static WastageLog? GetForUpdate(int id, SqlConnection conn, SqlTransaction tx)
    {
        var t = DBUtil.QueryDataTable("SELECT * FROM WastageLogs WITH (UPDLOCK, ROWLOCK) WHERE Id = @1", new List<object> { id }, conn, tx);
        return t.Rows.Count == 0 ? null : FromDataRow(t.Rows[0]);
    }

    /// <summary>Báo cáo hủy hàng: các dòng của phiếu đã duyệt, quyết định trong [from, toExclusive).</summary>
    public static List<WastageReportRow> Report(int? cinemaId, DateTime from, DateTime toExclusive, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT w.Id, w.DecidedAt, c.Name AS CinemaName, rm.Name AS MaterialName, rm.Unit, i.Quantity, i.Reason, cu.FullName AS CreatorName, du.FullName AS DeciderName" +
                           " FROM WastageLogItems i JOIN WastageLogs w ON w.Id = i.WastageLogId JOIN RawMaterials rm ON rm.Id = i.RawMaterialId JOIN Cinemas c ON c.Id = w.CinemaId" +
                           " JOIN Users cu ON cu.Id = w.CreatedBy LEFT JOIN Users du ON du.Id = w.DecidedBy" +
                           " WHERE w.Status = @1 AND w.DecidedAt >= @2 AND w.DecidedAt < @3 AND (@4 IS NULL OR w.CinemaId = @4) ORDER BY w.DecidedAt DESC, rm.Name";
        return DBUtil.QueryDataTable(sql, new List<object> { WastageStatus.Approved, from, toExclusive, DBUtil.Value(cinemaId) }, conn, tx).Rows.Cast<DataRow>()
            .Select(r => new WastageReportRow(r.GetInt("Id"), r.GetDateTime("DecidedAt"), r.GetString("CinemaName"), r.GetString("MaterialName"), r.GetString("Unit"),
                r.GetDecimal("Quantity"), r.GetStringN("Reason"), r.GetString("CreatorName"), r.GetStringN("DeciderName"))).ToList();
    }
}

public static partial class WastageLogItemDAL
{
    public static List<WastageItemRow> RowsByLog(int logId, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        DBUtil.QueryDataTable("SELECT i.Id, i.RawMaterialId, rm.Name, rm.Unit, i.Quantity, i.Reason FROM WastageLogItems i JOIN RawMaterials rm ON rm.Id = i.RawMaterialId WHERE i.WastageLogId = @1 ORDER BY i.Id",
            new List<object> { logId }, conn, tx).Rows.Cast<DataRow>().Select(r => new WastageItemRow(r.GetInt("Id"), r.GetInt("RawMaterialId"), r.GetString("Name"),
            r.GetString("Unit"), r.GetDecimal("Quantity"), r.GetStringN("Reason"))).ToList();
}

public static partial class CorporateBookingDAL
{
    /// <summary>Hợp đồng doanh nghiệp, mới nhất trước, kèm suất, hóa đơn, người lập và số voucher đã dùng.</summary>
    public static List<CorporateRow> Rows(int? cinemaId, int limit, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT TOP (@2) cb.*, m.Title, t.StartTime, a.Name AS AuditoriumName, b.Code AS BillCode, u.FullName AS CreatorName," +
                           " (SELECT COUNT(*) FROM Vouchers v WHERE v.CorporateBookingId = cb.Id AND v.UsedCount > 0) AS VouchersUsed" +
                           " FROM CorporateBookings cb JOIN Users u ON u.Id = cb.CreatedBy LEFT JOIN Showtimes t ON t.Id = cb.ShowtimeId LEFT JOIN Movies m ON m.Id = t.MovieId" +
                           " LEFT JOIN Auditoriums a ON a.Id = t.AuditoriumId LEFT JOIN Bills b ON b.Id = cb.BillId" +
                           " WHERE (@1 IS NULL OR a.CinemaId = @1 OR cb.ShowtimeId IS NULL) ORDER BY cb.CreatedAt DESC";
        return DBUtil.QueryDataTable(sql, new List<object> { DBUtil.Value(cinemaId), limit }, conn, tx).Rows.Cast<DataRow>().Select(r => new CorporateRow(FromDataRow(r),
            r.GetStringN("Title"), r.GetDateTimeN("StartTime"), r.GetStringN("AuditoriumName"), r.GetStringN("BillCode"), r.GetString("CreatorName"), r.GetInt("VouchersUsed"))).ToList();
    }

    /// <summary>Số hợp đồng gắn với suất (suất đã thuê trọn).</summary>
    public static int CountForShowtime(int showtimeId, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        Convert.ToInt32(DBUtil.ExecuteScalar("SELECT COUNT(*) FROM CorporateBookings WHERE ShowtimeId = @1", new List<object> { showtimeId }, conn, tx));
}

public static partial class VoucherDAL
{
    /// <summary>Voucher của một hợp đồng doanh nghiệp.</summary>
    public static List<Voucher> ByCorporate(int corporateBookingId, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        FromDataTable(DBUtil.QueryDataTable("SELECT * FROM Vouchers WHERE CorporateBookingId = @1 ORDER BY Id", new List<object> { corporateBookingId }, conn, tx));
}

public static partial class SupervisorApprovalDAL
{
    private const string DetailSql = "SELECT a.Id, a.BillId, b.Code, b.CinemaId, b.Total, a.Reason, a.RequestedBy, u.FullName, a.Status, a.Method, a.CreatedAt, a.ExpiresAt" +
                                     " FROM SupervisorApprovals a JOIN Bills b ON b.Id = a.BillId JOIN Users u ON u.Id = a.RequestedBy";

    private static ApprovalDetailRow DetailOf(DataRow r) => new(r.GetInt("Id"), r.GetInt("BillId"), r.GetString("Code"), r.GetInt("CinemaId"), r.GetLong("Total"),
        r.GetString("Reason"), r.GetInt("RequestedBy"), r.GetString("FullName"), r.GetString("Status"), r.GetStringN("Method"), r.GetDateTime("CreatedAt"), r.GetDateTime("ExpiresAt"));

    public static ApprovalDetailRow? GetDetail(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        var t = DBUtil.QueryDataTable(DetailSql + " WHERE a.Id = @1", new List<object> { id }, conn, tx);
        return t.Rows.Count == 0 ? null : DetailOf(t.Rows[0]);
    }

    /// <summary>Yêu cầu đang chờ duyệt (còn hạn) của rạp (null là mọi rạp), cũ nhất trước.</summary>
    public static List<ApprovalDetailRow> PendingDetails(int? cinemaId, DateTime now, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        DBUtil.QueryDataTable(DetailSql + " WHERE a.Status = @1 AND a.ExpiresAt > @2 AND (@3 IS NULL OR b.CinemaId = @3) ORDER BY a.CreatedAt",
            new List<object> { ApprovalStatus.Pending, now, DBUtil.Value(cinemaId) }, conn, tx).Rows.Cast<DataRow>().Select(DetailOf).ToList();

    /// <summary>Yêu cầu PENDING còn hạn của một hóa đơn (chống gửi trùng).</summary>
    public static int CountPendingForBill(int billId, DateTime now, SqlConnection conn, SqlTransaction tx) =>
        Convert.ToInt32(DBUtil.ExecuteScalar("SELECT COUNT(*) FROM SupervisorApprovals WHERE BillId = @1 AND Status = @2 AND ExpiresAt > @3",
            new List<object> { billId, ApprovalStatus.Pending, now }, conn, tx));

    /// <summary>Khóa yêu cầu (UPDLOCK) trước khi quyết định.</summary>
    public static SupervisorApproval? GetForUpdate(int id, SqlConnection conn, SqlTransaction tx)
    {
        var t = DBUtil.QueryDataTable("SELECT * FROM SupervisorApprovals WITH (UPDLOCK, ROWLOCK) WHERE Id = @1", new List<object> { id }, conn, tx);
        return t.Rows.Count == 0 ? null : FromDataRow(t.Rows[0]);
    }

    /// <summary>Quyết định yêu cầu đang PENDING. Trả số dòng đã đổi (0 nếu đã được xử lý).</summary>
    public static int Decide(int id, string status, int? decidedBy, DateTime at, SqlConnection conn, SqlTransaction tx) =>
        DBUtil.ExecuteNonQuery("UPDATE SupervisorApprovals SET Status = @2, DecidedBy = @3, DecidedAt = @4 WHERE Id = @1 AND Status = @5",
            new List<object> { id, status, DBUtil.Value(decidedBy), at, ApprovalStatus.Pending }, conn, tx);

    /// <summary>Các yêu cầu PENDING đã quá hạn chuyển EXPIRED; trả danh sách đã đổi (để báo về máy POS).</summary>
    public static List<ApprovalDetailRow> ExpireOverdue(DateTime now, SqlConnection conn, SqlTransaction tx)
    {
        var rows = DBUtil.QueryDataTable(DetailSql + " WHERE a.Status = @1 AND a.ExpiresAt <= @2", new List<object> { ApprovalStatus.Pending, now }, conn, tx)
            .Rows.Cast<DataRow>().Select(DetailOf).ToList();
        foreach (var r in rows) Decide(r.Id, ApprovalStatus.Expired, null, now, conn, tx);
        return rows;
    }
}
