using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;

namespace FPolyCinema.Tests;

/// <summary>Unit test cho quy tắc thuần của Phase 3: AgeRules, ScheduleConstraintChecker, VietnameseFilmQuota, ShowtimeLock, AuditoriumLayout, DeleteRules.</summary>
public class Phase3RulesTests
{
    private static readonly CinemaRules Rules = new();
    private static readonly DateTime Day = new(2026, 11, 13);

    private static DateTime At(int h, int m, int addDays = 0) => Day.AddDays(addDays).AddHours(h).AddMinutes(m);

    // ------------------------------------------------------------------ AgeRules
    [Fact]
    public void AgeRules_MinimumAgeAndCanWatch()
    {
        Assert.Equal(0, AgeRules.MinimumAge(AgeRating.P));
        Assert.Equal(0, AgeRules.MinimumAge(AgeRating.K));
        Assert.Equal(13, AgeRules.MinimumAge(AgeRating.T13));
        Assert.Equal(16, AgeRules.MinimumAge(AgeRating.T16));
        Assert.Equal(18, AgeRules.MinimumAge(AgeRating.T18));
        Assert.True(AgeRules.RequiresMinimumAge(AgeRating.T13));
        Assert.False(AgeRules.RequiresMinimumAge(AgeRating.K));
        Assert.True(AgeRules.CanWatch(AgeRating.T16, 16));
        Assert.False(AgeRules.CanWatch(AgeRating.T16, 15));
        Assert.False(AgeRules.CanWatch(AgeRating.T18, 17));
        Assert.False(AgeRules.CanWatch(AgeRating.C, 40));
        Assert.Equal(14, AgeRules.AgeOn(new DateTime(2011, 11, 14), Day));
        Assert.Equal(15, AgeRules.AgeOn(new DateTime(2011, 11, 13), Day));
        Assert.Equal(16, AgeRules.AgeOn(new DateTime(2010, 11, 13), Day));
    }

    [Fact]
    public void AgeRules_CanSchedule_RequiresLicenseAndNotC()
    {
        Assert.True(AgeRules.CanSchedule(AgeRating.T16, "123/GP-ĐA").Ok);
        Assert.Equal(ErrorCodes.LicenseMissing, AgeRules.CanSchedule(AgeRating.P, null).ErrorCode);
        Assert.Equal(ErrorCodes.LicenseMissing, AgeRules.CanSchedule(AgeRating.P, "  ").ErrorCode);
        Assert.Equal(ErrorCodes.RatingC, AgeRules.CanSchedule(AgeRating.C, "1/GP").ErrorCode);
    }

    [Fact]
    public void AgeRules_ChildTicketsRestrictedByRating()
    {
        var start = At(14, 0); var end = At(16, 0);
        Assert.Equal(ErrorCodes.AgeRestricted, AgeRules.CheckTicket(AudienceType.ChildU13, AgeRating.T13, start, end, Rules).ErrorCode);
        Assert.Equal(ErrorCodes.AgeRestricted, AgeRules.CheckTicket(AudienceType.ChildU13, AgeRating.T18, start, end, Rules).ErrorCode);
        Assert.True(AgeRules.CheckTicket(AudienceType.ChildU13, AgeRating.K, start, end, Rules).Ok);
        Assert.True(AgeRules.CheckTicket(AudienceType.ChildU13, AgeRating.P, start, end, Rules).Ok);
        Assert.True(AgeRules.CheckTicket(AudienceType.Child13To15, AgeRating.T13, start, end, Rules).Ok);
        var t16 = AgeRules.CheckTicket(AudienceType.Child13To15, AgeRating.T16, start, end, Rules);
        Assert.Equal(ErrorCodes.AgeRestricted, t16.ErrorCode);
        Assert.Equal("Phim T16 dành cho khán giả từ đủ 16 tuổi. Vé trẻ em không áp dụng cho phim này.", t16.Message);
        Assert.Equal(ErrorCodes.AgeRestricted, AgeRules.CheckTicket(AudienceType.Child13To15, AgeRating.T18, start, end, Rules).ErrorCode);
        Assert.True(AgeRules.CheckTicket(AudienceType.Adult, AgeRating.T18, start, end, Rules).Ok);
    }

    [Fact]
    public void AgeRules_ChildCurfew_ByViewerAgeNotByRating()
    {
        // CHILD_U13: kết thúc trước 22:00
        Assert.True(AgeRules.CheckTicket(AudienceType.ChildU13, AgeRating.P, At(19, 30), At(21, 59), Rules).Ok);
        var late = AgeRules.CheckTicket(AudienceType.ChildU13, AgeRating.P, At(20, 0), At(22, 35), Rules);
        Assert.Equal(ErrorCodes.ChildCurfew, late.ErrorCode);
        Assert.Equal("Suất này kết thúc lúc 22:35. Theo quy định, trẻ em dưới 13 tuổi chỉ xem suất kết thúc trước 22:00.", late.Message);
        Assert.Equal(ErrorCodes.ChildCurfew, AgeRules.CheckTicket(AudienceType.ChildU13, AgeRating.P, At(20, 0), At(22, 0), Rules).ErrorCode);
        // CHILD_13_15: kết thúc trước 23:00
        Assert.True(AgeRules.CheckTicket(AudienceType.Child13To15, AgeRating.T13, At(20, 0), At(22, 35), Rules).Ok);
        Assert.Equal(ErrorCodes.ChildCurfew, AgeRules.CheckTicket(AudienceType.Child13To15, AgeRating.P, At(21, 0), At(23, 0), Rules).ErrorCode);
        // Người lớn không bị giới hạn giờ
        Assert.True(AgeRules.CheckTicket(AudienceType.Adult, AgeRating.P, At(23, 0), At(1, 10, 1), Rules).Ok);
    }

    [Fact]
    public void AgeRules_LateShow_StartBefore6OrEndsNextDay()
    {
        Assert.True(AgeRules.IsLateShow(At(0, 30, 1), At(2, 30, 1)));
        Assert.True(AgeRules.IsLateShow(At(5, 59, 1), At(7, 0, 1)));
        Assert.True(AgeRules.IsLateShow(At(22, 30), At(0, 5, 1)));
        Assert.False(AgeRules.IsLateShow(At(6, 0), At(8, 0)));
        Assert.False(AgeRules.IsLateShow(At(19, 0), At(21, 0)));
        // Suất khuya không bao giờ "kết thúc trước 22:00", kể cả khi giờ kết thúc là 02:00 sáng
        Assert.False(AgeRules.EndsBefore(At(0, 10, 1), At(2, 0, 1), Rules.ChildU13LatestEnd));
        Assert.Equal(ErrorCodes.ChildCurfew, AgeRules.CheckTicket(AudienceType.ChildU13, AgeRating.P, At(0, 10, 1), At(2, 0, 1), Rules).ErrorCode);
        Assert.Contains("ngày hôm sau", AgeRules.CheckTicket(AudienceType.Child13To15, AgeRating.P, At(22, 30), At(0, 20, 1), Rules).Message);
    }

    [Fact]
    public void AgeRules_GuardianRequiredForKWithChildUnder13()
    {
        var onlyChild = new[] { AudienceType.ChildU13, AudienceType.ChildU13 };
        var g = AgeRules.CheckGuardian(AgeRating.K, onlyChild, guardianConfirmed: false);
        Assert.Equal(ErrorCodes.GuardianRequired, g.ErrorCode);
        Assert.Equal("Phim K: trẻ dưới 13 tuổi cần cha, mẹ hoặc người giám hộ đi cùng. Thêm 1 vé người lớn hoặc xác nhận có người giám hộ.", g.Message);
        Assert.True(AgeRules.CheckGuardian(AgeRating.K, onlyChild, guardianConfirmed: true).Ok);
        Assert.True(AgeRules.CheckGuardian(AgeRating.K, new[] { AudienceType.ChildU13, AudienceType.Adult }, false).Ok);
        Assert.True(AgeRules.CheckGuardian(AgeRating.K, new[] { AudienceType.ChildU13, AudienceType.Senior }, false).Ok);
        Assert.Equal(ErrorCodes.GuardianRequired, AgeRules.CheckGuardian(AgeRating.K, new[] { AudienceType.ChildU13, AudienceType.U22 }, false).ErrorCode);
        Assert.True(AgeRules.CheckGuardian(AgeRating.P, onlyChild, false).Ok);
        Assert.True(AgeRules.CheckGuardian(AgeRating.K, new[] { AudienceType.Child13To15 }, false).Ok);
    }

    [Fact]
    public void AgeRules_ScheduleWarning_KAndT13EndingLateOnlyWarn()
    {
        Assert.Null(AgeRules.ScheduleWarning(AgeRating.K, At(19, 0), At(21, 55), Rules));
        Assert.NotNull(AgeRules.ScheduleWarning(AgeRating.K, At(20, 0), At(22, 0), Rules));
        Assert.Null(AgeRules.ScheduleWarning(AgeRating.T13, At(20, 0), At(22, 30), Rules));
        Assert.NotNull(AgeRules.ScheduleWarning(AgeRating.T13, At(21, 0), At(23, 5), Rules));
        Assert.Null(AgeRules.ScheduleWarning(AgeRating.T16, At(22, 0), At(0, 30, 1), Rules));
        Assert.Null(AgeRules.ScheduleWarning(AgeRating.P, At(21, 0), At(23, 30), Rules));
    }

    // ------------------------------------------------------------------ ScheduleConstraintChecker
    private static readonly ScheduleMovie Movie = new(1, "Phim thử", AgeRating.T13, "01/GP-ĐA", false, new[] { LanguageFormat.Subtitled }, 120);
    private static readonly KdmWindow[] FullKdm = { new(Day.AddDays(-10), Day.AddDays(30)) };

    private static ScheduleSlot Slot(int? id, int audId, DateTime start, int duration = 120, string lang = LanguageFormat.Subtitled, int movieId = 1, string audName = "Phòng 2")
    {
        var t = ShowtimeTimeCalculator.Compute(start, duration, Rules);
        return new ScheduleSlot(id, movieId, audId, audName, t.StartTime, t.FeatureStartTime, t.EndTime, t.CleaningEndTime, lang);
    }

    private static ScheduleContext Ctx(ScheduleMovie? movie = null, IReadOnlyList<KdmWindow>? kdms = null, IReadOnlyList<ScheduleSlot>? neighbours = null,
        IReadOnlyList<ScheduleSlot>? upcoming = null, VietnameseShare? share = null, DateTime? now = null, ScheduleSlot? original = null, bool locked = false) =>
        new(movie ?? Movie, kdms ?? FullKdm, neighbours ?? Array.Empty<ScheduleSlot>(), upcoming ?? Array.Empty<ScheduleSlot>(),
            share ?? new VietnameseShare(30, 100), now ?? Day.AddHours(8), original, locked);

    [Fact]
    public void Checker_ValidSlot_HasNoIssues()
    {
        var r = ScheduleConstraintChecker.Check(Slot(null, 2, At(14, 0)), Ctx(), Rules);
        Assert.Empty(r.Issues);
        Assert.False(r.HasErrors);
    }

    [Fact]
    public void Checker_Overlap_BlocksWithMessageFromUi()
    {
        // Suất có sẵn 19:00 dài 110 phút: chiếu xong 21:05, dọn xong 21:20 → làm tròn lên 21:20
        var existing = Slot(10, 2, At(19, 0), 110);
        var cand = Slot(null, 2, At(21, 0));
        var r = ScheduleConstraintChecker.Check(cand, Ctx(neighbours: new[] { existing }), Rules);
        var issue = Assert.Single(r.Errors);
        Assert.Equal(ErrorCodes.ShowtimeOverlap, issue.Code);
        Assert.Equal($"Phòng 2 đã có suất 19:00–{Fmt.Time(existing.CleaningEndTime)} (gồm thời gian dọn phòng). Suất mới phải bắt đầu từ {Fmt.Time(existing.CleaningEndTime)}.", issue.Message);
        // Bắt đầu đúng lúc dọn xong thì không chồng giờ ([Start, CleaningEnd) là khoảng nửa mở)
        Assert.False(ScheduleConstraintChecker.Check(Slot(null, 2, existing.CleaningEndTime), Ctx(neighbours: new[] { existing }), Rules).HasErrors);
        // Phòng khác cùng giờ không chồng giờ
        Assert.False(ScheduleConstraintChecker.Check(Slot(null, 3, At(21, 0)), Ctx(neighbours: new[] { existing }), Rules).HasErrors);
        // Chính suất đang sửa không tự chồng lên mình
        Assert.False(ScheduleConstraintChecker.Check(Slot(10, 2, At(19, 30), 110), Ctx(neighbours: new[] { existing }), Rules).HasErrors);
    }

    [Fact]
    public void Checker_LicenseAndRatingC_Block()
    {
        var noLicense = Movie with { ClassificationLicenseNo = null };
        Assert.True(ScheduleConstraintChecker.Check(Slot(null, 2, At(14, 0)), Ctx(movie: noLicense), Rules).Has(ErrorCodes.LicenseMissing));
        var c = Movie with { AgeRating = AgeRating.C };
        Assert.True(ScheduleConstraintChecker.Check(Slot(null, 2, At(14, 0)), Ctx(movie: c), Rules).Has(ErrorCodes.RatingC));
    }

    [Fact]
    public void Checker_KdmMustCoverFeatureToEnd()
    {
        var cand = Slot(null, 2, At(14, 0));
        // KDM phủ đúng [FeatureStartTime, EndTime]: hợp lệ (quảng cáo và dọn phòng không cần KDM)
        Assert.False(ScheduleConstraintChecker.Check(cand, Ctx(kdms: new[] { new KdmWindow(cand.FeatureStartTime, cand.EndTime) }), Rules).HasErrors);
        var shortKdm = new[] { new KdmWindow(Day.AddDays(-5), new DateTime(2026, 11, 13, 15, 59, 0)) };
        var r = ScheduleConstraintChecker.Check(cand, Ctx(kdms: shortKdm), Rules);
        var issue = Assert.Single(r.Errors);
        Assert.Equal(ErrorCodes.KdmInvalid, issue.Code);
        Assert.Equal("Bản quyền chiếu (KDM) của phim này tại rạp chỉ có hiệu lực đến 13/11/2026 15:59. Chọn giờ trong khoảng hiệu lực hoặc cập nhật KDM.", issue.Message);
        Assert.True(ScheduleConstraintChecker.Check(cand, Ctx(kdms: Array.Empty<KdmWindow>()), Rules).Has(ErrorCodes.KdmInvalid));
        Assert.Contains("hiệu lực từ", ScheduleConstraintChecker.Check(cand, Ctx(kdms: new[] { new KdmWindow(Day.AddDays(2), Day.AddDays(9)) }), Rules).Errors.Single().Message);
    }

    [Fact]
    public void Checker_StartOutsideOperatingWindow_Blocks()
    {
        Assert.Contains(ScheduleConstraintChecker.Check(Slot(null, 2, At(7, 55)), Ctx(), Rules).Errors, e => e.Field == "StartTime" && e.Code == ErrorCodes.ValidationFailed);
        Assert.False(ScheduleConstraintChecker.Check(Slot(null, 2, At(8, 0)), Ctx(), Rules).HasErrors);
        Assert.False(ScheduleConstraintChecker.Check(Slot(null, 2, At(23, 30)), Ctx(movie: Movie with { AgeRating = AgeRating.T16 }), Rules).HasErrors);
        Assert.True(ScheduleConstraintChecker.Check(Slot(null, 2, At(23, 35)), Ctx(), Rules).HasErrors);
        Assert.True(ScheduleConstraintChecker.Check(Slot(null, 2, At(0, 30, 1)), Ctx(), Rules).HasErrors);
    }

    [Fact]
    public void Checker_LanguageMustBeOneOfMovieFormats()
    {
        var r = ScheduleConstraintChecker.Check(Slot(null, 2, At(14, 0), lang: LanguageFormat.Dubbed), Ctx(), Rules);
        var e = Assert.Single(r.Errors);
        Assert.Equal("LanguageFormat", e.Field);
        Assert.Contains("Phụ đề", e.Message);
    }

    [Fact]
    public void Checker_SameMinuteStarts_WarnsOnly()
    {
        var others = new[] { Slot(20, 3, At(19, 0), audName: "Phòng 3"), Slot(21, 4, At(19, 0), audName: "Phòng 4") };
        var r = ScheduleConstraintChecker.Check(Slot(null, 2, At(19, 0), movieId: 1), Ctx(movie: Movie with { AgeRating = AgeRating.T16 }, neighbours: others), Rules);
        Assert.False(r.HasErrors);
        Assert.True(r.Has(ScheduleWarningCode.SameMinuteStarts));
        var one = ScheduleConstraintChecker.Check(Slot(null, 2, At(19, 0)), Ctx(movie: Movie with { AgeRating = AgeRating.T16 }, neighbours: others.Take(1).ToList()), Rules);
        Assert.False(one.Has(ScheduleWarningCode.SameMinuteStarts));
    }

    [Fact]
    public void Checker_LateEndForKAndT13_WarnsNotBlocks()
    {
        var k = Movie with { AgeRating = AgeRating.K };
        var r = ScheduleConstraintChecker.Check(Slot(null, 2, At(20, 0), 110), Ctx(movie: k), Rules);
        Assert.False(r.HasErrors);
        Assert.True(r.Has(ScheduleWarningCode.LateEndForAudience));
        var t13 = ScheduleConstraintChecker.Check(Slot(null, 2, At(21, 10), 110), Ctx(), Rules);
        Assert.False(t13.HasErrors);
        Assert.True(t13.Has(ScheduleWarningCode.LateEndForAudience));
    }

    [Fact]
    public void Checker_KdmExpiringWithin24hWithShowtimesAfter_Warns()
    {
        var now = Day.AddHours(8);
        var kdm = new[] { new KdmWindow(Day.AddDays(-20), Day.AddHours(23)) };
        var cand = Slot(null, 2, At(14, 0));
        var afterExpiry = Slot(30, 3, At(21, 0), audName: "Phòng 3");
        var r = ScheduleConstraintChecker.Check(cand, Ctx(kdms: kdm, upcoming: new[] { afterExpiry }, now: now), Rules);
        Assert.False(r.HasErrors);
        Assert.True(r.Has(ScheduleWarningCode.KdmExpiringSoon));
        // Không còn suất nào sau thời điểm hết hạn thì không cảnh báo
        Assert.False(ScheduleConstraintChecker.Check(cand, Ctx(kdms: kdm, now: now), Rules).Has(ScheduleWarningCode.KdmExpiringSoon));
        // KDM còn hơn 24 giờ thì không cảnh báo
        Assert.False(ScheduleConstraintChecker.Check(cand, Ctx(upcoming: new[] { afterExpiry }, now: now), Rules).Has(ScheduleWarningCode.KdmExpiringSoon));
    }

    [Fact]
    public void Checker_VietnameseShareBelowMinimum_Warns()
    {
        var low = ScheduleConstraintChecker.Check(Slot(null, 2, At(19, 0)), Ctx(movie: Movie with { AgeRating = AgeRating.T16 }, share: new VietnameseShare(15, 99)), Rules);
        Assert.False(low.HasErrors);
        var w = Assert.Single(low.Warnings);
        Assert.Equal(ScheduleWarningCode.VietnameseShareLow, w.Code);
        Assert.Contains("15%", w.Message);
        Assert.Contains("18:00–22:00", w.Message);
        // Thêm một suất phim Việt đưa tỷ lệ lên đủ 20% thì không cảnh báo
        var vn = Movie with { IsVietnameseFilm = true };
        Assert.Empty(ScheduleConstraintChecker.Check(Slot(null, 2, At(14, 0)), Ctx(movie: vn, share: new VietnameseShare(19, 99)), Rules).Issues);
    }

    [Fact]
    public void Checker_LockedShowtime_CannotMoveTimeRoomOrMovie()
    {
        var original = Slot(40, 2, At(14, 0));
        var moved = Slot(40, 2, At(15, 0));
        var r = ScheduleConstraintChecker.Check(moved, Ctx(original: original, locked: true), Rules);
        var e = Assert.Single(r.Errors);
        Assert.Equal(ErrorCodes.ShowtimeLocked, e.Code);
        Assert.Equal("Suất này đã có khách mua vé nên không đổi giờ được. Nếu cần, dùng chức năng Hủy suất do sự cố.", e.Message);
        Assert.True(ScheduleConstraintChecker.Check(Slot(40, 3, At(14, 0)), Ctx(original: original, locked: true), Rules).Has(ErrorCodes.ShowtimeLocked));
        Assert.True(ScheduleConstraintChecker.Check(Slot(40, 2, At(14, 0), movieId: 9), Ctx(original: original, locked: true), Rules).Has(ErrorCodes.ShowtimeLocked));
        Assert.False(ScheduleConstraintChecker.Check(original, Ctx(original: original, locked: true), Rules).HasErrors);
        Assert.False(ScheduleConstraintChecker.Check(moved, Ctx(original: original, locked: false), Rules).HasErrors);
    }

    // ------------------------------------------------------------------ VietnameseFilmQuota, ShowtimeLock
    [Fact]
    public void VietnameseQuota_ShareWindowAndEffectiveDate()
    {
        Assert.Equal(0.2m, VietnameseFilmQuota.Share(20, 100));
        Assert.Equal(1m, VietnameseFilmQuota.Share(0, 0));
        Assert.True(VietnameseFilmQuota.MeetsMinimum(20, 100, Rules));
        Assert.False(VietnameseFilmQuota.MeetsMinimum(19, 100, Rules));
        Assert.True(VietnameseFilmQuota.InPriorityWindow(At(18, 0), Rules));
        Assert.True(VietnameseFilmQuota.InPriorityWindow(At(21, 59), Rules));
        Assert.False(VietnameseFilmQuota.InPriorityWindow(At(22, 0), Rules));
        Assert.False(VietnameseFilmQuota.InPriorityWindow(At(17, 59), Rules));
        Assert.False(VietnameseFilmQuota.Applies(new DateTime(2025, 12, 31)));
        Assert.True(VietnameseFilmQuota.Applies(new DateTime(2026, 1, 1)));
        Assert.Equal("18,5%", VietnameseFilmQuota.Percent(0.185m));
    }

    [Fact]
    public void ShowtimeLock_BookedOrActiveHeldSeatLocks()
    {
        var now = At(10, 0);
        Assert.False(ShowtimeLock.IsLocked(new (string, DateTime?)[] { (ShowtimeSeatStatus.Available, null), (ShowtimeSeatStatus.HouseHold, null) }, now));
        Assert.True(ShowtimeLock.IsLocked(new (string, DateTime?)[] { (ShowtimeSeatStatus.Booked, null) }, now));
        Assert.True(ShowtimeLock.IsLocked(new (string, DateTime?)[] { (ShowtimeSeatStatus.Held, now.AddMinutes(5)) }, now));
        Assert.False(ShowtimeLock.IsLocked(new (string, DateTime?)[] { (ShowtimeSeatStatus.Held, now.AddMinutes(-1)) }, now));
        Assert.True(ShowtimeLock.IsLocked(1, 0));
        Assert.True(ShowtimeLock.IsLocked(0, 2));
        Assert.False(ShowtimeLock.IsLocked(0, 0));
        Assert.Equal("LOCKED_HAS_TICKETS", ShowtimeLock.LockedHasTickets);
    }

    // ------------------------------------------------------------------ AuditoriumLayout
    private static List<LayoutCell> FullGrid(int rows, int cols, params int[] aisleCols) =>
        (from r in Enumerable.Range(1, rows) from c in Enumerable.Range(1, cols) where !aisleCols.Contains(c)
         select new LayoutCell(r, c, SeatType.Standard, false, false)).ToList();

    [Fact]
    public void Layout_HeatLevelsMatchUiScale()
    {
        Assert.Equal(1, AuditoriumLayout.HeatLevel(0));
        Assert.Equal(1, AuditoriumLayout.HeatLevel(39));
        Assert.Equal(2, AuditoriumLayout.HeatLevel(40));
        Assert.Equal(2, AuditoriumLayout.HeatLevel(59));
        Assert.Equal(3, AuditoriumLayout.HeatLevel(60));
        Assert.Equal(3, AuditoriumLayout.HeatLevel(74));
        Assert.Equal(4, AuditoriumLayout.HeatLevel(75));
        Assert.Equal(4, AuditoriumLayout.HeatLevel(89));
        Assert.Equal(5, AuditoriumLayout.HeatLevel(90));
        Assert.Equal(5, AuditoriumLayout.HeatLevel(100));
        Assert.True(AuditoriumLayout.HeatUsesDarkText(4));
        Assert.True(AuditoriumLayout.HeatUsesDarkText(5));
        Assert.False(AuditoriumLayout.HeatUsesDarkText(3));
    }

    [Fact]
    public void Layout_BuildLabelsSkipAislesPairsSweetboxAndScores()
    {
        var cells = FullGrid(5, 7, 4);
        cells.RemoveAll(c => c.Row == 5);
        cells.AddRange(new[] { new LayoutCell(5, 1, SeatType.Sweetbox, false, false), new LayoutCell(5, 2, SeatType.Sweetbox, false, false),
                               new LayoutCell(5, 6, SeatType.Sweetbox, false, false), new LayoutCell(5, 7, SeatType.Sweetbox, false, false) });
        Assert.Empty(AuditoriumLayout.Validate(5, 7, cells));
        var seats = AuditoriumLayout.Build(5, 7, cells, 0.65m);
        Assert.Equal("A4", seats.Single(s => s.Row == 1 && s.Col == 5).Label);
        Assert.Equal("E3", seats.Single(s => s.Row == 5 && s.Col == 6).Label);
        var pairs = seats.Where(s => s.SeatType == SeatType.Sweetbox).GroupBy(s => s.PairIndex).ToList();
        Assert.Equal(2, pairs.Count);
        Assert.All(pairs, p => Assert.Equal(2, p.Count()));
        Assert.All(seats, s => Assert.InRange(s.Score, 0, 100));
        var expected = SeatScoring.Score(5, 7, 3, 3, SeatType.Standard, SeatPersona.BestViewAudio, 0.65m);
        Assert.Equal(expected, seats.Single(s => s.Row == 3 && s.Col == 3).Score);
        Assert.Equal(AuditoriumLayout.HeatLevel(expected), seats.Single(s => s.Row == 3 && s.Col == 3).HeatLevel);
    }

    [Fact]
    public void Layout_ValidateRejectsOddSweetboxAndOutOfGrid()
    {
        var odd = new List<LayoutCell> { new(1, 1, SeatType.Sweetbox, false, false), new(1, 2, SeatType.Sweetbox, false, false), new(1, 3, SeatType.Sweetbox, false, false) };
        Assert.Contains(AuditoriumLayout.Validate(2, 5, odd), e => e.Contains("Ghế đôi đi theo cặp"));
        var lonely = new List<LayoutCell> { new(1, 1, SeatType.Sweetbox, false, false), new(1, 3, SeatType.Sweetbox, false, false) };
        Assert.Equal(2, AuditoriumLayout.Validate(2, 5, lonely).Count);
        Assert.NotEmpty(AuditoriumLayout.Validate(2, 5, new List<LayoutCell> { new(3, 1, SeatType.Standard, false, false) }));
        Assert.NotEmpty(AuditoriumLayout.Validate(2, 5, new List<LayoutCell>()));
        Assert.NotEmpty(AuditoriumLayout.Validate(27, 5, FullGrid(1, 1)));
        Assert.NotEmpty(AuditoriumLayout.Validate(2, 5, new List<LayoutCell> { new(1, 1, "SOFA", false, false) }));
    }

    [Fact]
    public void Layout_SuggestHouseSeats_CenterOfIdealRow()
    {
        var cells = FullGrid(10, 12, 3, 10);
        var picked = AuditoriumLayout.SuggestHouseSeats(10, 12, cells, 4, 0.65m);
        Assert.Equal(4, picked.Count);
        Assert.All(picked, p => Assert.Equal(7, p.Row)); // 0,65 × 10 = 6,5 → hàng 7 (G) được chọn khi bằng khoảng cách
        Assert.Equal(new[] { 5, 6, 7, 8 }, picked.Select(p => p.Col).OrderBy(c => c).ToArray());
        // Bỏ qua ghế bảo trì, ghế đôi, xe lăn
        var withMaint = cells.Select(c => c.Row == 7 && c.Col == 6 ? c with { IsMaintenance = true } : c).ToList();
        Assert.DoesNotContain((7, 6), AuditoriumLayout.SuggestHouseSeats(10, 12, withMaint, 4, 0.65m));
    }

    [Fact]
    public void Layout_StructuralChange_IgnoresMaintenanceToggle()
    {
        var cells = FullGrid(4, 6);
        var maint = cells.Select(c => c.Row == 2 && c.Col == 2 ? c with { IsMaintenance = true } : c).ToList();
        Assert.False(AuditoriumLayout.IsStructuralChange(4, 6, cells, 4, 6, maint));
        var vip = cells.Select(c => c.Row == 2 && c.Col == 2 ? c with { SeatType = SeatType.Vip } : c).ToList();
        Assert.True(AuditoriumLayout.IsStructuralChange(4, 6, cells, 4, 6, vip));
        var house = cells.Select(c => c.Row == 2 && c.Col == 2 ? c with { IsHouseSeat = true } : c).ToList();
        Assert.True(AuditoriumLayout.IsStructuralChange(4, 6, cells, 4, 6, house));
        Assert.True(AuditoriumLayout.IsStructuralChange(4, 6, cells, 4, 6, cells.Skip(1)));
        Assert.True(AuditoriumLayout.IsStructuralChange(4, 6, cells, 5, 6, cells));
        var stats = AuditoriumLayout.Stats(house.Select(c => c.Row == 1 && c.Col == 1 ? c with { IsMaintenance = true } : c));
        Assert.Equal(24, stats.Total);
        Assert.Equal(1, stats.House);
        Assert.Equal(1, stats.Maintenance);
        Assert.Equal(22, stats.Sellable);
    }

    // ------------------------------------------------------------------ DeleteRules Phase 3
    [Fact]
    public void DeleteRules_MovieAuditoriumDistributorShowtime()
    {
        Assert.True(DeleteRules.Movie(0, 0, 2, 3, true).Allowed);
        var m = DeleteRules.Movie(5, 12, 0, 0, true);
        Assert.False(m.Allowed);
        Assert.True(m.CanDeactivate);
        Assert.Contains("ngưng chiếu", m.Reason);
        Assert.True(DeleteRules.Auditorium(0, 120, true).Allowed);
        Assert.False(DeleteRules.Auditorium(1, 120, true).Allowed);
        Assert.False(DeleteRules.Distributor(2, true).Allowed);
        Assert.True(DeleteRules.Distributor(0, true).Allowed);
        Assert.True(DeleteRules.Showtime(0, false, 0).Allowed);
        Assert.False(DeleteRules.Showtime(3, true, 0).Allowed);
        Assert.False(DeleteRules.Showtime(0, true, 0).Allowed);
    }
}
