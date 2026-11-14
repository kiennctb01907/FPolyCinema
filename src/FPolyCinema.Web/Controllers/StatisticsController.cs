using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>
/// Thống kê và báo cáo (SPEC 3.12, UI A1, A6; Lab 6; ManagerOnly). StatisticDAL gọi 4 stored procedure bằng EXEC. Dashboard quản lý /admin;
/// báo cáo doanh thu (biểu đồ Chart.js top 5 đồ uống và doanh thu theo khoảng thời gian), tỷ lệ phim Việt, tỷ lệ lấp đầy, biên bản ca; đối soát
/// nhà phát hành; xuất CSV.
/// </summary>
[Authorize(Policy = Policies.ManagerOnly)]
public class StatisticsController : AppControllerBase
{
    public const string TabRevenue = "revenue", TabVietnamese = "vietnamese", TabOccupancy = "occupancy", TabShifts = "shifts", TabWastage = "wastage";

    private readonly StatisticsService _stats;
    private readonly CinemaContextService _cinema;
    private readonly CinemaRules _rules;

    public StatisticsController(StatisticsService stats, CinemaContextService cinema, CinemaRules rules)
    {
        _stats = stats;
        _cinema = cinema;
        _rules = rules;
    }

    private string Scope => _cinema.Current?.Name ?? "Toàn chuỗi";

    /// <summary>Dashboard quản lý (UI A1): thẻ KPI, doanh thu 30 ngày, top phim, tỷ lệ phim Việt có vạch 20%, cảnh báo vận hành.</summary>
    [HttpGet("admin")]
    public IActionResult Dashboard([FromServices] OpsDashboardPages ops)
    {
        var data = _stats.Dashboard(_cinema.CurrentCinemaId, Scope);
        var range = $"{Fmt.Date(data.Range30.From)}–{Fmt.Date(data.Range30.To)}";
        return View(new DashboardVm
        {
            Data = data, Ops = ops.Build(),
            Revenue = new ChartVm("chart-revenue", "line", $"Doanh thu 30 ngày toàn chuỗi ({range})",
                data.Revenue30.Select(r => r.RevenueDate.ToString("dd/MM")).ToList(),
                new[] { new ChartSeries("Vé", data.Revenue30.Select(r => r.TicketRevenue).ToList(), "primary"), new ChartSeries("Bắp nước", data.Revenue30.Select(r => r.FnbRevenue).ToList(), "warning") }),
            TopMovies = new ChartVm("chart-top-movies", "bar", $"Top 5 phim theo doanh thu vé ({range})",
                data.TopMovies.Select(m => m.Title).ToList(), new[] { new ChartSeries("Doanh thu vé", data.TopMovies.Select(m => m.TicketRevenue).ToList(), "primary") }, Horizontal: true),
        });
    }

    /// <summary>Báo cáo (UI A6): bộ lọc khoảng ngày có mốc nhanh; thẻ KPI; biểu đồ; bảng chi tiết; nút "Xuất CSV".</summary>
    [HttpGet("admin/reports")]
    public IActionResult Reports(string? tab, string? preset, DateTime? from, DateTime? to)
    {
        tab = tab is TabVietnamese or TabOccupancy or TabShifts or TabWastage ? tab : TabRevenue;
        var range = _stats.Range(preset, from, to, tab == TabVietnamese ? StatisticsService.ThisMonth : StatisticsService.Last30);
        if (tab == TabVietnamese && from == null && string.IsNullOrEmpty(preset)) range = new DateRange(new DateTime(range.To.Year, 1, 1), range.To, StatisticsService.Custom);
        var cinemaId = _cinema.CurrentCinemaId;
        var label = $"{Fmt.Date(range.From)}–{Fmt.Date(range.To)}";
        var vm = new ReportVm
        {
            Tab = tab, Range = range, CinemaId = cinemaId, ScopeName = Scope, MinShare = _rules.VietnameseFilmMinShare,
            ExportUrl = $"/admin/reports/export?tab={tab}&from={range.From:yyyy-MM-dd}&to={range.To:yyyy-MM-dd}",
        };
        switch (tab)
        {
            case TabRevenue:
            {
                var r = _stats.Revenue(range);
                vm = With(vm, revenue: r, charts: new List<ChartVm>
                {
                    new("chart-rev-day", "line", $"Doanh thu theo ngày, toàn chuỗi ({label})", r.Days.Select(d => d.RevenueDate.ToString("dd/MM")).ToList(),
                        new[] { new ChartSeries("Vé", r.Days.Select(d => d.TicketRevenue).ToList(), "primary"), new ChartSeries("Bắp nước", r.Days.Select(d => d.FnbRevenue).ToList(), "warning") }),
                    new("chart-top-drinks", "bar", $"Top 5 đồ uống bán chạy theo số lượng ({label})", r.TopDrinks.Select(d => d.DrinkName).ToList(),
                        new[] { new ChartSeries("Số phần", r.TopDrinks.Select(d => (long)d.TotalQuantitySold).ToList(), "warning") }, Horizontal: true, Money: false),
                    new("chart-top-movies", "bar", $"Top 5 phim theo doanh thu vé ({label})", r.TopMovies.Select(m => m.Title).ToList(),
                        new[] { new ChartSeries("Doanh thu vé", r.TopMovies.Select(m => m.TicketRevenue).ToList(), "primary") }, Horizontal: true),
                });
                break;
            }
            case TabVietnamese:
            {
                var (rows, chain) = _stats.VietnameseShares(range.From, range.To);
                vm = With(vm, shares: rows, chain: chain);
                break;
            }
            case TabOccupancy:
                vm = With(vm, occupancy: _stats.Occupancy(range, cinemaId));
                break;
            case TabShifts:
                vm = With(vm, shifts: _stats.Shifts(range, cinemaId));
                break;
            case TabWastage:
                vm = With(vm, wastage: WastageLogDAL.Report(cinemaId, range.From, range.To.AddDays(1)));
                break;
        }
        return View(vm);
    }

    private static ReportVm With(ReportVm v, RevenueReport? revenue = null, List<ChartVm>? charts = null, List<Entities.ReadModels.VietnameseShareRow>? shares = null,
        Entities.ReadModels.VietnameseShareRow? chain = null, List<Entities.ReadModels.OccupancyRow>? occupancy = null, List<Entities.ReadModels.ShiftRow>? shifts = null, List<Entities.ReadModels.WastageReportRow>? wastage = null) => new()
    {
        Tab = v.Tab, Range = v.Range, CinemaId = v.CinemaId, ScopeName = v.ScopeName, MinShare = v.MinShare, ExportUrl = v.ExportUrl,
        Revenue = revenue, Charts = charts ?? new(), Shares = shares ?? new(), ChainShare = chain, Occupancy = occupancy ?? new(), Shifts = shifts ?? new(), Wastage = wastage ?? new(),
    };

    /// <summary>Xuất CSV của tab báo cáo đang xem.</summary>
    [HttpGet("admin/reports/export")]
    public IActionResult Export(string? tab, DateTime? from, DateTime? to)
    {
        var range = _stats.Range(null, from, to);
        var cinemaId = _cinema.CurrentCinemaId;
        byte[] csv;
        string name;
        switch (tab)
        {
            case TabVietnamese:
            {
                var (rows, chain) = _stats.VietnameseShares(range.From, range.To);
                csv = StatisticsService.Csv(new[] { "Rạp", "Suất phim Việt", "Tổng suất", "Tỷ lệ (%)", "Suất phim Việt 18:00–22:00", "Tổng suất 18:00–22:00", "Tỷ lệ 18:00–22:00 (%)" },
                    rows.Append(chain).Select(r => new object?[] { r.CinemaName, r.Vietnamese, r.Total, StatisticsService.Percent(r.Vietnamese, r.Total), r.PrimeVietnamese, r.PrimeTotal, StatisticsService.Percent(r.PrimeVietnamese, r.PrimeTotal) }));
                name = "ty-le-phim-viet";
                break;
            }
            case TabOccupancy:
                csv = StatisticsService.Csv(new[] { "Suất", "Rạp", "Phòng", "Phim", "Ghế đã bán", "Sức chứa", "Tỷ lệ lấp đầy (%)" },
                    _stats.Occupancy(range, cinemaId).Select(r => new object?[] { r.StartTime, r.CinemaName, r.AuditoriumName, r.MovieTitle, r.Sold, r.Capacity, StatisticsService.Percent(r.Sold, r.Capacity) }));
                name = "ty-le-lap-day";
                break;
            case TabShifts:
                csv = StatisticsService.Csv(new[] { "Rạp", "Máy", "Nhân viên", "Mở ca", "Đóng ca", "Tiền đầu ca", "Tiền mặt thu", "Tiền mặt hoàn", "Hệ thống tính", "Đếm được", "Chênh lệch" },
                    _stats.Shifts(range, cinemaId).Select(r => new object?[] { r.CinemaName, r.Shift.TerminalCode, r.UserName, r.Shift.OpenedAt, r.Shift.ClosedAt, r.Shift.OpeningCashFloat,
                        r.CashIn, r.CashRefund, r.Shift.ExpectedCash, r.Shift.ActualCashCounted, r.Shift.Discrepancy }));
                name = "bien-ban-ca";
                break;
            case TabWastage:
                csv = StatisticsService.Csv(new[] { "Ngày duyệt", "Phiếu", "Rạp", "Nguyên liệu", "Số lượng", "Đơn vị", "Lý do", "Người lập", "Người duyệt" },
                    WastageLogDAL.Report(cinemaId, range.From, range.To.AddDays(1)).Select(r => new object?[] { r.DecidedAt, r.LogId, r.CinemaName, r.MaterialName, r.Quantity, r.Unit, r.Reason, r.CreatorName, r.DeciderName }));
                name = "huy-hang";
                break;
            default:
            {
                var r = _stats.Revenue(range);
                csv = StatisticsService.Csv(new[] { "Ngày", "Số hóa đơn", "Doanh thu vé", "Doanh thu bắp nước", "Tổng doanh thu" },
                    r.Days.Select(d => new object?[] { d.RevenueDate, d.TotalBills, d.TicketRevenue, d.FnbRevenue, d.TotalRevenue }));
                name = "doanh-thu";
                break;
            }
        }
        return File(csv, "text/csv; charset=utf-8", $"{name}_{range.From:yyyyMMdd}-{range.To:yyyyMMdd}.csv");
    }

    /// <summary>Đối soát nhà phát hành (UC39): chọn phim và tuần (một rạp hoặc toàn chuỗi); bảng kết quả ghi rõ phần rạp tự chịu do giá sàn.</summary>
    [HttpGet("admin/reports/settlement")]
    public IActionResult Settlement(int? movieId, int? week, int? cinemaId)
    {
        cinemaId = _cinema.BoundCinemaId ?? cinemaId; // quản lý gắn một rạp chỉ đối soát rạp mình
        var movies = MovieDAL.GetAll().OrderByDescending(m => m.ReleaseDate).ThenBy(m => m.Title).ToList();
        var movie = movieId is int id ? movies.FirstOrDefault(m => m.Id == id) : null;
        int maxWeek = movie == null ? 1 : Math.Min(52, _stats.WeeksSinceRelease(movie));
        int w = Math.Clamp(week ?? maxWeek, 1, 52);
        SettlementReport? report = null;
        string? error = null;
        if (movie != null)
        {
            try { report = _stats.Settlement(movie.Id, w, cinemaId); }
            catch (BusinessRuleException ex) { error = ex.Message; }
        }
        return View(new SettlementVm { Movies = movies, MovieId = movie?.Id, Week = w, MaxWeek = Math.Max(maxWeek, w), CinemaId = cinemaId, Cinemas = _cinema.Cinemas.Where(c => _cinema.BoundCinemaId is not int b || c.Id == b).ToList(), Report = report, Error = error });
    }

    /// <summary>Xuất CSV bảng đối soát theo mức giá.</summary>
    [HttpGet("admin/reports/settlement/export")]
    public IActionResult SettlementExport(int movieId, int week, int? cinemaId)
    {
        cinemaId = _cinema.BoundCinemaId ?? cinemaId;
        var r = _stats.Settlement(movieId, week, cinemaId);
        var rows = r.Lines.Select(l => new object?[] { l.Line.FinalPrice, l.Line.VatRate, l.Line.Count, Math.Round(l.Net, 0), Math.Round(l.FloorNet, 0), l.BelowFloor ? "Dưới giá sàn" : "Đạt" })
            .Append(new object?[] { "Tổng", null, r.Result.TicketCount, r.Result.NetRevenue, null, $"Rạp tự chịu {r.Result.FloorAbsorbedAmount}; trả nhà phát hành {r.Result.DistributorAmount}; rạp giữ lại {r.Result.CinemaRetainedAmount}" });
        var csv = StatisticsService.Csv(new[] { "Giá vé (gồm VAT)", "VAT (%)", "Số vé", "Giá thuần", "Giá sàn thuần", "So với giá sàn" }, rows);
        return File(csv, "text/csv; charset=utf-8", $"doi-soat_{r.Movie.Id}_tuan-{week}.csv");
    }
}
