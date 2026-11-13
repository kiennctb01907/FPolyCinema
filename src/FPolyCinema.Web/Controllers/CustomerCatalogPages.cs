using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.ViewModels;

namespace FPolyCinema.Web.Controllers;

/// <summary>
/// Dựng ViewModel cho các trang web khách (lớp giao diện, dùng chung cho các controller của khách; SPEC 8.5-01, UI C1, C2): trang chủ, danh sách phim, chi tiết phim, lịch chiếu lọc theo rạp, ngày, định dạng, ngôn ngữ, thực đơn bắp nước.
/// Ngày là ngày vận hành (suất trước 06:00 thuộc ngày hôm trước). Suất đã qua giờ bán online thì ẩn; suất hết ghế hiện "Hết vé".
/// </summary>
public class CustomerCatalogPages
{
    private readonly IClock _clock;
    private readonly CinemaRules _rules;
    private readonly InventoryService _inventory;

    public CustomerCatalogPages(IClock clock, CinemaRules rules, InventoryService inventory)
    {
        _clock = clock;
        _rules = rules;
        _inventory = inventory;
    }

    public const int ScheduleDays = 7;

    public static string BookUrl(int movieId) => $"/movies/{movieId}#lich-chieu";

    public static MovieCardVm Card(Movie m, bool bookable) =>
        new(m.Id, m.Title, m.PosterUrl, m.AgeRating, DevToolsService.MovieSubline(m), bookable ? BookUrl(m.Id) : $"/movies/{m.Id}", bookable ? "Đặt vé" : "Xem chi tiết");

    /// <summary>Phim đang chiếu (có suất trong 7 ngày tới) và sắp chiếu (khởi chiếu sau hôm nay, chưa có suất).</summary>
    public (List<Movie> Now, List<Movie> Soon) Movies()
    {
        var now = _clock.Now;
        var today = PricingCalculator.OperatingDate(now);
        var ids = ShowtimeDAL.MoviesWithShowtimes(now, today.AddDays(ScheduleDays).Add(PricingCalculator.NightCutoff));
        var all = MovieDAL.GetAll().Where(m => m.Active && m.AgeRating != AgeRating.C).ToList();
        var nowShowing = ids.Select(id => all.FirstOrDefault(m => m.Id == id)).Where(m => m != null).Select(m => m!).ToList();
        var soon = all.Where(m => m.ReleaseDate.Date > today && !ids.Contains(m.Id)).OrderBy(m => m.ReleaseDate).ToList();
        return (nowShowing, soon);
    }

    public HomeVm Home(int? cinemaId, string? tab)
    {
        var (nowShowing, soon) = Movies();
        var featured = nowShowing.FirstOrDefault(m => !string.IsNullOrEmpty(m.PosterUrl)) ?? nowShowing.FirstOrDefault();
        var drinks = DrinkDAL.GetAll().Where(d => d.Active && d.IsCombo).OrderBy(d => d.Price).ToList();
        var portions = cinemaId is int cid ? _inventory.Portions(cid, drinks) : drinks.ToDictionary(d => d.Id, d => d.IsSoldOut ? 0 : 1);
        return new HomeVm
        {
            Featured = featured == null ? null : new FeaturedMovieVm(featured.Id, featured.Title, featured.AgeRating, featured.PosterUrl, Meta(featured, FeaturedFormats(featured.Id, cinemaId)), featured.TrailerUrl, BookUrl(featured.Id)),
            NowShowing = nowShowing.Select(m => Card(m, true)).ToList(),
            ComingSoon = soon.Select(m => Card(m, false) with { Subline = $"Khởi chiếu {Fmt.Date(m.ReleaseDate)}" }).ToList(),
            Combos = drinks.Take(4).Select(d => new FoodCardVm(d.Id, d.Name, d.Image, d.Price, portions.TryGetValue(d.Id, out var p) && p <= 0)).ToList(),
            Promos = VoucherDAL.PublicPromos(_clock.Now).Take(4).ToList(),
            Cinemas = CinemaDAL.GetActive(),
            CinemaId = cinemaId,
            Tab = tab == "soon" ? "soon" : "now",
        };
    }

    /// <summary>Dòng "thể loại · thời lượng · định dạng" của hero (UI C1-02); định dạng là định dạng phòng của các suất sắp tới, ví dụ "2D, IMAX".</summary>
    public static string Meta(Movie m, IEnumerable<string> formats)
    {
        var genres = CsvList.Split(m.Genres).Take(2).Select(g => StatusDisplay.Of(StatusDisplay.Genres, g).Label);
        return string.Join(" · ", new[] { string.Join(", ", genres), Fmt.Duration(m.DurationMinutes), string.Join(", ", formats) }.Where(x => !string.IsNullOrEmpty(x)));
    }

    /// <summary>Định dạng phòng (2D, 3D, IMAX, 4DX) của các suất còn bán trong 7 ngày tới, theo rạp đang chọn.</summary>
    private List<string> FeaturedFormats(int movieId, int? cinemaId)
    {
        var now = _clock.Now;
        var to = PricingCalculator.OperatingDate(now).AddDays(ScheduleDays).Add(PricingCalculator.NightCutoff);
        var order = new[] { "2D", "3D", "IMAX", "4DX" };
        return ShowtimeDAL.ForCustomer(cinemaId, movieId, now, to, now).Select(r => r.AuditoriumFormat).Distinct()
            .OrderBy(x => Array.IndexOf(order, x) is var i && i < 0 ? 99 : i).ToList();
    }

    // ------------------------------------------------------------------ lịch chiếu

    private ScheduleFilterVm Filter(DateTime? date, string? format, string? language, int? cinemaId, string basePath)
    {
        var now = _clock.Now;
        var today = PricingCalculator.OperatingDate(now);
        var d = date?.Date ?? today;
        if (d < today || d >= today.AddDays(ScheduleDays)) d = today;
        return new ScheduleFilterVm
        {
            Days = Enumerable.Range(0, ScheduleDays).Select(i => today.AddDays(i)).ToList(),
            Date = d, Today = today,
            Format = AuditoriumFormat.All.Contains(format) ? format : null,
            Language = LanguageFormat.All.Contains(language) ? language : null,
            CinemaId = cinemaId, Cinemas = CinemaDAL.GetActive(), BasePath = basePath,
            Formats = AuditoriumFormat.All.ToList(), Languages = LanguageFormat.All.ToList(),
        };
    }

    /// <summary>Các suất của một ngày vận hành đã lọc; bỏ suất đã hết giờ bán online.</summary>
    private List<CustomerShowtimeRow> Rows(ScheduleFilterVm f, int? movieId)
    {
        var now = _clock.Now;
        var from = f.Date.Add(PricingCalculator.NightCutoff);
        return ShowtimeDAL.ForCustomer(f.CinemaId, movieId, from, from.AddDays(1), now)
            .Where(r => SeatHoldRules.IsSalesOpen(r.FeatureStartTime, r.OperationalStatus, BillChannel.Online, now, _rules))
            .Where(r => f.Format == null || r.AuditoriumFormat == f.Format)
            .Where(r => f.Language == null || r.LanguageFormat == f.Language)
            .ToList();
    }

    private ShowtimeChipVm Chip(CustomerShowtimeRow r) => new(r.ShowtimeId, r.StartTime, r.EndTime, r.FreeSeats, r.FreeSeats <= 0,
        OnlineAgeRules.CurfewNotice(r.StartTime, r.EndTime, _rules.ChildU13LatestEnd, _rules.ChildU16LatestEnd), $"/booking/{r.ShowtimeId}");

    private List<FormatGroupVm> ByFormat(IEnumerable<CustomerShowtimeRow> rows) =>
        rows.GroupBy(r => (r.AuditoriumFormat, r.LanguageFormat)).OrderBy(g => g.Key.AuditoriumFormat).ThenBy(g => g.Key.LanguageFormat)
            .Select(g => new FormatGroupVm($"{g.Key.AuditoriumFormat} · {StatusDisplay.Of(StatusDisplay.Language, g.Key.LanguageFormat).Label}",
                g.OrderBy(r => r.StartTime).Select(Chip).ToList())).ToList();

    public MovieDetailVm? MovieDetail(int id, DateTime? date, string? format, string? language, int? cinemaId)
    {
        var m = MovieDAL.GetById(id);
        if (m == null || !m.Active || m.AgeRating == AgeRating.C) return null;
        var f = Filter(date, format, language, cinemaId, $"/movies/{id}");
        var rows = Rows(f, id);
        return new MovieDetailVm
        {
            Movie = m,
            DistributorName = m.DistributorId is int did ? DistributorDAL.GetById(did)?.Name : null,
            Genres = StatusDisplay.LabelsOfCsv(StatusDisplay.Genres, m.Genres).ToList(),
            Languages = StatusDisplay.LabelsOfCsv(StatusDisplay.Language, m.LanguageFormats).ToList(),
            Warnings = StatusDisplay.LabelsOfCsv(StatusDisplay.Warning, m.ContentWarnings).ToList(),
            Filter = f,
            Groups = rows.GroupBy(r => (r.CinemaId, r.CinemaName)).Select(g => new ScheduleGroupVm(g.Key.CinemaName, null, null, null, null, ByFormat(g))).ToList(),
        };
    }

    public ShowtimesVm Showtimes(DateTime? date, string? format, string? language, int? cinemaId)
    {
        var f = Filter(date, format, language, cinemaId, "/showtimes");
        var rows = Rows(f, null);
        var movies = MovieDAL.GetAll().ToDictionary(m => m.Id);
        return new ShowtimesVm
        {
            Filter = f,
            Groups = rows.GroupBy(r => r.MovieId).OrderByDescending(g => g.Count()).Select(g =>
            {
                var m = movies[g.Key];
                var sub = f.CinemaId == null ? string.Join(", ", g.Select(r => r.CinemaName).Distinct()) : DevToolsService.MovieSubline(m);
                return new ScheduleGroupVm(m.Title, sub, m.AgeRating, m.PosterUrl, $"/movies/{m.Id}", ByFormat(g));
            }).ToList(),
        };
    }

    /// <summary>Mua vé nhanh (C1): danh sách phim của rạp, ngày có suất của phim, suất của ngày.</summary>
    public object QuickBuy(int? cinemaId, int? movieId, DateTime? date)
    {
        var now = _clock.Now;
        var today = PricingCalculator.OperatingDate(now);
        var rows = ShowtimeDAL.ForCustomer(cinemaId, movieId, now, today.AddDays(ScheduleDays).Add(PricingCalculator.NightCutoff), now)
            .Where(r => SeatHoldRules.IsSalesOpen(r.FeatureStartTime, r.OperationalStatus, BillChannel.Online, now, _rules)).ToList();
        var movies = rows.GroupBy(r => r.MovieId).Select(g => new { id = g.Key, title = g.First().MovieTitle }).OrderBy(x => x.title).ToList();
        var days = rows.Select(r => PricingCalculator.OperatingDate(r.StartTime)).Distinct().OrderBy(d => d)
            .Select(d => new { value = d.ToString("yyyy-MM-dd"), label = DayLabel(d, today) }).ToList();
        var times = date == null ? new List<object>() : rows.Where(r => PricingCalculator.OperatingDate(r.StartTime) == date.Value.Date)
            .OrderBy(r => r.StartTime).Select(r => (object)new
            {
                id = r.ShowtimeId, label = $"{Fmt.Time(r.StartTime)} · {r.AuditoriumFormat} · {StatusDisplay.Of(StatusDisplay.Language, r.LanguageFormat).Label}" + (r.FreeSeats <= 0 ? " · Hết vé" : string.Empty),
                soldOut = r.FreeSeats <= 0,
            }).ToList();
        return new { movies, days, times };
    }

    /// <summary>"Hôm nay 06/11", "T7 07/11".</summary>
    public static string DayLabel(DateTime d, DateTime today) => (d == today ? "Hôm nay" : Fmt.WeekdayShort(d.DayOfWeek)) + " " + d.ToString("dd/MM");

    /// <summary>Các suất khác cùng phim, cùng rạp, cùng ngày (bước 1 của C3: đổi suất).</summary>
    public List<CustomerShowtimeRow> OtherTimes(ShowtimeInfo info)
    {
        var now = _clock.Now;
        var day = PricingCalculator.OperatingDate(info.Showtime.StartTime).Add(PricingCalculator.NightCutoff);
        return ShowtimeDAL.ForCustomer(info.Auditorium.CinemaId, info.Movie.Id, day, day.AddDays(1), now)
            .Where(r => SeatHoldRules.IsSalesOpen(r.FeatureStartTime, r.OperationalStatus, BillChannel.Online, now, _rules)).ToList();
    }

    // ------------------------------------------------------------------ thực đơn

    /// <summary>Thực đơn bắp nước; món hết nguyên liệu ở rạp đang chọn hiện "Tạm hết" và không chọn được (SPEC 3.11-13).</summary>
    public MenuVm Menu(int? cinemaId, int? categoryId)
    {
        var drinks = DrinkDAL.GetAll().Where(d => d.Active).OrderBy(d => d.IsCombo ? 0 : 1).ThenBy(d => d.Name).ToList();
        var cid = cinemaId ?? CinemaDAL.GetActive().First().Id;
        var portions = _inventory.Portions(cid, drinks);
        var cats = CategoryDAL.GetActive();
        return new MenuVm
        {
            Categories = cats, CategoryId = categoryId, CinemaName = CinemaDAL.GetById(cid)?.Name ?? string.Empty,
            Items = drinks.Where(d => categoryId == null || d.CategoryId == categoryId)
                .Select(d => (new FoodCardVm(d.Id, d.Name, d.Image, d.Price, portions[d.Id] <= 0), d.CategoryId, d.Description)).ToList(),
        };
    }

    /// <summary>Món bán online của rạp (bước 3 của C3) kèm số lượng đã có trong giỏ.</summary>
    public List<FoodCardVm> BookingDrinks(int cinemaId, Cart? cart)
    {
        var drinks = DrinkDAL.GetAll().Where(d => d.Active).OrderBy(d => d.IsCombo ? 0 : 1).ThenBy(d => d.Price).ToList();
        var portions = _inventory.Portions(cinemaId, drinks);
        return drinks.Select(d => new FoodCardVm(d.Id, d.Name, d.Image, d.Price, portions[d.Id] <= 0, cart?.Items.FirstOrDefault(i => i.DrinkId == d.Id)?.Quantity ?? 0)).ToList();
    }

    /// <summary>Số phần còn bán của từng món (cập nhật "Tạm hết" sau khi giỏ đổi).</summary>
    public Dictionary<int, int> Portions(int cinemaId) => _inventory.Portions(cinemaId, DrinkDAL.GetAll().Where(d => d.Active));

    public static Movie? ActiveMovie(int id) => MovieDAL.GetById(id) is { Active: true } m ? m : null;

    public static BusinessRuleException ShowtimeNotFound() => CategoryService.NotFound("Không tìm thấy suất chiếu.");
}
