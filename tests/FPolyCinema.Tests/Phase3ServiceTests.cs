using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.Utils;
using Microsoft.Extensions.Options;

namespace FPolyCinema.Tests;

/// <summary>
/// Test tích hợp Phase 3 trên CSDL mẫu: suất chiếu của DbSeeder qua ScheduleConstraintChecker không có lỗi chặn; xếp suất sinh ShowtimeSeats,
/// chặn chồng giờ và SHOWTIME_LOCKED; sơ đồ phòng (BaseViewScore, khóa cấu trúc); bảng giá ghi AuditLogs; mức giảm không dưới mức tối thiểu;
/// quy tắc xóa phim và phòng. Mỗi test tự dọn dữ liệu đã thêm để không ảnh hưởng test kiểm dữ liệu mẫu.
/// </summary>
[Collection("Database")]
public class Phase3ServiceTests
{
    private readonly SeededDatabaseFixture _f;
    private static readonly DateTime Now = SeededDatabaseFixture.Now;

    public Phase3ServiceTests(SeededDatabaseFixture f)
    {
        _f = f;
        DBUtil.Init(f.ConnectionString);
    }

    private UploadService Uploads() => new(new FakeWebEnv(_f.WebRoot), Options.Create(new UploadOptions { Path = Path.Combine(_f.WebRoot, "uploads") }));

    private int Cinema1 => CinemaDAL.GetActive().OrderBy(c => c.Id).First().Id;

    /// <summary>Phim có giấy phép, khác C, có KDM tại rạp phủ cả ngày cho trước.</summary>
    private Movie SchedulableMovie(int cinemaId, DateTime day) =>
        MovieDAL.GetAll().First(m => m.AgeRating is AgeRating.P or AgeRating.T16 or AgeRating.T18 && m.ClassificationLicenseNo != null && m.Active
            && KdmLicenseDAL.GetByMovieCinema(m.Id, cinemaId).Any(k => k.ValidFrom <= day && k.ValidUntil >= day.AddDays(1)));

    [Fact]
    public void SeededShowtimes_PassScheduleConstraintChecker_WithoutBlockingErrors()
    {
        var rules = _f.Rules;
        var rows = ShowtimeDAL.AllRows(Now).Where(r => r.Showtime.OperationalStatus != OperationalStatus.Cancelled).ToList();
        Assert.True(rows.Count > 1000);
        var movies = MovieDAL.GetAll().ToDictionary(m => m.Id);
        var kdms = KdmLicenseDAL.GetAll();
        foreach (var cinema in rows.GroupBy(r => r.CinemaId))
        {
            var slots = cinema.Select(ScheduleService.ToSlot).ToList();
            foreach (var r in cinema)
            {
                var slot = ScheduleService.ToSlot(r);
                var neighbours = slots.Where(s => s.StartTime < slot.CleaningEndTime.AddHours(4) && s.CleaningEndTime > slot.StartTime.AddHours(-4)).ToList();
                var ctx = new ScheduleContext(ScheduleService.ToScheduleMovie(movies[r.Showtime.MovieId]),
                    kdms.Where(k => k.MovieId == r.Showtime.MovieId && k.CinemaId == r.CinemaId).Select(k => new KdmWindow(k.ValidFrom, k.ValidUntil)).ToList(),
                    neighbours, Array.Empty<ScheduleSlot>(), new VietnameseShare(0, 0), Now, slot, false, r.IsVietnameseFilm);
                var result = ScheduleConstraintChecker.Check(slot, ctx, rules);
                Assert.False(result.HasErrors, $"Suất {r.Showtime.Id} ({r.MovieTitle}, {r.AuditoriumName}, {r.Showtime.StartTime:dd/MM HH:mm}) có lỗi chặn: {string.Join(" | ", result.Errors.Select(e => e.Message))}");
            }
        }
    }

    [Fact]
    public void Schedule_SaveCreatesShowtimeSeats_BlocksOverlapAndLockedChanges()
    {
        var audService = new AuditoriumService(_f.Rules);
        var aud = audService.Create(new AuditoriumInput(Cinema1, "Phòng thử xếp lịch", AuditoriumFormat.F2D, 5, 8, 0.65m, true));
        var schedule = new ScheduleService(_f.Rules);
        var day = Now.Date.AddDays(2);
        var movie = SchedulableMovie(Cinema1, day);
        var lang = CsvList.Split(movie.LanguageFormats)[0];
        int? first = null;
        try
        {
            // Đặt 2 ghế nội bộ trước khi tạo suất
            var cells = SeatDAL.GetByAuditorium(aud.Id).Select(s => new LayoutCell(s.RowIndex, s.ColIndex, s.SeatType, s.RowIndex == 3 && s.ColIndex is 4 or 5, false)).ToList();
            Assert.True(audService.SaveLayout(aud.Id, 5, 8, cells, Now));

            var input = new ShowtimeInput(aud.Id, movie.Id, day.AddHours(10).AddMinutes(2), lang);
            var check = schedule.Check(null, input, Now);
            Assert.False(check.Result.HasErrors, string.Join(" | ", check.Result.Errors.Select(e => e.Message)));
            Assert.Equal(day.AddHours(10), check.Times!.StartTime); // làm tròn 5 phút
            first = schedule.Save(null, input, Now);
            var seats = ShowtimeSeatDAL.GetByShowtime(first.Value);
            Assert.Equal(40, seats.Count);
            Assert.Equal(2, seats.Count(s => s.Status == ShowtimeSeatStatus.HouseHold));
            Assert.Equal(38, seats.Count(s => s.Status == ShowtimeSeatStatus.Available));

            // Chồng giờ cùng phòng
            var overlap = Assert.Throws<BusinessRuleException>(() => schedule.Save(null, input with { StartTime = day.AddHours(11) }, Now));
            Assert.Equal(ErrorCodes.ShowtimeOverlap, overlap.Code);
            Assert.Contains("Phòng thử xếp lịch đã có suất 10:00–", overlap.Message);

            // Chưa bán vé: đổi giờ được
            Assert.Equal(first, schedule.Save(first, input with { StartTime = day.AddHours(9) }, Now));
            // Có ghế BOOKED: LOCKED_HAS_TICKETS, không đổi giờ, phòng, phim; không xóa được
            var ss = ShowtimeSeatDAL.GetByShowtime(first.Value).First(s => s.Status == ShowtimeSeatStatus.Available);
            ss.Status = ShowtimeSeatStatus.Booked;
            ShowtimeSeatDAL.Update(ss);
            var locked = Assert.Throws<BusinessRuleException>(() => schedule.Save(first, input with { StartTime = day.AddHours(12) }, Now));
            Assert.Equal(ErrorCodes.ShowtimeLocked, locked.Code);
            Assert.False(schedule.CheckDelete(first.Value, Now).Allowed);
            Assert.Equal(ErrorCodes.DeleteBlocked, Assert.Throws<BusinessRuleException>(() => schedule.Delete(first.Value, Now)).Code);
            // Giữ ghế còn hạn cũng khóa; hết hạn thì không
            ss.Status = ShowtimeSeatStatus.Held; ss.HoldExpiresAt = Now.AddMinutes(5);
            ShowtimeSeatDAL.Update(ss);
            Assert.True(ShowtimeLock.IsLocked(ShowtimeSeatDAL.CountLocking(first.Value, Now).Booked, ShowtimeSeatDAL.CountLocking(first.Value, Now).ActiveHeld));
            ss.HoldExpiresAt = Now.AddMinutes(-1);
            ShowtimeSeatDAL.Update(ss);
            Assert.False(schedule.Check(first, input with { StartTime = day.AddHours(12) }, Now).Result.HasErrors);

            ss.Status = ShowtimeSeatStatus.Available; ss.HoldExpiresAt = null;
            ShowtimeSeatDAL.Update(ss);
            schedule.Delete(first.Value, Now);
            first = null;
        }
        finally
        {
            if (first is int id) { ShowtimeSeatDAL.DeleteByShowtime(id); ShowtimeDAL.Delete(id); }
            audService.Delete(aud.Id);
        }
    }

    [Fact]
    public void Schedule_MovieWithoutLicenseOrKdm_IsBlocked_PastStartBlocked()
    {
        var movies = new MovieService(Uploads());
        var m = movies.Save(null, new MovieInput("Phim thử chưa giấy phép", null, new[] { Genre.Drama }, 100, AgeRating.T16, null, Array.Empty<string>(), false,
            new[] { LanguageFormat.Subtitled }, Now.Date, null, 0, false, null, null, null, true, Array.Empty<RentalRuleInput>(), Array.Empty<KdmInput>()), new[] { Cinema1 }, Now);
        try
        {
            var aud = AuditoriumDAL.GetActiveByCinema(Cinema1).First();
            var schedule = new ScheduleService(_f.Rules);
            var r = schedule.Check(null, new ShowtimeInput(aud.Id, m.Id, Now.Date.AddDays(3).AddHours(9), LanguageFormat.Subtitled), Now);
            Assert.True(r.Result.Has(ErrorCodes.LicenseMissing));
            Assert.True(r.Result.Has(ErrorCodes.KdmInvalid));
            var past = schedule.Check(null, new ShowtimeInput(aud.Id, m.Id, Now.AddHours(-2), LanguageFormat.Subtitled), Now);
            Assert.Contains(past.Result.Errors, e => e.Field == "StartTime" && e.Message.Contains("đã qua"));
            Assert.True(movies.CheckDelete(m.Id).Allowed);
        }
        finally { movies.Delete(m.Id); }
        Assert.Null(MovieDAL.GetById(m.Id));
    }

    [Fact]
    public void Movie_SaveWithRentalRulesAndKdm_DeleteBlockedWhenHasShowtimes()
    {
        var service = new MovieService(Uploads());
        var cinemas = CinemaDAL.GetActive().Select(c => c.Id).ToList();
        var bad = Assert.Throws<BusinessRuleException>(() => service.Save(null, new MovieInput("", null, Array.Empty<string>(), 0, "X", null, Array.Empty<string>(), false,
            Array.Empty<string>(), null, null, -1, false, "http://ngoai.vn/a.png", "ftp://x", null, true,
            new[] { new RentalRuleInput(1, 50), new RentalRuleInput(1, 120) }, new[] { new KdmInput(cinemas[0], Now, Now.AddDays(-1), null) }), cinemas, Now));
        var fields = Assert.IsAssignableFrom<IDictionary<string, string>>(bad.Details);
        foreach (var k in new[] { "Title", "Genres", "DurationMinutes", "AgeRating", "LanguageFormats", "ReleaseDate", "MinimumTicketPrice", "PosterUrl", "TrailerUrl",
                     "RentalRules[1].WeekNumber", "RentalRules[1].DistributorSharePercent", "Kdms[0].ValidUntil" })
            Assert.True(fields.ContainsKey(k), "thiếu lỗi ô " + k);

        var m = service.Save(null, new MovieInput("Phim thử đủ hồ sơ", "Full Record", new[] { Genre.Action, Genre.Drama }, 118, AgeRating.T13, "999/GP-TEST",
            new[] { ContentWarning.Violence }, true, new[] { LanguageFormat.Dubbed, LanguageFormat.Subtitled }, Now.Date, DistributorDAL.GetAll().First().Id, 45_000, true,
            null, "https://example.vn/trailer", "Mô tả", true,
            new[] { new RentalRuleInput(2, 45), new RentalRuleInput(1, 55) }, new[] { new KdmInput(cinemas[0], Now.Date, Now.Date.AddDays(30), "Bản DCP 2D") }), cinemas, Now);
        try
        {
            var saved = service.Get(m.Id);
            Assert.Equal("ACTION,DRAMA", saved.Genres);
            Assert.Equal("SUBTITLED,DUBBED", saved.LanguageFormats);
            Assert.Equal("VIOLENCE", saved.ContentWarnings);
            Assert.Equal(new[] { 1, 2 }, service.RentalRules(m.Id).Select(r => r.WeekNumber).ToArray());
            Assert.Equal(55m, service.RentalRules(m.Id)[0].DistributorSharePercent);
            Assert.Single(service.Kdms(m.Id));
        }
        finally { service.Delete(m.Id); }

        // Phim mẫu đã có suất chiếu và vé: chỉ được ngưng
        var seeded = MovieDAL.GetAll().First(x => MovieDAL.CountShowtimes(x.Id) > 0);
        var d = service.CheckDelete(seeded.Id);
        Assert.False(d.Allowed);
        Assert.True(d.CanDeactivate);
        Assert.Equal(ErrorCodes.DeleteBlocked, Assert.Throws<BusinessRuleException>(() => service.Delete(seeded.Id)).Code);
    }

    [Fact]
    public void Auditorium_LayoutRescoresAndLocksStructureWhenFutureShowtimeSold()
    {
        var service = new AuditoriumService(_f.Rules);
        var a = service.Create(new AuditoriumInput(Cinema1, "Phòng thử sơ đồ", AuditoriumFormat.F3D, 4, 6, 0.65m, true));
        int? showtimeId = null;
        try
        {
            Assert.Equal(24, service.Seats(a.Id).Count);
            // Lối đi ở cột 3, hàng cuối ghế đôi, 1 ghế bảo trì
            var cells = new List<LayoutCell>();
            for (int r = 1; r <= 4; r++)
                for (int c = 1; c <= 6; c++)
                {
                    if (c == 3) continue;
                    var type = r == 4 && c is 1 or 2 ? SeatType.Sweetbox : SeatType.Standard;
                    cells.Add(new LayoutCell(r, c, type, false, r == 1 && c == 6));
                }
            Assert.True(service.SaveLayout(a.Id, 4, 6, cells, Now));
            var seats = service.Seats(a.Id);
            Assert.Equal(20, seats.Count);
            Assert.Equal("A3", seats.Single(s => s.RowIndex == 1 && s.ColIndex == 4).Label);
            Assert.Single(seats.Where(s => s.SeatType == SeatType.Sweetbox).Select(s => s.PairGroupId).Distinct());
            Assert.All(seats, s => Assert.Equal(SeatScoring.Score(4, 6, s.RowIndex, s.ColIndex, s.SeatType, SeatPersona.BestViewAudio, 0.65m), s.BaseViewScore));
            Assert.True(seats.Single(s => s.RowIndex == 1 && s.ColIndex == 6).IsMaintenance);

            // Có suất tương lai đã bán vé: không đổi cấu trúc, chỉ bật tắt bảo trì
            var movie = SchedulableMovie(Cinema1, Now.Date.AddDays(3));
            showtimeId = new ScheduleService(_f.Rules).Save(null, new ShowtimeInput(a.Id, movie.Id, Now.Date.AddDays(3).AddHours(9), CsvList.Split(movie.LanguageFormats)[0]), Now);
            var ss = ShowtimeSeatDAL.GetByShowtime(showtimeId.Value).First();
            ss.Status = ShowtimeSeatStatus.Booked;
            ShowtimeSeatDAL.Update(ss);
            Assert.True(service.IsStructureLocked(a.Id, Now));
            var vip = cells.Select(c => c.Row == 2 && c.Col == 2 ? c with { SeatType = SeatType.Vip } : c).ToList();
            var ex = Assert.Throws<BusinessRuleException>(() => service.SaveLayout(a.Id, 4, 6, vip, Now));
            Assert.Contains("chỉ bật hoặc tắt ghế bảo trì", ex.Message);
            var maint = cells.Select(c => c.Row == 2 && c.Col == 2 ? c with { IsMaintenance = true } : c).ToList();
            Assert.False(service.SaveLayout(a.Id, 4, 6, maint, Now));
            Assert.True(service.Seats(a.Id).Single(s => s.RowIndex == 2 && s.ColIndex == 2).IsMaintenance);

            // Phòng đã có suất: không xóa được
            Assert.False(service.CheckDelete(a.Id).Allowed);
            Assert.Equal(ErrorCodes.DeleteBlocked, Assert.Throws<BusinessRuleException>(() => service.Delete(a.Id)).Code);

            // Không bán vé nữa: thêm ghế vào lối đi thì suất tương lai có thêm dòng ShowtimeSeats
            ss.Status = ShowtimeSeatStatus.Available;
            ShowtimeSeatDAL.Update(ss);
            var withAisle = maint.Append(new LayoutCell(2, 3, SeatType.Standard, true, false)).ToList();
            Assert.True(service.SaveLayout(a.Id, 4, 6, withAisle, Now));
            var after = ShowtimeSeatDAL.GetByShowtime(showtimeId.Value);
            Assert.Equal(21, after.Count);
            Assert.Single(after, s => s.Status == ShowtimeSeatStatus.HouseHold);
        }
        finally
        {
            if (showtimeId is int id) { ShowtimeSeatDAL.DeleteByShowtime(id); ShowtimeDAL.Delete(id); }
            service.Delete(a.Id);
        }
    }

    [Fact]
    public void Pricing_SaveWritesAuditLog_DiscountBelowLegalMinimumRejected_TryPrice()
    {
        var service = new PricingService(_f.Rules);
        var cinema = Cinema1;
        var table = service.Table(cinema);
        Assert.Equal(PricingRuleType.All.Sum(t => PricingRuleKey.For(t).Count), table.Count);
        var weekend = table.Single(c => c.RuleType == PricingRuleType.DayType && c.RuleKey == DayType.Weekend);
        int before = AuditLogDAL.GetAll().Count(l => l.Action == AuditAction.PricingUpdated);
        try
        {
            Assert.Equal(1, service.SaveTable(cinema, table.Select(c => c == weekend ? c with { Amount = c.Amount + 7_000 } : c).ToList(), null, Now));
            var log = AuditLogDAL.GetAll().Where(l => l.Action == AuditAction.PricingUpdated).OrderByDescending(l => l.Id).First();
            Assert.Equal("PricingRules", log.EntityType);
            Assert.Contains("DAY_TYPE/WEEKEND", log.Detail);
            Assert.Equal(before + 1, AuditLogDAL.GetAll().Count(l => l.Action == AuditAction.PricingUpdated));
            Assert.Equal(0, service.SaveTable(cinema, service.Table(cinema), null, Now)); // không đổi gì thì không ghi
            var badBase = Assert.Throws<BusinessRuleException>(() => service.SaveTable(cinema, table.Select(c => c.RuleType == PricingRuleType.Base ? c with { Amount = 0 } : c).ToList(), null, Now));
            Assert.Equal(ErrorCodes.ValidationFailed, badBase.Code);
        }
        finally { service.SaveTable(cinema, table, null, Now); }

        var below = Assert.Throws<BusinessRuleException>(() => service.SaveDiscounts(new Dictionary<string, decimal> { [AudienceType.ChildU13] = 10, [AudienceType.DisabledSevere] = 40 }, null, Now));
        var fields = Assert.IsAssignableFrom<IDictionary<string, string>>(below.Details);
        Assert.Contains("ít nhất 20%", fields["Discounts[CHILD_U13]"]);
        Assert.Contains("ít nhất 50%", fields["Discounts[DISABLED_SEVERE]"]);
        var u22 = AudienceDiscountDAL.GetById(AudienceType.U22)!.DiscountPercent;
        try
        {
            Assert.Equal(1, service.SaveDiscounts(new Dictionary<string, decimal> { [AudienceType.U22] = 15, [AudienceType.ChildU13] = 25 - 5 }, null, Now));
            Assert.Equal(15m, AudienceDiscountDAL.GetById(AudienceType.U22)!.DiscountPercent);
            Assert.Contains(AuditLogDAL.GetAll(), l => l.EntityType == "AudienceDiscounts" && l.Detail!.Contains("U22"));
        }
        finally { service.SaveDiscounts(new Dictionary<string, decimal> { [AudienceType.U22] = u22 }, null, Now); }

        // Thử tính giá khớp PricingCalculator với bảng giá trong CSDL
        var start = new DateTime(2026, 11, 14, 19, 0, 0); // thứ Bảy
        var p = service.Try(new PriceTryInput(cinema, start, AuditoriumFormat.IMAX, SeatType.Vip, true, AudienceType.Adult));
        Assert.Equal(DayType.Weekend, p.DayType);
        Assert.Equal(TimeSlot.Golden, p.TimeSlot);
        var t = service.Table(cinema);
        long Amt(string type, string key) => t.Single(c => c.RuleType == type && c.RuleKey == key).Amount;
        Assert.Equal(Amt(PricingRuleType.Base, PricingRuleKey.Default) + Amt(PricingRuleType.DayType, DayType.Weekend) + Amt(PricingRuleType.TimeSlot, TimeSlot.Golden)
                     + Amt(PricingRuleType.SeatType, SeatType.Vip) + Amt(PricingRuleType.Format, AuditoriumFormat.IMAX) + Amt(PricingRuleType.Blockbuster, PricingRuleKey.Yes), p.GrossAmount);

        // Ngày lễ: thêm, trùng bị chặn, xóa
        var h = service.AddHoliday(new DateTime(2026, 12, 24), "Ngày thử");
        try
        {
            Assert.Equal(ErrorCodes.ValidationFailed, Assert.Throws<BusinessRuleException>(() => service.AddHoliday(h.HolidayDate, "Trùng")).Code);
            Assert.Equal(DayType.Holiday, service.Try(new PriceTryInput(cinema, new DateTime(2026, 12, 24, 10, 0, 0), AuditoriumFormat.F2D, SeatType.Standard, false, AudienceType.Adult)).DayType);
        }
        finally { service.DeleteHoliday(h.HolidayDate); }
    }
}
