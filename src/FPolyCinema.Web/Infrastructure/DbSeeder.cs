using System.Diagnostics;
using System.Text;
using FPolyCinema.Web.DAL;
using FPolyCinema.Web.Domain.Constants;
using FPolyCinema.Web.Domain.Rules;
using FPolyCinema.Web.Entities;
using FPolyCinema.Web.Infrastructure.Seed;
using FPolyCinema.Web.Services;
using FPolyCinema.Web.Utils;
using Microsoft.Data.SqlClient;

namespace FPolyCinema.Web.Infrastructure;

/// <summary>
/// Nạp dữ liệu mẫu khi bảng Users còn rỗng (SPEC 2.4, SPEC 8.1 bước 6). Toàn bộ chạy trong một transaction.
/// Thời điểm gốc lấy từ IClock; số ngẫu nhiên dùng hạt cố định nên cùng một ngày luôn ra cùng một bộ dữ liệu.
/// Hóa đơn được tạo ở waiting rồi chuyển trạng thái qua BillStatusService (BillStateMachine) như nghiệp vụ thật.
/// </summary>
public sealed partial class DbSeeder
{
    public const int HistoryDays = 30;
    public const int FutureDays = 6;
    private const string Terminal = "POS-01";
    private const long OpeningCashFloat = 2_000_000;

    private readonly IClock _clock;
    private readonly CinemaRules _rules;
    private readonly string _webRootPath;
    private readonly ILogger _logger;
    private readonly BillStatusService _status;
    private readonly Random _rnd = new(20261106);

    // ---------------------------------------------------------------- trạng thái trong lúc nạp
    private DateTime _now;
    private DateTime _today;
    private readonly List<int> _cinemaIds = new();
    private readonly Dictionary<string, int> _userIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<int> _customerIds = new();
    private int _managerId;
    private int _khachId;
    private int _khach2Id;
    private readonly Dictionary<int, int> _cashierOf = new();
    private readonly Dictionary<int, int> _gateOf = new();
    private readonly Dictionary<int, int> _shiftManagerOf = new();
    private readonly List<int> _movieIds = new();
    private readonly List<DateTime> _releaseDates = new();
    private readonly List<SeedDrink> _drinks = new();
    private readonly List<SeedAuditorium> _auditoriums = new();
    private readonly List<SeedShow> _shows = new();
    private readonly List<PriceTable> _priceTables = new();
    private readonly List<DateTime> _holidays = new();
    private readonly Dictionary<string, decimal> _discounts = new();
    private readonly Dictionary<(int Cinema, DateTime Date), SeedShift> _shifts = new();
    private readonly Dictionary<(int Cinema, DateTime Date), int> _pickupCounters = new();
    private readonly HashSet<string> _billCodes = new();
    private readonly Dictionary<string, Voucher> _vouchers = new();
    private readonly List<CustomerBill> _customerBills = new();
    private readonly Dictionary<int, long> _customerSpend = new();
    private readonly Dictionary<int, (string Name, decimal Stock, decimal Min)> _materialTargets = new();

    public DbSeeder(IClock clock, CinemaRules rules, string webRootPath, ILogger logger)
    {
        _clock = clock;
        _rules = rules;
        _webRootPath = webRootPath;
        _logger = logger;
        _status = new BillStatusService(clock);
    }

    /// <summary>Nạp dữ liệu mẫu nếu bảng Users còn rỗng; trả về true nếu đã nạp.</summary>
    public bool SeedIfEmpty()
    {
        if (UserDAL.Count() > 0) return false;
        var sw = Stopwatch.StartNew();
        WriteSeedArt();
        DBUtil.InTransaction((conn, tx) => Seed(conn, tx));
        _logger.LogInformation("Đã nạp dữ liệu mẫu trong {Seconds:0.0} giây.", sw.Elapsed.TotalSeconds);
        return true;
    }

    // ---------------------------------------------------------------- ảnh mẫu
    /// <summary>Ảnh thật (Unsplash License) lưu sẵn trong repo ở wwwroot/uploads/seed/photos; nguồn ghi ở photos/CREDITS.md.</summary>
    public static string PosterPath(int movieIndex) => $"/uploads/seed/photos/poster-{movieIndex + 1:D2}.jpg";
    public static string DrinkImagePath(int drinkIndex) => $"/uploads/seed/photos/drink-{drinkIndex + 1:D2}.jpg";

    /// <summary>Sinh bản SVG dự phòng bằng SvgArtGenerator vào wwwroot/uploads/seed (lúc chạy không tải ảnh từ internet).</summary>
    public void WriteSeedArt()
    {
        var dir = Path.Combine(_webRootPath, "uploads", "seed");
        Directory.CreateDirectory(dir);
        var today = PricingCalculator.OperatingDate(_clock.Now);
        for (int i = 0; i < SeedCatalog.Movies.Length; i++)
        {
            var m = SeedCatalog.Movies[i];
            var svg = SvgArtGenerator.Poster(m.Title, m.Genres, today.AddDays(m.ReleaseOffsetDays).Year, m.AgeRating);
            WriteIfChanged(Path.Combine(dir, $"poster-{i + 1:D2}.svg"), svg);
        }
        for (int i = 0; i < SeedCatalog.Drinks.Length; i++)
        {
            var d = SeedCatalog.Drinks[i];
            WriteIfChanged(Path.Combine(dir, $"drink-{i + 1:D2}.svg"), SvgArtGenerator.DrinkImage(d.Art, d.Name));
        }
    }

    private static void WriteIfChanged(string path, string content)
    {
        if (File.Exists(path) && File.ReadAllText(path, Encoding.UTF8) == content) return;
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    // ---------------------------------------------------------------- toàn bộ quy trình
    private void Seed(SqlConnection conn, SqlTransaction tx)
    {
        _now = _clock.Now;
        _today = PricingCalculator.OperatingDate(_now);

        var steps = new (string Name, Action<SqlConnection, SqlTransaction> Run)[]
        {
            ("rạp và tài khoản", SeedCinemasAndUsers),
            ("ngày lễ, đối tượng vé, bảng giá", SeedReferenceData),
            ("phim, chia doanh thu, KDM", SeedMovies),
            ("bắp nước và kho", SeedFnb),
            ("phòng chiếu và ghế", SeedAuditoriums),
            ("suất chiếu", SeedShowtimes),
            ("voucher", SeedVouchers),
            ("ca bán hàng", SeedShifts),
            ("hóa đơn", SeedSales),
            ("điểm và hạng khách", FinalizeCustomers),
            ("lượt dùng voucher", FinalizeVouchers),
            ("đóng ca", FinalizeShifts),
            ("sổ kho", FinalizeInventory),
            ("thông báo", SeedNotifications),
        };
        foreach (var (name, run) in steps)
        {
            var sw = Stopwatch.StartNew();
            run(conn, tx);
            _logger.LogInformation("Dữ liệu mẫu – {Step}: {Ms} ms.", name, sw.ElapsedMilliseconds);
        }
    }

    // ---------------------------------------------------------------- rạp và tài khoản
    private void SeedCinemasAndUsers(SqlConnection conn, SqlTransaction tx)
    {
        foreach (var c in SeedCatalog.Cinemas)
        {
            _cinemaIds.Add(CinemaDAL.Insert(new Cinema { Name = c.Name, Address = c.Address, Phone = c.Phone, Active = true }, conn, tx));
        }

        int memberSeq = 0;
        var createdAt = _today.AddDays(-HistoryDays - 60).AddHours(9);
        // Mọi tài khoản mẫu dùng chung mật khẩu 123456 và PIN 246810 (CONTRACTS mục 11): băm một lần bằng PasswordHasher
        // rồi dùng lại để nạp nhanh; tài khoản tạo mới về sau luôn có muối riêng.
        var passwordHash = PasswordUtil.Hash(SeedCatalog.SamplePassword);
        var pinHash = PasswordUtil.Hash(SeedCatalog.SampleManagerPin);
        foreach (var a in SeedCatalog.Accounts)
        {
            bool isCustomer = a.Role == Roles.Customer;
            var u = new User
            {
                Email = a.Email,
                PasswordHash = passwordHash,
                FullName = a.FullName,
                Phone = a.Phone,
                BirthDate = a.BirthDate,
                Role = a.Role,
                CinemaId = a.CinemaIndex is int ci ? _cinemaIds[ci] : null,
                Active = true,
                MustChangePassword = false,
                MemberCode = isCustomer ? CodeGenerator.MemberCode(++memberSeq) : null,
                Tier = LoyaltyTier.Member,
                ManagerPinHash = a.HasPin ? pinHash : null,
                CreatedAt = createdAt,
            };
            int id = UserDAL.Insert(u, conn, tx);
            _userIds[a.Email] = id;
            if (isCustomer) _customerIds.Add(id);
            if (a.Role == Roles.Manager) _managerId = id;
            if (a.CinemaIndex is int cinema)
            {
                if (a.Role == Roles.POSCashier) _cashierOf[cinema] = id;
                if (a.Role == Roles.TicketInspector) _gateOf[cinema] = id;
                if (a.Role == Roles.ShiftManager) _shiftManagerOf[cinema] = id;
            }
        }
        _khachId = _userIds[SeedCatalog.KhachEmail];
        _khach2Id = _userIds[SeedCatalog.Khach2Email];
    }

    // ---------------------------------------------------------------- ngày lễ, đối tượng vé, bảng giá
    private void SeedReferenceData(SqlConnection conn, SqlTransaction tx)
    {
        foreach (var h in SeedCatalog.Holidays2026)
        {
            HolidayDAL.Insert(new Holiday { HolidayDate = h.Date, Name = h.Name }, conn, tx);
            _holidays.Add(h.Date);
        }
        foreach (var d in SeedCatalog.AudienceDiscounts)
        {
            AudienceDiscountDAL.Insert(new AudienceDiscount
            {
                AudienceType = d.Type,
                DiscountPercent = d.Discount,
                LegalMinPercent = d.LegalMin,
                PosOnly = d.PosOnly,
                RequiresDocument = d.RequiresDocument,
            }, conn, tx);
            _discounts[d.Type] = d.Discount;
        }
        for (int c = 0; c < _cinemaIds.Count; c++)
        {
            var table = new PriceTable();
            foreach (var (type, key, amount) in SeedCatalog.PricingFor(c))
            {
                PricingRuleDAL.Insert(new PricingRule { CinemaId = _cinemaIds[c], RuleType = type, RuleKey = key, Amount = amount }, conn, tx);
                table.Set(type, key, amount);
            }
            _priceTables.Add(table);
        }
    }

    // ---------------------------------------------------------------- nhà phát hành, phim, chia doanh thu, KDM
    private readonly List<PlanKdm> _kdms = new();

    private void SeedMovies(SqlConnection conn, SqlTransaction tx)
    {
        var distributorIds = SeedCatalog.Distributors
            .Select(d => DistributorDAL.Insert(new Distributor { Name = d.Name, Email = d.Email, Phone = d.Phone, Active = true }, conn, tx))
            .ToList();

        for (int i = 0; i < SeedCatalog.Movies.Length; i++)
        {
            var m = SeedCatalog.Movies[i];
            var release = _today.AddDays(m.ReleaseOffsetDays);
            var movie = new Movie
            {
                Title = m.Title,
                OriginalTitle = m.OriginalTitle,
                Genres = CsvList.Join(m.Genres),
                DurationMinutes = m.DurationMinutes,
                AgeRating = m.AgeRating,
                ClassificationLicenseNo = $"{i + 101:D4}/{release.Year}/QĐ-ĐA",
                ContentWarnings = CsvList.Join(m.ContentWarnings),
                IsVietnameseFilm = m.IsVietnamese,
                LanguageFormats = CsvList.Join(m.LanguageFormats),
                ReleaseDate = release,
                DistributorId = distributorIds[m.DistributorIndex],
                MinimumTicketPrice = m.MinimumTicketPrice,
                IsBlockbuster = m.IsBlockbuster,
                PosterUrl = PosterPath(i),
                TrailerUrl = null,
                Description = m.Description,
                AiDemandScore = null,
                Active = true,
                CreatedAt = Min(release.AddDays(-14).AddHours(10), _now),
            };
            int movieId = MovieDAL.Insert(movie, conn, tx);
            _movieIds.Add(movieId);
            _releaseDates.Add(release);

            for (int w = 0; w < m.SharePercents.Length; w++)
            {
                FilmRentalRuleDAL.Insert(new FilmRentalRule { MovieId = movieId, WeekNumber = w + 1, DistributorSharePercent = m.SharePercents[w] }, conn, tx);
            }

            for (int c = 0; c < _cinemaIds.Count; c++)
            {
                var from = release.AddDays(-1);
                var until = _today.AddDays(FutureDays + 40).AddHours(23).AddMinutes(59);
                string note = "KDM nhận từ nhà phát hành";
                if (i == SeedCatalog.KdmExpiringMovieIndex && c == 0)
                {
                    // KDM hết hạn trong 24 giờ tới để thử cảnh báo khi xếp lịch
                    until = new DateTime(_now.Year, _now.Month, _now.Day, _now.Hour, 0, 0).AddHours(20);
                    note = "KDM sắp hết hạn, chờ nhà phát hành gửi khóa mới";
                }
                KdmLicenseDAL.Insert(new KdmLicense { MovieId = movieId, CinemaId = _cinemaIds[c], ValidFrom = from, ValidUntil = until, Note = note }, conn, tx);
                _kdms.Add(new PlanKdm(i, c, from, until));
            }
        }
    }

    // ---------------------------------------------------------------- bắp nước, nguyên liệu, định mức
    private void SeedFnb(SqlConnection conn, SqlTransaction tx)
    {
        var categoryIds = new Dictionary<string, int>();
        foreach (var name in SeedCatalog.Categories)
        {
            categoryIds[name] = CategoryDAL.Insert(new Category { Name = name, Active = true }, conn, tx);
        }

        var materialIds = new Dictionary<(int Cinema, string Name), int>();
        for (int c = 0; c < _cinemaIds.Count; c++)
        {
            for (int i = 0; i < SeedCatalog.Materials.Length; i++)
            {
                var m = SeedCatalog.Materials[i];
                // Rạp 1 có một nguyên liệu gần ngưỡng tối thiểu; rạp 2 tồn kho dồi dào hơn
                var stock = c == 0 ? m.Stock : (i == SeedCatalog.NearThresholdMaterialIndex ? m.MinThreshold * 6 : m.Stock * 1.2m);
                int id = RawMaterialDAL.Insert(new RawMaterial
                {
                    CinemaId = _cinemaIds[c],
                    Name = m.Name,
                    Unit = m.Unit,
                    ActualStock = stock,
                    ReservedStock = 0,
                    MinThreshold = m.MinThreshold,
                    Active = true,
                }, conn, tx);
                materialIds[(c, m.Name)] = id;
                _materialTargets[id] = (m.Name, stock, m.MinThreshold);
            }
        }

        for (int i = 0; i < SeedCatalog.Drinks.Length; i++)
        {
            var d = SeedCatalog.Drinks[i];
            int id = DrinkDAL.Insert(new Drink
            {
                CategoryId = categoryIds[d.Category],
                Name = d.Name,
                Price = d.Price,
                Image = DrinkImagePath(i),
                Description = d.Description,
                Active = true,
                IsCombo = d.IsCombo,
                IsSoldOut = false,
                CreatedAt = _today.AddDays(-HistoryDays - 30).AddHours(9),
            }, conn, tx);
            _drinks.Add(new SeedDrink(id, d));
            for (int c = 0; c < _cinemaIds.Count; c++)
            {
                foreach (var (material, qty) in d.Recipe)
                {
                    ProductRecipeDAL.Insert(new ProductRecipe { DrinkId = id, RawMaterialId = materialIds[(c, material)], Quantity = qty }, conn, tx);
                }
            }
        }
    }

    // ---------------------------------------------------------------- phòng chiếu và ghế
    private void SeedAuditoriums(SqlConnection conn, SqlTransaction tx)
    {
        int pairBase = 0;
        for (int c = 0; c < _cinemaIds.Count; c++)
        {
            for (int a = 0; a < SeedCatalog.Auditoriums.Length; a++)
            {
                var spec = SeedCatalog.Auditoriums[a];
                var aud = new Auditorium
                {
                    CinemaId = _cinemaIds[c],
                    Name = spec.Name,
                    Format = spec.Format,
                    TotalRows = spec.Rows,
                    TotalCols = spec.Cols,
                    IdealRowRatio = SeatScoring.DefaultIdealRowRatio,
                    Active = true,
                };
                int audId = AuditoriumDAL.Insert(aud, conn, tx);
                var seedAud = new SeedAuditorium(audId, c, a, spec);
                var planned = SeatLayoutPlanner.Plan(spec, _rules.HouseSeatsPerAuditorium, aud.IdealRowRatio);
                int maxPair = 0;
                foreach (var p in planned)
                {
                    int? pairId = p.PairIndex is int pi ? pairBase + pi : null;
                    if (p.PairIndex is int pj) maxPair = Math.Max(maxPair, pj);
                    int seatId = SeatDAL.Insert(new Seat
                    {
                        AuditoriumId = audId,
                        RowIndex = p.Row,
                        ColIndex = p.Col,
                        Label = p.Label,
                        SeatType = p.SeatType,
                        PairGroupId = pairId,
                        IsHouseSeat = p.IsHouseSeat,
                        IsMaintenance = p.IsMaintenance,
                        BaseViewScore = p.BaseViewScore,
                    }, conn, tx);
                    seedAud.Seats.Add(new SeedSeat(seatId, p.Row, p.Col, p.SeatType, pairId, p.IsHouseSeat, p.IsMaintenance, p.BaseViewScore));
                }
                pairBase += maxPair;
                _auditoriums.Add(seedAud);
            }
        }
    }

    // ---------------------------------------------------------------- suất chiếu
    private void SeedShowtimes(SqlConnection conn, SqlTransaction tx)
    {
        var dates = Enumerable.Range(-HistoryDays, HistoryDays + FutureDays + 1).Select(d => _today.AddDays(d)).ToList();
        var movies = SeedCatalog.Movies.Select((m, i) => new PlanMovie(i, m.DurationMinutes, m.AgeRating, m.LanguageFormats,
            _releaseDates[i], m.IsVietnamese, true, m.Genres.Contains(Genre.Animation))).ToList();
        var plan = SchedulePlanner.Plan(dates, _cinemaIds.Count, SeedCatalog.Auditoriums.Length, movies, _kdms, _rules);

        foreach (var p in plan.OrderBy(x => x.Times.StartTime))
        {
            var aud = _auditoriums.First(a => a.CinemaIndex == p.CinemaIndex && a.Index == p.AuditoriumIndex);
            var st = new Showtime
            {
                MovieId = _movieIds[p.MovieIndex],
                AuditoriumId = aud.Id,
                StartTime = p.Times.StartTime,
                FeatureStartTime = p.Times.FeatureStartTime,
                EndTime = p.Times.EndTime,
                CleaningEndTime = p.Times.CleaningEndTime,
                LanguageFormat = p.LanguageFormat,
                OperationalStatus = p.Times.CleaningEndTime <= _now ? OperationalStatus.Completed : OperationalStatus.Scheduled,
                IsAiGenerated = false,
                IsPrivate = false,
                CancelNote = null,
                CreatedAt = Min(p.OperatingDate.AddDays(-7).AddHours(9), _now),
            };
            int id = ShowtimeDAL.Insert(st, conn, tx);
            ShowtimeSeatDAL.CreateForShowtime(id, aud.Id, conn, tx);
            _shows.Add(new SeedShow(id, p, aud));
        }
        _logger.LogInformation("Đã tạo {Count} suất chiếu mẫu.", _shows.Count);
    }

    // ---------------------------------------------------------------- voucher
    private void SeedVouchers(SqlConnection conn, SqlTransaction tx)
    {
        var list = new[]
        {
            new Voucher { Code = "FPOLY50K", Type = VoucherType.Amount, Value = 50_000, MinBillAmount = 200_000, ValidFrom = _today.AddDays(-20), ValidUntil = _today.AddDays(60).AddHours(23).AddMinutes(59), UsageLimit = 500, Source = VoucherSource.Promo, Active = true },
            new Voucher { Code = "GIAM10", Type = VoucherType.Percent, Value = 10, MinBillAmount = 100_000, ValidFrom = _today.AddDays(-HistoryDays - 15), ValidUntil = _today.AddDays(90).AddHours(23).AddMinutes(59), UsageLimit = 1000, Source = VoucherSource.Promo, Active = true },
            new Voucher { Code = "BUTHUONG-HA01", Type = VoucherType.Amount, Value = _rules.CompensationVoucherAmount, MinBillAmount = 0, ValidFrom = _now.AddDays(-3), ValidUntil = _now.AddDays(_rules.CompensationVoucherValidDays - 3), UsageLimit = 1, OwnerUserId = _khachId, Source = VoucherSource.Compensation, Active = true },
        };
        foreach (var v in list)
        {
            VoucherDAL.Insert(v, conn, tx);
            _vouchers[v.Code] = v;
        }
    }

    // ---------------------------------------------------------------- ca bán hàng
    private void SeedShifts(SqlConnection conn, SqlTransaction tx)
    {
        for (int c = 0; c < _cinemaIds.Count; c++)
        {
            for (int d = -HistoryDays; d <= 0; d++)
            {
                var date = _today.AddDays(d);
                var opened = date.AddHours(8).AddMinutes(30);
                bool isToday = d == 0;
                if (isToday && opened > _now) opened = _now.AddMinutes(-5);
                // Mỗi máy POS chỉ có một ca mở (UX_PosShifts_OpenTerminal): ca các ngày đã qua tạo ở CLOSED,
                // số tiền kiểm đếm được chốt trong FinalizeShifts sau khi mô phỏng bán hàng
                var shift = new PosShift
                {
                    UserId = _cashierOf[c],
                    CinemaId = _cinemaIds[c],
                    TerminalCode = Terminal,
                    OpenedAt = opened,
                    OpeningCashFloat = OpeningCashFloat,
                    ClosedAt = isToday ? null : date.AddHours(23).AddMinutes(55),
                    Status = isToday ? ShiftStatus.Open : ShiftStatus.Closed,
                };
                int id = PosShiftDAL.Insert(shift, conn, tx);
                _shifts[(c, date)] = new SeedShift(id, opened, isToday);
            }
        }
    }

    // ---------------------------------------------------------------- tiện ích chung
    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private string NewBillCode(DateTime at)
    {
        string code;
        do code = CodeGenerator.BillCode(at); while (!_billCodes.Add(code));
        return code;
    }

    private int NextPickup(int cinema, DateTime date)
    {
        var key = (cinema, date.Date);
        _pickupCounters.TryGetValue(key, out var last);
        var next = CodeGenerator.NextPickupNumber(last == 0 ? null : last);
        _pickupCounters[key] = next;
        return next;
    }

    private sealed record SeedDrink(int Id, DrinkSpec Spec);

    private sealed record SeedSeat(int Id, int Row, int Col, string Type, int? Pair, bool House, bool Maintenance, int Score);

    private sealed record SeedAuditorium(int Id, int CinemaIndex, int Index, AuditoriumSpec Spec)
    {
        public List<SeedSeat> Seats { get; } = new();
    }

    private sealed record SeedShow(int Id, PlannedShowtime Plan, SeedAuditorium Auditorium)
    {
        public HashSet<int> Taken { get; } = new();
        public int CinemaIndex => Plan.CinemaIndex;
        public DateTime Start => Plan.Times.StartTime;
        public DateTime Feature => Plan.Times.FeatureStartTime;
        public DateTime End => Plan.Times.EndTime;
        public MovieSpec Movie => SeedCatalog.Movies[Plan.MovieIndex];
    }

    private sealed class SeedShift
    {
        public SeedShift(int id, DateTime openedAt, bool isOpen)
        {
            Id = id;
            OpenedAt = openedAt;
            IsOpen = isOpen;
        }

        public int Id { get; }
        public DateTime OpenedAt { get; }
        public bool IsOpen { get; }
        public long CashIn { get; set; }
        public long CashRefund { get; set; }
        public DateTime LastActivity { get; set; }
    }

    private sealed record CustomerBill(int CustomerId, int BillId, string Code, DateTime PaidAt, long Total, DateTime? CancelledAt);
}
