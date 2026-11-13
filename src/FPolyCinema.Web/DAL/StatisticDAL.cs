using System.Data;
using FPolyCinema.Web.Utils;

namespace FPolyCinema.Web.DAL;

/// <summary>
/// Thống kê theo slide bài 6: gọi 4 stored procedure bằng EXEC (CONTRACTS mục 10, SPEC 3.12).
/// Khoảng ngày [startDate, endDate] gồm cả hai đầu, tính theo Bills.PaidAt.
/// </summary>
public static partial class StatisticDAL
{
    public static List<TopDrinkStat> TopBestSellingDrinks(DateTime startDate, DateTime endDate)
    {
        const string sql = "EXEC sp_top_5_best_selling_drinks @StartDate = @1, @EndDate = @2";
        var table = DBUtil.QueryDataTable(sql, new List<object> { DateOnly.FromDateTime(startDate), DateOnly.FromDateTime(endDate) });
        return table.Rows.Cast<DataRow>().Select(r => new TopDrinkStat
        {
            DrinkId = r.GetInt("DrinkId"),
            DrinkName = r.GetString("DrinkName"),
            TotalQuantitySold = r.GetInt("TotalQuantitySold"),
            TotalRevenue = r.GetLong("TotalRevenue"),
        }).ToList();
    }

    public static List<TopMovieStat> TopBestSellingMovies(DateTime startDate, DateTime endDate)
    {
        const string sql = "EXEC sp_top_5_best_selling_movies @StartDate = @1, @EndDate = @2";
        var table = DBUtil.QueryDataTable(sql, new List<object> { DateOnly.FromDateTime(startDate), DateOnly.FromDateTime(endDate) });
        return table.Rows.Cast<DataRow>().Select(r => new TopMovieStat
        {
            MovieId = r.GetInt("MovieId"),
            Title = r.GetString("Title"),
            TicketsSold = r.GetInt("TicketsSold"),
            TicketRevenue = r.GetLong("TicketRevenue"),
        }).ToList();
    }

    public static List<RevenueByDayStat> RevenueByDay(DateTime startDate, DateTime endDate)
    {
        const string sql = "EXEC sp_revenue_by_day @StartDate = @1, @EndDate = @2";
        var table = DBUtil.QueryDataTable(sql, new List<object> { DateOnly.FromDateTime(startDate), DateOnly.FromDateTime(endDate) });
        return table.Rows.Cast<DataRow>().Select(r => new RevenueByDayStat
        {
            RevenueDate = r.GetDateTime("RevenueDate"),
            TotalBills = r.GetInt("TotalBills"),
            TicketRevenue = r.GetLong("TicketRevenue"),
            FnbRevenue = r.GetLong("FnbRevenue"),
            TotalRevenue = r.GetLong("TotalRevenue"),
        }).ToList();
    }

    /// <summary>Đối soát nhà phát hành của một phim, tuần thứ weekNumber (từ 1), một rạp hoặc toàn chuỗi (cinemaId = null).</summary>
    public static BoxOfficeSettlement GetBoxOfficeSettlement(int movieId, int weekNumber, int? cinemaId = null)
    {
        const string sql = "EXEC sp_box_office_settlement @MovieId = @1, @WeekNumber = @2, @CinemaId = @3";
        var table = DBUtil.QueryDataTable(sql, new List<object> { movieId, weekNumber, DBUtil.Value(cinemaId) });
        var r = table.Rows[0];
        return new BoxOfficeSettlement
        {
            MovieId = r.GetInt("MovieId"),
            Title = r.GetString("Title"),
            WeekNumber = r.GetInt("WeekNumber"),
            CinemaId = r.GetIntN("CinemaId"),
            WeekStartDate = r.GetDateTime("WeekStartDate"),
            WeekEndDate = r.GetDateTime("WeekEndDate"),
            DistributorSharePercent = r.GetDecimal("DistributorSharePercent"),
            TicketCount = r.GetInt("TicketCount"),
            GrossRevenue = r.GetLong("GrossRevenue"),
            NetRevenue = r.GetLong("NetRevenue"),
            BelowFloorTickets = r.GetInt("BelowFloorTickets"),
            FloorAbsorbedAmount = r.GetLong("FloorAbsorbedAmount"),
            DistributorAmount = r.GetLong("DistributorAmount"),
            CinemaRetainedAmount = r.GetLong("CinemaRetainedAmount"),
        };
    }
}

/// <summary>Một dòng kết quả của sp_top_5_best_selling_drinks.</summary>
public class TopDrinkStat
{
    public int DrinkId { get; set; }
    public string DrinkName { get; set; } = string.Empty;
    public int TotalQuantitySold { get; set; }
    public long TotalRevenue { get; set; }
}

/// <summary>Một dòng kết quả của sp_top_5_best_selling_movies.</summary>
public class TopMovieStat
{
    public int MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TicketsSold { get; set; }
    public long TicketRevenue { get; set; }
}

/// <summary>Một dòng kết quả của sp_revenue_by_day (mỗi ngày trong khoảng một dòng).</summary>
public class RevenueByDayStat
{
    public DateTime RevenueDate { get; set; }
    public int TotalBills { get; set; }
    public long TicketRevenue { get; set; }
    public long FnbRevenue { get; set; }
    public long TotalRevenue { get; set; }
}

/// <summary>Kết quả đối soát nhà phát hành của một phim trong một tuần (sp_box_office_settlement).</summary>
public class BoxOfficeSettlement
{
    public int MovieId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int WeekNumber { get; set; }
    public int? CinemaId { get; set; }
    public DateTime WeekStartDate { get; set; }
    public DateTime WeekEndDate { get; set; }
    public decimal DistributorSharePercent { get; set; }
    /// <summary>Số vé.</summary>
    public int TicketCount { get; set; }
    /// <summary>Doanh thu gộp (đã gồm VAT).</summary>
    public long GrossRevenue { get; set; }
    /// <summary>Doanh thu thuần (trước VAT).</summary>
    public long NetRevenue { get; set; }
    /// <summary>Số vé có giá thuần dưới giá sàn.</summary>
    public int BelowFloorTickets { get; set; }
    /// <summary>Phần rạp tự chịu do giá sàn.</summary>
    public long FloorAbsorbedAmount { get; set; }
    /// <summary>Tiền trả nhà phát hành = tổng Base × tỷ lệ.</summary>
    public long DistributorAmount { get; set; }
    /// <summary>Tiền rạp giữ lại = doanh thu thuần − tiền trả nhà phát hành.</summary>
    public long CinemaRetainedAmount { get; set; }
}
