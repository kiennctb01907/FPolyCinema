using System.Data;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Utils;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Truy vấn báo cáo của Phase 4 (dashboard UI A1, báo cáo UI A6). Quy tắc chung SPEC 3.12: chỉ tính hóa đơn finish; vé SWAPPED và VOIDED
/// không tính; doanh thu dùng FinalPrice và LineFinalAmount; ngày theo Bills.PaidAt. Ngày vận hành của suất: bắt đầu trước 06:00 thuộc ngày hôm trước.
/// </summary>
public static partial class StatisticDAL
{
    /// <summary>Doanh thu vé, bắp nước, số vé, số hóa đơn trong [from, to) (theo PaidAt); rạp null là toàn chuỗi.</summary>
    public static DashboardKpi Kpi(DateTime from, DateTime toExclusive, int? cinemaId)
    {
        const string sql =
            "SELECT" +
            " (SELECT COALESCE(SUM(t.FinalPrice), 0) FROM Tickets t JOIN Bills b ON b.Id = t.BillId WHERE b.Status = @4 AND t.CheckInStatus NOT IN (@5, @6) AND b.PaidAt >= @1 AND b.PaidAt < @2 AND (@3 IS NULL OR b.CinemaId = @3)) AS TicketRevenue," +
            " (SELECT COALESCE(SUM(bd.LineFinalAmount), 0) FROM BillDetails bd JOIN Bills b ON b.Id = bd.BillId WHERE b.Status = @4 AND b.PaidAt >= @1 AND b.PaidAt < @2 AND (@3 IS NULL OR b.CinemaId = @3)) AS FnbRevenue," +
            " (SELECT COUNT(*) FROM Tickets t JOIN Bills b ON b.Id = t.BillId WHERE b.Status = @4 AND t.CheckInStatus NOT IN (@5, @6) AND b.PaidAt >= @1 AND b.PaidAt < @2 AND (@3 IS NULL OR b.CinemaId = @3)) AS Tickets," +
            " (SELECT COUNT(*) FROM Bills b WHERE b.Status = @4 AND b.PaidAt >= @1 AND b.PaidAt < @2 AND (@3 IS NULL OR b.CinemaId = @3)) AS Bills";
        var r = DBUtil.QueryDataTable(sql, new List<object> { from, toExclusive, DBUtil.Value(cinemaId), BillStatus.Finish, TicketCheckInStatus.Swapped, TicketCheckInStatus.Voided }).Rows[0];
        var occ = OccupancyTotals(from, toExclusive, cinemaId);
        return new DashboardKpi(r.GetLong("TicketRevenue") + r.GetLong("FnbRevenue"), r.GetLong("FnbRevenue"), r.GetInt("Tickets"), r.GetInt("Bills"), occ.Sold, occ.Capacity);
    }

    /// <summary>Tổng ghế đã bán và sức chứa của các suất không bị hủy bắt đầu trong [from, to).</summary>
    public static (int Sold, int Capacity) OccupancyTotals(DateTime from, DateTime toExclusive, int? cinemaId)
    {
        const string sql = "SELECT COALESCE(SUM(CASE WHEN ss.Status = @4 THEN 1 ELSE 0 END), 0) AS Sold, COUNT(ss.SeatId) AS Capacity" +
                           " FROM Showtimes s JOIN Auditoriums a ON a.Id = s.AuditoriumId JOIN ShowtimeSeats ss ON ss.ShowtimeId = s.Id" +
                           " WHERE s.OperationalStatus <> @5 AND s.StartTime >= @1 AND s.StartTime < @2 AND (@3 IS NULL OR a.CinemaId = @3)";
        var r = DBUtil.QueryDataTable(sql, new List<object> { from, toExclusive, DBUtil.Value(cinemaId), ShowtimeSeatStatus.Booked, OperationalStatus.Cancelled }).Rows[0];
        return (r.GetInt("Sold"), r.GetInt("Capacity"));
    }

    /// <summary>
    /// Tỷ lệ phim Việt theo rạp (SPEC 3.1 mục 6): số suất không bị hủy có ngày vận hành trong [fromDate, toDate], số suất phim Việt,
    /// và cùng số liệu trong khung VietnameseFilmPriorityStart–End (giờ bắt đầu) để tham khảo.
    /// </summary>
    public static List<VietnameseShareRow> VietnameseShares(DateTime fromDate, DateTime toDate, TimeSpan primeStart, TimeSpan primeEnd)
    {
        const string sql = "SELECT c.Id, c.Name," +
                           " SUM(CASE WHEN m.IsVietnameseFilm = 1 THEN 1 ELSE 0 END) AS Vn, COUNT(*) AS Total," +
                           " SUM(CASE WHEN m.IsVietnameseFilm = 1 AND CAST(s.StartTime AS TIME) >= @4 AND CAST(s.StartTime AS TIME) < @5 THEN 1 ELSE 0 END) AS PrimeVn," +
                           " SUM(CASE WHEN CAST(s.StartTime AS TIME) >= @4 AND CAST(s.StartTime AS TIME) < @5 THEN 1 ELSE 0 END) AS PrimeTotal" +
                           " FROM Showtimes s JOIN Auditoriums a ON a.Id = s.AuditoriumId JOIN Cinemas c ON c.Id = a.CinemaId JOIN Movies m ON m.Id = s.MovieId" +
                           " WHERE s.OperationalStatus <> @3 AND CAST(DATEADD(HOUR, -6, s.StartTime) AS DATE) BETWEEN @1 AND @2 GROUP BY c.Id, c.Name ORDER BY c.Name";
        var ps = new List<object> { fromDate.Date, toDate.Date, OperationalStatus.Cancelled, primeStart, primeEnd };
        return DBUtil.QueryDataTable(sql, ps).Rows.Cast<DataRow>().Select(r => new VietnameseShareRow(r.GetInt("Id"), r.GetString("Name"),
            r.GetInt("Vn"), r.GetInt("Total"), r.GetInt("PrimeVn"), r.GetInt("PrimeTotal"))).ToList();
    }

    /// <summary>Tỷ lệ lấp đầy từng suất không bị hủy bắt đầu trong [from, to).</summary>
    public static List<OccupancyRow> OccupancyByShowtime(DateTime from, DateTime toExclusive, int? cinemaId)
    {
        const string sql = "SELECT s.Id, c.Name AS CinemaName, a.Name AS AuditoriumName, m.Title, s.StartTime," +
                           " SUM(CASE WHEN ss.Status = @4 THEN 1 ELSE 0 END) AS Sold, COUNT(ss.SeatId) AS Capacity" +
                           " FROM Showtimes s JOIN Auditoriums a ON a.Id = s.AuditoriumId JOIN Cinemas c ON c.Id = a.CinemaId JOIN Movies m ON m.Id = s.MovieId" +
                           " JOIN ShowtimeSeats ss ON ss.ShowtimeId = s.Id" +
                           " WHERE s.OperationalStatus <> @5 AND s.StartTime >= @1 AND s.StartTime < @2 AND (@3 IS NULL OR a.CinemaId = @3)" +
                           " GROUP BY s.Id, c.Name, a.Name, m.Title, s.StartTime ORDER BY s.StartTime, c.Name, a.Name";
        var ps = new List<object> { from, toExclusive, DBUtil.Value(cinemaId), ShowtimeSeatStatus.Booked, OperationalStatus.Cancelled };
        return DBUtil.QueryDataTable(sql, ps).Rows.Cast<DataRow>().Select(r => new OccupancyRow(r.GetInt("Id"), r.GetString("CinemaName"),
            r.GetString("AuditoriumName"), r.GetString("Title"), r.GetDateTime("StartTime"), r.GetInt("Sold"), r.GetInt("Capacity"))).ToList();
    }

    /// <summary>Vé tính đối soát của một phim trong tuần n, gộp theo (FinalPrice, VatRate) – cùng điều kiện với sp_box_office_settlement.</summary>
    public static List<(long FinalPrice, decimal VatRate, int Count)> SettlementLines(int movieId, DateTime weekStart, DateTime weekEndExclusive, int? cinemaId)
    {
        const string sql = "SELECT t.FinalPrice, t.VatRate, COUNT(*) AS N FROM Tickets t JOIN Bills b ON b.Id = t.BillId JOIN Showtimes s ON s.Id = t.ShowtimeId JOIN Auditoriums a ON a.Id = s.AuditoriumId" +
                           " WHERE s.MovieId = @1 AND b.Status = @5 AND t.CheckInStatus NOT IN (@6, @7) AND (@4 IS NULL OR a.CinemaId = @4)" +
                           " AND CAST(DATEADD(HOUR, -6, s.StartTime) AS DATE) >= @2 AND CAST(DATEADD(HOUR, -6, s.StartTime) AS DATE) < @3" +
                           " GROUP BY t.FinalPrice, t.VatRate ORDER BY t.FinalPrice DESC";
        var ps = new List<object> { movieId, weekStart.Date, weekEndExclusive.Date, DBUtil.Value(cinemaId), BillStatus.Finish, TicketCheckInStatus.Swapped, TicketCheckInStatus.Voided };
        return DBUtil.QueryDataTable(sql, ps).Rows.Cast<DataRow>().Select(r => (r.GetLong("FinalPrice"), r.GetDecimal("VatRate"), r.GetInt("N"))).ToList();
    }

    /// <summary>KDM hết hạn trong [now, until] của phim còn hoạt động (cảnh báo vận hành).</summary>
    public static List<OpsAlertRow> KdmExpiring(DateTime now, DateTime until, int? cinemaId)
    {
        const string sql = "SELECT k.MovieId, m.Title, c.Name, k.ValidUntil FROM KdmLicenses k JOIN Movies m ON m.Id = k.MovieId JOIN Cinemas c ON c.Id = k.CinemaId" +
                           " WHERE m.Active = 1 AND k.ValidUntil >= @1 AND k.ValidUntil <= @2 AND (@3 IS NULL OR k.CinemaId = @3)" +
                           " AND NOT EXISTS (SELECT 1 FROM KdmLicenses k2 WHERE k2.MovieId = k.MovieId AND k2.CinemaId = k.CinemaId AND k2.ValidUntil > k.ValidUntil) ORDER BY k.ValidUntil";
        return DBUtil.QueryDataTable(sql, new List<object> { now, until, DBUtil.Value(cinemaId) }).Rows.Cast<DataRow>().Select(r => new OpsAlertRow("kdm",
            $"KDM của {r.GetString("Title")} sắp hết hạn", $"{r.GetString("Name")} · hết hạn {r.GetDateTime("ValidUntil"):dd/MM/yyyy HH:mm}", $"/admin/movies/{r.GetInt("MovieId")}")).ToList();
    }

    /// <summary>Nguyên liệu có tồn thực tế dưới ngưỡng MinThreshold.</summary>
    public static List<OpsAlertRow> LowStock(int? cinemaId)
    {
        const string sql = "SELECT rm.Name, rm.Unit, rm.ActualStock, rm.MinThreshold, c.Name AS CinemaName FROM RawMaterials rm JOIN Cinemas c ON c.Id = rm.CinemaId" +
                           " WHERE rm.Active = 1 AND rm.ActualStock < rm.MinThreshold AND (@1 IS NULL OR rm.CinemaId = @1) ORDER BY c.Name, rm.Name";
        return DBUtil.QueryDataTable(sql, new List<object> { DBUtil.Value(cinemaId) }).Rows.Cast<DataRow>().Select(r => new OpsAlertRow("stock",
            $"{r.GetString("Name")} dưới ngưỡng tồn", $"{r.GetString("CinemaName")} · còn {r.GetDecimal("ActualStock"):0.###} {r.GetString("Unit")}, ngưỡng {r.GetDecimal("MinThreshold"):0.###}", null)).ToList();
    }

    /// <summary>
    /// Ghế đang bảo trì đã được bán ở suất tương lai (SPEC 3.2-10): đưa vào danh sách "Cần đổi ghế" của dashboard.
    /// </summary>
    public static List<OpsAlertRow> SeatsNeedingChange(DateTime now, int? cinemaId)
    {
        const string sql = "SELECT s.Label, a.Name AS AuditoriumName, c.Name AS CinemaName, st.StartTime, m.Title, b.Code" +
                           " FROM ShowtimeSeats ss JOIN Seats s ON s.Id = ss.SeatId JOIN Showtimes st ON st.Id = ss.ShowtimeId JOIN Auditoriums a ON a.Id = st.AuditoriumId" +
                           " JOIN Cinemas c ON c.Id = a.CinemaId JOIN Movies m ON m.Id = st.MovieId JOIN Tickets t ON t.Id = ss.TicketId JOIN Bills b ON b.Id = t.BillId" +
                           " WHERE s.IsMaintenance = 1 AND ss.Status = @2 AND st.StartTime > @1 AND st.OperationalStatus <> @4 AND (@3 IS NULL OR a.CinemaId = @3) ORDER BY st.StartTime";
        return DBUtil.QueryDataTable(sql, new List<object> { now, ShowtimeSeatStatus.Booked, DBUtil.Value(cinemaId), OperationalStatus.Cancelled }).Rows.Cast<DataRow>().Select(r => new OpsAlertRow("seat",
            $"Cần đổi ghế {r.GetString("Label")} – {r.GetString("AuditoriumName")}",
            $"{r.GetString("CinemaName")} · {r.GetString("Title")} {r.GetDateTime("StartTime"):dd/MM HH:mm} · hóa đơn {r.GetString("Code")} · ghế đang bảo trì", null)).ToList();
    }
}
