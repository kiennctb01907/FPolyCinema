using System.Data;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.DAL;

/// <summary>Số liệu nhu cầu của một phim cho AiDemandScore: tỷ lệ lấp đầy trung bình 7 ngày qua (null khi chưa có suất) và số vé bán trước.</summary>
public sealed record MovieDemandStats(int MovieId, decimal? AvgOccupancy, int PresoldTickets);

/// <summary>Các hàm thêm của MovieDAL cho Phase 7 (AI xếp lịch, SPEC 4.3-01).</summary>
public static partial class MovieDAL
{
    /// <summary>
    /// Theo từng phim: trung bình (ghế BOOKED / tổng ghế của suất) của các suất không bị hủy bắt đầu trong [since, now),
    /// và tổng ghế BOOKED của các suất không bị hủy bắt đầu từ now (vé bán trước).
    /// </summary>
    public static Dictionary<int, MovieDemandStats> DemandStats(DateTime since, DateTime now, SqlConnection? conn = null, SqlTransaction? tx = null)
    {
        const string sql = "SELECT t.MovieId," +
                           " AVG(CASE WHEN t.StartTime < @2 THEN CAST(x.Booked AS DECIMAL(9,4)) / NULLIF(x.Total, 0) END) AS Occ," +
                           " SUM(CASE WHEN t.StartTime >= @2 THEN x.Booked ELSE 0 END) AS Presold" +
                           " FROM Showtimes t CROSS APPLY (SELECT COUNT(*) AS Total, SUM(CASE WHEN ss.Status = @3 THEN 1 ELSE 0 END) AS Booked FROM ShowtimeSeats ss WHERE ss.ShowtimeId = t.Id) x" +
                           " WHERE t.OperationalStatus <> @4 AND t.StartTime >= @1 GROUP BY t.MovieId";
        var table = DBUtil.QueryDataTable(sql, new List<object> { since, now, ShowtimeSeatStatus.Booked, OperationalStatus.Cancelled }, conn, tx);
        return table.Rows.Cast<DataRow>().Select(r => new MovieDemandStats(r.GetInt("MovieId"), r.GetDecimalN("Occ"), r.GetIntN("Presold") ?? 0))
            .ToDictionary(s => s.MovieId);
    }

    /// <summary>Lưu AiDemandScore của phim (mỗi lần chạy bộ xếp lịch).</summary>
    public static int UpdateDemandScore(int movieId, decimal score, SqlConnection? conn = null, SqlTransaction? tx = null) =>
        DBUtil.ExecuteNonQuery("UPDATE Movies SET AiDemandScore = @1 WHERE Id = @2", new List<object> { score, movieId }, conn, tx);
}
