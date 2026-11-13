using System.Globalization;
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
/// TODO(Phase 7): nút chuyển chế độ Gantt và "AI tự động xếp lịch" (UI A5-03).
/// </summary>
[Authorize(Policy = Policies.ManagerOnly)]
[Route("scheduler")]
public class SchedulerController : AppControllerBase
{
    private readonly ScheduleService _service;
    private readonly CinemaContextService _cinemas;
    private readonly IClock _clock;

    public SchedulerController(ScheduleService service, CinemaContextService cinemas, IClock clock)
    {
        _service = service;
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
    public IActionResult Index(int? cinemaId, string? date)
    {
        var id = PickCinema(cinemaId);
        var day = ParseDate(date) ?? OperatingToday;
        return View(new SchedulerVm { Cinemas = _cinemas.Cinemas.ToList(), CinemaId = id, Date = day, Today = OperatingToday, Day = _service.Day(id, day, _clock.Now) });
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
