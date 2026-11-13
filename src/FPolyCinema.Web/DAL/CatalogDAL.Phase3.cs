using System.Data;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>Các hàm thêm của MovieDAL cho Phase 3: danh sách lọc theo trạng thái và nhãn tuổi, quy tắc xóa (SPEC 3.14).</summary>
public static partial class MovieDAL
{
    public static List<MovieRow> Search(string? query, bool? active, string? ageRating, int offset, int limit, out int total)
    {
        var ps = new List<object> { DBUtil.Value(DalText.LikeContains(query)), DBUtil.Value(active), DBUtil.Value(ageRating) };
        const string countSql = "SELECT COUNT(*) FROM Movies m WHERE (@1 IS NULL OR m.Title LIKE @1 ESCAPE '!' OR m.OriginalTitle LIKE @1 ESCAPE '!') AND (@2 IS NULL OR m.Active = @2) AND (@3 IS NULL OR m.AgeRating = @3)";
        total = Convert.ToInt32(DBUtil.ExecuteScalar(countSql, ps));
        const string sql = "SELECT m.Id, m.Title, m.OriginalTitle, m.Genres, m.DurationMinutes, m.AgeRating, m.ClassificationLicenseNo, m.ContentWarnings, m.IsVietnameseFilm, m.LanguageFormats, m.ReleaseDate, m.DistributorId, m.MinimumTicketPrice, m.IsBlockbuster, m.PosterUrl, m.TrailerUrl, m.Description, m.AiDemandScore, m.Active, m.CreatedAt, d.Name AS DistributorName, (SELECT COUNT(*) FROM Showtimes s WHERE s.MovieId = m.Id) AS ShowtimeCount" +
                           " FROM Movies m LEFT JOIN Distributors d ON d.Id = m.DistributorId" +
                           " WHERE (@1 IS NULL OR m.Title LIKE @1 ESCAPE '!' OR m.OriginalTitle LIKE @1 ESCAPE '!') AND (@2 IS NULL OR m.Active = @2) AND (@3 IS NULL OR m.AgeRating = @3)" +
                           " ORDER BY m.Active DESC, m.ReleaseDate DESC, m.Title, m.Id OFFSET @4 ROWS FETCH NEXT @5 ROWS ONLY";
        var table = DBUtil.QueryDataTable(sql, new List<object>(ps) { offset, limit });
        return table.Rows.Cast<DataRow>().Select(r => new MovieRow(FromDataRow(r), r.GetStringN("DistributorName"), r.GetInt("ShowtimeCount"))).ToList();
    }

    /// <summary>Phim đang chiếu (Active = 1) cho ô chọn phim khi xếp suất, sắp theo tên.</summary>
    public static List<Movie> GetActiveByTitle(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT m.Id, m.Title, m.OriginalTitle, m.Genres, m.DurationMinutes, m.AgeRating, m.ClassificationLicenseNo, m.ContentWarnings, m.IsVietnameseFilm, m.LanguageFormats, m.ReleaseDate, m.DistributorId, m.MinimumTicketPrice, m.IsBlockbuster, m.PosterUrl, m.TrailerUrl, m.Description, m.AiDemandScore, m.Active, m.CreatedAt FROM Movies m WHERE m.Active = 1 ORDER BY m.Title";
        return FromDataTable(DBUtil.QueryDataTable(sql, null, conn, tx));
    }

    public static int CountShowtimes(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM Showtimes WHERE MovieId = @1";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { id }, conn, tx));
    }

    public static int CountTickets(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM Tickets t JOIN Showtimes s ON s.Id = t.ShowtimeId WHERE s.MovieId = @1";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { id }, conn, tx));
    }

    public static int SetActive(int id, bool active, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Movies SET Active = @1 WHERE Id = @2";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { active, id }, conn, tx);
    }
}

/// <summary>Các hàm thêm của DistributorDAL: danh sách, đếm phim (quy tắc xóa), ô chọn.</summary>
public static partial class DistributorDAL
{
    public static List<DistributorRow> Search(string? query, bool? active, int offset, int limit, out int total)
    {
        var ps = new List<object> { DBUtil.Value(DalText.LikeContains(query)), DBUtil.Value(active) };
        const string countSql = "SELECT COUNT(*) FROM Distributors d WHERE (@1 IS NULL OR d.Name LIKE @1 ESCAPE '!') AND (@2 IS NULL OR d.Active = @2)";
        total = Convert.ToInt32(DBUtil.ExecuteScalar(countSql, ps));
        const string sql = "SELECT d.Id, d.Name, d.Email, d.Phone, d.Active, (SELECT COUNT(*) FROM Movies m WHERE m.DistributorId = d.Id) AS MovieCount" +
                           " FROM Distributors d WHERE (@1 IS NULL OR d.Name LIKE @1 ESCAPE '!') AND (@2 IS NULL OR d.Active = @2) ORDER BY d.Name, d.Id OFFSET @3 ROWS FETCH NEXT @4 ROWS ONLY";
        var table = DBUtil.QueryDataTable(sql, new List<object>(ps) { offset, limit });
        return table.Rows.Cast<DataRow>().Select(r => new DistributorRow(FromDataRow(r), r.GetInt("MovieCount"))).ToList();
    }

    public static List<Distributor> GetActiveOrId(int? includeId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, Name, Email, Phone, Active FROM Distributors WHERE Active = 1 OR Id = @1 ORDER BY Name";
        return FromDataTable(DBUtil.QueryDataTable(sql, new List<object> { DBUtil.Value(includeId) }, conn, tx));
    }

    public static int CountMovies(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM Movies WHERE DistributorId = @1";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { id }, conn, tx));
    }

    public static bool NameExists(string name, int? exceptId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM Distributors WHERE Name = @1 AND (@2 IS NULL OR Id <> @2)";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { name, DBUtil.Value(exceptId) }, conn, tx)) > 0;
    }

    public static int SetActive(int id, bool active, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Distributors SET Active = @1 WHERE Id = @2";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { active, id }, conn, tx);
    }
}

/// <summary>Các hàm thêm của FilmRentalRuleDAL: quy tắc chia doanh thu theo tuần của một phim.</summary>
public static partial class FilmRentalRuleDAL
{
    public static List<FilmRentalRule> GetByMovie(int movieId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, MovieId, WeekNumber, DistributorSharePercent FROM FilmRentalRules WHERE MovieId = @1 ORDER BY WeekNumber";
        return FromDataTable(DBUtil.QueryDataTable(sql, new List<object> { movieId }, conn, tx));
    }

    public static int DeleteByMovie(int movieId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM FilmRentalRules WHERE MovieId = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { movieId }, conn, tx);
    }
}

/// <summary>Các hàm thêm của KdmLicenseDAL: KDM của phim theo rạp.</summary>
public static partial class KdmLicenseDAL
{
    public static List<KdmRow> GetByMovie(int movieId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT k.Id, k.MovieId, k.CinemaId, k.ValidFrom, k.ValidUntil, k.Note, c.Name AS CinemaName FROM KdmLicenses k JOIN Cinemas c ON c.Id = k.CinemaId WHERE k.MovieId = @1 ORDER BY c.Name, k.ValidFrom";
        var table = DBUtil.QueryDataTable(sql, new List<object> { movieId }, conn, tx);
        return table.Rows.Cast<DataRow>().Select(r => new KdmRow(FromDataRow(r), r.GetString("CinemaName"))).ToList();
    }

    public static List<KdmLicense> GetByMovieCinema(int movieId, int cinemaId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, MovieId, CinemaId, ValidFrom, ValidUntil, Note FROM KdmLicenses WHERE MovieId = @1 AND CinemaId = @2 ORDER BY ValidFrom";
        return FromDataTable(DBUtil.QueryDataTable(sql, new List<object> { movieId, cinemaId }, conn, tx));
    }

    public static int DeleteByMovie(int movieId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM KdmLicenses WHERE MovieId = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { movieId }, conn, tx);
    }
}

/// <summary>Các hàm thêm của AuditoriumDAL: danh sách theo rạp, quy tắc xóa, khóa cấu trúc khi đã bán vé suất tương lai (SPEC 3.2).</summary>
public static partial class AuditoriumDAL
{
    public static List<AuditoriumRow> GetRows(int? cinemaId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT a.Id, a.CinemaId, a.Name, a.Format, a.TotalRows, a.TotalCols, a.IdealRowRatio, a.Active, c.Name AS CinemaName," +
                           " (SELECT COUNT(*) FROM Seats s WHERE s.AuditoriumId = a.Id) AS SeatCount," +
                           " (SELECT COUNT(*) FROM Showtimes t WHERE t.AuditoriumId = a.Id) AS ShowtimeCount" +
                           " FROM Auditoriums a JOIN Cinemas c ON c.Id = a.CinemaId WHERE (@1 IS NULL OR a.CinemaId = @1) ORDER BY c.Name, a.Name";
        var table = DBUtil.QueryDataTable(sql, new List<object> { DBUtil.Value(cinemaId) }, conn, tx);
        return table.Rows.Cast<DataRow>().Select(r => new AuditoriumRow(FromDataRow(r), r.GetString("CinemaName"), r.GetInt("SeatCount"), r.GetInt("ShowtimeCount"))).ToList();
    }

    public static List<Auditorium> GetActiveByCinema(int cinemaId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, CinemaId, Name, Format, TotalRows, TotalCols, IdealRowRatio, Active FROM Auditoriums WHERE CinemaId = @1 AND Active = 1 ORDER BY Name";
        return FromDataTable(DBUtil.QueryDataTable(sql, new List<object> { cinemaId }, conn, tx));
    }

    public static List<Auditorium> GetByCinema(int cinemaId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, CinemaId, Name, Format, TotalRows, TotalCols, IdealRowRatio, Active FROM Auditoriums WHERE CinemaId = @1 ORDER BY Name";
        return FromDataTable(DBUtil.QueryDataTable(sql, new List<object> { cinemaId }, conn, tx));
    }

    public static int CountShowtimes(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM Showtimes WHERE AuditoriumId = @1";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { id }, conn, tx));
    }

    /// <summary>Số suất tương lai (chưa bắt đầu, không bị hủy) của phòng đã có ghế BOOKED hoặc HELD còn hạn.</summary>
    public static int CountFutureShowtimesWithSales(int id, DateTime now, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM Showtimes t WHERE t.AuditoriumId = @1 AND t.StartTime > @2 AND t.OperationalStatus <> @3" +
                           " AND EXISTS (SELECT 1 FROM ShowtimeSeats ss WHERE ss.ShowtimeId = t.Id AND (ss.Status = @4 OR (ss.Status = @5 AND ss.HoldExpiresAt > @2)))";
        var ps = new List<object> { id, now, OperationalStatus.Cancelled, ShowtimeSeatStatus.Booked, ShowtimeSeatStatus.Held };
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps, conn, tx));
    }

    public static bool NameExists(int cinemaId, string name, int? exceptId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM Auditoriums WHERE CinemaId = @1 AND Name = @2 AND (@3 IS NULL OR Id <> @3)";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { cinemaId, name, DBUtil.Value(exceptId) }, conn, tx)) > 0;
    }

    public static int SetActive(int id, bool active, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Auditoriums SET Active = @1 WHERE Id = @2";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { active, id }, conn, tx);
    }
}

/// <summary>Các hàm thêm của SeatDAL cho công cụ thiết kế phòng.</summary>
public static partial class SeatDAL
{
    public static List<Seat> GetByAuditorium(int auditoriumId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, AuditoriumId, RowIndex, ColIndex, Label, SeatType, PairGroupId, IsHouseSeat, IsMaintenance, BaseViewScore FROM Seats WHERE AuditoriumId = @1 ORDER BY RowIndex, ColIndex";
        return FromDataTable(DBUtil.QueryDataTable(sql, new List<object> { auditoriumId }, conn, tx));
    }

    /// <summary>Id các ghế của phòng đã có vé (kể cả vé cũ): không xóa được khỏi sơ đồ.</summary>
    public static HashSet<int> IdsWithTickets(int auditoriumId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT DISTINCT t.SeatId FROM Tickets t JOIN Seats s ON s.Id = t.SeatId WHERE s.AuditoriumId = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { auditoriumId }, conn, tx);
        return table.Rows.Cast<DataRow>().Select(r => r.GetInt("SeatId")).ToHashSet();
    }

    public static int MaxPairGroupId(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT ISNULL(MAX(PairGroupId), 0) FROM Seats";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, null, conn, tx));
    }

    /// <summary>Đổi nhãn tạm (~Id) cho mọi ghế của phòng, để gán lại nhãn mới không vướng UNIQUE(AuditoriumId, Label).</summary>
    public static int SetTemporaryLabels(int auditoriumId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Seats SET Label = CONCAT('~', Id) WHERE AuditoriumId = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { auditoriumId }, conn, tx);
    }

    public static int SetMaintenance(int id, bool isMaintenance, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE Seats SET IsMaintenance = @1 WHERE Id = @2";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { isMaintenance, id }, conn, tx);
    }

    public static int DeleteByAuditorium(int auditoriumId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM Seats WHERE AuditoriumId = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { auditoriumId }, conn, tx);
    }
}

/// <summary>Các hàm thêm của ShowtimeSeatDAL khi sửa sơ đồ phòng.</summary>
public static partial class ShowtimeSeatDAL
{
    /// <summary>Xóa trạng thái của một ghế ở mọi suất (chỉ gọi khi ghế chưa có vé nào).</summary>
    public static int DeleteForSeat(int seatId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM ShowtimeSeats WHERE SeatId = @1 AND TicketId IS NULL";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { seatId }, conn, tx);
    }

    /// <summary>Thêm ghế mới vào mọi suất tương lai không bị hủy của phòng (AVAILABLE hoặc HOUSE_HOLD).</summary>
    public static int AddSeatToFutureShowtimes(int seatId, int auditoriumId, bool isHouseSeat, DateTime now, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "INSERT INTO ShowtimeSeats (ShowtimeId, SeatId, Status) SELECT t.Id, @1, @3 FROM Showtimes t WHERE t.AuditoriumId = @2 AND t.StartTime > @4 AND t.OperationalStatus <> @5";
        var ps = new List<object> { seatId, auditoriumId, isHouseSeat ? ShowtimeSeatStatus.HouseHold : ShowtimeSeatStatus.Available, now, OperationalStatus.Cancelled };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    /// <summary>Đặt lại AVAILABLE hoặc HOUSE_HOLD theo Seats.IsHouseSeat cho các suất tương lai của phòng (ghế chưa giữ, chưa bán).</summary>
    public static int SyncHouseSeatsForFutureShowtimes(int auditoriumId, DateTime now, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "UPDATE ss SET ss.Status = CASE WHEN s.IsHouseSeat = 1 THEN @3 ELSE @4 END FROM ShowtimeSeats ss JOIN Seats s ON s.Id = ss.SeatId JOIN Showtimes t ON t.Id = ss.ShowtimeId" +
                           " WHERE s.AuditoriumId = @1 AND t.StartTime > @2 AND ss.Status IN (@3, @4)";
        var ps = new List<object> { auditoriumId, now, ShowtimeSeatStatus.HouseHold, ShowtimeSeatStatus.Available };
        return DBUtil.ExecuteNonQuery(sql, ps, conn, tx);
    }

    public static int DeleteByShowtime(int showtimeId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "DELETE FROM ShowtimeSeats WHERE ShowtimeId = @1";
        return DBUtil.ExecuteNonQuery(sql, new List<object> { showtimeId }, conn, tx);
    }

    /// <summary>Số ghế BOOKED và số ghế HELD còn hạn của một suất (cờ LOCKED_HAS_TICKETS).</summary>
    public static (int Booked, int ActiveHeld) CountLocking(int showtimeId, DateTime now, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT SUM(CASE WHEN Status = @2 THEN 1 ELSE 0 END) AS Booked, SUM(CASE WHEN Status = @3 AND HoldExpiresAt > @4 THEN 1 ELSE 0 END) AS Held FROM ShowtimeSeats WHERE ShowtimeId = @1";
        var table = DBUtil.QueryDataTable(sql, new List<object> { showtimeId, ShowtimeSeatStatus.Booked, ShowtimeSeatStatus.Held, now }, conn, tx);
        var r = table.Rows[0];
        return (r.GetIntN("Booked") ?? 0, r.GetIntN("Held") ?? 0);
    }
}

/// <summary>Các hàm thêm của ShowtimeDAL cho lịch chiếu (UI A5) và ScheduleConstraintChecker.</summary>
public static partial class ShowtimeDAL
{
    private const string RowSql = "SELECT t.Id, t.MovieId, t.AuditoriumId, t.StartTime, t.FeatureStartTime, t.EndTime, t.CleaningEndTime, t.LanguageFormat, t.OperationalStatus, t.IsAiGenerated, t.IsPrivate, t.CancelNote, t.CreatedAt," +
                                  " a.CinemaId, m.Title AS MovieTitle, m.AgeRating, m.ClassificationLicenseNo, m.IsVietnameseFilm, a.Name AS AuditoriumName, a.Format AS AuditoriumFormat," +
                                  " (SELECT COUNT(*) FROM Seats s WHERE s.AuditoriumId = a.Id AND s.IsHouseSeat = 0 AND s.IsMaintenance = 0) AS Capacity," +
                                  " (SELECT COUNT(*) FROM ShowtimeSeats ss WHERE ss.ShowtimeId = t.Id AND ss.Status = @1) AS BookedSeats," +
                                  " (SELECT COUNT(*) FROM ShowtimeSeats ss WHERE ss.ShowtimeId = t.Id AND ss.Status = @2 AND ss.HoldExpiresAt > @3) AS ActiveHeldSeats" +
                                  " FROM Showtimes t JOIN Auditoriums a ON a.Id = t.AuditoriumId JOIN Movies m ON m.Id = t.MovieId";

    private static ShowtimeRow RowOf(DataRow r) => new(FromDataRow(r), r.GetInt("CinemaId"), r.GetString("MovieTitle"), r.GetString("AgeRating"), r.GetStringN("ClassificationLicenseNo"),
        r.GetBool("IsVietnameseFilm"), r.GetString("AuditoriumName"), r.GetString("AuditoriumFormat"), r.GetInt("Capacity"), r.GetInt("BookedSeats"), r.GetInt("ActiveHeldSeats"));

    /// <summary>Tham số đếm ghế đứng đầu (@1 BOOKED, @2 HELD, @3 bây giờ); tham số riêng của từng câu bắt đầu từ @4.</summary>
    private static List<object> WithCounters(DateTime now, params object[] ps)
    {
        var list = new List<object> { ShowtimeSeatStatus.Booked, ShowtimeSeatStatus.Held, now };
        list.AddRange(ps);
        return list;
    }

    /// <summary>Các suất của rạp có StartTime trong [from, to), kể cả suất đã hủy, kèm số ghế đã bán và đang giữ.</summary>
    public static List<ShowtimeRow> ListForCinema(int cinemaId, DateTime from, DateTime to, DateTime now, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = RowSql + " WHERE a.CinemaId = @4 AND t.StartTime >= @5 AND t.StartTime < @6 ORDER BY a.Name, t.StartTime";
        var table = DBUtil.QueryDataTable(sql, WithCounters(now, cinemaId, from, to), conn, tx);
        return table.Rows.Cast<DataRow>().Select(RowOf).ToList();
    }

    public static ShowtimeRow? GetRow(int id, DateTime now, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = RowSql + " WHERE t.Id = @4";
        var table = DBUtil.QueryDataTable(sql, WithCounters(now, id), conn, tx);
        return table.Rows.Count == 0 ? null : RowOf(table.Rows[0]);
    }

    /// <summary>Suất không bị hủy của rạp có khoảng [StartTime, CleaningEndTime) chạm vào [from, to).</summary>
    public static List<ShowtimeRow> ActiveNear(int cinemaId, DateTime from, DateTime to, DateTime now, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = RowSql + " WHERE a.CinemaId = @4 AND t.OperationalStatus <> @7 AND t.StartTime < @6 AND t.CleaningEndTime > @5 ORDER BY t.StartTime";
        var table = DBUtil.QueryDataTable(sql, WithCounters(now, cinemaId, from, to, OperationalStatus.Cancelled), conn, tx);
        return table.Rows.Cast<DataRow>().Select(RowOf).ToList();
    }

    /// <summary>
    /// Khóa (UPDLOCK, HOLDLOCK) các suất không bị hủy của phòng chạm vào [from, to) trong transaction lưu suất, để hai người
    /// cùng xếp vào một khoảng giờ không vượt qua kiểm tra chồng giờ cùng lúc (SPEC 3.3).
    /// </summary>
    public static List<Showtime> LockAuditoriumRange(int auditoriumId, DateTime from, DateTime to, SqlConnection conn, SqlTransaction tx)
    {
        const string sql = "SELECT Id, MovieId, AuditoriumId, StartTime, FeatureStartTime, EndTime, CleaningEndTime, LanguageFormat, OperationalStatus, IsAiGenerated, IsPrivate, CancelNote, CreatedAt" +
                           " FROM Showtimes WITH (UPDLOCK, HOLDLOCK) WHERE AuditoriumId = @1 AND OperationalStatus <> @4 AND StartTime < @3 AND CleaningEndTime > @2";
        return FromDataTable(DBUtil.QueryDataTable(sql, new List<object> { auditoriumId, from, to, OperationalStatus.Cancelled }, conn, tx));
    }

    /// <summary>Suất không bị hủy của một phim tại rạp còn chưa kết thúc (cảnh báo KDM sắp hết hạn).</summary>
    public static List<ShowtimeRow> UpcomingForMovie(int movieId, int cinemaId, DateTime now, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = RowSql + " WHERE t.MovieId = @4 AND a.CinemaId = @5 AND t.EndTime > @3 AND t.OperationalStatus <> @6 ORDER BY t.StartTime";
        var table = DBUtil.QueryDataTable(sql, WithCounters(now, movieId, cinemaId, OperationalStatus.Cancelled), conn, tx);
        return table.Rows.Cast<DataRow>().Select(RowOf).ToList();
    }

    /// <summary>
    /// Số suất phim Việt và tổng số suất không bị hủy của rạp có ngày vận hành trong [fromDate, toDate] (SPEC 3.1 mục 6).
    /// Ngày vận hành: suất bắt đầu trước 06:00 thuộc ngày hôm trước.
    /// </summary>
    public static (int Vietnamese, int Total) CountVietnameseShare(int cinemaId, DateTime fromDate, DateTime toDate, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT SUM(CASE WHEN m.IsVietnameseFilm = 1 THEN 1 ELSE 0 END) AS Vn, COUNT(*) AS Total FROM Showtimes t JOIN Auditoriums a ON a.Id = t.AuditoriumId JOIN Movies m ON m.Id = t.MovieId" +
                           " WHERE a.CinemaId = @1 AND t.OperationalStatus <> @4 AND CAST(DATEADD(HOUR, -6, t.StartTime) AS DATE) BETWEEN @2 AND @3";
        var table = DBUtil.QueryDataTable(sql, new List<object> { cinemaId, fromDate.Date, toDate.Date, OperationalStatus.Cancelled }, conn, tx);
        var r = table.Rows[0];
        return (r.GetIntN("Vn") ?? 0, r.GetIntN("Total") ?? 0);
    }

    public static int CountTickets(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM Tickets WHERE ShowtimeId = @1";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { id }, conn, tx));
    }

    public static int CountCorporateBookings(int id, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT COUNT(*) FROM CorporateBookings WHERE ShowtimeId = @1";
        return Convert.ToInt32(DBUtil.ExecuteScalar(sql, new List<object> { id }, conn, tx));
    }

    /// <summary>Mọi suất kèm rạp và tên phòng (dùng cho test kiểm suất của DbSeeder).</summary>
    public static List<ShowtimeRow> AllRows(DateTime now, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = RowSql + " ORDER BY a.CinemaId, t.StartTime";
        var table = DBUtil.QueryDataTable(sql, WithCounters(now), conn, tx);
        return table.Rows.Cast<DataRow>().Select(RowOf).ToList();
    }
}

/// <summary>Các hàm thêm của PricingRuleDAL: bảng giá của một rạp.</summary>
public static partial class PricingRuleDAL
{
    public static List<PricingRule> GetByCinema(int cinemaId, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT Id, CinemaId, RuleType, RuleKey, Amount FROM PricingRules WHERE CinemaId = @1 ORDER BY RuleType, RuleKey";
        return FromDataTable(DBUtil.QueryDataTable(sql, new List<object> { cinemaId }, conn, tx));
    }
}

/// <summary>Các hàm thêm của HolidayDAL: ngày lễ theo năm.</summary>
public static partial class HolidayDAL
{
    public static List<Holiday> GetByYear(int year, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT HolidayDate, Name FROM Holidays WHERE YEAR(HolidayDate) = @1 ORDER BY HolidayDate";
        return FromDataTable(DBUtil.QueryDataTable(sql, new List<object> { year }, conn, tx));
    }

    public static List<int> Years(SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT DISTINCT YEAR(HolidayDate) AS Y FROM Holidays ORDER BY Y";
        return DBUtil.QueryDataTable(sql, null, conn, tx).Rows.Cast<DataRow>().Select(r => r.GetInt("Y")).ToList();
    }
}
