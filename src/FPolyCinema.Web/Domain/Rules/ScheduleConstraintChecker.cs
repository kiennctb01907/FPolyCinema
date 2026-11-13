using System.Globalization;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Web.Domain.Rules;

/// <summary>Phim đang xét khi xếp suất.</summary>
public sealed record ScheduleMovie(int Id, string Title, string AgeRating, string? ClassificationLicenseNo, bool IsVietnameseFilm,
    IReadOnlyList<string> LanguageFormats, int DurationMinutes);

/// <summary>Một suất (đã có hoặc đang xếp) với đủ 4 mốc thời gian. Id null là suất mới.</summary>
public sealed record ScheduleSlot(int? Id, int MovieId, int AuditoriumId, string AuditoriumName, DateTime StartTime, DateTime FeatureStartTime,
    DateTime EndTime, DateTime CleaningEndTime, string LanguageFormat);

/// <summary>Khoảng hiệu lực của một KDM của phim tại rạp.</summary>
public sealed record KdmWindow(DateTime ValidFrom, DateTime ValidUntil);

/// <summary>Số suất phim Việt và tổng số suất (không bị hủy) của rạp từ đầu năm, chưa tính suất đang xét.</summary>
public sealed record VietnameseShare(int VietnameseCount, int TotalCount);

/// <summary>
/// Dữ liệu chung quanh suất đang xét: phim, KDM của phim tại rạp, các suất khác không bị hủy của rạp quanh ngày đó (Neighbours),
/// các suất sắp tới của cùng phim tại rạp (SameMovieUpcoming), tỷ lệ phim Việt từ đầu năm, và suất gốc khi sửa.
/// </summary>
public sealed record ScheduleContext(
    ScheduleMovie Movie,
    IReadOnlyList<KdmWindow> Kdms,
    IReadOnlyList<ScheduleSlot> Neighbours,
    IReadOnlyList<ScheduleSlot> SameMovieUpcoming,
    VietnameseShare YearShare,
    DateTime Now,
    ScheduleSlot? Original = null,
    bool OriginalLocked = false,
    bool OriginalIsVietnamese = false);

public enum IssueSeverity { Error, Warning }

/// <summary>
/// Một lỗi chặn hoặc một cảnh báo. Lỗi chặn có Code là mã lỗi CONTRACTS mục 8 (giờ ngoài khung và ngôn ngữ không hợp lệ
/// dùng VALIDATION_FAILED kèm Field); cảnh báo có Code là một hằng của ScheduleWarningCode (chỉ dùng nội bộ, không gửi giao diện).
/// </summary>
public sealed record ScheduleIssue(IssueSeverity Severity, string Code, string Message, string? Field = null)
{
    public bool IsError => Severity == IssueSeverity.Error;
}

/// <summary>Tên các loại cảnh báo không chặn khi xếp lịch (SPEC 3.3).</summary>
public static class ScheduleWarningCode
{
    public const string SameMinuteStarts = "SAME_MINUTE_STARTS";
    public const string LateEndForAudience = "LATE_END_FOR_AUDIENCE";
    public const string KdmExpiringSoon = "KDM_EXPIRING_SOON";
    public const string VietnameseShareLow = "VIETNAMESE_SHARE_LOW";
}

/// <summary>Kết quả kiểm tra một suất.</summary>
public sealed class ScheduleCheckResult
{
    public ScheduleCheckResult(IReadOnlyList<ScheduleIssue> issues) => Issues = issues;

    public IReadOnlyList<ScheduleIssue> Issues { get; }
    public IEnumerable<ScheduleIssue> Errors => Issues.Where(i => i.IsError);
    public IEnumerable<ScheduleIssue> Warnings => Issues.Where(i => !i.IsError);
    public bool HasErrors => Issues.Any(i => i.IsError);
    public bool Has(string code) => Issues.Any(i => i.Code == code);
}

/// <summary>
/// Kiểm tra ràng buộc khi xếp suất chiếu (SPEC 3.3). Logic thuần, có unit test; dùng chung cho form, chế độ bảng,
/// Gantt và bộ xếp lịch AI (Phase 7), và cho test kiểm suất của DbSeeder.
/// Lỗi chặn:
/// - SHOWTIME_OVERLAP: hai khoảng [StartTime, CleaningEndTime) cùng phòng giao nhau.
/// - LICENSE_MISSING, RATING_C: phim thiếu giấy phép phân loại hoặc là phim C.
/// - KDM_INVALID: không KDM nào phủ trọn [FeatureStartTime, EndTime].
/// - StartTime ngoài khung OperatingDayStart đến LastShowStartLatest (VALIDATION_FAILED, ô StartTime).
/// - LanguageFormat không nằm trong Movies.LanguageFormats (VALIDATION_FAILED, ô LanguageFormat).
/// - SHOWTIME_LOCKED: suất gốc đang LOCKED_HAS_TICKETS mà đổi giờ, phòng hoặc phim.
/// Cảnh báo, không chặn:
/// - Quá MaxSameMinuteStarts phòng cùng rạp bắt đầu cùng một phút.
/// - Phim K hoặc T13 kết thúc muộn (AgeRules.ScheduleWarning).
/// - KDM hết hạn trong 24 giờ tới mà vẫn còn suất sau thời điểm đó.
/// - Tỷ lệ phim Việt từ đầu năm của rạp dưới VietnameseFilmMinShare.
/// </summary>
public static class ScheduleConstraintChecker
{
    public static ScheduleCheckResult Check(ScheduleSlot candidate, ScheduleContext ctx, CinemaRules rules)
    {
        var issues = new List<ScheduleIssue>();
        var movie = ctx.Movie;

        // Suất đã có khách mua vé: không đổi giờ, phòng hay phim
        if (ctx.Original != null && ctx.OriginalLocked && IsMoved(ctx.Original, candidate))
            issues.Add(new(IssueSeverity.Error, ErrorCodes.ShowtimeLocked,
                "Suất này đã có khách mua vé nên không đổi giờ được. Nếu cần, dùng chức năng Hủy suất do sự cố."));

        var showable = AgeRules.CanSchedule(movie.AgeRating, movie.ClassificationLicenseNo);
        if (!showable.Ok) issues.Add(new(IssueSeverity.Error, showable.ErrorCode!, showable.Message!, "MovieId"));

        if (!InOperatingWindow(candidate.StartTime, rules))
            issues.Add(new(IssueSeverity.Error, ErrorCodes.ValidationFailed,
                $"Suất phải bắt đầu trong khung {rules.OperatingDayStart:hh\\:mm}–{rules.LastShowStartLatest:hh\\:mm}. Bạn chọn giờ bắt đầu trong khung này.", "StartTime"));

        if (!movie.LanguageFormats.Contains(candidate.LanguageFormat))
        {
            var allowed = string.Join(", ", movie.LanguageFormats.Select(l => StatusDisplay.Of(StatusDisplay.Language, l).Label));
            issues.Add(new(IssueSeverity.Error, ErrorCodes.ValidationFailed,
                $"Phim này chỉ có bản {(allowed.Length > 0 ? allowed : "chưa khai báo")}. Bạn chọn đúng bản ngôn ngữ của phim.", "LanguageFormat"));
        }

        var kdm = KdmIssue(candidate, ctx.Kdms);
        if (kdm != null) issues.Add(kdm);

        foreach (var other in ctx.Neighbours)
        {
            if (other.AuditoriumId != candidate.AuditoriumId || SameShowtime(other, candidate)) continue;
            if (!Overlaps(candidate, other)) continue;
            var tail = candidate.StartTime >= other.StartTime
                ? $"Suất mới phải bắt đầu từ {Fmt.Time(other.CleaningEndTime)}."
                : $"Suất mới phải bắt đầu từ {Fmt.Time(other.CleaningEndTime)}, hoặc dọn phòng xong trước {Fmt.Time(other.StartTime)}.";
            issues.Add(new(IssueSeverity.Error, ErrorCodes.ShowtimeOverlap,
                $"{other.AuditoriumName} đã có suất {Fmt.Time(other.StartTime)}–{Fmt.Time(other.CleaningEndTime)} (gồm thời gian dọn phòng). {tail}", "StartTime"));
            break;
        }

        // ------------------------------------------------ cảnh báo
        int sameMinute = ctx.Neighbours.Count(o => !SameShowtime(o, candidate) && o.AuditoriumId != candidate.AuditoriumId
                                                   && TrimToMinute(o.StartTime) == TrimToMinute(candidate.StartTime));
        if (sameMinute + 1 > rules.MaxSameMinuteStarts)
            issues.Add(new(IssueSeverity.Warning, ScheduleWarningCode.SameMinuteStarts,
                $"Đã có {sameMinute} phòng khác bắt đầu lúc {Fmt.Time(candidate.StartTime)}. Rạp chỉ nên có tối đa {rules.MaxSameMinuteStarts} phòng bắt đầu cùng một phút để sảnh không quá tải; bạn dời suất này khoảng {rules.MinStartStaggerMinutes} phút."));

        var late = AgeRules.ScheduleWarning(movie.AgeRating, candidate.StartTime, candidate.EndTime, rules);
        if (late != null) issues.Add(new(IssueSeverity.Warning, ScheduleWarningCode.LateEndForAudience, late));

        var expiring = KdmExpiringWarning(candidate, ctx);
        if (expiring != null) issues.Add(expiring);

        var share = VietnameseShareWarning(candidate, ctx, rules);
        if (share != null) issues.Add(share);

        return new ScheduleCheckResult(issues);
    }

    /// <summary>Hai suất cùng phòng chồng giờ: [StartTime, CleaningEndTime) giao nhau.</summary>
    public static bool Overlaps(ScheduleSlot a, ScheduleSlot b) => a.StartTime < b.CleaningEndTime && b.StartTime < a.CleaningEndTime;

    /// <summary>StartTime trong khung OperatingDayStart đến LastShowStartLatest của cùng ngày.</summary>
    public static bool InOperatingWindow(DateTime startTime, CinemaRules rules) =>
        startTime.TimeOfDay >= rules.OperatingDayStart && startTime.TimeOfDay <= rules.LastShowStartLatest;

    /// <summary>Có KDM phủ trọn [FeatureStartTime, EndTime] không.</summary>
    public static bool KdmCovers(IEnumerable<KdmWindow> kdms, DateTime featureStart, DateTime end) =>
        kdms.Any(k => k.ValidFrom <= featureStart && end <= k.ValidUntil);

    /// <summary>Suất gốc bị đổi giờ, phòng hoặc phim.</summary>
    public static bool IsMoved(ScheduleSlot original, ScheduleSlot candidate) =>
        original.StartTime != candidate.StartTime || original.AuditoriumId != candidate.AuditoriumId || original.MovieId != candidate.MovieId;

    private static bool SameShowtime(ScheduleSlot a, ScheduleSlot b) => a.Id != null && a.Id == b.Id;

    private static DateTime TrimToMinute(DateTime t) => new(t.Ticks - t.Ticks % TimeSpan.TicksPerMinute);

    private static string Stamp(DateTime t) => t.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    private static ScheduleIssue? KdmIssue(ScheduleSlot c, IReadOnlyList<KdmWindow> kdms)
    {
        if (KdmCovers(kdms, c.FeatureStartTime, c.EndTime)) return null;
        string message;
        var started = kdms.Where(k => k.ValidFrom <= c.FeatureStartTime).OrderByDescending(k => k.ValidUntil).FirstOrDefault();
        var later = kdms.Where(k => k.ValidFrom > c.FeatureStartTime).OrderBy(k => k.ValidFrom).FirstOrDefault();
        if (started != null)
            message = $"Bản quyền chiếu (KDM) của phim này tại rạp chỉ có hiệu lực đến {Stamp(started.ValidUntil)}. Chọn giờ trong khoảng hiệu lực hoặc cập nhật KDM.";
        else if (later != null)
            message = $"Bản quyền chiếu (KDM) của phim này tại rạp chỉ có hiệu lực từ {Stamp(later.ValidFrom)} đến {Stamp(later.ValidUntil)}. Chọn giờ trong khoảng hiệu lực hoặc cập nhật KDM.";
        else
            message = "Phim này chưa có bản quyền chiếu (KDM) tại rạp. Thêm KDM cho rạp ở hồ sơ phim rồi xếp lại suất.";
        return new ScheduleIssue(IssueSeverity.Error, ErrorCodes.KdmInvalid, message, "StartTime");
    }

    /// <summary>KDM (hạn cuối cùng của phim tại rạp) hết hạn trong 24 giờ tới mà vẫn còn suất kết thúc sau thời điểm đó.</summary>
    private static ScheduleIssue? KdmExpiringWarning(ScheduleSlot candidate, ScheduleContext ctx)
    {
        if (ctx.Kdms.Count == 0) return null;
        var expiry = ctx.Kdms.Max(k => k.ValidUntil);
        if (expiry <= ctx.Now || expiry > ctx.Now.AddHours(24)) return null;
        var slots = ctx.SameMovieUpcoming.Where(s => !SameShowtime(s, candidate)).Append(candidate);
        int after = slots.Count(s => s.EndTime > expiry);
        if (after == 0) return null;
        return new ScheduleIssue(IssueSeverity.Warning, ScheduleWarningCode.KdmExpiringSoon,
            $"KDM của phim tại rạp hết hạn lúc {Stamp(expiry)} nhưng vẫn còn {after} suất kết thúc sau thời điểm đó. Bạn gia hạn KDM hoặc đổi các suất này.");
    }

    private static ScheduleIssue? VietnameseShareWarning(ScheduleSlot candidate, ScheduleContext ctx, CinemaRules rules)
    {
        var date = PricingCalculator.OperatingDate(candidate.StartTime);
        if (!VietnameseFilmQuota.Applies(date)) return null;
        int vn = ctx.YearShare.VietnameseCount, total = ctx.YearShare.TotalCount;
        // Khi sửa suất, bỏ suất gốc ra khỏi số đếm rồi cộng suất mới vào
        if (ctx.Original != null && ctx.Original.Id != null) { total = Math.Max(total - 1, 0); if (ctx.OriginalIsVietnamese) vn = Math.Max(vn - 1, 0); }
        total += 1;
        if (ctx.Movie.IsVietnameseFilm) vn += 1;
        var share = VietnameseFilmQuota.Share(vn, total);
        if (share >= rules.VietnameseFilmMinShare) return null;
        var priority = $"{rules.VietnameseFilmPriorityStart:hh\\:mm}–{rules.VietnameseFilmPriorityEnd:hh\\:mm}";
        var hint = !ctx.Movie.IsVietnameseFilm && VietnameseFilmQuota.InPriorityWindow(candidate.FeatureStartTime, rules)
            ? $" Khung {priority} nên ưu tiên phim Việt."
            : $" Bạn ưu tiên xếp thêm phim Việt, nhất là trong khung {priority}.";
        return new ScheduleIssue(IssueSeverity.Warning, ScheduleWarningCode.VietnameseShareLow,
            $"Tỷ lệ suất phim Việt của rạp từ đầu năm {date.Year} là {VietnameseFilmQuota.Percent(share)}, dưới mức tối thiểu {VietnameseFilmQuota.Percent(rules.VietnameseFilmMinShare)}.{hint}");
    }
}

/// <summary>
/// Hạn ngạch phim Việt (SPEC 3.1 mục 6; Điều 9 khoản 2, 3 NĐ 131/2022): từ 01/01/2026 phim Việt chiếm ít nhất VietnameseFilmMinShare
/// tổng số suất chiếu trong năm (chỉ tính suất không bị hủy) và được ưu tiên chiếu trong khung VietnameseFilmPriorityStart–End. Logic thuần.
/// </summary>
public static class VietnameseFilmQuota
{
    /// <summary>Ngày bắt đầu áp dụng hạn ngạch.</summary>
    public static readonly DateTime EffectiveFrom = new(2026, 1, 1);

    public static bool Applies(DateTime operatingDate) => operatingDate.Date >= EffectiveFrom;

    /// <summary>Tỷ lệ phim Việt; chưa có suất nào thì coi như đạt (1).</summary>
    public static decimal Share(int vietnameseCount, int totalCount) => totalCount <= 0 ? 1m : (decimal)vietnameseCount / totalCount;

    public static bool MeetsMinimum(int vietnameseCount, int totalCount, CinemaRules rules) =>
        Share(vietnameseCount, totalCount) >= rules.VietnameseFilmMinShare;

    /// <summary>Phần phim bắt đầu (FeatureStartTime) nằm trong khung ưu tiên phim Việt.</summary>
    public static bool InPriorityWindow(DateTime featureStartTime, CinemaRules rules) =>
        featureStartTime.TimeOfDay >= rules.VietnameseFilmPriorityStart && featureStartTime.TimeOfDay < rules.VietnameseFilmPriorityEnd;

    /// <summary>Tỷ lệ dạng "18,5%".</summary>
    public static string Percent(decimal share) => (share * 100m).ToString("0.#", Fmt.Vi) + "%";
}

/// <summary>
/// Cờ LOCKED_HAS_TICKETS (CONTRACTS C3-37, tính khi đọc, không lưu DB): suất có ít nhất một ghế BOOKED hoặc HELD còn hạn.
/// Suất bị khóa thì không đổi giờ, phòng hay phim (SHOWTIME_LOCKED). Logic thuần.
/// </summary>
public static class ShowtimeLock
{
    public const string LockedHasTickets = "LOCKED_HAS_TICKETS";

    public static bool IsLocked(IEnumerable<(string Status, DateTime? HoldExpiresAt)> seats, DateTime now) =>
        seats.Any(s => s.Status == ShowtimeSeatStatus.Booked || (s.Status == ShowtimeSeatStatus.Held && s.HoldExpiresAt > now));

    /// <summary>Theo số ghế đã đếm sẵn ở SQL: ghế BOOKED và ghế HELD còn hạn.</summary>
    public static bool IsLocked(int bookedSeats, int activeHeldSeats) => bookedSeats > 0 || activeHeldSeats > 0;
}
