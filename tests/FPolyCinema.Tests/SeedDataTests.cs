using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Infrastructure.Seed;

namespace FPolyCinema.Tests;

/// <summary>Unit test cho dữ liệu mẫu của DbSeeder (SPEC 8.1 bước 6): danh mục, sơ đồ ghế, lịch chiếu hợp lệ.</summary>
public class SeedDataTests
{
    private static readonly CinemaRules Rules = new();
    private static readonly DateTime Today = new(2026, 11, 6);

    // ---------------------------------------------------------------- danh mục tĩnh
    [Fact]
    public void Catalog_TwoCinemasThreeAuditoriumsEach()
    {
        Assert.Equal(2, SeedCatalog.Cinemas.Length);
        var a = SeedCatalog.Auditoriums;
        Assert.Equal(3, a.Length);
        Assert.Equal((AuditoriumFormat.IMAX, 12, 18, true), (a[0].Format, a[0].Rows, a[0].Cols, a[0].SweetboxLastRow));
        Assert.Equal((AuditoriumFormat.F3D, 10, 14), (a[1].Format, a[1].Rows, a[1].Cols));
        Assert.Equal((AuditoriumFormat.F2D, 8, 12), (a[2].Format, a[2].Rows, a[2].Cols));
        Assert.All(a, x => Assert.NotEmpty(x.AisleCols));
    }

    [Fact]
    public void Catalog_TenFictionalMoviesCoveringRules()
    {
        var m = SeedCatalog.Movies;
        Assert.Equal(3, SeedCatalog.Distributors.Length);
        Assert.Equal(10, m.Length);
        Assert.Equal(3, m.Count(x => x.IsVietnamese));
        foreach (var r in new[] { AgeRating.P, AgeRating.K, AgeRating.T13, AgeRating.T16, AgeRating.T18 }) Assert.Contains(m, x => x.AgeRating == r);
        Assert.Single(m, x => x.AgeRating == AgeRating.C);
        Assert.Equal(2, m.Count(x => x.Genres.Contains(Genre.Animation) && x.LanguageFormats.Contains(LanguageFormat.Dubbed)));
        Assert.Contains(m, x => x.Title.Length >= 38 && x.Title.Length <= 42);
        Assert.All(m, x =>
        {
            Assert.NotEmpty(x.ContentWarnings);
            Assert.All(x.ContentWarnings, w => Assert.Contains(w, ContentWarning.All));
            Assert.All(x.Genres, g => Assert.Contains(g, Genre.All));
            Assert.All(x.LanguageFormats, l => Assert.Contains(l, LanguageFormat.All));
            Assert.Equal(3, x.SharePercents.Length);
            Assert.True(x.MinimumTicketPrice > 0);
        });
        Assert.Equal(m.Length, m.Select(x => x.Title).Distinct().Count());
    }

    [Fact]
    public void Catalog_FnbFourCategoriesAbout14ItemsAnd4CombosWithRecipes()
    {
        Assert.Equal(4, SeedCatalog.Categories.Length);
        Assert.Equal(14, SeedCatalog.Drinks.Count(d => !d.IsCombo));
        Assert.Equal(4, SeedCatalog.Drinks.Count(d => d.IsCombo));
        Assert.Equal(12, SeedCatalog.Materials.Length);
        var materials = SeedCatalog.Materials.Select(x => x.Name).ToHashSet();
        Assert.All(SeedCatalog.Drinks, d =>
        {
            Assert.Contains(d.Category, SeedCatalog.Categories);
            Assert.NotEmpty(d.Recipe);
            Assert.All(d.Recipe, r => { Assert.Contains(r.Material, materials); Assert.True(r.Quantity > 0); });
            Assert.Equal(d.Recipe.Length, d.Recipe.Select(r => r.Material).Distinct().Count());
        });
        var near = SeedCatalog.Materials[SeedCatalog.NearThresholdMaterialIndex];
        Assert.True(near.Stock > near.MinThreshold && near.Stock <= near.MinThreshold * 1.5m);
    }

    [Fact]
    public void Catalog_PricingCoversAllSixDimensionsForEachCinema()
    {
        for (int c = 0; c < SeedCatalog.Cinemas.Length; c++)
        {
            var rows = SeedCatalog.PricingFor(c);
            foreach (var type in PricingRuleType.All)
                foreach (var key in PricingRuleKey.For(type))
                    Assert.Single(rows, r => r.Type == type && r.Key == key);
            Assert.All(rows, r => Assert.True(PricingRuleKey.IsValid(r.Type, r.Key)));
            Assert.Contains(rows, r => r.Amount < 0);
        }
    }

    [Fact]
    public void PricingRuleKey_RejectsKeysOfAnotherRuleType()
    {
        Assert.True(PricingRuleKey.IsValid(PricingRuleType.Base, PricingRuleKey.Default));
        Assert.False(PricingRuleKey.IsValid(PricingRuleType.Base, DayType.Weekday));
        Assert.False(PricingRuleKey.IsValid(PricingRuleType.DayType, TimeSlot.Morning));
        Assert.False(PricingRuleKey.IsValid(PricingRuleType.Format, SeatType.Vip));
        Assert.False(PricingRuleKey.IsValid(PricingRuleType.Blockbuster, PricingRuleKey.Default));
        Assert.False(PricingRuleKey.IsValid("KHONG_CO", PricingRuleKey.Default));
        Assert.Equal(new[] { "2D", "3D", "IMAX", "4DX" }, PricingRuleKey.For(PricingRuleType.Format));
        // Mặc định tỷ lệ tích điểm có đủ các hạng của LoyaltyTier
        Assert.Equal(LoyaltyTier.All.OrderBy(x => x), new CinemaRules().PointEarnPercent.Keys.OrderBy(x => x));
    }

    [Fact]
    public void Catalog_AudienceDiscountsMatchContracts()
    {
        var expected = new Dictionary<string, (decimal, decimal, bool, bool)>
        {
            [AudienceType.Adult] = (0, 0, false, false),
            [AudienceType.U22] = (10, 0, false, true),
            [AudienceType.ChildU13] = (20, 20, true, true),
            [AudienceType.Child13To15] = (20, 20, true, true),
            [AudienceType.Senior] = (20, 20, true, true),
            [AudienceType.Merit] = (20, 20, true, true),
            [AudienceType.Hardship] = (20, 20, true, true),
            [AudienceType.DisabledSevere] = (50, 50, true, true),
            [AudienceType.DisabledExtreme] = (100, 100, true, true),
        };
        Assert.Equal(AudienceType.All.Length, SeedCatalog.AudienceDiscounts.Length);
        foreach (var d in SeedCatalog.AudienceDiscounts)
        {
            Assert.Equal(expected[d.Type], (d.Discount, d.LegalMin, d.PosOnly, d.RequiresDocument));
            Assert.True(d.Discount >= d.LegalMin);
        }
    }

    [Fact]
    public void Catalog_Holidays2026AndSampleAccounts()
    {
        Assert.All(SeedCatalog.Holidays2026, h => Assert.Equal(2026, h.Date.Year));
        Assert.Contains(SeedCatalog.Holidays2026, h => h.Date == new DateTime(2026, 2, 17));
        Assert.Contains(SeedCatalog.Holidays2026, h => h.Date == new DateTime(2026, 9, 2));
        var acc = SeedCatalog.Accounts.ToDictionary(a => a.Email);
        Assert.Equal((Roles.Manager, (int?)null, true), (acc["manager@fpolycinema.vn"].Role, acc["manager@fpolycinema.vn"].CinemaIndex, acc["manager@fpolycinema.vn"].HasPin));
        Assert.Equal((Roles.ShiftManager, (int?)0, true), (acc["shift@fpolycinema.vn"].Role, acc["shift@fpolycinema.vn"].CinemaIndex, acc["shift@fpolycinema.vn"].HasPin));
        Assert.Equal((Roles.POSCashier, (int?)0), (acc["cashier@fpolycinema.vn"].Role, acc["cashier@fpolycinema.vn"].CinemaIndex));
        Assert.Equal((Roles.TicketInspector, (int?)0), (acc["gate@fpolycinema.vn"].Role, acc["gate@fpolycinema.vn"].CinemaIndex));
        Assert.Equal(Roles.Customer, acc["khach@fpolycinema.vn"].Role);
        Assert.Equal(Roles.Customer, acc["khach2@fpolycinema.vn"].Role);
        Assert.Equal("123456", SeedCatalog.SamplePassword);
        Assert.Equal("246810", SeedCatalog.SampleManagerPin);
        Assert.All(SeedCatalog.Accounts, a => Assert.True(Roles.IsValid(a.Role)));
        Assert.Equal(SeedCatalog.Accounts.Length, acc.Count);
    }

    // ---------------------------------------------------------------- sơ đồ ghế
    [Fact]
    public void SeatLayout_ImaxHasSweetboxPairsWheelchairHouseAndMaintenance()
    {
        var spec = SeedCatalog.Auditoriums[0];
        var seats = SeatLayoutPlanner.Plan(spec, Rules.HouseSeatsPerAuditorium);
        Assert.DoesNotContain(seats, s => spec.AisleCols.Contains(s.Col));
        Assert.Equal(spec.Rows * (spec.Cols - spec.AisleCols.Length), seats.Count);

        var sweet = seats.Where(s => s.SeatType == SeatType.Sweetbox).ToList();
        Assert.All(sweet, s => Assert.Equal(spec.Rows, s.Row));
        Assert.All(sweet.GroupBy(s => s.PairIndex), g =>
        {
            Assert.Equal(2, g.Count());
            var cols = g.Select(s => s.Col).OrderBy(c => c).ToArray();
            Assert.Equal(cols[0] + 1, cols[1]);
        });
        Assert.Equal(2, seats.Count(s => s.SeatType == SeatType.Wheelchair));
        Assert.Equal(4, seats.Count(s => s.IsHouseSeat));
        Assert.Equal(1, seats.Count(s => s.IsMaintenance));
        Assert.DoesNotContain(seats, s => s.IsHouseSeat && s.IsMaintenance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SeatLayout_VipInMiddleRowsLabelsAndScores(int auditoriumIndex)
    {
        var spec = SeedCatalog.Auditoriums[auditoriumIndex];
        var seats = SeatLayoutPlanner.Plan(spec, Rules.HouseSeatsPerAuditorium);
        var vip = seats.Where(s => s.SeatType == SeatType.Vip).ToList();
        Assert.NotEmpty(vip);
        Assert.All(vip, s => Assert.InRange(s.Row, 3, spec.Rows - 1));
        Assert.DoesNotContain(seats, s => s.Row == 1 && s.SeatType == SeatType.Vip);
        Assert.Equal(seats.Count, seats.Select(s => s.Label).Distinct().Count());
        // Nhãn = chữ hàng + số thứ tự ghế thật (không đếm lối đi)
        var firstCenterCol = Enumerable.Range(1, spec.Cols).First(c => c > spec.AisleCols[0]);
        Assert.Equal($"A{firstCenterCol - 1}", seats.Single(s => s.Row == 1 && s.Col == firstCenterCol).Label);
        Assert.All(seats, s =>
        {
            Assert.InRange(s.BaseViewScore, 0, 100);
            var rowCols = seats.Where(x => x.Row == s.Row).Select(x => x.Col).ToHashSet();
            Assert.Equal(SeatScoring.Score(spec.Rows, spec.Cols, s.Row, s.Col, s.SeatType, SeatPersona.BestViewAudio, SeatScoring.DefaultIdealRowRatio,
                SeatScoring.IsSegmentEnd(rowCols, s.Col, spec.Cols)), s.BaseViewScore);
        });
        Assert.True(seats.Single(s => s.IsMaintenance).SeatType == SeatType.Standard);
    }

    // ---------------------------------------------------------------- lịch chiếu mẫu
    private static (List<PlannedShowtime> Plan, List<PlanMovie> Movies, List<PlanKdm> Kdms) BuildPlan()
    {
        var movies = SeedCatalog.Movies.Select((m, i) => new PlanMovie(i, m.DurationMinutes, m.AgeRating, m.LanguageFormats,
            Today.AddDays(m.ReleaseOffsetDays), m.IsVietnamese, true, m.Genres.Contains(Genre.Animation))).ToList();
        var kdms = new List<PlanKdm>();
        var now = Today.AddHours(21);
        for (int i = 0; i < movies.Count; i++)
            for (int c = 0; c < 2; c++)
                kdms.Add(new PlanKdm(i, c, movies[i].ReleaseDate.AddDays(-1),
                    i == SeedCatalog.KdmExpiringMovieIndex && c == 0 ? now.AddHours(20) : Today.AddDays(46)));
        var dates = Enumerable.Range(-DbSeeder.HistoryDays, DbSeeder.HistoryDays + DbSeeder.FutureDays + 1).Select(d => Today.AddDays(d)).ToList();
        return (SchedulePlanner.Plan(dates, 2, 3, movies, kdms, Rules), movies, kdms);
    }

    [Fact]
    public void Schedule_CoversTodayToSixDaysLaterForEveryAuditorium()
    {
        var (plan, _, _) = BuildPlan();
        for (int d = 0; d <= DbSeeder.FutureDays; d++)
            for (int c = 0; c < 2; c++)
                for (int a = 0; a < 3; a++)
                    Assert.True(plan.Count(p => p.OperatingDate == Today.AddDays(d) && p.CinemaIndex == c && p.AuditoriumIndex == a) >= 4);
    }

    [Fact]
    public void Schedule_SeededShowtimesSatisfyAllConstraints()
    {
        var (plan, movies, kdms) = BuildPlan();
        Assert.NotEmpty(plan);
        foreach (var room in plan.GroupBy(p => (p.CinemaIndex, p.AuditoriumIndex)))
        {
            var list = room.OrderBy(p => p.Times.StartTime).ToList();
            for (int i = 1; i < list.Count; i++)
                Assert.True(list[i].Times.StartTime >= list[i - 1].Times.CleaningEndTime, $"Chồng giờ ở phòng {room.Key}: {list[i].Times.StartTime}");
        }
        foreach (var p in plan)
        {
            var m = movies[p.MovieIndex];
            var t = p.Times;
            Assert.NotEqual(AgeRating.C, m.AgeRating);
            Assert.True(m.HasLicense);
            Assert.True(m.ReleaseDate <= p.OperatingDate);
            Assert.Contains(p.LanguageFormat, m.LanguageFormats);
            Assert.InRange(t.StartTime.TimeOfDay, Rules.OperatingDayStart, Rules.LastShowStartLatest);
            Assert.Equal(0, t.StartTime.Minute % Rules.ScheduleRoundMinutes);
            Assert.Equal(t.StartTime.AddMinutes(Rules.AdsMinutes), t.FeatureStartTime);
            Assert.Equal(t.FeatureStartTime.AddMinutes(m.DurationMinutes), t.EndTime);
            Assert.True(t.CleaningEndTime >= t.EndTime.AddMinutes(Rules.CleaningMinutes) && t.CleaningEndTime.Minute % Rules.ScheduleRoundMinutes == 0);
            Assert.Contains(kdms, k => k.MovieIndex == p.MovieIndex && k.CinemaIndex == p.CinemaIndex && k.ValidFrom <= t.FeatureStartTime && t.EndTime <= k.ValidUntil);
            if (m.AgeRating == AgeRating.K) Assert.True(SchedulePlanner.EndsBefore(t.EndTime, p.OperatingDate, Rules.ChildU13LatestEnd));
            if (m.AgeRating == AgeRating.T13) Assert.True(SchedulePlanner.EndsBefore(t.EndTime, p.OperatingDate, Rules.ChildU16LatestEnd));
        }
        Assert.All(plan.GroupBy(p => (p.CinemaIndex, p.Times.StartTime)), g => Assert.True(g.Count() <= Rules.MaxSameMinuteStarts));
    }

    [Fact]
    public void Schedule_VietnameseFilmShareAboveTarget()
    {
        var (plan, movies, _) = BuildPlan();
        foreach (var cinema in plan.GroupBy(p => p.CinemaIndex))
        {
            decimal share = (decimal)cinema.Count(p => movies[p.MovieIndex].IsVietnamese) / cinema.Count();
            Assert.True(share >= Rules.VietnameseFilmTargetShare, $"Rạp {cinema.Key}: phim Việt chỉ chiếm {share:P1}");
        }
    }

    [Fact]
    public void Schedule_ExpiringKdmMovieHasNoShowAfterExpiry()
    {
        var (plan, _, kdms) = BuildPlan();
        var kdm = kdms.Single(k => k.MovieIndex == SeedCatalog.KdmExpiringMovieIndex && k.CinemaIndex == 0);
        Assert.True(kdm.ValidUntil - Today.AddHours(21) <= TimeSpan.FromHours(24));
        Assert.DoesNotContain(plan, p => p.MovieIndex == kdm.MovieIndex && p.CinemaIndex == 0 && p.Times.EndTime > kdm.ValidUntil);
    }
}
