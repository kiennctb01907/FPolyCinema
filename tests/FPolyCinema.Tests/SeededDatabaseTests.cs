using System.Data;
using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FPolyCinema.Tests;

/// <summary>
/// Tạo CSDL thử riêng ("_SeedTest_" + chuỗi ngẫu nhiên), chạy DbInitializer và DbSeeder thật với đồng hồ giả (06/11/2026 21:00) và thư mục web tạm.
/// Dùng chung cho các test kiểm dữ liệu mẫu của SPEC 8.1 bước 6.
/// </summary>
public sealed class SeededDatabaseFixture : IDisposable
{
    public static readonly DateTime Now = new(2026, 11, 6, 21, 0, 0);

    public SeededDatabaseFixture()
    {
        ConnectionString = TestDatabase.NewConnectionString("SeedTest");
        WebRoot = Path.Combine(Path.GetTempPath(), "fpc-seed-" + Guid.NewGuid().ToString("N"));
        Clock = new FakeClock(Now);
        Rules = new CinemaRules();
        DBUtil.Init(ConnectionString);
        new DbInitializer(ConnectionString, RepoPaths.File("Database"), Clock, NullLogger.Instance).Initialize(resetDb: true);
        Seeded = new DbSeeder(Clock, Rules, WebRoot, NullLogger.Instance).SeedIfEmpty();
        SeededAgain = new DbSeeder(Clock, Rules, WebRoot, NullLogger.Instance).SeedIfEmpty();
    }

    public string ConnectionString { get; }
    public string WebRoot { get; }
    public FakeClock Clock { get; }
    public CinemaRules Rules { get; }
    public bool Seeded { get; }
    public bool SeededAgain { get; }

    public void Dispose()
    {
        TestDatabase.Drop(ConnectionString);
        try { Directory.Delete(WebRoot, true); } catch (IOException) { }
    }
}

/// <summary>Kiểm dữ liệu mẫu do DbSeeder nạp (SPEC 8.1 bước 6, CONTRACTS mục 4 và 11).</summary>
[Collection("Database")]
public class SeededDatabaseTests
{
    private static readonly DateTime Today = SeededDatabaseFixture.Now.Date;
    private readonly SeededDatabaseFixture _f;

    public SeededDatabaseTests(SeededDatabaseFixture f)
    {
        _f = f;
        DBUtil.Init(f.ConnectionString);
    }

    private static DataTable Q(string sql, params object[] ps) => DBUtil.QueryDataTable(sql, ps.ToList());
    private static int N(string sql, params object[] ps) => Convert.ToInt32(DBUtil.ExecuteScalar(sql, ps.ToList()));

    [Fact]
    public void Seeder_RunsOnlyWhenUsersTableIsEmpty()
    {
        Assert.True(_f.Seeded);
        Assert.False(_f.SeededAgain);
    }

    [Fact]
    public void Seed_TwoCinemasEachWithImax3D2DAuditoriums()
    {
        Assert.Equal(2, CinemaDAL.GetAll().Count);
        var auds = AuditoriumDAL.GetAll();
        Assert.Equal(6, auds.Count);
        foreach (var cinema in auds.GroupBy(a => a.CinemaId))
        {
            var list = cinema.ToList();
            Assert.Contains(list, a => a.Format == AuditoriumFormat.IMAX && a.TotalRows == 12 && a.TotalCols == 18);
            Assert.Contains(list, a => a.Format == AuditoriumFormat.F3D && a.TotalRows == 10 && a.TotalCols == 14);
            Assert.Contains(list, a => a.Format == AuditoriumFormat.F2D && a.TotalRows == 8 && a.TotalCols == 12);
        }
        var seats = SeatDAL.GetAll();
        foreach (var a in auds)
        {
            var s = seats.Where(x => x.AuditoriumId == a.Id).ToList();
            Assert.True(s.Count < a.TotalRows * a.TotalCols, "phòng phải có lối đi (ô không có ghế)");
            Assert.Equal(2, s.Count(x => x.SeatType == SeatType.Wheelchair));
            Assert.Equal(4, s.Count(x => x.IsHouseSeat));
            Assert.Equal(1, s.Count(x => x.IsMaintenance));
            Assert.All(s.Where(x => x.SeatType == SeatType.Vip), x => Assert.InRange(x.RowIndex, 3, a.TotalRows - 1));
            Assert.All(s, x => Assert.InRange(x.BaseViewScore, 0, 100));
            Assert.Contains(s, x => x.BaseViewScore >= 90);
            if (a.Format == AuditoriumFormat.IMAX)
            {
                var sweet = s.Where(x => x.SeatType == SeatType.Sweetbox).ToList();
                Assert.All(sweet, x => Assert.Equal(a.TotalRows, x.RowIndex));
                Assert.All(sweet.GroupBy(x => x.PairGroupId), g => Assert.Equal(2, g.Count()));
                Assert.Equal(s.Count(x => x.RowIndex == a.TotalRows), sweet.Count);
            }
        }
    }

    [Fact]
    public void Seed_DistributorsAndTenMoviesWithLegalData()
    {
        Assert.Equal(3, DistributorDAL.GetAll().Count);
        var movies = MovieDAL.GetAll();
        Assert.Equal(10, movies.Count);
        Assert.Equal(3, movies.Count(m => m.IsVietnameseFilm));
        foreach (var r in new[] { AgeRating.P, AgeRating.K, AgeRating.T13, AgeRating.T16, AgeRating.T18 }) Assert.Contains(movies, m => m.AgeRating == r);
        var c = Assert.Single(movies, m => m.AgeRating == AgeRating.C);
        Assert.Equal(0, N("SELECT COUNT(*) FROM Showtimes WHERE MovieId = @1", c.Id));
        Assert.Equal(2, movies.Count(m => CsvList.Split(m.Genres).Contains(Genre.Animation) && CsvList.Split(m.LanguageFormats).Contains(LanguageFormat.Dubbed)));
        Assert.True(N("SELECT COUNT(*) FROM Showtimes s JOIN Movies m ON m.Id = s.MovieId WHERE s.LanguageFormat = @1 AND m.Genres LIKE @2", LanguageFormat.Dubbed, "%ANIMATION%") > 0);
        Assert.Contains(movies, m => m.Title.Length >= 38 && m.Title.Length <= 42);
        var rules = FilmRentalRuleDAL.GetAll();
        var kdms = KdmLicenseDAL.GetAll();
        foreach (var m in movies)
        {
            Assert.False(string.IsNullOrWhiteSpace(m.ClassificationLicenseNo));
            Assert.False(string.IsNullOrWhiteSpace(m.ContentWarnings));
            Assert.True(m.MinimumTicketPrice > 0);
            Assert.Equal(new[] { 1, 2, 3 }, rules.Where(r => r.MovieId == m.Id).Select(r => r.WeekNumber).OrderBy(x => x));
            Assert.Equal(2, kdms.Count(k => k.MovieId == m.Id));
            Assert.StartsWith("/uploads/seed/poster-", m.PosterUrl);
            Assert.True(File.Exists(Path.Combine(_f.WebRoot, "uploads", "seed", m.PosterUrl!.Split('/').Last())));
        }
        var expiring = kdms.Where(k => k.ValidUntil > _f.Clock.Now && k.ValidUntil <= _f.Clock.Now.AddHours(24)).ToList();
        Assert.Single(expiring);
    }

    [Fact]
    public void Seed_FnbCategoriesItemsCombosMaterialsAndRecipes()
    {
        Assert.Equal(4, CategoryDAL.GetAll().Count);
        var drinks = DrinkDAL.GetAll();
        Assert.Equal(14, drinks.Count(d => !d.IsCombo));
        Assert.Equal(4, drinks.Count(d => d.IsCombo));
        Assert.All(drinks, d => Assert.True(File.Exists(Path.Combine(_f.WebRoot, "uploads", "seed", d.Image!.Split('/').Last()))));
        var materials = RawMaterialDAL.GetAll();
        Assert.Equal(24, materials.Count);
        Assert.All(materials.GroupBy(m => m.CinemaId), g => Assert.Equal(12, g.Count()));
        // Mọi món có định mức với nguyên liệu của từng rạp
        Assert.Equal(0, N("SELECT COUNT(*) FROM Drinks d CROSS JOIN Cinemas c WHERE NOT EXISTS (SELECT 1 FROM ProductRecipes pr JOIN RawMaterials rm ON rm.Id = pr.RawMaterialId WHERE pr.DrinkId = d.Id AND rm.CinemaId = c.Id)"));
        Assert.Contains(materials, m => m.ActualStock - m.ReservedStock > m.MinThreshold && m.ActualStock - m.ReservedStock <= m.MinThreshold * 1.5m);
        Assert.All(materials, m => Assert.True(m.ActualStock >= m.ReservedStock));
        // Sổ kho khớp tồn: nhập − xuất − hủy = tồn thực tế
        Assert.Equal(0, N("SELECT COUNT(*) FROM RawMaterials rm WHERE rm.ActualStock <> (SELECT SUM(CASE WHEN Type = @1 THEN Quantity WHEN Type IN (@2, @3) THEN -Quantity ELSE 0 END) FROM InventoryTransactions it WHERE it.RawMaterialId = rm.Id)",
            InventoryTxnType.Import, InventoryTxnType.Consume, InventoryTxnType.Wastage));
    }

    [Fact]
    public void Seed_PricingAudienceDiscountsAndHolidays()
    {
        var rules = PricingRuleDAL.GetAll();
        foreach (var cinema in CinemaDAL.GetAll())
            foreach (var type in PricingRuleType.All)
                foreach (var key in PricingRuleKey.For(type))
                    Assert.Single(rules, r => r.CinemaId == cinema.Id && r.RuleType == type && r.RuleKey == key);
        var discounts = AudienceDiscountDAL.GetAll().ToDictionary(d => d.AudienceType);
        Assert.Equal(AudienceType.All.Length, discounts.Count);
        Assert.Equal((0m, false), (discounts[AudienceType.Adult].DiscountPercent, discounts[AudienceType.Adult].PosOnly));
        Assert.Equal((10m, false, true), (discounts[AudienceType.U22].DiscountPercent, discounts[AudienceType.U22].PosOnly, discounts[AudienceType.U22].RequiresDocument));
        Assert.Equal((20m, 20m, true), (discounts[AudienceType.ChildU13].DiscountPercent, discounts[AudienceType.ChildU13].LegalMinPercent, discounts[AudienceType.ChildU13].PosOnly));
        Assert.Equal((50m, 50m), (discounts[AudienceType.DisabledSevere].DiscountPercent, discounts[AudienceType.DisabledSevere].LegalMinPercent));
        Assert.Equal((100m, 100m), (discounts[AudienceType.DisabledExtreme].DiscountPercent, discounts[AudienceType.DisabledExtreme].LegalMinPercent));
        var holidays = HolidayDAL.GetAll();
        Assert.True(holidays.Count >= 10);
        Assert.All(holidays, h => Assert.Equal(2026, h.HolidayDate.Year));
    }

    [Fact]
    public void Seed_ShowtimesTodayToSixDaysLaterValidAndSomeLocked()
    {
        foreach (var aud in AuditoriumDAL.GetAll())
            for (int d = 0; d <= 6; d++)
                Assert.True(N("SELECT COUNT(*) FROM Showtimes WHERE AuditoriumId = @1 AND StartTime >= @2 AND StartTime < @3", aud.Id, Today.AddDays(d).AddHours(6), Today.AddDays(d + 1).AddHours(6)) >= 4);
        Assert.Equal(0, N("SELECT COUNT(*) FROM Showtimes a JOIN Showtimes b ON a.AuditoriumId = b.AuditoriumId AND a.Id < b.Id AND a.StartTime < b.CleaningEndTime AND b.StartTime < a.CleaningEndTime"));
        Assert.Equal(0, N("SELECT COUNT(*) FROM Showtimes s JOIN Auditoriums a ON a.Id = s.AuditoriumId WHERE NOT EXISTS (SELECT 1 FROM KdmLicenses k WHERE k.MovieId = s.MovieId AND k.CinemaId = a.CinemaId AND k.ValidFrom <= s.FeatureStartTime AND s.EndTime <= k.ValidUntil)"));
        Assert.Equal(0, N("SELECT COUNT(*) FROM Showtimes s JOIN Movies m ON m.Id = s.MovieId WHERE m.AgeRating = @1 OR m.ClassificationLicenseNo IS NULL OR CHARINDEX(s.LanguageFormat, m.LanguageFormats) = 0", AgeRating.C));
        Assert.Equal(0, N("SELECT COUNT(*) FROM Showtimes WHERE CAST(StartTime AS TIME) < @1 OR CAST(StartTime AS TIME) > @2", _f.Rules.OperatingDayStart, _f.Rules.LastShowStartLatest));
        Assert.Equal(0, N("SELECT COUNT(*) FROM Showtimes s WHERE (SELECT COUNT(*) FROM ShowtimeSeats ss WHERE ss.ShowtimeId = s.Id) <> (SELECT COUNT(*) FROM Seats x WHERE x.AuditoriumId = s.AuditoriumId)"));
        Assert.Equal(0, N("SELECT COUNT(*) FROM ShowtimeSeats ss JOIN Seats s ON s.Id = ss.SeatId WHERE s.IsHouseSeat = 1 AND ss.Status <> @1", ShowtimeSeatStatus.HouseHold));
        int locked = N("SELECT COUNT(DISTINCT ss.ShowtimeId) FROM ShowtimeSeats ss JOIN Showtimes s ON s.Id = ss.ShowtimeId WHERE ss.Status = @1 AND s.StartTime > @2", ShowtimeSeatStatus.Booked, _f.Clock.Now);
        Assert.True(locked >= 10, $"chỉ có {locked} suất sắp chiếu đã có vé");
    }

    [Fact]
    public void Seed_PaidBillsOf30DaysBothChannelsWithTicketsAndFnb()
    {
        for (int d = -29; d <= 0; d++)
            Assert.True(N("SELECT COUNT(*) FROM Bills WHERE Status = @1 AND PaidAt >= @2 AND PaidAt < @3", BillStatus.Finish, Today.AddDays(d), Today.AddDays(d + 1)) > 20, $"ngày {Today.AddDays(d):dd/MM} ít hóa đơn");
        foreach (var ch in new[] { BillChannel.Pos, BillChannel.Online })
            Assert.True(N("SELECT COUNT(*) FROM Bills WHERE Status = @1 AND Channel = @2", BillStatus.Finish, ch) > 500);
        Assert.True(N("SELECT COUNT(*) FROM Bills b WHERE b.Status = @1 AND EXISTS (SELECT 1 FROM Tickets t WHERE t.BillId = b.Id) AND EXISTS (SELECT 1 FROM BillDetails d WHERE d.BillId = b.Id)", BillStatus.Finish) > 300);
        // Cuối tuần đông hơn ngày thường; suất tối đông hơn suất ban ngày
        var perDay = Q("SELECT CAST(PaidAt AS DATE) AS D, COUNT(*) AS N FROM Bills WHERE Status = @1 AND PaidAt >= @2 GROUP BY CAST(PaidAt AS DATE)", BillStatus.Finish, Today.AddDays(-29))
            .Rows.Cast<DataRow>().Select(r => ((DateTime)r["D"], Convert.ToInt32(r["N"]))).ToList();
        double weekend = perDay.Where(x => x.Item1.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday).Average(x => x.Item2);
        double weekday = perDay.Where(x => x.Item1.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).Average(x => x.Item2);
        Assert.True(weekend > weekday * 1.15, $"cuối tuần {weekend:0} hóa đơn/ngày, ngày thường {weekday:0}");
        var bySlot = Q("SELECT CASE WHEN CAST(s.FeatureStartTime AS TIME) >= '17:00' AND CAST(s.FeatureStartTime AS TIME) < '22:00' THEN 1 ELSE 0 END AS Evening, 1.0 * COUNT(t.Id) / COUNT(DISTINCT s.Id) AS PerShow " +
                       "FROM Showtimes s LEFT JOIN Tickets t ON t.ShowtimeId = s.Id WHERE s.StartTime < @1 GROUP BY CASE WHEN CAST(s.FeatureStartTime AS TIME) >= '17:00' AND CAST(s.FeatureStartTime AS TIME) < '22:00' THEN 1 ELSE 0 END", _f.Clock.Now)
            .Rows.Cast<DataRow>().ToDictionary(r => Convert.ToInt32(r["Evening"]), r => Convert.ToDouble(r["PerShow"]));
        Assert.True(bySlot[1] > bySlot[0] * 1.5, $"suất tối {bySlot[1]:0.0} vé, ban ngày {bySlot[0]:0.0} vé");
        // Hủy với các lý do khác nhau
        var reasons = Q("SELECT DISTINCT CancelReason FROM Bills WHERE Status = @1", BillStatus.Cancel).Rows.Cast<DataRow>().Select(r => (string)r[0]).ToList();
        Assert.True(reasons.Count >= 4);
        Assert.Equal(0, N("SELECT COUNT(*) FROM Bills WHERE (Status = @1 AND PaidAt IS NULL) OR (Status = @2 AND (CancelReason IS NULL OR CancelledAt IS NULL))", BillStatus.Finish, BillStatus.Cancel));
        // Giá đã gồm VAT: mọi vé và dòng bắp nước lưu VatRate theo cấu hình
        Assert.Equal(0, N("SELECT COUNT(*) FROM Tickets WHERE VatRate <> @1", _f.Rules.TicketVatPercent));
        Assert.Equal(0, N("SELECT COUNT(*) FROM BillDetails WHERE VatRate <> @1", _f.Rules.FnbVatPercent));
        Assert.Equal(0, N("SELECT COUNT(*) FROM Bills b WHERE b.Status = @1 AND b.Total <> ISNULL((SELECT SUM(FinalPrice) FROM Tickets t WHERE t.BillId = b.Id), 0) + ISNULL((SELECT SUM(LineFinalAmount) FROM BillDetails d WHERE d.BillId = b.Id), 0)", BillStatus.Finish));
        Assert.Equal(N("SELECT COUNT(*) FROM ShowtimeSeats WHERE Status = @1", ShowtimeSeatStatus.Booked),
            N("SELECT COUNT(*) FROM Tickets t JOIN Bills b ON b.Id = t.BillId WHERE b.Status = @1 AND t.CheckInStatus IN (@2, @3)", BillStatus.Finish, TicketCheckInStatus.NotCheckedIn, TicketCheckInStatus.CheckedIn));
        // Đủ số liệu cho thống kê và đối soát
        Assert.Equal(5, StatisticDAL.TopBestSellingDrinks(Today.AddDays(-29), Today).Count);
        Assert.Equal(5, StatisticDAL.TopBestSellingMovies(Today.AddDays(-29), Today).Count);
        Assert.All(StatisticDAL.RevenueByDay(Today.AddDays(-29), Today), r => Assert.True(r.TotalRevenue > 0));
        var settlement = StatisticDAL.GetBoxOfficeSettlement(MovieDAL.GetAll().First(m => m.IsBlockbuster).Id, 2);
        Assert.True(settlement.TicketCount > 0 && settlement.DistributorAmount > 0);
    }

    [Fact]
    public void Seed_AccountsShiftVouchersAndSampleCustomer()
    {
        var expected = new (string Email, string Role, int? CinemaIndex)[]
        {
            ("manager@fpolycinema.vn", Roles.Manager, null), ("shift@fpolycinema.vn", Roles.ShiftManager, 0), ("cashier@fpolycinema.vn", Roles.POSCashier, 0),
            ("gate@fpolycinema.vn", Roles.TicketInspector, 0), ("khach@fpolycinema.vn", Roles.Customer, null), ("khach2@fpolycinema.vn", Roles.Customer, null),
        };
        var cinemas = CinemaDAL.GetAll().OrderBy(c => c.Id).ToList();
        foreach (var (email, role, ci) in expected)
        {
            var u = UserDAL.Authenticate(email, "123456");
            Assert.NotNull(u);
            Assert.Equal(role, u!.Role);
            Assert.Equal(ci is int i ? cinemas[i].Id : null, u.CinemaId);
            Assert.NotEqual("123456", u.PasswordHash);
            if (role is Roles.Manager or Roles.ShiftManager) Assert.True(PasswordUtil.Verify(u.ManagerPinHash, "246810"));
        }
        var khach = UserDAL.GetByEmail("khach@fpolycinema.vn")!;
        Assert.Equal(LoyaltyTier.Vip, khach.Tier);
        Assert.True(khach.LoyaltyPoints > 0);
        Assert.True(khach.WalletBalance > 0);
        Assert.Matches("^TV\\d{8}$", khach.MemberCode);
        Assert.Equal(khach.LoyaltyPoints, N("SELECT ISNULL(SUM(Points), 0) FROM PointTransactions WHERE UserId = @1", khach.Id));
        Assert.Equal(khach.WalletBalance, Convert.ToInt64(DBUtil.ExecuteScalar("SELECT ISNULL(SUM(Amount), 0) FROM WalletTransactions WHERE UserId = @1", new List<object> { khach.Id })));
        Assert.True(N("SELECT COUNT(*) FROM Bills b JOIN Showtimes s ON s.Id = b.ShowtimeId WHERE b.CustomerId = @1 AND b.Status = @2 AND s.StartTime > @3", khach.Id, BillStatus.Finish, _f.Clock.Now) >= 1);

        var cashier = UserDAL.GetByEmail("cashier@fpolycinema.vn")!;
        var open = PosShiftDAL.GetAll().Where(s => s.Status == ShiftStatus.Open && s.UserId == cashier.Id).ToList();
        var shift = Assert.Single(open);
        Assert.Equal("POS-01", shift.TerminalCode);
        Assert.Equal(cashier.CinemaId, shift.CinemaId);

        var vouchers = VoucherDAL.GetAll();
        Assert.Equal(2, vouchers.Count(v => v.Source == VoucherSource.Promo && v.OwnerUserId == null && v.Active));
        Assert.Single(vouchers, v => v.Source == VoucherSource.Compensation && v.OwnerUserId == khach.Id && v.ValidUntil > _f.Clock.Now);
    }
}
