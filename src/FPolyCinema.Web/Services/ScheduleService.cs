using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Entities.ReadModels;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Utils;

namespace FPolyCinema.Web.Services;

/// <summary>Dữ liệu form suất chiếu: phòng, phim, giờ bắt đầu, bản ngôn ngữ.</summary>
public sealed record ShowtimeInput(int AuditoriumId, int MovieId, DateTime? StartTime, string? LanguageFormat);

/// <summary>Kết quả kiểm tra một suất: các mốc thời gian đã tính (null khi thiếu dữ liệu) và các lỗi chặn, cảnh báo.</summary>
public sealed record ShowtimeCheck(ShowtimeTimes? Times, ScheduleCheckResult Result, Auditorium? Auditorium, Movie? Movie);

/// <summary>Một suất trong lịch theo ngày, kèm cờ LOCKED_HAS_TICKETS và kết quả kiểm tra hiện tại.</summary>
public sealed record ScheduledShowtime(ShowtimeRow Row, bool IsLocked, ScheduleCheckResult Result);

/// <summary>Một nhóm phòng của chế độ Bảng: phòng, sức chứa bán được, các suất theo giờ.</summary>
public sealed record AuditoriumSchedule(Auditorium Auditorium, int Capacity, List<ScheduledShowtime> Showtimes);

/// <summary>
/// Lịch chiếu một ngày vận hành của một rạp (UI A5, chế độ Bảng) và tỷ lệ phim Việt: trong ngày, trong khung ưu tiên 18:00–22:00,
/// và lũy kế cả năm của rạp (SPEC 3.1 mục 6).
/// </summary>
public sealed record ScheduleDay(int CinemaId, DateTime Date, List<AuditoriumSchedule> Auditoriums, int DayVietnamese, int DayTotal,
    int PriorityVietnamese, int PriorityTotal, int YearVietnamese, int YearTotal, int WeekVietnamese = 0, int WeekTotal = 0);

/// <summary>
/// Quản lý suất chiếu (UC34, SPEC 3.3, UI A5): mọi kiểm tra đi qua ScheduleConstraintChecker và AgeRules (logic thuần);
/// service chỉ nạp dữ liệu quanh suất, kiểm lại chồng giờ trong transaction có khóa khi lưu, và sinh ShowtimeSeats cho mọi ghế
/// (AVAILABLE hoặc HOUSE_HOLD với ghế nội bộ) khi tạo suất.
/// </summary>
public class ScheduleService
{
    private readonly CinemaRules _rules;

    public ScheduleService(CinemaRules rules) => _rules = rules;

    /// <summary>Khoảng giờ thật của một ngày vận hành: từ 06:00 ngày đó tới 06:00 hôm sau.</summary>
    public static (DateTime From, DateTime To) OperatingRange(DateTime date) =>
        (date.Date + PricingCalculator.NightCutoff, date.Date.AddDays(1) + PricingCalculator.NightCutoff);

    public ScheduleDay Day(int cinemaId, DateTime date, DateTime now)
    {
        var (from, to) = OperatingRange(date);
        var rows = ShowtimeDAL.ListForCinema(cinemaId, from, to, now);
        var neighbours = ShowtimeDAL.ActiveNear(cinemaId, from.AddHours(-6), to.AddHours(6), now).Select(ToSlot).ToList();
        var share = ShowtimeDAL.CountVietnameseShare(cinemaId, new DateTime(date.Year, 1, 1), new DateTime(date.Year, 12, 31));
        var movies = rows.Select(r => r.Showtime.MovieId).Distinct().ToDictionary(id => id, id => MovieDAL.GetById(id)!);
        var kdms = movies.Keys.ToDictionary(id => id, id => KdmLicenseDAL.GetByMovieCinema(id, cinemaId).Select(k => new KdmWindow(k.ValidFrom, k.ValidUntil)).ToList());
        var upcoming = movies.Keys.ToDictionary(id => id, id => ShowtimeDAL.UpcomingForMovie(id, cinemaId, now).Select(ToSlot).ToList());

        var groups = new List<AuditoriumSchedule>();
        foreach (var aud in AuditoriumDAL.GetByCinema(cinemaId).Where(a => a.Active || rows.Any(r => r.Showtime.AuditoriumId == a.Id)))
        {
            var seats = SeatDAL.GetByAuditorium(aud.Id);
            var list = rows.Where(r => r.Showtime.AuditoriumId == aud.Id).OrderBy(r => r.Showtime.StartTime).Select(r =>
            {
                bool locked = ShowtimeLock.IsLocked(r.BookedSeats, r.ActiveHeldSeats);
                if (r.Showtime.OperationalStatus == OperationalStatus.Cancelled)
                    return new ScheduledShowtime(r, locked, new ScheduleCheckResult(Array.Empty<ScheduleIssue>()));
                var slot = ToSlot(r);
                var ctx = new ScheduleContext(ToScheduleMovie(movies[r.Showtime.MovieId]), kdms[r.Showtime.MovieId], neighbours, upcoming[r.Showtime.MovieId],
                    new VietnameseShare(share.Vietnamese, share.Total), now, slot, locked, r.IsVietnameseFilm);
                var result = ScheduleConstraintChecker.Check(slot, ctx, _rules);
                // Tỷ lệ phim Việt hiện một lần ở đầu bảng thay vì lặp ở từng suất
                var issues = result.Issues.Where(i => i.Code != ScheduleWarningCode.VietnameseShareLow).ToList();
                return new ScheduledShowtime(r, locked, new ScheduleCheckResult(issues));
            }).ToList();
            groups.Add(new AuditoriumSchedule(aud, seats.Count(s => !s.IsHouseSeat && !s.IsMaintenance), list));
        }

        var active = rows.Where(r => r.Showtime.OperationalStatus != OperationalStatus.Cancelled).ToList();
        var priority = active.Where(r => VietnameseFilmQuota.InPriorityWindow(r.Showtime.FeatureStartTime, _rules)).ToList();
        // Tuần chứa ngày đang xem (thứ Hai đến Chủ nhật) cho chân trang Gantt (UI A5-08)
        var weekStart = date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        var week = ShowtimeDAL.CountVietnameseShare(cinemaId, weekStart, weekStart.AddDays(6));
        return new ScheduleDay(cinemaId, date.Date, groups, active.Count(r => r.IsVietnameseFilm), active.Count,
            priority.Count(r => r.IsVietnameseFilm), priority.Count, share.Vietnamese, share.Total, week.Vietnamese, week.Total);
    }

    public ShowtimeRow GetRow(int id, DateTime now) => ShowtimeDAL.GetRow(id, now) ?? throw CategoryService.NotFound();

    /// <summary>Phim cho ô chọn: phim đang chiếu (Active = 1); phim chưa có giấy phép hoặc phim C vẫn hiện để form báo lý do.</summary>
    public List<Movie> MovieOptions(int? includeId)
    {
        var list = MovieDAL.GetActiveByTitle();
        if (includeId is int id && list.All(m => m.Id != id) && MovieDAL.GetById(id) is Movie m) list.Insert(0, m);
        return list;
    }

    public List<Auditorium> AuditoriumOptions(int cinemaId, int? includeId)
    {
        var list = AuditoriumDAL.GetActiveByCinema(cinemaId);
        if (includeId is int id && list.All(a => a.Id != id) && AuditoriumDAL.GetById(id) is Auditorium a && a.CinemaId == cinemaId) list.Add(a);
        return list;
    }

    /// <summary>Kiểm tra một suất chưa lưu (form gọi mỗi khi đổi phòng, phim, giờ hoặc ngôn ngữ). id là suất đang sửa.</summary>
    public ShowtimeCheck Check(int? id, ShowtimeInput input, DateTime now) => Check(id, input, now, null, null);

    /// <summary>
    /// Kiểm tra một suất khi kéo thả trên Gantt (POST /scheduler/validate, SPEC 4.3-10): pending là các suất chưa lưu đang hiện trên Gantt
    /// (bản nháp AI, suất đã kéo mà chưa lưu). Suất có Id trong pending thay cho vị trí đang lưu trong DB.
    /// </summary>
    public ShowtimeCheck Check(int? id, ShowtimeInput input, DateTime now, IReadOnlyList<ScheduleSlot> pending) => Check(id, input, now, null, null, pending);

    private ShowtimeCheck Check(int? id, ShowtimeInput input, DateTime now, Microsoft.Data.SqlClient.SqlConnection? conn, Microsoft.Data.SqlClient.SqlTransaction? tx,
        IReadOnlyList<ScheduleSlot>? pending = null)
    {
        var basic = new List<ScheduleIssue>();
        var aud = AuditoriumDAL.GetById(input.AuditoriumId, conn, tx);
        var movie = MovieDAL.GetById(input.MovieId, conn, tx);
        ShowtimeRow? original = id is int sid ? ShowtimeDAL.GetRow(sid, now, conn, tx) ?? throw CategoryService.NotFound() : null;

        if (aud == null || (!aud.Active && original?.Showtime.AuditoriumId != aud.Id)) basic.Add(Field("AuditoriumId", "Bạn chọn phòng chiếu đang hoạt động."));
        if (movie == null || (!movie.Active && original?.Showtime.MovieId != movie.Id)) basic.Add(Field("MovieId", "Bạn chọn phim đang chiếu."));
        if (input.StartTime is null) basic.Add(Field("StartTime", "Bạn chọn ngày và giờ bắt đầu."));
        if (!LanguageFormat.All.Contains(input.LanguageFormat)) basic.Add(Field("LanguageFormat", "Bạn chọn bản ngôn ngữ của suất."));
        if (original != null && aud != null && aud.CinemaId != original.CinemaId) basic.Add(Field("AuditoriumId", "Chỉ đổi được sang phòng cùng rạp."));
        if (original != null && original.Showtime.OperationalStatus == OperationalStatus.Cancelled)
            basic.Add(new ScheduleIssue(IssueSeverity.Error, ErrorCodes.ValidationFailed, "Suất này đã bị hủy nên không sửa được."));
        else if (original != null && original.Showtime.StartTime <= now)
            basic.Add(new ScheduleIssue(IssueSeverity.Error, ErrorCodes.ValidationFailed, "Suất này đã bắt đầu hoặc đã chiếu xong nên không sửa được."));
        if (basic.Any(b => b.Field is "AuditoriumId" or "MovieId" or "StartTime") || aud == null || movie == null || input.StartTime is null)
            return new ShowtimeCheck(null, new ScheduleCheckResult(basic), aud, movie);

        var times = ShowtimeTimeCalculator.Compute(input.StartTime.Value, movie.DurationMinutes, _rules);
        bool moved = original == null || original.Showtime.StartTime != times.StartTime;
        if (moved && times.StartTime <= now) basic.Add(Field("StartTime", "Không xếp suất vào thời điểm đã qua. Bạn chọn giờ bắt đầu sau thời điểm hiện tại."));

        var candidate = new ScheduleSlot(id, movie.Id, aud.Id, aud.Name, times.StartTime, times.FeatureStartTime, times.EndTime, times.CleaningEndTime, input.LanguageFormat ?? string.Empty);
        var neighbours = ShowtimeDAL.ActiveNear(aud.CinemaId, times.StartTime.AddHours(-12), times.CleaningEndTime.AddHours(12), now, conn, tx).Select(ToSlot).ToList();
        if (pending is { Count: > 0 })
        {
            var pendingIds = pending.Where(p => p.Id != null).Select(p => p.Id).ToHashSet();
            neighbours = neighbours.Where(n => !pendingIds.Contains(n.Id)).Concat(pending.Where(p => p.Id == null || p.Id != id)).ToList();
        }
        var kdms = KdmLicenseDAL.GetByMovieCinema(movie.Id, aud.CinemaId, conn, tx).Select(k => new KdmWindow(k.ValidFrom, k.ValidUntil)).ToList();
        var upcoming = ShowtimeDAL.UpcomingForMovie(movie.Id, aud.CinemaId, now, conn, tx).Select(ToSlot).ToList();
        var year = PricingCalculator.OperatingDate(times.StartTime).Year;
        var share = ShowtimeDAL.CountVietnameseShare(aud.CinemaId, new DateTime(year, 1, 1), new DateTime(year, 12, 31), conn, tx);
        bool locked = original != null && ShowtimeLock.IsLocked(original.BookedSeats, original.ActiveHeldSeats);
        var ctx = new ScheduleContext(ToScheduleMovie(movie), kdms, neighbours, upcoming, new VietnameseShare(share.Vietnamese, share.Total), now,
            original == null ? null : ToSlot(original), locked, original?.IsVietnameseFilm ?? false);
        var result = ScheduleConstraintChecker.Check(candidate, ctx, _rules);
        return new ShowtimeCheck(times, new ScheduleCheckResult(basic.Concat(result.Issues).ToList()), aud, movie);
    }

    /// <summary>
    /// Lưu suất (thêm hoặc sửa). Có lỗi chặn thì ném BusinessRuleException với mã của lỗi đầu tiên và danh sách lỗi trong details.issues;
    /// cảnh báo không chặn. Chồng giờ được kiểm lại trong transaction sau khi khóa các suất cùng phòng quanh khoảng giờ đó.
    /// Tạo suất thì sinh ShowtimeSeats cho mọi ghế của phòng.
    /// </summary>
    public int Save(int? id, ShowtimeInput input, DateTime now)
    {
        var pre = Check(id, input, now);
        ThrowIfErrors(pre.Result);

        return DBUtil.InTransaction((conn, tx) =>
        {
            var times = pre.Times!;
            var aud = pre.Auditorium!;
            // Khóa các suất cùng phòng quanh khoảng giờ rồi kiểm lại toàn bộ trong transaction (SPEC 3.3)
            ShowtimeDAL.LockAuditoriumRange(aud.Id, times.StartTime, times.CleaningEndTime, conn, tx);
            var check = Check(id, input, now, conn, tx);
            ThrowIfErrors(check.Result);

            if (id is null)
            {
                var s = new Showtime
                {
                    MovieId = input.MovieId, AuditoriumId = aud.Id, StartTime = times.StartTime, FeatureStartTime = times.FeatureStartTime,
                    EndTime = times.EndTime, CleaningEndTime = times.CleaningEndTime, LanguageFormat = input.LanguageFormat!,
                    OperationalStatus = OperationalStatus.Scheduled, CreatedAt = now,
                };
                ShowtimeDAL.Insert(s, conn, tx);
                ShowtimeSeatDAL.CreateForShowtime(s.Id, aud.Id, conn, tx);
                return s.Id;
            }

            var current = ShowtimeDAL.GetById(id.Value, conn, tx) ?? throw CategoryService.NotFound();
            bool roomChanged = current.AuditoriumId != aud.Id;
            current.MovieId = input.MovieId; current.AuditoriumId = aud.Id; current.StartTime = times.StartTime; current.FeatureStartTime = times.FeatureStartTime;
            current.EndTime = times.EndTime; current.CleaningEndTime = times.CleaningEndTime; current.LanguageFormat = input.LanguageFormat!;
            ShowtimeDAL.Update(current, conn, tx);
            if (roomChanged)
            {
                // Chỉ tới được đây khi suất chưa bán vé (ScheduleConstraintChecker chặn SHOWTIME_LOCKED)
                ShowtimeSeatDAL.DeleteByShowtime(current.Id, conn, tx);
                ShowtimeSeatDAL.CreateForShowtime(current.Id, aud.Id, conn, tx);
            }
            return current.Id;
        });
    }

    /// <summary>
    /// "Áp dụng" bản nháp AI (SPEC 4.3-08): lưu mọi suất nháp trong một transaction với IsAiGenerated = 1 và sinh ShowtimeSeats.
    /// Mỗi suất được kiểm lại bằng ScheduleConstraintChecker sau khi khóa phòng; có lỗi chặn ở suất nào thì hủy cả lượt và báo suất đó.
    /// Không sửa, không xóa suất đã có.
    /// </summary>
    public List<int> ApplyAiDrafts(IReadOnlyList<ShowtimeInput> drafts, int cinemaId, DateTime now)
    {
        if (drafts.Count == 0) throw FieldErrors.Of("drafts", "Không có suất nháp nào để áp dụng.");
        foreach (var d in drafts)
        {
            var aud = AuditoriumDAL.GetById(d.AuditoriumId);
            if (aud == null || aud.CinemaId != cinemaId) throw FieldErrors.Of("drafts", "Suất nháp có phòng không thuộc rạp đang xếp.");
        }
        return DBUtil.InTransaction((conn, tx) =>
        {
            var ids = new List<int>();
            for (int i = 0; i < drafts.Count; i++)
            {
                var input = drafts[i];
                var first = Check(null, input, now, conn, tx);
                if (first.Times == null) ThrowIfErrors(first.Result);
                ShowtimeDAL.LockAuditoriumRange(input.AuditoriumId, first.Times!.StartTime, first.Times.CleaningEndTime, conn, tx);
                var check = Check(null, input, now, conn, tx);
                if (check.Result.HasErrors)
                {
                    var e = check.Result.Errors.First();
                    throw new BusinessRuleException(e.Code, $"Suất nháp {Fmt.Time(check.Times!.StartTime)} {check.Auditorium?.Name} ({check.Movie?.Title}): {e.Message}",
                        new { index = i, issues = IssuesJson(check.Result) });
                }
                var t = check.Times!;
                var s = new Showtime
                {
                    MovieId = input.MovieId, AuditoriumId = input.AuditoriumId, StartTime = t.StartTime, FeatureStartTime = t.FeatureStartTime, EndTime = t.EndTime,
                    CleaningEndTime = t.CleaningEndTime, LanguageFormat = input.LanguageFormat!, OperationalStatus = OperationalStatus.Scheduled,
                    IsAiGenerated = true, CreatedAt = now,
                };
                ShowtimeDAL.Insert(s, conn, tx);
                ShowtimeSeatDAL.CreateForShowtime(s.Id, input.AuditoriumId, conn, tx);
                ids.Add(s.Id);
            }
            return ids;
        });
    }

    public DeleteDecision CheckDelete(int id, DateTime now)
    {
        var row = GetRow(id, now);
        return DeleteDecisionOf(row, ShowtimeDAL.CountTickets(id), ShowtimeDAL.CountCorporateBookings(id), now);
    }

    private static DeleteDecision DeleteDecisionOf(ShowtimeRow row, int tickets, int corporate, DateTime now)
    {
        if (row.Showtime.StartTime <= now && tickets == 0)
            return DeleteDecision.Block("Suất này đã bắt đầu hoặc đã chiếu xong nên không xóa được, để giữ đúng lịch sử lịch chiếu.", false);
        return DeleteRules.Showtime(tickets, ShowtimeLock.IsLocked(row.BookedSeats, row.ActiveHeldSeats), corporate);
    }

    /// <summary>Xóa suất chưa bán vé (kèm ShowtimeSeats). Suất đã bán thì dùng hủy suất do sự cố (Phase 6).</summary>
    public void Delete(int id, DateTime now)
    {
        DBUtil.InTransaction((conn, tx) =>
        {
            var row = ShowtimeDAL.GetRow(id, now, conn, tx) ?? throw CategoryService.NotFound();
            var d = DeleteDecisionOf(row, ShowtimeDAL.CountTickets(id, conn, tx), ShowtimeDAL.CountCorporateBookings(id, conn, tx), now);
            if (!d.Allowed) throw CategoryService.Blocked(d);
            ShowtimeSeatDAL.DeleteByShowtime(id, conn, tx);
            ShowtimeDAL.Delete(id, conn, tx);
            return 0;
        });
    }

    /// <summary>Danh sách lỗi và cảnh báo gửi cho giao diện (cảnh báo không gửi mã nội bộ).</summary>
    public static List<object> IssuesJson(ScheduleCheckResult r) =>
        r.Issues.Select(i => (object)new { severity = i.IsError ? "error" : "warning", code = i.IsError ? i.Code : null, message = i.Message, field = i.Field }).ToList();

    private static void ThrowIfErrors(ScheduleCheckResult r)
    {
        var first = r.Errors.FirstOrDefault();
        if (first == null) return;
        var fields = r.Errors.Where(e => e.Field != null).GroupBy(e => e.Field!).ToDictionary(g => g.Key, g => g.First().Message);
        throw new BusinessRuleException(first.Code, first.Message, new { issues = IssuesJson(r), fields });
    }

    private static ScheduleIssue Field(string field, string message) => new(IssueSeverity.Error, ErrorCodes.ValidationFailed, message, field);

    public static ScheduleMovie ToScheduleMovie(Movie m) =>
        new(m.Id, m.Title, m.AgeRating, m.ClassificationLicenseNo, m.IsVietnameseFilm, CsvList.Split(m.LanguageFormats), m.DurationMinutes);

    public static ScheduleSlot ToSlot(ShowtimeRow r) =>
        new(r.Showtime.Id, r.Showtime.MovieId, r.Showtime.AuditoriumId, r.AuditoriumName, r.Showtime.StartTime, r.Showtime.FeatureStartTime,
            r.Showtime.EndTime, r.Showtime.CleaningEndTime, r.Showtime.LanguageFormat);
}
