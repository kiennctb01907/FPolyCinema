using System.Data;
using System.Reflection;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Utils;
using Microsoft.Extensions.Configuration;

namespace FPolyCinema.Tests;

/// <summary>Unit test cho IClock, cấu hình CinemaRules, DBUtil (phần không cần DB) và DbInitializer.</summary>
public class InfrastructureTests
{
    [Fact]
    public void SystemClock_ReturnsVietnamTime()
    {
        var clock = new SystemClock(new CinemaRules());
        var expected = DateTime.UtcNow.AddHours(7);
        Assert.True(Math.Abs((clock.Now - expected).TotalSeconds) < 5, $"Giờ trả về {clock.Now:O}, mong đợi khoảng {expected:O}");
        Assert.Equal(DateTimeKind.Unspecified, clock.Now.Kind);
        Assert.Equal(0, clock.Now.Millisecond);
        Assert.Equal(clock.Now.Date, clock.Today);
    }

    [Fact]
    public void SystemClock_UnknownZoneFallsBackToUtcPlus7()
    {
        var zone = SystemClock.ResolveTimeZone("Khong/Ton_Tai");
        Assert.Equal(TimeSpan.FromHours(7), zone.BaseUtcOffset);
    }

    [Fact]
    public void FakeClock_IsUsedForTimeInTests()
    {
        IClock clock = new FakeClock(new DateTime(2026, 2, 17, 9, 0, 0));
        Assert.Equal(new DateTime(2026, 2, 17), clock.Today);
        ((FakeClock)clock).Advance(TimeSpan.FromMinutes(90));
        Assert.Equal(new DateTime(2026, 2, 17, 10, 30, 0), clock.Now);
    }

    private static IConfiguration LoadAppSettings() => new ConfigurationBuilder().AddJsonFile(RepoPaths.File("appsettings.json")).Build();

    [Fact]
    public void AppSettings_CinemaRulesDefaultsMatchContracts()
    {
        var bound = LoadAppSettings().GetSection(CinemaRules.SectionName).Get<CinemaRules>()!;
        var defaults = new CinemaRules();
        foreach (var p in typeof(CinemaRules).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var a = p.GetValue(bound);
            var b = p.GetValue(defaults);
            if (a is Dictionary<string, int> da && b is Dictionary<string, int> db)
                Assert.Equal(db.OrderBy(x => x.Key), da.OrderBy(x => x.Key));
            else
                Assert.True(Equals(a, b), $"CinemaRules:{p.Name} trong appsettings.json là {a}, mặc định là {b}");
        }
        Assert.Equal(TimeSpan.FromHours(8), bound.OperatingDayStart);
        Assert.Equal(new TimeSpan(23, 30, 0), bound.LastShowStartLatest);
        Assert.Equal(0.20m, bound.VietnameseFilmMinShare);
        Assert.Equal(7, bound.PointEarnPercent["VIP"]);
    }

    [Fact]
    public void AppSettings_ConnectionStringKeyPresentButEmpty()
    {
        var cfg = LoadAppSettings();
        Assert.NotNull(cfg.GetSection("ConnectionStrings").GetChildren().FirstOrDefault(c => c.Key == "Default"));
        Assert.Equal(string.Empty, cfg.GetConnectionString("Default"));
    }

    [Fact]
    public void Configuration_CanBeOverriddenByEnvironmentVariables()
    {
        const string prefix = "FPCTEST_";
        Environment.SetEnvironmentVariable(prefix + "CinemaRules__SeatHoldMinutes", "12");
        Environment.SetEnvironmentVariable(prefix + "CinemaRules__PointEarnPercent__VIP", "8");
        Environment.SetEnvironmentVariable(prefix + "ConnectionStrings__Default", "Server=db;Database=X");
        try
        {
            var cfg = new ConfigurationBuilder()
                .AddJsonFile(RepoPaths.File("appsettings.json"))
                .AddEnvironmentVariables(prefix)
                .Build();
            var rules = cfg.GetSection(CinemaRules.SectionName).Get<CinemaRules>()!;
            Assert.Equal(12, rules.SeatHoldMinutes);
            Assert.Equal(8, rules.PointEarnPercent["VIP"]);
            Assert.Equal("Server=db;Database=X", cfg.GetConnectionString("Default"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(prefix + "CinemaRules__SeatHoldMinutes", null);
            Environment.SetEnvironmentVariable(prefix + "CinemaRules__PointEarnPercent__VIP", null);
            Environment.SetEnvironmentVariable(prefix + "ConnectionStrings__Default", null);
        }
    }

    [Fact]
    public void DBUtil_BuildsParametersInOrderWithTypes()
    {
        var ps = DBUtil.BuildParameters(new List<object>
        {
            "Bắp", 5, 120_000L, true, new DateTime(2026, 11, 6, 9, 30, 0), 8.5m, DBNull.Value, DBUtil.Value(null),
        });
        Assert.Equal(new[] { "@1", "@2", "@3", "@4", "@5", "@6", "@7", "@8" }, ps.Select(p => p.ParameterName));
        Assert.Equal(SqlDbType.NVarChar, ps[0].SqlDbType);
        Assert.Equal(SqlDbType.Int, ps[1].SqlDbType);
        Assert.Equal(SqlDbType.BigInt, ps[2].SqlDbType);
        Assert.Equal(SqlDbType.Bit, ps[3].SqlDbType);
        Assert.Equal(SqlDbType.DateTime2, ps[4].SqlDbType);
        Assert.Equal(SqlDbType.Decimal, ps[5].SqlDbType);
        Assert.Equal(DBNull.Value, ps[6].Value);
        Assert.Equal(DBNull.Value, ps[7].Value);
        Assert.Empty(DBUtil.BuildParameters(null));
    }

    [Fact]
    public void DBUtil_InParams_AppendsPlaceholdersAfterExistingParameters()
    {
        var ps = new List<object> { "finish", 3 };
        var sql = DBUtil.InParams(new object[] { 10, 11, 12 }, ps);
        Assert.Equal("@3, @4, @5", sql);
        Assert.Equal(5, ps.Count);
        Assert.Equal(12, ps[4]);
        Assert.Equal("NULL", DBUtil.InParams(Array.Empty<object>(), ps));
    }

    [Fact]
    public void DBUtil_InitRejectsEmptyConnectionString()
        => Assert.Throws<InvalidOperationException>(() => DBUtil.Init(" "));

    [Fact]
    public void DbInitializer_SplitsBatchesOnGoLinesOnly()
    {
        var script = "CREATE TABLE A (Id INT);\r\nGO\r\n  go  \nCREATE TABLE GOODS (Id INT); -- GO trong chú thích\nGO\n/* chỉ có chú thích */\nGO\nSELECT 'GO' AS X;";
        var batches = DbInitializer.SplitBatches(script);
        Assert.Equal(3, batches.Count);
        Assert.StartsWith("CREATE TABLE A", batches[0]);
        Assert.Contains("GOODS", batches[1]);
        Assert.Equal("SELECT 'GO' AS X;", batches[2]);
    }

    [Fact]
    public void DbInitializer_ScriptsCreateAllTablesAndProceduresAreRerunnable()
    {
        var schema = File.ReadAllText(RepoPaths.File("Database", "01_schema.sql"));
        var procs = File.ReadAllText(RepoPaths.File("Database", "02_procedures.sql"));
        string[] tables =
        {
            "Users", "PasswordResetOtps", "EmailOutbox", "Cinemas", "Categories", "Drinks", "RawMaterials", "ProductRecipes",
            "InventoryTransactions", "WastageLogs", "WastageLogItems", "Distributors", "Movies", "FilmRentalRules", "KdmLicenses",
            "Auditoriums", "Seats", "Showtimes", "ShowtimeSeats", "Holidays", "PricingRules", "AudienceDiscounts", "Bills",
            "BillDetails", "Tickets", "Payments", "Vouchers", "WalletTransactions", "PointTransactions", "CorporateBookings",
            "PosShifts", "SupervisorApprovals", "Notifications", "AuditLogs", "SchemaVersions",
        };
        Assert.Equal(35, tables.Length);
        foreach (var t in tables) Assert.Contains($"CREATE TABLE {t} (", schema);
        Assert.False(DbInitializer.IsRerunnable(DbInitializer.SplitBatches(schema)));
        var procBatches = DbInitializer.SplitBatches(procs);
        Assert.Equal(4, procBatches.Count);
        Assert.True(DbInitializer.IsRerunnable(procBatches));
        foreach (var sp in new[] { "sp_top_5_best_selling_drinks", "sp_top_5_best_selling_movies", "sp_revenue_by_day", "sp_box_office_settlement" })
            Assert.Contains($"CREATE OR ALTER PROCEDURE {sp}", procs);
        Assert.DoesNotContain("GETDATE(", (schema + procs).ToUpperInvariant());
        Assert.DoesNotContain("SYSDATETIME(", (schema + procs).ToUpperInvariant());
    }

    [Fact]
    public void PasswordUtil_HashesAndVerifies()
    {
        var hash = PasswordUtil.Hash("123456");
        Assert.NotEqual("123456", hash);
        Assert.True(PasswordUtil.Verify(hash, "123456"));
        Assert.False(PasswordUtil.Verify(hash, "654321"));
        Assert.False(PasswordUtil.Verify("khong-phai-chuoi-bam", "123456"));
        Assert.NotEqual(hash, PasswordUtil.Hash("123456")); // mỗi lần băm có muối khác
    }
}
