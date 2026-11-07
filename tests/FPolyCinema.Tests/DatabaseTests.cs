using System.Reflection;
using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FPolyCinema.Tests;

/// <summary>
/// Tạo một CSDL thử riêng (tên DB thật + "_Test_" + chuỗi ngẫu nhiên) bằng DbInitializer cho cả nhóm test, xóa khi chạy xong.
/// Chuỗi kết nối lấy từ biến môi trường ConnectionStrings__Default hoặc appsettings.Development.json.
/// </summary>
public sealed class DatabaseFixture : IDisposable
{
    public DatabaseFixture()
    {
        ConnectionString = TestDatabase.NewConnectionString("Test");
        Clock = new FakeClock(new DateTime(2026, 11, 6, 10, 0, 0));
        DBUtil.Init(ConnectionString);
        Initializer = new DbInitializer(ConnectionString, RepoPaths.File("Database"), Clock, NullLogger.Instance);
        CreatedFresh = Initializer.Initialize(resetDb: true);
    }

    public string ConnectionString { get; }
    public FakeClock Clock { get; }
    public DbInitializer Initializer { get; }
    public bool CreatedFresh { get; }

    public void Dispose() => TestDatabase.Drop(ConnectionString);
}

/// <summary>Các test dùng CSDL chạy tuần tự trong một collection vì DBUtil giữ một chuỗi kết nối tĩnh.</summary>
[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>, ICollectionFixture<SeededDatabaseFixture> { }

/// <summary>Test tích hợp cho DBUtil, DAL, DbInitializer và BillStatusService trên CSDL thử.</summary>
[Collection("Database")]
public class DatabaseTests
{
    private readonly DatabaseFixture _db;

    public DatabaseTests(DatabaseFixture db)
    {
        _db = db;
        DBUtil.Init(db.ConnectionString);
    }

    private sealed class RollbackSignal : Exception { }

    /// <summary>Chạy work trong một transaction rồi luôn rollback để CSDL thử sạch giữa các test.</summary>
    private static void InRollback(Action<SqlConnection, SqlTransaction> work)
    {
        try
        {
            DBUtil.InTransaction((c, t) => { work(c, t); throw new RollbackSignal(); });
        }
        catch (RollbackSignal)
        {
        }
    }

    private static int NewCinema(SqlConnection c, SqlTransaction t) =>
        CinemaDAL.Insert(new Cinema { Name = "Rạp thử " + Guid.NewGuid().ToString("N")[..6], Address = "Hà Nội", Active = true }, c, t);

    // ---------------------------------------------------------------- DbInitializer
    [Fact]
    public void DbInitializer_CreatesDatabaseRunsScriptsAndRecordsVersions()
    {
        Assert.True(_db.CreatedFresh);
        var versions = SchemaVersionDAL.GetAll().Select(v => v.Version).ToList();
        Assert.Contains("01_schema.sql", versions);
        Assert.Contains("02_procedures.sql", versions);
        var tables = Convert.ToInt32(DBUtil.ExecuteScalar("SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0"));
        Assert.Equal(35, tables);
        var procs = Convert.ToInt32(DBUtil.ExecuteScalar("SELECT COUNT(*) FROM sys.procedures WHERE name LIKE 'sp[_]%'"));
        Assert.Equal(4, procs);
    }

    [Fact]
    public void DbInitializer_RunAgainOnExistingDatabase_SkipsSchemaAndRerunsProcedures()
    {
        var before = SchemaVersionDAL.GetAll().ToDictionary(v => v.Version);
        _db.Clock.Advance(TimeSpan.FromMinutes(5));
        bool created = _db.Initializer.Initialize(resetDb: false);
        var after = SchemaVersionDAL.GetAll().ToDictionary(v => v.Version);
        Assert.False(created);
        Assert.Equal(before["01_schema.sql"].AppliedAt, after["01_schema.sql"].AppliedAt);
        Assert.True(after["02_procedures.sql"].AppliedAt > before["02_procedures.sql"].AppliedAt);
    }

    // ---------------------------------------------------------------- DBUtil
    [Fact]
    public void DBUtil_QueryScalarNonQuery_WithConnAndTxOverloads()
    {
        InRollback((c, t) =>
        {
            var id = Convert.ToInt32(DBUtil.ExecuteScalar("INSERT INTO Categories (Name, Active) VALUES (@1, @2); SELECT SCOPE_IDENTITY();", new List<object> { "Loại thử A", true }, c, t));
            Assert.True(id > 0);
            var table = DBUtil.QueryDataTable("SELECT Id, Name FROM Categories WHERE Id = @1", new List<object> { id }, c, t);
            Assert.Equal("Loại thử A", table.Rows[0]["Name"]);
            var aliasTable = DBUtil.queryDataTable("SELECT COUNT(*) AS N FROM Categories WHERE Id = @1", new List<object> { id }, c, t);
            Assert.Equal(1, Convert.ToInt32(aliasTable.Rows[0]["N"]));
            var rows = Convert.ToInt32(DBUtil.executeScalar("UPDATE Categories SET Name = @1 WHERE Id = @2; SELECT @@ROWCOUNT;", new List<object> { "Loại thử B", id }, c, t));
            Assert.Equal(1, rows);
            Assert.Equal(1, DBUtil.ExecuteNonQuery("DELETE FROM Categories WHERE Id = @1", new List<object> { id }, c, t));
            Assert.Null(DBUtil.ExecuteScalar("SELECT Name FROM Categories WHERE Id = @1", new List<object> { id }, c, t));
        });
    }

    [Fact]
    public void DBUtil_InTransaction_CommitsOnSuccessAndRollsBackOnError()
    {
        var name = "Loại commit " + Guid.NewGuid().ToString("N")[..6];
        var id = DBUtil.InTransaction((c, t) => CategoryDAL.Insert(new Category { Name = name }, c, t));
        Assert.NotNull(CategoryDAL.GetById(id));

        var failed = "Loại rollback " + Guid.NewGuid().ToString("N")[..6];
        Assert.Throws<InvalidOperationException>(() => DBUtil.InTransaction<int>((c, t) =>
        {
            CategoryDAL.Insert(new Category { Name = failed }, c, t);
            throw new InvalidOperationException("lỗi giữa chừng");
        }));
        Assert.Equal(0, Convert.ToInt32(DBUtil.ExecuteScalar("SELECT COUNT(*) FROM Categories WHERE Name = @1", new List<object> { failed })));
        CategoryDAL.Delete(id);
    }

    [Fact]
    public void DBUtil_ParametersRoundTripNullDateTime2AndBigInt()
    {
        var at = new DateTime(2026, 11, 6, 23, 59, 59);
        var row = DBUtil.QueryDataTable("SELECT @1 AS A, @2 AS B, @3 AS C, SQL_VARIANT_PROPERTY(@2, 'BaseType') AS BT, SQL_VARIANT_PROPERTY(@3, 'BaseType') AS CT",
            new List<object> { DBNull.Value, at, 9_000_000_000L }).Rows[0];
        Assert.Equal(DBNull.Value, row["A"]);
        Assert.Equal(at, row["B"]);
        Assert.Equal(9_000_000_000L, row["C"]);
        Assert.Equal("datetime2", row["BT"]);
        Assert.Equal("bigint", row["CT"]);
        var ps = new List<object>();
        var inList = DBUtil.InParams(new object[] { 1, 2, 3 }, ps);
        Assert.Equal(3, Convert.ToInt32(DBUtil.ExecuteScalar("SELECT COUNT(*) FROM (VALUES (1), (2), (3), (4)) v(x) WHERE x IN (" + inList + ")", ps)));
    }

    // ---------------------------------------------------------------- DAL
    [Fact]
    public void EveryEntityHasDalWithSixStandardMethods()
    {
        var asm = typeof(Bill).Assembly;
        var entities = asm.GetTypes().Where(t => t.Namespace == "FPolyCinema.Web.Entities" && t.IsClass && t.IsPublic).ToList();
        Assert.Equal(35, entities.Count);
        foreach (var e in entities)
        {
            var dal = asm.GetType($"FPolyCinema.Web.DAL.{e.Name}DAL");
            Assert.True(dal != null, $"Thiếu {e.Name}DAL");
            foreach (var m in new[] { "FromDataRow", "GetAll", "GetById", "Insert", "Update", "Delete" })
                Assert.True(dal!.GetMethod(m, BindingFlags.Public | BindingFlags.Static) != null, $"{dal.Name} thiếu {m}");
            foreach (var m in new[] { "Insert", "Update", "Delete" })
            {
                var ps = dal!.GetMethod(m, BindingFlags.Public | BindingFlags.Static)!.GetParameters();
                Assert.Contains(ps, p => p.ParameterType == typeof(SqlConnection) && p.HasDefaultValue);
                Assert.Contains(ps, p => p.ParameterType == typeof(SqlTransaction) && p.HasDefaultValue);
            }
        }
    }

    [Fact]
    public void CategoryAndDrinkDal_CrudRoundTrip()
    {
        InRollback((c, t) =>
        {
            var cat = new Category { Name = "Bắp rang thử", Active = true };
            CategoryDAL.Insert(cat, c, t);
            var drink = new Drink { CategoryId = cat.Id, Name = "Bắp thử", Price = 49_000, Image = "/uploads/a.png", Active = true, CreatedAt = _db.Clock.Now };
            DrinkDAL.Insert(drink, c, t);
            var loaded = DrinkDAL.GetById(drink.Id, c, t)!;
            Assert.Equal(49_000, loaded.Price);
            Assert.Null(loaded.Description);
            loaded.Price = 55_000;
            loaded.IsSoldOut = true;
            Assert.Equal(1, DrinkDAL.Update(loaded, c, t));
            Assert.Equal(55_000, DrinkDAL.GetById(drink.Id, c, t)!.Price);
            Assert.Contains(DrinkDAL.GetAll(c, t), d => d.Id == drink.Id && d.IsSoldOut);
            Assert.Equal(1, DrinkDAL.Delete(drink.Id, c, t));
            Assert.Equal(1, CategoryDAL.Delete(cat.Id, c, t));
            Assert.Null(CategoryDAL.GetById(cat.Id, c, t));
        });
    }

    [Fact]
    public void UserDal_AuthenticateWithHashedPasswordAndSetActive()
    {
        var email = $"thu{Guid.NewGuid():N}"[..12] + "@fpolycinema.vn";
        var user = new User { Email = email, PasswordHash = PasswordUtil.Hash("123456"), FullName = "Người thử", Role = Roles.POSCashier, CreatedAt = _db.Clock.Now };
        UserDAL.Insert(user);
        try
        {
            Assert.NotEqual("123456", UserDAL.GetById(user.Id)!.PasswordHash);
            Assert.Equal(user.Id, UserDAL.Authenticate(email, "123456")!.Id);
            Assert.Null(UserDAL.Authenticate(email, "sai-mat-khau"));
            Assert.Null(UserDAL.Authenticate("khong-ton-tai@fpolycinema.vn", "123456"));
            Assert.Equal(1, UserDAL.SetActive(user.Id, false));
            Assert.Null(UserDAL.Authenticate(email, "123456"));
            UserDAL.SetActive(user.Id, true);
            Assert.NotNull(UserDAL.Authenticate(email, "123456"));
        }
        finally
        {
            UserDAL.Delete(user.Id);
        }
    }

    [Fact]
    public void BillDal_InsertOnlyWaitingAndUpdateDoesNotChangeStatus()
    {
        InRollback((c, t) =>
        {
            int cinema = NewCinema(c, t);
            Assert.Throws<InvalidOperationException>(() => BillDAL.Insert(new Bill { Code = "HDX1", Channel = BillChannel.Pos, CinemaId = cinema, Status = BillStatus.Finish, CreatedAt = _db.Clock.Now }, c, t));
            var bill = new Bill { Code = CodeGeneratorCode(), Channel = BillChannel.Pos, CinemaId = cinema, CreatedAt = _db.Clock.Now, Total = 100_000 };
            BillDAL.Insert(bill, c, t);
            bill.Status = BillStatus.Finish;
            bill.Total = 120_000;
            BillDAL.Update(bill, c, t);
            var loaded = BillDAL.GetById(bill.Id, c, t)!;
            Assert.Equal(BillStatus.Waiting, loaded.Status);
            Assert.Equal(120_000, loaded.Total);
            BillDetailDAL.Insert(new BillDetail { BillId = bill.Id, DrinkId = InsertDrink(c, t), Quantity = 2, Price = 35_000, VatRate = 8, LineFinalAmount = 70_000 }, c, t);
            Assert.Single(BillDetailDAL.GetAll(c, t), d => d.BillId == bill.Id);
            Assert.Equal(bill.Id, BillDAL.GetByCode(bill.Code, c, t)!.Id);
        });
    }

    private int InsertDrink(SqlConnection c, SqlTransaction t)
    {
        var cat = CategoryDAL.Insert(new Category { Name = "Loại " + Guid.NewGuid().ToString("N")[..8] }, c, t);
        return DrinkDAL.Insert(new Drink { CategoryId = cat, Name = "Nước", Price = 35_000, CreatedAt = _db.Clock.Now }, c, t);
    }

    private string CodeGeneratorCode() => FPolyCinema.Web.Domain.Rules.CodeGenerator.BillCode(_db.Clock.Now);

    [Fact]
    public void BillStatusService_ChangesStatusOnlyThroughStateMachine()
    {
        InRollback((c, t) =>
        {
            int cinema = NewCinema(c, t);
            var service = new BillStatusService(_db.Clock);
            var bill = new Bill { Code = CodeGeneratorCode(), Channel = BillChannel.Online, CinemaId = cinema, CreatedAt = _db.Clock.Now };
            BillDAL.Insert(bill, c, t);

            // Khách không được tự chuyển sang finish
            var ex = Assert.Throws<BusinessRuleException>(() => service.ChangeStatus(bill.Id, BillStatus.Finish, null, Roles.Customer, c, t));
            Assert.Equal(ErrorCodes.InvalidTransition, ex.Code);

            var paid = service.ChangeStatus(bill.Id, BillStatus.Finish, null, Actors.System, c, t);
            Assert.Equal(_db.Clock.Now, paid.PaidAt);
            var loaded = BillDAL.GetById(bill.Id, c, t)!;
            Assert.Equal(BillStatus.Finish, loaded.Status);
            Assert.Equal(_db.Clock.Now, loaded.PaidAt);

            // finish → waiting không có trong bảng
            Assert.Throws<BusinessRuleException>(() => service.ChangeStatus(bill.Id, BillStatus.Waiting, null, Roles.Manager, c, t));

            var cancelled = service.ChangeStatus(bill.Id, BillStatus.Cancel, CancelReason.CancelledByUser, Roles.Customer, _db.Clock.Now.AddHours(1), c, t);
            loaded = BillDAL.GetById(bill.Id, c, t)!;
            Assert.Equal((BillStatus.Cancel, CancelReason.CancelledByUser), (loaded.Status, loaded.CancelReason));
            Assert.Equal(_db.Clock.Now.AddHours(1), loaded.CancelledAt);
            Assert.Equal(_db.Clock.Now, loaded.PaidAt);
            Assert.Equal(cancelled.CancelledAt, loaded.CancelledAt);
        });
    }

    [Fact]
    public void BillStatusService_ExpiredBillFinishedByLateWebhook()
    {
        InRollback((c, t) =>
        {
            int cinema = NewCinema(c, t);
            var service = new BillStatusService(_db.Clock);
            var bill = new Bill { Code = CodeGeneratorCode(), Channel = BillChannel.Online, CinemaId = cinema, CreatedAt = _db.Clock.Now };
            BillDAL.Insert(bill, c, t);
            service.ChangeStatus(bill.Id, BillStatus.Cancel, CancelReason.Expired, Actors.System, c, t);
            var done = service.ChangeStatus(bill.Id, BillStatus.Finish, null, Actors.System, c, t);
            Assert.Null(done.CancelReason);
            var loaded = BillDAL.GetById(bill.Id, c, t)!;
            Assert.Equal(BillStatus.Finish, loaded.Status);
            Assert.Null(loaded.CancelledAt);
        });
    }

    [Fact]
    public void BillDal_UpdateStatusIsConditionalOnCurrentStatus()
    {
        InRollback((c, t) =>
        {
            int cinema = NewCinema(c, t);
            var bill = new Bill { Code = CodeGeneratorCode(), Channel = BillChannel.Pos, CinemaId = cinema, CreatedAt = _db.Clock.Now };
            BillDAL.Insert(bill, c, t);
            Assert.Equal(1, BillDAL.UpdateStatus(bill.Id, BillStatus.Waiting, null, BillStatus.Finish, null, _db.Clock.Now, null, c, t));
            // Người thứ hai vẫn tưởng hóa đơn đang waiting: không đổi được
            Assert.Equal(0, BillDAL.UpdateStatus(bill.Id, BillStatus.Waiting, null, BillStatus.Cancel, CancelReason.CancelledBeforePayment, null, _db.Clock.Now, c, t));
        });
    }

    [Fact]
    public void ShowtimeSeatDal_CreateForShowtimeUsesHouseHoldForHouseSeats()
    {
        InRollback((c, t) =>
        {
            int cinema = NewCinema(c, t);
            int aud = AuditoriumDAL.Insert(new Auditorium { CinemaId = cinema, Name = "P1", Format = AuditoriumFormat.F2D, TotalRows = 1, TotalCols = 3 }, c, t);
            for (int col = 1; col <= 3; col++)
                SeatDAL.Insert(new Seat { AuditoriumId = aud, RowIndex = 1, ColIndex = col, Label = $"A{col}", SeatType = SeatType.Standard, IsHouseSeat = col == 2, BaseViewScore = 50 }, c, t);
            var movie = MovieDAL.Insert(new Movie { Title = "Phim thử", Genres = Genre.Drama, DurationMinutes = 90, AgeRating = AgeRating.P, LanguageFormats = LanguageFormat.Original, ReleaseDate = _db.Clock.Today, CreatedAt = _db.Clock.Now }, c, t);
            var start = _db.Clock.Today.AddHours(9);
            int st = ShowtimeDAL.Insert(new Showtime { MovieId = movie, AuditoriumId = aud, StartTime = start, FeatureStartTime = start.AddMinutes(15), EndTime = start.AddMinutes(105), CleaningEndTime = start.AddMinutes(120), LanguageFormat = LanguageFormat.Original, CreatedAt = _db.Clock.Now }, c, t);
            Assert.Equal(3, ShowtimeSeatDAL.CreateForShowtime(st, aud, c, t));
            var seats = ShowtimeSeatDAL.GetByShowtime(st, c, t);
            Assert.Equal(1, seats.Count(s => s.Status == ShowtimeSeatStatus.HouseHold));
            Assert.Equal(2, seats.Count(s => s.Status == ShowtimeSeatStatus.Available));
        });
    }

    [Fact]
    public void StatisticDal_CallsAllFourStoredProcedures()
    {
        var day = new DateTime(2026, 11, 1);
        Assert.Empty(StatisticDAL.TopBestSellingDrinks(day, day.AddDays(6)));
        Assert.Empty(StatisticDAL.TopBestSellingMovies(day, day.AddDays(6)));
        var revenue = StatisticDAL.RevenueByDay(day, day.AddDays(6));
        Assert.Equal(7, revenue.Count);
        Assert.All(revenue, r => Assert.Equal(0, r.TotalRevenue));
        Assert.Equal(day, revenue[0].RevenueDate);
        Assert.Throws<SqlException>(() => StatisticDAL.GetBoxOfficeSettlement(-1, 1));
    }
}
