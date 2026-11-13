using System.Globalization;
using System.Text;
using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Web.Services;

/// <summary>Khoảng ngày [From, To] gồm cả hai đầu, kèm mốc nhanh đang chọn.</summary>
public sealed record DateRange(DateTime From, DateTime To, string Preset)
{
    public DateTime ToExclusive => To.AddDays(1);
    public int Days => (To - From).Days + 1;
}

/// <summary>Số liệu dashboard quản lý (UI A1).</summary>
public sealed record DashboardData(DateTime Today, DashboardKpi TodayKpi, List<RevenueByDayStat> Revenue30, List<TopMovieStat> TopMovies, DateRange Range30,
    List<VietnameseShareRow> VietnameseShares, VietnameseShareRow ChainShare, List<OpsAlertRow> Alerts, decimal MinShare, int? CinemaId, string ScopeName);

/// <summary>Báo cáo doanh thu theo khoảng ngày (UI A6).</summary>
public sealed record RevenueReport(DateRange Range, List<RevenueByDayStat> Days, List<TopDrinkStat> TopDrinks, List<TopMovieStat> TopMovies, DashboardKpi Kpi);

/// <summary>Đối soát nhà phát hành: kết quả stored procedure kèm bảng theo mức giá (SettlementCalculator).</summary>
public sealed record SettlementReport(BoxOfficeSettlement Result, Movie Movie, List<(SettlementLine Line, decimal Net, decimal FloorNet, bool BelowFloor)> Lines, SettlementResult Check);

/// <summary>
/// Thống kê và báo cáo (SPEC 3.12, UI A1, A6): 4 stored procedure qua StatisticDAL (EXEC), tỷ lệ phim Việt (3.1 mục 6), tỷ lệ lấp đầy, biên bản ca,
/// đối soát nhà phát hành; xuất CSV.
/// </summary>
public class StatisticsService
{
    public const string Today = "today", Last7 = "7d", Last30 = "30d", ThisMonth = "month", LastMonth = "lastmonth", Custom = "custom";

    /// <summary>Các mốc nhanh của bộ chọn khoảng ngày.</summary>
    public static readonly (string Key, string Label)[] Presets =
    {
        (Today, "Hôm nay"), (Last7, "7 ngày"), (Last30, "30 ngày"), (ThisMonth, "Tháng này"), (LastMonth, "Tháng trước"),
    };

    private readonly IClock _clock;
    private readonly CinemaRules _rules;

    public StatisticsService(IClock clock, CinemaRules rules)
    {
        _clock = clock;
        _rules = rules;
    }

    /// <summary>Khoảng ngày theo mốc nhanh hoặc ngày tự chọn (giới hạn 366 ngày, từ ≤ đến).</summary>
    public DateRange Range(string? preset, DateTime? from, DateTime? to, string fallback = Last30)
    {
        var today = _clock.Now.Date;
        DateRange Of(string p) => p switch
        {
            Today => new DateRange(today, today, Today),
            Last7 => new DateRange(today.AddDays(-6), today, Last7),
            ThisMonth => new DateRange(new DateTime(today.Year, today.Month, 1), today, ThisMonth),
            LastMonth => new DateRange(new DateTime(today.Year, today.Month, 1).AddMonths(-1), new DateTime(today.Year, today.Month, 1).AddDays(-1), LastMonth),
            _ => new DateRange(today.AddDays(-29), today, Last30),
        };
        if (from is DateTime f && to is DateTime t && (preset == null || preset == Custom))
        {
            if (t.Date < f.Date) throw FieldErrors.Of("to", "Ngày kết thúc phải từ ngày bắt đầu trở đi.");
            if ((t.Date - f.Date).TotalDays > 366) throw FieldErrors.Of("to", "Khoảng ngày tối đa 366 ngày.");
            return new DateRange(f.Date, t.Date, Custom);
        }
        return Of(Presets.Any(p => p.Key == preset) ? preset! : fallback);
    }

    /// <summary>Dashboard: KPI hôm nay, doanh thu 30 ngày, top phim, tỷ lệ phim Việt từ đầu năm theo rạp và toàn chuỗi, cảnh báo vận hành.</summary>
    public DashboardData Dashboard(int? cinemaId, string scopeName)
    {
        var now = _clock.Now;
        var today = now.Date;
        var kpi = StatisticDAL.Kpi(today, today.AddDays(1), cinemaId);
        var range = new DateRange(today.AddDays(-29), today, Last30);
        var revenue = StatisticDAL.RevenueByDay(range.From, range.To);
        var top = StatisticDAL.TopBestSellingMovies(range.From, range.To);
        var shares = VietnameseShares(new DateTime(today.Year, 1, 1), today);
        var alerts = new List<OpsAlertRow>();
        alerts.AddRange(StatisticDAL.SeatsNeedingChange(now, cinemaId));
        alerts.AddRange(StatisticDAL.KdmExpiring(now, now.AddDays(3), cinemaId));
        alerts.AddRange(StatisticDAL.LowStock(cinemaId));
        return new DashboardData(today, kpi, revenue, top, range, shares.Rows, shares.Chain, alerts, _rules.VietnameseFilmMinShare, cinemaId, scopeName);
    }

    /// <summary>Tỷ lệ phim Việt theo rạp và toàn chuỗi (chỉ suất không bị hủy), kèm khung 18:00–22:00 để tham khảo.</summary>
    public (List<VietnameseShareRow> Rows, VietnameseShareRow Chain) VietnameseShares(DateTime from, DateTime to)
    {
        var rows = StatisticDAL.VietnameseShares(from, to, _rules.VietnameseFilmPriorityStart, _rules.VietnameseFilmPriorityEnd);
        var chain = new VietnameseShareRow(null, "Toàn chuỗi", rows.Sum(r => r.Vietnamese), rows.Sum(r => r.Total), rows.Sum(r => r.PrimeVietnamese), rows.Sum(r => r.PrimeTotal));
        return (rows, chain);
    }

    public RevenueReport Revenue(DateRange range) => new(range, StatisticDAL.RevenueByDay(range.From, range.To),
        StatisticDAL.TopBestSellingDrinks(range.From, range.To), StatisticDAL.TopBestSellingMovies(range.From, range.To), StatisticDAL.Kpi(range.From, range.ToExclusive, null));

    public List<OccupancyRow> Occupancy(DateRange range, int? cinemaId) => StatisticDAL.OccupancyByShowtime(range.From, range.ToExclusive, cinemaId);

    public List<ShiftRow> Shifts(DateRange range, int? cinemaId) => PosShiftDAL.ClosedRows(range.From, range.ToExclusive, cinemaId);

    /// <summary>Đối soát một phim, tuần n (từ 1), một rạp hoặc toàn chuỗi: kết quả sp_box_office_settlement và bảng theo mức giá.</summary>
    public SettlementReport Settlement(int movieId, int week, int? cinemaId)
    {
        var movie = MovieDAL.GetById(movieId) ?? throw FieldErrors.Of("movieId", "Bạn chọn phim cần đối soát.");
        if (week < 1 || week > 52) throw FieldErrors.Of("week", "Tuần đối soát từ 1 đến 52.");
        var result = StatisticDAL.GetBoxOfficeSettlement(movieId, week, cinemaId);
        var raw = StatisticDAL.SettlementLines(movieId, result.WeekStartDate, result.WeekEndDate.AddDays(1), cinemaId);
        var lines = raw.Select(r => new SettlementLine(r.FinalPrice, r.VatRate, r.Count)).ToList();
        var check = SettlementCalculator.Compute(lines, movie.MinimumTicketPrice, result.DistributorSharePercent);
        var detail = lines.Select(l =>
        {
            var net = SettlementCalculator.Net(l.FinalPrice, l.VatRate);
            var floor = SettlementCalculator.FloorNet(movie.MinimumTicketPrice, l.VatRate);
            return (l, net, floor, net < floor);
        }).ToList();
        return new SettlementReport(result, movie, detail, check);
    }

    /// <summary>Số tuần đã chiếu của phim tới hôm nay (ít nhất 1) để gợi ý ô chọn tuần.</summary>
    public int WeeksSinceRelease(Movie m) => Math.Max(1, (int)Math.Ceiling(((_clock.Now.Date - m.ReleaseDate.Date).TotalDays + 1) / 7.0));

    // ------------------------------------------------------------------ CSV

    /// <summary>Tạo nội dung CSV (UTF-8 có BOM để Excel đọc đúng tiếng Việt), dấu phẩy, bọc ngoặc kép khi cần.</summary>
    public static byte[] Csv(IEnumerable<string> header, IEnumerable<IEnumerable<object?>> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", header.Select(Cell)));
        foreach (var r in rows) sb.AppendLine(string.Join(",", r.Select(Cell)));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Cell(object? v)
    {
        var s = v switch
        {
            null => string.Empty,
            DateTime d => d.TimeOfDay == TimeSpan.Zero ? Fmt.Date(d) : Fmt.DateTime(d),
            decimal m => m.ToString("0.##", CultureInfo.InvariantCulture),
            double x => x.ToString("0.##", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => v.ToString() ?? string.Empty,
        };
        // Chặn công thức khi mở bằng bảng tính
        if (s.Length > 0 && "=+-@".Contains(s[0]) && !decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) s = "'" + s;
        return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    /// <summary>Tỷ lệ phần trăm có một chữ số thập phân.</summary>
    public static decimal Percent(int part, int total) => total == 0 ? 0 : Math.Round(part * 100m / total, 1);
}

/// <summary>Chi tiết một hóa đơn (UI A8).</summary>
public sealed record BillDetailData(Bill Bill, BillListRow Row, List<TicketRow> Tickets, List<BillLineRow> Lines, List<PaymentRow> Payments,
    List<ApprovalRow> Approvals, PosShift? Shift, string? CustomerName, string? CustomerCode, string? VoucherCode, long VatTotal);

/// <summary>
/// Quản lý hóa đơn (SPEC 3.6, BillsController, Lab 6 bài 1): danh sách lọc theo khoảng ngày, trạng thái, kênh, rạp, nhân viên, tìm theo mã;
/// chi tiết; in lại. Không sửa trực tiếp số tiền; hủy đi qua BillService theo đúng quy tắc chuyển trạng thái.
/// </summary>
public class BillQueryService
{
    public List<BillListRow> Search(DateRange range, string? status, string? channel, int? cinemaId, int? staffId, string? code, int page, int pageSize, out int total)
    {
        if (status != null && !BillStatus.All.Contains(status)) status = null;
        if (channel != null && !BillChannel.All.Contains(channel)) channel = null;
        return BillDAL.Search(range.From, range.ToExclusive, status, channel, cinemaId, staffId, code, Math.Max(0, page - 1) * pageSize, pageSize, out total);
    }

    public List<BillListRow> All(DateRange range, string? status, string? channel, int? cinemaId, int? staffId, string? code)
    {
        if (status != null && !BillStatus.All.Contains(status)) status = null;
        if (channel != null && !BillChannel.All.Contains(channel)) channel = null;
        return BillDAL.Search(range.From, range.ToExclusive, status, channel, cinemaId, staffId, code, 0, 0, out _);
    }

    /// <summary>Hóa đơn do một nhân viên tạo trong một ngày (hóa đơn của tôi).</summary>
    public List<BillListRow> CreatedBy(int userId, DateTime date) =>
        BillDAL.Search(date.Date, date.Date.AddDays(1), null, null, null, userId, null, 0, 0, out _);

    public BillDetailData? Detail(int id)
    {
        var bill = BillDAL.GetById(id);
        var row = bill == null ? null : BillDAL.GetListRow(id);
        if (bill == null || row == null) return null;
        var tickets = TicketDAL.RowsByBill(id);
        var lines = BillDetailDAL.RowsByBill(id);
        var customer = bill.CustomerId is int cid ? UserDAL.GetById(cid) : null;
        long vat = tickets.Where(t => t.CheckInStatus != TicketCheckInStatus.Swapped).Sum(t => PricingCalculator.VatAmount(t.FinalPrice, t.VatRate))
                   + lines.Sum(l => PricingCalculator.VatAmount(l.LineFinalAmount, l.VatRate));
        return new BillDetailData(bill, row, tickets, lines, PaymentDAL.RowsByBill(id), SupervisorApprovalDAL.RowsByBill(id),
            bill.PosShiftId is int sid ? PosShiftDAL.GetById(sid) : null, customer?.FullName, customer?.MemberCode,
            bill.VoucherId is int vid ? VoucherDAL.GetById(vid)?.Code : null, vat);
    }

    /// <summary>Hóa đơn theo mã hóa đơn hoặc mã QR vé (quét tại quầy để in vé giấy).</summary>
    public Bill? FindByScan(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var c = code.Trim().ToUpperInvariant();
        return BillDAL.GetByCode(c) ?? (TicketDAL.BillIdByQr(c) is int id ? BillDAL.GetById(id) : null);
    }

    public static List<SupervisorRow> Supervisors(int cinemaId) => UserDAL.Supervisors(cinemaId);

    /// <summary>Nhân viên có thể lập hóa đơn (bộ lọc theo nhân viên).</summary>
    public static List<User> Staff(int? cinemaId) => UserDAL.GetAll().Where(u => u.Role is Roles.POSCashier or Roles.ShiftManager or Roles.Manager
        && (cinemaId == null || u.CinemaId == cinemaId || u.CinemaId == null)).OrderBy(u => u.FullName).ToList();
}
