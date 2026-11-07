using System.Data;
using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Tests;

/// <summary>
/// Test tích hợp cho 4 stored procedure (SPEC 3.12) với dữ liệu tự dựng có kết quả tính tay.
/// Mọi dữ liệu nằm trong một transaction và bị rollback sau test.
/// </summary>
[Collection("Database")]
public class StoredProcedureTests
{
    private readonly DatabaseFixture _db;

    public StoredProcedureTests(DatabaseFixture db)
    {
        _db = db;
        DBUtil.Init(db.ConnectionString);
    }

    private sealed class RollbackSignal : Exception { }

    private static void InRollback(Action<SqlConnection, SqlTransaction> work)
    {
        try { DBUtil.InTransaction((c, t) => { work(c, t); throw new RollbackSignal(); }); }
        catch (RollbackSignal) { }
    }

    private sealed class Builder
    {
        private readonly SqlConnection _c;
        private readonly SqlTransaction _t;
        private readonly DateTime _created = new(2026, 9, 1, 8, 0, 0);
        private int _seatCol;

        public Builder(SqlConnection c, SqlTransaction t) { _c = c; _t = t; }

        public int Cinema() => CinemaDAL.Insert(new Cinema { Name = "Rạp SP " + Guid.NewGuid().ToString("N")[..6], Address = "HN" }, _c, _t);

        public int Auditorium(int cinema) => AuditoriumDAL.Insert(new Auditorium { CinemaId = cinema, Name = "P", Format = AuditoriumFormat.F2D, TotalRows = 1, TotalCols = 40 }, _c, _t);

        public int Seat(int aud) { _seatCol++; return SeatDAL.Insert(new Seat { AuditoriumId = aud, RowIndex = 1, ColIndex = _seatCol, Label = "A" + _seatCol, SeatType = SeatType.Standard }, _c, _t); }

        public int Movie(string title, DateTime release, long minPrice, params decimal[] shares)
        {
            int id = MovieDAL.Insert(new Movie { Title = title, Genres = Genre.Drama, DurationMinutes = 100, AgeRating = AgeRating.P, LanguageFormats = LanguageFormat.Original, ReleaseDate = release, MinimumTicketPrice = minPrice, CreatedAt = _created }, _c, _t);
            for (int i = 0; i < shares.Length; i++) FilmRentalRuleDAL.Insert(new FilmRentalRule { MovieId = id, WeekNumber = i + 1, DistributorSharePercent = shares[i] }, _c, _t);
            return id;
        }

        public int Show(int movie, int aud, DateTime start) =>
            ShowtimeDAL.Insert(new Showtime { MovieId = movie, AuditoriumId = aud, StartTime = start, FeatureStartTime = start.AddMinutes(15), EndTime = start.AddMinutes(115), CleaningEndTime = start.AddMinutes(130), LanguageFormat = LanguageFormat.Original, CreatedAt = _created }, _c, _t);

        public int Drink(string name)
        {
            int cat = CategoryDAL.Insert(new Category { Name = "Loại " + Guid.NewGuid().ToString("N")[..8] }, _c, _t);
            return DrinkDAL.Insert(new Drink { CategoryId = cat, Name = name, Price = 30_000, CreatedAt = _created }, _c, _t);
        }

        /// <summary>Hóa đơn ở trạng thái cho trước; paidAt null nghĩa là chưa thanh toán.</summary>
        public int Bill(int cinema, int? show, string status, DateTime? paidAt)
        {
            var b = new Bill { Code = CodeGenerator.BillCode(_created) + Guid.NewGuid().ToString("N")[..4], Channel = BillChannel.Pos, CinemaId = cinema, ShowtimeId = show, CreatedAt = paidAt ?? _created };
            BillDAL.Insert(b, _c, _t);
            if (status == BillStatus.Finish) BillDAL.UpdateStatus(b.Id, BillStatus.Waiting, null, BillStatus.Finish, null, paidAt, null, _c, _t);
            if (status == BillStatus.Cancel)
            {
                BillDAL.UpdateStatus(b.Id, BillStatus.Waiting, null, BillStatus.Finish, null, paidAt, null, _c, _t);
                BillDAL.UpdateStatus(b.Id, BillStatus.Finish, null, BillStatus.Cancel, CancelReason.VoidedByPos, paidAt, paidAt, _c, _t);
            }
            return b.Id;
        }

        public void Ticket(int bill, int show, int seat, long finalPrice, string checkIn = TicketCheckInStatus.CheckedIn) =>
            TicketDAL.Insert(new Ticket { BillId = bill, ShowtimeId = show, SeatId = seat, AudienceType = AudienceType.Adult, ListPrice = finalPrice, FinalPrice = finalPrice, VatRate = 8, QrCode = CodeGenerator.TicketQrCode(), CheckInStatus = checkIn, CreatedAt = _created }, _c, _t);

        public void Line(int bill, int drink, int qty, long amount) =>
            BillDetailDAL.Insert(new BillDetail { BillId = bill, DrinkId = drink, Quantity = qty, Price = amount / qty, VatRate = 8, LineFinalAmount = amount }, _c, _t);

        public DataTable Exec(string sql, params object[] ps) => DBUtil.QueryDataTable(sql, ps.ToList(), _c, _t);
    }

    [Fact]
    public void Sp_TopDrinksTopMoviesRevenueByDay_FollowCommonRules()
    {
        InRollback((c, t) =>
        {
            var b = new Builder(c, t);
            int cinema = b.Cinema(), aud = b.Auditorium(cinema);
            int m1 = b.Movie("Phim SP Một", new DateTime(2026, 10, 1), 0, 50), m2 = b.Movie("Phim SP Hai", new DateTime(2026, 10, 1), 0, 50);
            int s1 = b.Show(m1, aud, new DateTime(2026, 11, 2, 19, 0, 0)), s2 = b.Show(m2, aud, new DateTime(2026, 11, 4, 9, 0, 0));
            int drinkA = b.Drink("Món SP A"), drinkB = b.Drink("Món SP B");

            // Ngày 02/11: 2 vé phim 1 (180.000) + 3 món A (90.000)
            int bill1 = b.Bill(cinema, s1, BillStatus.Finish, new DateTime(2026, 11, 2, 10, 0, 0));
            b.Ticket(bill1, s1, b.Seat(aud), 100_000);
            b.Ticket(bill1, s1, b.Seat(aud), 80_000);
            b.Line(bill1, drinkA, 3, 90_000);
            // Ngày 03/11 lúc 23:59:59: 1 vé phim 2 tính (300.000), 1 vé VOIDED và 1 vé SWAPPED không tính; 1 món B + 1 món A
            int bill2 = b.Bill(cinema, s2, BillStatus.Finish, new DateTime(2026, 11, 3, 23, 59, 59));
            b.Ticket(bill2, s2, b.Seat(aud), 300_000, TicketCheckInStatus.NotCheckedIn);
            b.Ticket(bill2, s2, b.Seat(aud), 300_000, TicketCheckInStatus.Voided);
            b.Ticket(bill2, s2, b.Seat(aud), 50_000, TicketCheckInStatus.Swapped);
            b.Line(bill2, drinkB, 1, 50_000);
            b.Line(bill2, drinkA, 1, 30_000);
            // Không tính: hóa đơn waiting, hóa đơn cancel, hóa đơn ngoài khoảng ngày
            int waiting = b.Bill(cinema, s2, BillStatus.Waiting, null);
            b.Ticket(waiting, s2, b.Seat(aud), 1_000_000, TicketCheckInStatus.NotCheckedIn);
            b.Line(waiting, drinkB, 10, 300_000);
            int cancelled = b.Bill(cinema, s2, BillStatus.Cancel, new DateTime(2026, 11, 3, 9, 0, 0));
            b.Ticket(cancelled, s2, b.Seat(aud), 900_000, TicketCheckInStatus.Voided);
            b.Line(cancelled, drinkB, 9, 270_000);
            int later = b.Bill(cinema, s2, BillStatus.Finish, new DateTime(2026, 11, 5, 0, 0, 0));
            b.Ticket(later, s2, b.Seat(aud), 700_000);
            b.Line(later, drinkB, 8, 240_000);

            var from = new DateOnly(2026, 11, 1);
            var to = new DateOnly(2026, 11, 4);
            var drinks = b.Exec("EXEC sp_top_5_best_selling_drinks @StartDate = @1, @EndDate = @2", from, to);
            Assert.Equal(new[] { "DrinkId", "DrinkName", "TotalQuantitySold", "TotalRevenue" }, drinks.Columns.Cast<DataColumn>().Select(x => x.ColumnName));
            Assert.Equal(2, drinks.Rows.Count);
            Assert.Equal(("Món SP A", 4, 120_000L), ((string)drinks.Rows[0]["DrinkName"], Convert.ToInt32(drinks.Rows[0]["TotalQuantitySold"]), Convert.ToInt64(drinks.Rows[0]["TotalRevenue"])));
            Assert.Equal(("Món SP B", 1, 50_000L), ((string)drinks.Rows[1]["DrinkName"], Convert.ToInt32(drinks.Rows[1]["TotalQuantitySold"]), Convert.ToInt64(drinks.Rows[1]["TotalRevenue"])));

            var movies = b.Exec("EXEC sp_top_5_best_selling_movies @StartDate = @1, @EndDate = @2", from, to);
            Assert.Equal(new[] { "MovieId", "Title", "TicketsSold", "TicketRevenue" }, movies.Columns.Cast<DataColumn>().Select(x => x.ColumnName));
            // Sắp theo doanh thu: phim 2 (1 vé, 300.000) đứng trước phim 1 (2 vé, 180.000)
            Assert.Equal((m2, 1, 300_000L), (Convert.ToInt32(movies.Rows[0]["MovieId"]), Convert.ToInt32(movies.Rows[0]["TicketsSold"]), Convert.ToInt64(movies.Rows[0]["TicketRevenue"])));
            Assert.Equal((m1, 2, 180_000L), (Convert.ToInt32(movies.Rows[1]["MovieId"]), Convert.ToInt32(movies.Rows[1]["TicketsSold"]), Convert.ToInt64(movies.Rows[1]["TicketRevenue"])));

            var days = b.Exec("EXEC sp_revenue_by_day @StartDate = @1, @EndDate = @2", from, to);
            Assert.Equal(new[] { "RevenueDate", "TotalBills", "TicketRevenue", "FnbRevenue", "TotalRevenue" }, days.Columns.Cast<DataColumn>().Select(x => x.ColumnName));
            var got = days.Rows.Cast<DataRow>().Select(r => ((DateTime)r["RevenueDate"], Convert.ToInt32(r["TotalBills"]), Convert.ToInt64(r["TicketRevenue"]), Convert.ToInt64(r["FnbRevenue"]), Convert.ToInt64(r["TotalRevenue"]))).ToList();
            Assert.Equal(new[]
            {
                (new DateTime(2026, 11, 1), 0, 0L, 0L, 0L),
                (new DateTime(2026, 11, 2), 1, 180_000L, 90_000L, 270_000L),
                (new DateTime(2026, 11, 3), 1, 300_000L, 80_000L, 380_000L),
                (new DateTime(2026, 11, 4), 0, 0L, 0L, 0L),
            }, got);
        });
    }

    [Fact]
    public void Sp_BoxOfficeSettlement_WeekWindowShareAndFloor()
    {
        InRollback((c, t) =>
        {
            var b = new Builder(c, t);
            int cinemaA = b.Cinema(), cinemaB = b.Cinema();
            int audA = b.Auditorium(cinemaA), audB = b.Auditorium(cinemaB);
            // Phát hành 01/10/2026; giá sàn 54.000 (thuần 50.000 với VAT 8%); tỷ lệ tuần 1–3: 50%, 45%, 40%
            int movie = b.Movie("Phim đối soát", new DateTime(2026, 10, 1), 54_000, 50, 45, 40);
            int lateNight = b.Show(movie, audA, new DateTime(2026, 10, 8, 0, 30, 0)); // ngày vận hành 07/10 → tuần 1
            int week2 = b.Show(movie, audA, new DateTime(2026, 10, 8, 10, 0, 0));     // ngày vận hành 08/10 → tuần 2
            int week5 = b.Show(movie, audA, new DateTime(2026, 10, 30, 19, 0, 0));    // tuần 5 → dùng quy tắc tuần 3
            int otherCinema = b.Show(movie, audB, new DateTime(2026, 10, 2, 19, 0, 0)); // tuần 1, rạp B

            int bill = b.Bill(cinemaA, lateNight, BillStatus.Finish, new DateTime(2026, 10, 7, 20, 0, 0));
            b.Ticket(bill, lateNight, b.Seat(audA), 108_000);                 // Net 100.000 ≥ sàn
            b.Ticket(bill, lateNight, b.Seat(audA), 43_200);                  // Net 40.000 < sàn 50.000 → rạp chịu 10.000
            b.Ticket(bill, lateNight, b.Seat(audA), 108_000, TicketCheckInStatus.Voided); // không tính
            int waiting = b.Bill(cinemaA, lateNight, BillStatus.Waiting, null);
            b.Ticket(waiting, lateNight, b.Seat(audA), 108_000, TicketCheckInStatus.NotCheckedIn); // không tính
            int bill2 = b.Bill(cinemaA, week2, BillStatus.Finish, new DateTime(2026, 10, 8, 9, 0, 0));
            b.Ticket(bill2, week2, b.Seat(audA), 108_000);
            int bill5 = b.Bill(cinemaA, week5, BillStatus.Finish, new DateTime(2026, 10, 30, 18, 0, 0));
            b.Ticket(bill5, week5, b.Seat(audA), 108_000);
            int billB = b.Bill(cinemaB, otherCinema, BillStatus.Finish, new DateTime(2026, 10, 2, 18, 0, 0));
            b.Ticket(billB, otherCinema, b.Seat(audB), 108_000);

            DataRow Run(int week, int? cinema) => b.Exec("EXEC sp_box_office_settlement @MovieId = @1, @WeekNumber = @2, @CinemaId = @3", movie, week, DBUtil.Value(cinema)).Rows[0];
            long L(DataRow r, string col) => Convert.ToInt64(r[col]);

            var w1 = Run(1, cinemaA);
            Assert.Equal(new DateTime(2026, 10, 1), w1["WeekStartDate"]);
            Assert.Equal(new DateTime(2026, 10, 7), w1["WeekEndDate"]);
            Assert.Equal(50m, Convert.ToDecimal(w1["DistributorSharePercent"]));
            Assert.Equal(2, L(w1, "TicketCount"));
            Assert.Equal(151_200, L(w1, "GrossRevenue"));
            Assert.Equal(140_000, L(w1, "NetRevenue"));
            Assert.Equal(1, L(w1, "BelowFloorTickets"));
            Assert.Equal(10_000, L(w1, "FloorAbsorbedAmount"));
            Assert.Equal(75_000, L(w1, "DistributorAmount"));      // (100.000 + 50.000) × 50%
            Assert.Equal(65_000, L(w1, "CinemaRetainedAmount"));   // 140.000 − 75.000

            var w1All = Run(1, null);
            Assert.Equal(3, L(w1All, "TicketCount"));               // thêm vé ở rạp B
            Assert.Equal(125_000, L(w1All, "DistributorAmount"));   // (150.000 + 100.000) × 50%

            var w2 = Run(2, cinemaA);
            Assert.Equal(1, L(w2, "TicketCount"));
            Assert.Equal(45_000, L(w2, "DistributorAmount"));

            var w5 = Run(5, cinemaA);
            Assert.Equal(40m, Convert.ToDecimal(w5["DistributorSharePercent"]));
            Assert.Equal(1, L(w5, "TicketCount"));
            Assert.Equal(40_000, L(w5, "DistributorAmount"));

            var empty = Run(4, cinemaA);
            Assert.Equal(0, L(empty, "TicketCount"));
            Assert.Equal(0, L(empty, "GrossRevenue"));
        });
    }
}
