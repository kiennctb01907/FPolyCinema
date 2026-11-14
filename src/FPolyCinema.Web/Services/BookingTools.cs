using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Web.Services;

/// <summary>Tham số của search_showtimes (SPEC 4.2-03). Dates rỗng là từ bây giờ tới 7 ngày tới.</summary>
public sealed record ShowtimeSearch(
    IReadOnlyList<DateTime> Dates,
    TimeSpan? TimeFrom = null,
    TimeSpan? TimeTo = null,
    string? Genre = null,
    string? LanguageFormat = null,
    string? Format = null,
    int? MovieId = null,
    string? MovieQuery = null,
    int? CinemaId = null,
    int? Quantity = null,
    DateTime? After = null);

/// <summary>Một suất tìm được (kết quả search_showtimes).</summary>
public sealed record AgentShowtime(int ShowtimeId, int MovieId, string MovieTitle, string AgeRating, int CinemaId, string CinemaName, string AuditoriumName,
    string Format, string LanguageFormat, DateTime StartTime, DateTime EndTime, int FreeSeats)
{
    /// <summary>Chữ gọn cho chip, ví dụ "T7 15/11 19:15 · Tên phim · 2D Phụ đề".</summary>
    public string Label => $"{Fmt.WeekdayDate(StartTime)} {Fmt.Time(StartTime)} · {MovieTitle} · {Format} {StatusDisplay.Of(StatusDisplay.Language, LanguageFormat).Label}";
}

/// <summary>Một ghế trong phương án get_best_seats.</summary>
public sealed record AgentSeat(int SeatId, string Label, string SeatType, int Score, long Price);

/// <summary>Phương án cụm ghế tốt nhất (get_best_seats, SPEC 4.2-04).</summary>
public sealed record AgentSeatPick(int ShowtimeId, string Persona, List<AgentSeat> Seats, string Range, int Score);

/// <summary>Một món bắp nước trong thẻ đặt vé (find_combo, SPEC 4.2-05).</summary>
public sealed record AgentItem(int DrinkId, string Name, int Quantity, long Price)
{
    public long Amount => Price * Quantity;
}

/// <summary>Kết quả check_rules (SPEC 4.2-06): Ok = false khi khách không được mua (theo ngày sinh); Notes là các lưu ý độ tuổi và giờ chiếu cho trẻ em.</summary>
public sealed record AgentRuleCheck(bool Ok, string? Message, List<string> Notes);

/// <summary>Một ô của sơ đồ thu nhỏ quanh cụm ghế: Col, nhãn, trạng thái (selected, free, taken).</summary>
public sealed record MiniSeat(int Col, string Label, string State);

/// <summary>Một hàng của sơ đồ thu nhỏ.</summary>
public sealed record MiniRow(string Name, List<MiniSeat> Seats);

/// <summary>Thẻ đặt vé trong khung chat (build_preview, SPEC 4.2-07; UI C7-04). Chỉ là bản xem trước: chưa giữ ghế, chưa thanh toán.</summary>
public sealed record AgentCard(
    int ShowtimeId, int MovieId, string Title, string AgeRating, string AgeExplanation, string Poster, string Cinema, string Room,
    string Date, string Time, string Format, string Language, DateTime StartTime, string Persona, int Quantity,
    List<AgentSeat> Seats, string Range, int Score, List<AgentItem> Items, long TicketTotal, long Total,
    int Cols, List<MiniRow> Map, List<string> Notes)
{
    public string BookingUrl => $"/booking/{ShowtimeId}?persona={Persona}&qty={Quantity}";
}

/// <summary>Từ điển cho NlpParser: phim đang chiếu và món đang bán.</summary>
public sealed record AgentCatalog(List<NlpCatalogItem> Movies, List<NlpCatalogItem> Drinks);

/// <summary>
/// Bộ công cụ dùng chung của LocalAgent và LlmAgent (SPEC 4.2-03 → 4.2-07). Không có công cụ giữ ghế hay thanh toán (SPEC 4.2-08):
/// việc đó chỉ xảy ra khi khách bấm "Giữ ghế &amp; thanh toán ngay" trên thẻ đặt vé.
/// </summary>
public interface IBookingTools
{
    AgentCatalog Catalog(int? cinemaId);
    DateTime Now { get; }
    TimeSpan DayStart { get; }
    List<AgentShowtime> SearchShowtimes(ShowtimeSearch q);
    AgentSeatPick? GetBestSeats(int showtimeId, int quantity, string? persona);
    AgentItem? FindCombo(string query, int quantity, int? cinemaId);
    AgentRuleCheck CheckRules(int showtimeId, int quantity, int? userId, bool forChild);
    AgentCard? BuildPreview(int showtimeId, IReadOnlyList<int> seatIds, IReadOnlyList<AgentItem> items, string? persona, IReadOnlyList<string>? notes = null);
}

/// <summary>Cài đặt bộ công cụ trên DAL và các service đặt vé sẵn có (SeatSuggestService, TicketPricer, OnlineAgeRules, InventoryService).</summary>
public class BookingTools : IBookingTools
{
    private readonly IClock _clock;
    private readonly CinemaRules _rules;
    private readonly SeatSuggestService _suggest;
    private readonly InventoryService _inventory;

    public BookingTools(IClock clock, CinemaRules rules, SeatSuggestService suggest, InventoryService inventory)
    {
        _clock = clock;
        _rules = rules;
        _suggest = suggest;
        _inventory = inventory;
    }

    public DateTime Now => _clock.Now;
    public TimeSpan DayStart => _rules.OperatingDayStart;

    private List<Movie>? _movies;
    private List<Movie> Movies => _movies ??= MovieDAL.GetActiveByTitle().Where(m => m.AgeRating != AgeRating.C).ToList();

    public AgentCatalog Catalog(int? cinemaId) => new(
        Movies.Select(m => new NlpCatalogItem(m.Id, m.Title, m.OriginalTitle)).ToList(),
        AvailableDrinks(cinemaId).Select(d => new NlpCatalogItem(d.Id, d.Name)).ToList());

    private List<Drink> AvailableDrinks(int? cinemaId)
    {
        var drinks = DrinkDAL.GetAll().Where(d => d.Active && !d.IsSoldOut).ToList();
        if (cinemaId is not int cid) return drinks;
        var portions = _inventory.Portions(cid, drinks);
        return drinks.Where(d => portions.TryGetValue(d.Id, out var p) && p > 0).ToList();
    }

    /// <summary>
    /// search_showtimes: suất bán được trên web khách (chưa hủy, không thuê trọn, còn trong giờ bán online), lọc theo ngày vận hành, khung giờ bắt đầu,
    /// thể loại, bản ngôn ngữ, định dạng phòng, phim (Id hoặc tên gần đúng), rạp và số ghế trống tối thiểu; xếp theo giờ bắt đầu.
    /// </summary>
    public List<AgentShowtime> SearchShowtimes(ShowtimeSearch q)
    {
        var now = _clock.Now;
        var ranges = q.Dates.Count == 0
            ? new List<(DateTime From, DateTime To)> { (q.After ?? now, (q.After ?? now).AddDays(7)) }
            : q.Dates.Select(ScheduleService.OperatingRange).ToList();
        int? movieId = q.MovieId;
        if (movieId == null && !string.IsNullOrWhiteSpace(q.MovieQuery))
        {
            var key = NlpParser.Normalize(q.MovieQuery);
            movieId = Movies.Select(m => (m, s: Math.Max(NlpParser.Similarity(NlpParser.Normalize(m.Title), key),
                    NlpParser.Normalize(m.Title).Contains(key) || NlpParser.Normalize(m.OriginalTitle).Contains(key) ? 1 : 0)))
                .Where(x => x.s >= 0.6).OrderByDescending(x => x.s).Select(x => (int?)x.m.Id).FirstOrDefault() ?? -1;
        }
        var genres = Movies.ToDictionary(m => m.Id, m => CsvList.Split(m.Genres));
        var list = new List<AgentShowtime>();
        foreach (var (from, to) in ranges)
        {
            var rows = ShowtimeDAL.ForCustomer(q.CinemaId, movieId, from, to, now);
            foreach (var r in rows)
            {
                if (!genres.TryGetValue(r.MovieId, out var g)) continue;
                if (!SeatHoldRules.IsSalesOpen(r.FeatureStartTime, r.OperationalStatus, BillChannel.Online, now, _rules)) continue;
                if (q.After is DateTime after && r.StartTime <= after) continue;
                var tod = r.StartTime.TimeOfDay;
                if (q.TimeFrom is TimeSpan tf && tod < tf) continue;
                if (q.TimeTo is TimeSpan tt && tod > tt) continue;
                if (q.Genre != null && !g.Contains(q.Genre)) continue;
                if (q.LanguageFormat != null && r.LanguageFormat != q.LanguageFormat) continue;
                if (q.Format != null && r.AuditoriumFormat != q.Format) continue;
                if (q.Quantity is int qty && r.FreeSeats < qty) continue;
                list.Add(new AgentShowtime(r.ShowtimeId, r.MovieId, r.MovieTitle, r.AgeRating, r.CinemaId, r.CinemaName, r.AuditoriumName, r.AuditoriumFormat,
                    r.LanguageFormat, r.StartTime, r.EndTime, r.FreeSeats));
            }
        }
        return list.DistinctBy(s => s.ShowtimeId).OrderBy(s => s.StartTime).ThenBy(s => s.CinemaName).ToList();
    }

    /// <summary>get_best_seats: cụm ghế tốt nhất theo persona bằng AI Seat Scoring (SeatSuggestService, SPEC 4.1), kèm giá vé người lớn từng ghế.</summary>
    public AgentSeatPick? GetBestSeats(int showtimeId, int quantity, string? persona)
    {
        var info = OnlineBookingService.OnlineInfo(showtimeId);
        if (info == null) return null;
        var s = _suggest.Suggest(showtimeId, persona, quantity, null);
        var best = s.Clusters.FirstOrDefault();
        if (best == null) return null;
        var pricer = new TicketPricer(info, _rules);
        var types = ShowtimeSeatDAL.GetStates(showtimeId).ToDictionary(x => x.SeatId, x => x.SeatType);
        var seats = best.SeatIds.Select((id, i) => new AgentSeat(id, best.Labels[i], types[id], s.Scores.TryGetValue(id, out var sc) ? sc : 0,
            pricer.Price(types[id], AudienceType.Adult))).ToList();
        return new AgentSeatPick(showtimeId, s.Persona, seats, best.Range, best.Score);
    }

    /// <summary>
    /// find_combo: món đang bán (còn nguyên liệu ở rạp) khớp gần đúng, không dấu với query; query chung "combo" thì lấy combo rẻ nhất.
    /// </summary>
    public AgentItem? FindCombo(string query, int quantity, int? cinemaId)
    {
        var drinks = AvailableDrinks(cinemaId);
        var key = NlpParser.Normalize(query);
        if (key.Length == 0 || drinks.Count == 0) return null;
        Drink? pick;
        if (key == "combo") pick = drinks.Where(d => d.IsCombo).OrderBy(d => d.Price).FirstOrDefault();
        else
        {
            pick = drinks.FirstOrDefault(d => NlpParser.Normalize(d.Name) == key)
                   ?? drinks.Select(d => (d, s: NlpParser.Normalize(d.Name).Contains(key) ? 0.95 : NlpParser.Similarity(NlpParser.Normalize(System.Text.RegularExpressions.Regex.Replace(d.Name, @"\(.*?\)", " ")), key)))
                       .Where(x => x.s >= 0.7).OrderByDescending(x => x.s).ThenByDescending(x => x.d.IsCombo).Select(x => x.d).FirstOrDefault();
        }
        return pick == null ? null : new AgentItem(pick.Id, pick.Name, Math.Clamp(quantity, 1, 20), pick.Price);
    }

    /// <summary>
    /// check_rules: luật độ tuổi và giờ chiếu cho trẻ em (SPEC 3.1, OnlineAgeRules): khách có ngày sinh mà chưa đủ tuổi thì không được mua;
    /// các câu xác nhận sẽ phải tích ở bước thanh toán và lời nhắc suất kết thúc muộn được đưa vào Notes.
    /// </summary>
    public AgentRuleCheck CheckRules(int showtimeId, int quantity, int? userId, bool forChild)
    {
        var info = OnlineBookingService.OnlineInfo(showtimeId);
        if (info == null) return new AgentRuleCheck(false, "Không tìm thấy suất chiếu.", new List<string>());
        var m = info.Movie;
        var s = info.Showtime;
        var notes = new List<string>();
        if (userId is int uid && UserDAL.GetById(uid)?.BirthDate is DateTime bd && !AgeRules.CanWatch(m.AgeRating, AgeRules.AgeOn(bd, _clock.Now)))
            return new AgentRuleCheck(false, $"Phim {m.AgeRating} dành cho khán giả từ đủ {AgeRules.MinimumAge(m.AgeRating)} tuổi. Theo ngày sinh trong tài khoản, bạn chưa đủ tuổi xem phim này.", notes);
        if (forChild && AgeRules.MinimumAge(m.AgeRating) >= 13)
            return new AgentRuleCheck(false, $"Phim {m.AgeRating} không dành cho trẻ em: {StatusDisplay.AgeExplanation(m.AgeRating)}", notes);
        var confirm = OnlineAgeRules.Confirmations(m.AgeRating, s.StartTime, s.EndTime, _rules.ChildU13LatestEnd, _rules.ChildU16LatestEnd);
        if (confirm.Count > 0) notes.Add($"Ở bước thanh toán bạn cần xác nhận: {string.Join(" ", confirm.Select(c => c.Text))}");
        var curfew = OnlineAgeRules.CurfewNotice(s.StartTime, s.EndTime, _rules.ChildU13LatestEnd, _rules.ChildU16LatestEnd);
        if (curfew != null) notes.Add(curfew);
        return new AgentRuleCheck(true, null, notes);
    }

    /// <summary>
    /// build_preview: dựng thẻ đặt vé (UI C7-04) từ suất, ghế đã chọn và món: poster, phim, nhãn tuổi, rạp, phòng, giờ, định dạng, sơ đồ thu nhỏ quanh cụm ghế,
    /// danh sách ghế kèm điểm và giá, món, tổng tiền tạm tính. Không giữ ghế.
    /// </summary>
    public AgentCard? BuildPreview(int showtimeId, IReadOnlyList<int> seatIds, IReadOnlyList<AgentItem> items, string? persona, IReadOnlyList<string>? notes = null)
    {
        var info = OnlineBookingService.OnlineInfo(showtimeId);
        if (info == null || seatIds.Count == 0) return null;
        persona = SeatPersona.All.Contains(persona) ? persona! : SeatPersona.BestViewAudio;
        var now = _clock.Now;
        var states = ShowtimeSeatDAL.GetStates(showtimeId);
        var byId = states.ToDictionary(x => x.SeatId);
        // Chỉ dựng thẻ với ghế còn trống (mô hình có thể truyền mã ghế sai hoặc ghế vừa có người giữ)
        if (seatIds.Any(id => !byId.TryGetValue(id, out var st) || SeatMapService.Effective(st, now) != SeatDisplayStatus.Available)) return null;
        var aud = info.Auditorium;
        var clusterSeats = states.Select(x => new ClusterSeat(x.SeatId, x.RowIndex, x.ColIndex, x.Label, x.SeatType, x.PairGroupId,
            SeatMapService.Effective(x, now) == SeatDisplayStatus.Available)).ToList();
        var scores = ClusterFinder.ScoreAll(clusterSeats, aud.TotalRows, aud.TotalCols, aud.IdealRowRatio, persona);
        var pricer = new TicketPricer(info, _rules);
        var seats = seatIds.Select(id => byId[id]).OrderBy(x => x.RowIndex).ThenBy(x => x.ColIndex)
            .Select(x => new AgentSeat(x.SeatId, x.Label, x.SeatType, scores.TryGetValue(x.SeatId, out var sc) ? sc : 0, pricer.Price(x.SeatType, AudienceType.Adult))).ToList();
        int score = (int)Math.Round(seats.Average(x => (double)x.Score));
        var range = seats.Count == 1 ? seats[0].Label : $"{seats[0].Label}–{seats[^1].Label}";

        // Sơ đồ thu nhỏ: cụm ghế và một hàng trước, một hàng sau (hàng gần màn hình ở trên, như _SeatMap)
        int r0 = Math.Max(1, seatIds.Min(id => byId[id].RowIndex) - 1), r1 = Math.Min(aud.TotalRows, seatIds.Max(id => byId[id].RowIndex) + 1);
        var chosen = seatIds.ToHashSet();
        var map = states.Where(x => x.RowIndex >= r0 && x.RowIndex <= r1).GroupBy(x => x.RowIndex).OrderBy(g => g.Key)
            .Select(g => new MiniRow(SeatLabeler.RowLetter(g.Key).ToString(), g.OrderBy(x => x.ColIndex).Select(x => new MiniSeat(x.ColIndex, x.Label,
                chosen.Contains(x.SeatId) ? "selected" : SeatMapService.Effective(x, now) == SeatDisplayStatus.Available ? "free" : "taken")).ToList()))
            .ToList();

        long ticketTotal = seats.Sum(x => x.Price);
        var m = info.Movie;
        return new AgentCard(showtimeId, m.Id, m.Title, m.AgeRating, StatusDisplay.AgeExplanation(m.AgeRating), m.PosterUrl ?? "/img/placeholder-poster.svg",
            CinemaDAL.GetById(aud.CinemaId)?.Name ?? string.Empty, aud.Name, Fmt.WeekdayDate(info.Showtime.StartTime), Fmt.Time(info.Showtime.StartTime),
            aud.Format, StatusDisplay.Of(StatusDisplay.Language, info.Showtime.LanguageFormat).Label, info.Showtime.StartTime, persona, seats.Count,
            seats, range, score, items.ToList(), ticketTotal, ticketTotal + items.Sum(i => i.Amount), aud.TotalCols, map, (notes ?? Array.Empty<string>()).ToList());
    }
}
