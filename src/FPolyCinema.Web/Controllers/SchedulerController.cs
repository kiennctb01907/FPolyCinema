using System.Globalization;
using System.Text.Json;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FPolyCinema.Web.Controllers;

/// <summary>
/// A5. Lịch chiếu /scheduler, chế độ Bảng theo ngày và form suất chiếu (UC34, SPEC 3.3, 8.3): mọi kiểm tra qua ScheduleConstraintChecker
/// và AgeRules; form kiểm tra trực tiếp mỗi khi đổi phòng, phim, giờ, ngôn ngữ; lưu thì sinh ShowtimeSeats. Chỉ quản lý rạp (xếp lịch).
/// Phase 7 (SPEC 4.3, UC35): chế độ Gantt có kéo thả và kiểm tra tức thì (POST /scheduler/validate), nút "AI tự động xếp lịch"
/// (POST /scheduler/ai/generate trả bản nháp) và "Áp dụng" (POST /scheduler/ai/apply lưu IsAiGenerated = 1).
/// </summary>
[Authorize(Policy = Policies.ManagerOnly)]
[Route("scheduler")]
public class SchedulerController : AppControllerBase
{
    private readonly ScheduleService _service;
    private readonly CinemaContextService _cinemas;
    private readonly IClock _clock;
    private readonly AiSchedulerService _ai;
    private readonly CinemaRules _rules;

    public SchedulerController(ScheduleService service, CinemaContextService cinemas, IClock clock, AiSchedulerService ai, CinemaRules rules)
    {
        _service = service;
        _ai = ai;
        _rules = rules;
        _cinemas = cinemas;
        _clock = clock;
    }

    private int PickCinema(int? cinemaId) =>
        cinemaId is int c && _cinemas.Cinemas.Any(x => x.Id == c) ? c : _cinemas.CurrentCinemaId ?? _cinemas.Cinemas.FirstOrDefault()?.Id ?? 0;

    /// <summary>Ngày vận hành hiện tại (trước 06:00 vẫn là ngày hôm trước).</summary>
    private DateTime OperatingToday => PricingCalculator.OperatingDate(_clock.Now);

    private static DateTime? ParseDate(string? date) =>
        DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.Date : null;

    [HttpGet("")]
    public IActionResult Index(int? cinemaId, string? date, string? view)
    {
        var id = PickCinema(cinemaId);
        var day = ParseDate(date) ?? OperatingToday;
        var schedule = _service.Day(id, day, _clock.Now);
        var mode = view == "gantt" ? "gantt" : "table";
        return View(new SchedulerVm
        {
            Cinemas = _cinemas.Cinemas.ToList(), CinemaId = id, Date = day, Today = OperatingToday, Day = schedule,
            Ai7 = new SchedulerPhase7Vm { View = mode, Ai = _ai.Choices(id, day < OperatingToday ? OperatingToday : day, 1), Gantt = GanttData(schedule) },
        });
    }

    /// <summary>Dữ liệu Gantt (UI A5-05 → A5-07): trục 08:00–02:00, các phòng, các khối suất (đã khóa, lỗi, cảnh báo), mốc quảng cáo và dọn phòng.</summary>
    private object GanttData(ScheduleDay day)
    {
        static string Iso(DateTime t) => t.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
        var axisStart = day.Date + _rules.OperatingDayStart;
        return new
        {
            cinemaId = day.CinemaId, date = day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), axisStart = Iso(axisStart),
            axisEnd = Iso(day.Date.AddDays(1).AddHours(2)), now = Iso(_clock.Now), roundMinutes = _rules.ScheduleRoundMinutes,
            rooms = day.Auditoriums.Where(g => g.Auditorium.Active).Select(g => new { id = g.Auditorium.Id, name = g.Auditorium.Name, format = g.Auditorium.Format, capacity = g.Capacity }),
            showtimes = day.Auditoriums.SelectMany(g => g.Showtimes).Where(s => s.Row.Showtime.OperationalStatus != OperationalStatus.Cancelled).Select(s => new
            {
                id = s.Row.Showtime.Id, movieId = s.Row.Showtime.MovieId, title = s.Row.MovieTitle, ageRating = s.Row.AgeRating, isVietnamese = s.Row.IsVietnameseFilm,
                auditoriumId = s.Row.Showtime.AuditoriumId, start = Iso(s.Row.Showtime.StartTime), feature = Iso(s.Row.Showtime.FeatureStartTime),
                end = Iso(s.Row.Showtime.EndTime), cleaningEnd = Iso(s.Row.Showtime.CleaningEndTime), language = s.Row.Showtime.LanguageFormat,
                languageLabel = StatusDisplay.Of(StatusDisplay.Language, s.Row.Showtime.LanguageFormat).Label, locked = s.IsLocked,
                started = s.Row.Showtime.StartTime <= _clock.Now, isAi = s.Row.Showtime.IsAiGenerated, issues = ScheduleService.IssuesJson(s.Result),
            }),
            share = new { dayVn = day.DayVietnamese, dayTotal = day.DayTotal, weekVn = day.WeekVietnamese, weekTotal = day.WeekTotal },
        };
    }

    [HttpGet("create")]
    public IActionResult Create(int? cinemaId, string? date, int? auditoriumId)
    {
        var id = PickCinema(cinemaId);
        var day = ParseDate(date) ?? OperatingToday;
        // Giờ gợi ý: 09:00; nếu là hôm nay thì giờ tròn kế tiếp (không quá 23:00)
        int hour = day == _clock.Today ? Math.Clamp(_clock.Now.Hour + 1, 9, 23) : 9;
        return FormView(new ShowtimeFormVm { CinemaId = id, AuditoriumId = auditoriumId ?? 0, StartTime = day.AddHours(hour), BackDate = day });
    }

    [HttpGet("{id:int}/edit")]
    public IActionResult Edit(int id)
    {
        try
        {
            var row = _service.GetRow(id, _clock.Now);
            if (_cinemas.Cinemas.All(c => c.Id != row.CinemaId)) return NotFoundPage();
            var s = row.Showtime;
            return FormView(new ShowtimeFormVm
            {
                Id = s.Id, CinemaId = row.CinemaId, AuditoriumId = s.AuditoriumId, MovieId = s.MovieId, StartTime = s.StartTime, LanguageFormat = s.LanguageFormat,
                IsLocked = ShowtimeLock.IsLocked(row.BookedSeats, row.ActiveHeldSeats), BookedSeats = row.BookedSeats, BackDate = PricingCalculator.OperatingDate(s.StartTime),
            });
        }
        catch (BusinessRuleException ex) when (ex.Code == ErrorCodes.NotFound) { return NotFoundPage(); }
    }

    private IActionResult FormView(ShowtimeFormVm vm)
    {
        vm.CinemaName = _cinemas.Cinemas.FirstOrDefault(c => c.Id == vm.CinemaId)?.Name ?? string.Empty;
        vm.Auditoriums = _service.AuditoriumOptions(vm.CinemaId, vm.Id == null ? null : vm.AuditoriumId);
        vm.Movies = _service.MovieOptions(vm.Id == null ? null : vm.MovieId);
        return View("Form", vm);
    }

    private object CheckJson(ShowtimeCheck c) => new
    {
        times = c.Times == null ? null : new
        {
            start = Fmt.Time(c.Times.StartTime), feature = Fmt.Time(c.Times.FeatureStartTime), end = AgeRules.EndText(c.Times.StartTime, c.Times.EndTime),
            cleaningEnd = Fmt.Time(c.Times.CleaningEndTime), date = Fmt.WeekdayDate(c.Times.StartTime),
            duration = c.Movie == null ? null : Fmt.Duration(c.Movie.DurationMinutes),
        },
        ageRating = c.Movie?.AgeRating,
        ageExplanation = c.Movie == null ? null : StatusDisplay.AgeExplanation(c.Movie.AgeRating),
        languages = c.Movie == null ? Array.Empty<string>() : CsvList.Split(c.Movie.LanguageFormats),
        hasErrors = c.Result.HasErrors,
        issues = ScheduleService.IssuesJson(c.Result),
    };

    /// <summary>Kiểm tra suất chưa lưu (AJAX): các mốc giờ và danh sách lỗi chặn, cảnh báo.</summary>
    [HttpPost("check")]
    public IActionResult Check(int? id, int auditoriumId, int movieId, DateTime? startTime, string? languageFormat) =>
        ApiOk(CheckJson(_service.Check(id, new ShowtimeInput(auditoriumId, movieId, startTime, languageFormat), _clock.Now)));

    /// <summary>Lưu suất (AJAX). Có lỗi chặn thì trả mã lỗi của lỗi đầu tiên (SHOWTIME_OVERLAP, KDM_INVALID…) kèm details.issues.</summary>
    [HttpPost("save")]
    public IActionResult Save(int? id, int auditoriumId, int movieId, DateTime? startTime, string? languageFormat)
    {
        var input = new ShowtimeInput(auditoriumId, movieId, startTime, languageFormat);
        var savedId = _service.Save(id, input, _clock.Now);
        var row = _service.GetRow(savedId, _clock.Now);
        Toast("success", id == null ? $"Đã thêm suất {Fmt.Time(row.Showtime.StartTime)} {row.AuditoriumName}." : "Đã lưu thay đổi");
        var day = PricingCalculator.OperatingDate(row.Showtime.StartTime).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return ApiOk(new { id = savedId, redirect = $"/scheduler?cinemaId={row.CinemaId}&date={day}" });
    }

    /// <summary>Một suất chưa lưu đang hiện trên Gantt (bản nháp AI hoặc suất đã kéo mà chưa lưu), gửi kèm khi kiểm tra.</summary>
    public sealed record PendingSlot(int? Id, int AuditoriumId, int MovieId, DateTime StartTime, string? LanguageFormat);

    private static List<T> ParseJsonList<T>(string? json, string field)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<T>();
        try { return JsonSerializer.Deserialize<List<T>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new List<T>(); }
        catch (JsonException) { throw FieldErrors.Of(field, "Dữ liệu suất gửi lên không hợp lệ."); }
    }

    /// <summary>Đổi suất chưa lưu thành ScheduleSlot (tính lại 4 mốc thời gian theo thời lượng phim) để kiểm chồng giờ và lệch giờ.</summary>
    private List<ScheduleSlot> PendingSlots(IEnumerable<PendingSlot> pending) => pending.Select(p =>
    {
        var check = _service.Check(p.Id, new ShowtimeInput(p.AuditoriumId, p.MovieId, p.StartTime, p.LanguageFormat), _clock.Now);
        return check.Times == null || check.Auditorium == null ? null
            : new ScheduleSlot(p.Id, p.MovieId, p.AuditoriumId, check.Auditorium.Name, check.Times.StartTime, check.Times.FeatureStartTime, check.Times.EndTime,
                check.Times.CleaningEndTime, p.LanguageFormat ?? string.Empty);
    }).OfType<ScheduleSlot>().ToList();

    /// <summary>
    /// Kiểm tra tức thì khi thả một khối trên Gantt (SPEC 4.3-10): dùng ScheduleConstraintChecker như form; pending là các suất chưa lưu khác trên Gantt
    /// (JSON [{id, auditoriumId, movieId, startTime, languageFormat}]). Trả các mốc giờ đã làm tròn, lỗi chặn (viền đỏ) và cảnh báo (viền vàng).
    /// </summary>
    [HttpPost("validate")]
    public IActionResult Validate(int? id, int auditoriumId, int movieId, DateTime? startTime, string? languageFormat, string? pending)
    {
        var slots = PendingSlots(ParseJsonList<PendingSlot>(pending, "pending"));
        var c = _service.Check(id, new ShowtimeInput(auditoriumId, movieId, startTime, languageFormat), _clock.Now, slots);
        static string Iso(DateTime t) => t.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
        return ApiOk(new
        {
            times = c.Times == null ? null : new { start = Iso(c.Times.StartTime), feature = Iso(c.Times.FeatureStartTime), end = Iso(c.Times.EndTime), cleaningEnd = Iso(c.Times.CleaningEndTime) },
            hasErrors = c.Result.HasErrors,
            // Tỷ lệ phim Việt cả năm hiện ở chân trang, không tô viền từng khối
            issues = ScheduleService.IssuesJson(new ScheduleCheckResult(c.Result.Issues.Where(i => i.Code != ScheduleWarningCode.VietnameseShareLow).ToList())),
        });
    }

    /// <summary>
    /// "AI tự động xếp lịch" (SPEC 4.3-01 → 4.3-08): tính lại AiDemandScore (lưu vào Movies), chạy thuật toán tham lam và trả bản nháp (chưa lưu).
    /// </summary>
    [HttpPost("ai/generate")]
    public IActionResult AiGenerate(int? cinemaId, string? fromDate, int days, [FromForm] List<int>? movieIds, [FromForm] List<int>? auditoriumIds)
    {
        var id = PickCinema(cinemaId);
        var from = ParseDate(fromDate) ?? throw FieldErrors.Of("fromDate", "Bạn chọn ngày bắt đầu.");
        var (result, scores) = _ai.Generate(id, from, days, movieIds ?? new List<int>(), auditoriumIds ?? new List<int>(), _clock.Now);
        static string Iso(DateTime t) => t.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
        var drafts = result.Drafts.Select((d, i) => new
        {
            key = $"ai-{i}", movieId = d.MovieId, title = d.MovieTitle, ageRating = d.AgeRating, isVietnamese = d.IsVietnamese, auditoriumId = d.AuditoriumId,
            date = PricingCalculator.OperatingDate(d.StartTime).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            start = Iso(d.StartTime), feature = Iso(d.FeatureStartTime), end = Iso(d.EndTime), cleaningEnd = Iso(d.CleaningEndTime),
            language = d.LanguageFormat, languageLabel = StatusDisplay.Of(StatusDisplay.Language, d.LanguageFormat).Label,
            issues = ScheduleService.IssuesJson(new ScheduleCheckResult(d.Warnings.Where(w => w.Code != ScheduleWarningCode.VietnameseShareLow).ToList())),
        }).ToList();
        int draftVn = result.Drafts.Count(d => d.IsVietnamese);
        return ApiOk(new
        {
            cinemaId = id, fromDate = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), days, drafts,
            draftCount = drafts.Count, draftVietnamese = draftVn,
            share = VietnameseFilmQuota.Percent(VietnameseFilmQuota.Share(result.VietnameseCount, result.TotalCount)),
            target = VietnameseFilmQuota.Percent(_rules.VietnameseFilmTargetShare),
            notes = result.Notes,
            scores = scores.Select(kv => new { movieId = kv.Key, score = kv.Value }),
        });
    }

    /// <summary>
    /// Danh sách phim của hộp "AI tự động xếp lịch" lọc lại theo khoảng ngày đang chọn (SPEC 4.3-02): phim có giấy phép, khác C,
    /// KDM còn hiệu lực trong khoảng; phim không xếp được kèm lý do.
    /// </summary>
    [HttpGet("ai/choices")]
    public IActionResult AiChoices(int? cinemaId, string? fromDate, int days = 1)
    {
        var id = PickCinema(cinemaId);
        var from = ParseDate(fromDate) ?? OperatingToday;
        var c = _ai.Choices(id, from, Math.Clamp(days, 1, AiSchedulerService.MaxDays));
        return ApiOk(c.Movies.Select(o => new
        {
            id = o.Movie.Id, title = o.Movie.Title, ageRating = o.Movie.AgeRating, ageExplanation = StatusDisplay.AgeExplanation(o.Movie.AgeRating),
            isVietnamese = o.Movie.IsVietnameseFilm, eligible = o.Eligible,
            meta = o.Eligible ? (o.Movie.AiDemandScore is decimal sc ? $"AiDemandScore lần trước {Fmt.Decimal(sc)}/10" : "Chưa có AiDemandScore") : o.Reason,
        }));
    }

    /// <summary>"Áp dụng" bản nháp AI: lưu mọi suất nháp (IsAiGenerated = 1) trong một transaction; có lỗi chặn thì không lưu suất nào.</summary>
    [HttpPost("ai/apply")]
    public IActionResult AiApply(int? cinemaId, string? drafts)
    {
        var id = PickCinema(cinemaId);
        var list = ParseJsonList<PendingSlot>(drafts, "drafts");
        var ids = _service.ApplyAiDrafts(list.Select(d => new ShowtimeInput(d.AuditoriumId, d.MovieId, d.StartTime, d.LanguageFormat)).ToList(), id, _clock.Now);
        Toast("success", $"Đã áp dụng {ids.Count} suất do AI xếp.");
        var first = list.Min(d => d.StartTime);
        return ApiOk(new { ids, redirect = $"/scheduler?cinemaId={id}&date={PricingCalculator.OperatingDate(first):yyyy-MM-dd}&view=gantt" });
    }

    [HttpGet("{id:int}/delete-check")]
    public IActionResult DeleteCheck(int id)
    {
        var d = _service.CheckDelete(id, _clock.Now);
        return ApiOk(new { allowed = d.Allowed, reason = d.Reason, canDeactivate = d.CanDeactivate, consequences = d.Consequences });
    }

    /// <summary>Xóa suất chưa bán vé; suất đã bán vé dùng Hủy suất do sự cố.</summary>
    [HttpPost("{id:int}/delete")]
    public IActionResult Delete(int id)
    {
        _service.Delete(id, _clock.Now);
        return ApiOk(new { id });
    }
}
