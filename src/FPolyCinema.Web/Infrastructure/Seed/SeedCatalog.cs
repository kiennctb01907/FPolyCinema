using FPolyCinema.Web.Domain.Constants;

namespace FPolyCinema.Web.Infrastructure.Seed;

/// <summary>Phòng chiếu mẫu: kích thước lưới, cột lối đi, có hàng SWEETBOX cuối hay không.</summary>
public sealed record AuditoriumSpec(string Name, string Format, int Rows, int Cols, int[] AisleCols, bool SweetboxLastRow);

/// <summary>Phim mẫu (hư cấu). ReleaseOffsetDays tính từ hôm nay (âm là đã khởi chiếu).</summary>
public sealed record MovieSpec(
    string Title,
    string? OriginalTitle,
    string[] Genres,
    int DurationMinutes,
    string AgeRating,
    string[] ContentWarnings,
    bool IsVietnamese,
    string[] LanguageFormats,
    int ReleaseOffsetDays,
    int DistributorIndex,
    long MinimumTicketPrice,
    bool IsBlockbuster,
    decimal[] SharePercents,
    string Description);

/// <summary>Món bắp nước mẫu; Recipe là định mức (tên nguyên liệu → số lượng) cho một phần.</summary>
public sealed record DrinkSpec(string Category, string Name, long Price, bool IsCombo, DrinkArtKind Art, string Description, (string Material, decimal Quantity)[] Recipe);

/// <summary>Nguyên liệu và bao bì mẫu của mỗi rạp; Stock là tồn kho thực tế sau khi nạp xong lịch sử bán.</summary>
public sealed record MaterialSpec(string Name, string Unit, decimal Stock, decimal MinThreshold);

/// <summary>Tài khoản mẫu (CONTRACTS mục 11 và một số tài khoản phụ cho rạp 2 và lịch sử bán online).</summary>
public sealed record AccountSpec(string Email, string Role, int? CinemaIndex, string FullName, string? Phone, DateTime? BirthDate, bool HasPin);

/// <summary>
/// Dữ liệu mẫu tĩnh của DbSeeder (SPEC 8.1 bước 6). Tên phim, nhà phát hành, rạp đều hư cấu.
/// </summary>
public static class SeedCatalog
{
    public static readonly (string Name, string Address, string Phone)[] Cinemas =
    {
        ("FPoly Cinema Hà Nội", "Tầng 5, Tòa nhà FPoly, Nam Từ Liêm, Hà Nội", "024 3888 1001"),
        ("FPoly Cinema Hồ Chí Minh", "Tầng 4, Tòa nhà FPoly, Quận 12, TP. Hồ Chí Minh", "028 3888 1002"),
    };

    /// <summary>Mỗi rạp 3 phòng: IMAX 12 × 18 có hàng SWEETBOX cuối, 3D 10 × 14, 2D 8 × 12; có hai lối đi dọc.</summary>
    public static readonly AuditoriumSpec[] Auditoriums =
    {
        new("Phòng 1 – IMAX", AuditoriumFormat.IMAX, 12, 18, new[] { 5, 14 }, true),
        new("Phòng 2 – 3D", AuditoriumFormat.F3D, 10, 14, new[] { 4, 11 }, false),
        new("Phòng 3 – 2D", AuditoriumFormat.F2D, 8, 12, new[] { 3, 10 }, false),
    };

    public static readonly (string Name, string Email, string Phone)[] Distributors =
    {
        ("Ánh Dương Media", "lienhe@anhduongmedia.vn", "024 3555 0101"),
        ("Blue Lantern Distribution", "booking@bluelantern.vn", "028 3555 0202"),
        ("Hải Âu Entertainment", "phathanh@haiau-ent.vn", "024 3555 0303"),
    };

    /// <summary>
    /// 10 phim hư cấu: 3 phim Việt; đủ nhãn P, K, T13, T16, T18 và 1 phim C chỉ lưu hồ sơ; 2 phim hoạt hình có bản lồng tiếng;
    /// 1 phim tên dài khoảng 40 ký tự. Phim thứ 7 (index 6) có KDM tại rạp 1 hết hạn trong 24 giờ.
    /// </summary>
    public static readonly MovieSpec[] Movies =
    {
        new("Mùa Hè Trên Đồi Gió", null, new[] { Genre.Drama, Genre.Family }, 112, AgeRating.P,
            new[] { ContentWarning.Theme }, true, new[] { LanguageFormat.Original }, -40, 0, 55_000, false, new[] { 50m, 45m, 40m },
            "Hai chị em trở về quê ngoại và cùng ông bà giữ lại khu đồi lộng gió trước dự án san lấp."),
        new("Căn Gác Số 9 Phố Cũ", null, new[] { Genre.Horror, Genre.Thriller }, 98, AgeRating.T18,
            new[] { ContentWarning.Horror, ContentWarning.Violence }, true, new[] { LanguageFormat.Original }, -20, 0, 55_000, false, new[] { 50m, 45m, 40m },
            "Một nhóm bạn trẻ thuê căn nhà cổ trong phố cũ và phát hiện những bí mật bị chôn vùi nhiều thập kỷ."),
        new("Đội Đặc Nhiệm Sông Hồng", null, new[] { Genre.Action, Genre.Adventure }, 125, AgeRating.T16,
            new[] { ContentWarning.Violence, ContentWarning.Language }, true, new[] { LanguageFormat.Original }, -10, 1, 60_000, true, new[] { 55m, 50m, 45m },
            "Đội cứu hộ đặc biệt chạy đua với thời gian giữa trận lũ lịch sử để giải cứu một chuyến tàu mắc kẹt."),
        new("Chú Mèo Phi Hành Gia", "Orbit Paws", new[] { Genre.Animation, Genre.Comedy, Genre.Family }, 95, AgeRating.P,
            new[] { ContentWarning.Theme }, false, new[] { LanguageFormat.Dubbed, LanguageFormat.Subtitled }, -35, 2, 50_000, false, new[] { 55m, 50m, 45m },
            "Chú mèo nhỏ vô tình lên con tàu vũ trụ và phải tìm đường đưa cả phi hành đoàn trở về Trái Đất."),
        new("Cô Bé Mây Và Xứ Sở Kẹo Bông", "Candy Cloud Kingdom", new[] { Genre.Animation, Genre.Adventure }, 102, AgeRating.K,
            new[] { ContentWarning.Theme }, false, new[] { LanguageFormat.Dubbed, LanguageFormat.Subtitled }, -12, 2, 50_000, false, new[] { 55m, 50m, 45m },
            "Cô bé Mây lạc vào vương quốc làm từ kẹo và phải giải cứu nơi đây khỏi cơn mưa axit chanh."),
        new("Giao Lộ Tốc Độ", "Velocity Junction", new[] { Genre.Action, Genre.Thriller }, 130, AgeRating.T13,
            new[] { ContentWarning.Violence, ContentWarning.DangerousBehavior }, false, new[] { LanguageFormat.Subtitled, LanguageFormat.Original }, -28, 1, 65_000, true, new[] { 60m, 55m, 50m },
            "Tay đua đường phố về hưu bị kéo vào phi vụ cuối cùng xuyên qua năm thành phố trong một đêm."),
        new("Lời Hứa Dưới Mưa Sao Băng", "Promise Under Falling Stars", new[] { Genre.Romance, Genre.Drama }, 108, AgeRating.T13,
            new[] { ContentWarning.Theme, ContentWarning.Language }, false, new[] { LanguageFormat.Subtitled }, -6, 0, 55_000, false, new[] { 55m, 50m, 45m },
            "Hai người lạ hẹn gặp lại nhau mỗi khi có mưa sao băng, cho đến khi một trong hai không còn xuất hiện."),
        new("Biên Niên Sử Vùng Đất Băng Giá Phương Bắc", "Northbound Ice Chronicle", new[] { Genre.SciFi, Genre.Adventure }, 148, AgeRating.T16,
            new[] { ContentWarning.Violence }, false, new[] { LanguageFormat.Subtitled, LanguageFormat.Original }, -3, 1, 70_000, true, new[] { 60m, 55m, 50m },
            "Đoàn thám hiểm cuối cùng của nhân loại vượt qua vùng băng vĩnh cửu để tìm nguồn năng lượng cứu thế giới."),
        new("Thanh Tra Đêm Mất Điện", "Blackout Inspector", new[] { Genre.Thriller, Genre.Horror }, 117, AgeRating.T18,
            new[] { ContentWarning.Violence, ContentWarning.Horror, ContentWarning.Drugs }, false, new[] { LanguageFormat.Subtitled }, -16, 2, 60_000, false, new[] { 55m, 50m, 45m },
            "Thanh tra trẻ truy đuổi kẻ sát nhân chỉ ra tay trong những đêm mất điện toàn thành phố."),
        new("Phòng Thí Nghiệm Số 19", "Lab Nineteen", new[] { Genre.Horror, Genre.Thriller }, 104, AgeRating.C,
            new[] { ContentWarning.Horror, ContentWarning.Violence, ContentWarning.NuditySex }, false, new[] { LanguageFormat.Subtitled }, 5, 2, 55_000, false, new[] { 55m, 50m, 45m },
            "Nhóm phóng viên lẻn vào khu thí nghiệm bỏ hoang để điều tra những vụ mất tích bí ẩn."),
    };

    /// <summary>Index của phim có KDM tại rạp 1 hết hạn trong 24 giờ tới (để thử cảnh báo KDM).</summary>
    public const int KdmExpiringMovieIndex = 6;

    public static readonly string[] Categories = { "Bắp rang", "Nước uống", "Đồ ăn vặt", "Combo" };

    public static readonly MaterialSpec[] Materials =
    {
        new("Bắp hạt", "kg", 42m, 8m),
        new("Dầu bắp", "lít", 18m, 4m),
        new("Đường caramel", "kg", 9m, 2m),
        new("Bột phô mai", "kg", 1.25m, 1m),
        new("Siro nước có ga", "lít", 34m, 6m),
        new("Trà đào cô đặc", "lít", 11m, 2m),
        new("Nước suối chai 500ml", "chai", 160m, 40m),
        new("Xúc xích", "cây", 95m, 20m),
        new("Bánh mì hotdog", "cái", 70m, 20m),
        new("Snack khoai tây gói", "gói", 120m, 30m),
        new("Xô bắp giấy", "cái", 380m, 80m),
        new("Ly giấy kèm nắp và ống hút", "bộ", 520m, 100m),
    };

    /// <summary>Index nguyên liệu gần ngưỡng tối thiểu (Bột phô mai) – áp cho rạp 1.</summary>
    public const int NearThresholdMaterialIndex = 3;

    private static readonly (string, decimal)[] PopcornM = { ("Bắp hạt", 0.12m), ("Dầu bắp", 0.02m), ("Xô bắp giấy", 1m) };
    private static readonly (string, decimal)[] PopcornL = { ("Bắp hạt", 0.18m), ("Dầu bắp", 0.03m), ("Xô bắp giấy", 1m) };
    private static readonly (string, decimal)[] SodaM = { ("Siro nước có ga", 0.08m), ("Ly giấy kèm nắp và ống hút", 1m) };

    private static (string, decimal)[] With(params (string, decimal)[][] parts) =>
        parts.SelectMany(p => p).GroupBy(p => p.Item1).Select(g => (g.Key, g.Sum(x => x.Item2))).ToArray();

    /// <summary>Khoảng 14 món lẻ và 4 combo; mọi món đều có định mức nguyên liệu và bao bì.</summary>
    public static readonly DrinkSpec[] Drinks =
    {
        new("Bắp rang", "Bắp ngọt (M)", 49_000, false, DrinkArtKind.Popcorn, "Bắp rang bơ vị ngọt, xô cỡ vừa.", PopcornM),
        new("Bắp rang", "Bắp ngọt (L)", 65_000, false, DrinkArtKind.Popcorn, "Bắp rang bơ vị ngọt, xô cỡ lớn.", PopcornL),
        new("Bắp rang", "Bắp phô mai (M)", 59_000, false, DrinkArtKind.Popcorn, "Bắp rang phủ bột phô mai.", With(PopcornM, new[] { ("Bột phô mai", 0.03m) })),
        new("Bắp rang", "Bắp caramel (M)", 59_000, false, DrinkArtKind.Popcorn, "Bắp rang phủ caramel giòn.", With(PopcornM, new[] { ("Đường caramel", 0.04m) })),
        new("Bắp rang", "Bắp nửa phô mai nửa caramel (L)", 75_000, false, DrinkArtKind.Popcorn, "Hai vị trong một xô cỡ lớn.", With(PopcornL, new[] { ("Bột phô mai", 0.02m), ("Đường caramel", 0.03m) })),
        new("Nước uống", "Nước có ga vị cola (M)", 35_000, false, DrinkArtKind.Drink, "Ly 22oz, nhiều đá.", SodaM),
        new("Nước uống", "Nước có ga vị cola (L)", 42_000, false, DrinkArtKind.Drink, "Ly 32oz, nhiều đá.", new[] { ("Siro nước có ga", 0.12m), ("Ly giấy kèm nắp và ống hút", 1m) }),
        new("Nước uống", "Nước chanh có ga (M)", 35_000, false, DrinkArtKind.Drink, "Vị chanh tươi mát, ly 22oz.", SodaM),
        new("Nước uống", "Trà đào (M)", 39_000, false, DrinkArtKind.Drink, "Trà đào cam sả, ly 22oz.", new[] { ("Trà đào cô đặc", 0.06m), ("Ly giấy kèm nắp và ống hút", 1m) }),
        new("Nước uống", "Nước suối", 20_000, false, DrinkArtKind.Drink, "Chai 500ml.", new[] { ("Nước suối chai 500ml", 1m) }),
        new("Đồ ăn vặt", "Xúc xích nướng", 39_000, false, DrinkArtKind.Snack, "Xúc xích nướng kèm tương cà.", new[] { ("Xúc xích", 1m) }),
        new("Đồ ăn vặt", "Hotdog", 45_000, false, DrinkArtKind.Snack, "Bánh mì kẹp xúc xích.", new[] { ("Xúc xích", 1m), ("Bánh mì hotdog", 1m) }),
        new("Đồ ăn vặt", "Snack khoai tây", 25_000, false, DrinkArtKind.Snack, "Gói 52g.", new[] { ("Snack khoai tây gói", 1m) }),
        new("Đồ ăn vặt", "Hotdog phô mai", 52_000, false, DrinkArtKind.Snack, "Hotdog phủ phô mai.", new[] { ("Xúc xích", 1m), ("Bánh mì hotdog", 1m), ("Bột phô mai", 0.02m) }),
        new("Combo", "Combo Solo", 79_000, true, DrinkArtKind.Combo, "1 bắp ngọt (M) và 1 nước có ga (M).", With(PopcornM, SodaM)),
        new("Combo", "Combo Couple", 109_000, true, DrinkArtKind.Combo, "1 bắp ngọt (L) và 2 nước có ga (M).", With(PopcornL, SodaM, SodaM)),
        new("Combo", "Combo bắp phô mai", 95_000, true, DrinkArtKind.Combo, "1 bắp phô mai (L) và 1 nước có ga (L).", With(PopcornL, new[] { ("Bột phô mai", 0.04m), ("Siro nước có ga", 0.12m), ("Ly giấy kèm nắp và ống hút", 1m) })),
        new("Combo", "Combo Gia đình", 199_000, true, DrinkArtKind.Combo, "2 bắp ngọt (L), 4 nước có ga (M) và 2 snack.", With(PopcornL, PopcornL, SodaM, SodaM, SodaM, SodaM, new[] { ("Snack khoai tây gói", 2m) })),
    };

    /// <summary>Bảng giá 6 chiều của từng rạp (số tiền cộng vào giá gốc, có thể âm).</summary>
    public static (string Type, string Key, long Amount)[] PricingFor(int cinemaIndex)
    {
        long baseAmount = cinemaIndex == 0 ? 70_000 : 75_000;
        return new[]
        {
            (PricingRuleType.Base, PricingRuleKey.Default, baseAmount),
            (PricingRuleType.DayType, DayType.Weekday, 0L),
            (PricingRuleType.DayType, DayType.Weekend, 15_000L),
            (PricingRuleType.DayType, DayType.Holiday, 20_000L),
            (PricingRuleType.DayType, DayType.HappyTuesday, -20_000L),
            (PricingRuleType.TimeSlot, TimeSlot.Morning, -10_000L),
            (PricingRuleType.TimeSlot, TimeSlot.Afternoon, 0L),
            (PricingRuleType.TimeSlot, TimeSlot.Golden, 10_000L),
            (PricingRuleType.TimeSlot, TimeSlot.Late, -5_000L),
            (PricingRuleType.SeatType, SeatType.Standard, 0L),
            (PricingRuleType.SeatType, SeatType.Vip, 15_000L),
            (PricingRuleType.SeatType, SeatType.Sweetbox, 30_000L),
            (PricingRuleType.SeatType, SeatType.Wheelchair, 0L),
            (PricingRuleType.Format, AuditoriumFormat.F2D, 0L),
            (PricingRuleType.Format, AuditoriumFormat.F3D, 20_000L),
            (PricingRuleType.Format, AuditoriumFormat.IMAX, 50_000L),
            (PricingRuleType.Format, AuditoriumFormat.F4DX, 70_000L),
            (PricingRuleType.Blockbuster, PricingRuleKey.Yes, 10_000L),
        };
    }

    /// <summary>AudienceDiscounts theo CONTRACTS mục 4: (đối tượng, giảm mặc định, mức tối thiểu theo luật, chỉ bán tại quầy, cần giấy tờ).</summary>
    public static readonly (string Type, decimal Discount, decimal LegalMin, bool PosOnly, bool RequiresDocument)[] AudienceDiscounts =
    {
        (AudienceType.Adult, 0m, 0m, false, false),
        (AudienceType.U22, 10m, 0m, false, true),
        (AudienceType.ChildU13, 20m, 20m, true, true),
        (AudienceType.Child13To15, 20m, 20m, true, true),
        (AudienceType.Senior, 20m, 20m, true, true),
        (AudienceType.Merit, 20m, 20m, true, true),
        (AudienceType.Hardship, 20m, 20m, true, true),
        (AudienceType.DisabledSevere, 50m, 50m, true, true),
        (AudienceType.DisabledExtreme, 100m, 100m, true, true),
    };

    /// <summary>Các ngày lễ năm 2026 theo Bộ luật Lao động (Tết Nguyên đán Bính Ngọ: mùng 1 là 17/02/2026).</summary>
    public static readonly (DateTime Date, string Name)[] Holidays2026 =
    {
        (new DateTime(2026, 1, 1), "Tết Dương lịch"),
        (new DateTime(2026, 2, 16), "Tết Nguyên đán (29 Tết)"),
        (new DateTime(2026, 2, 17), "Tết Nguyên đán (mùng 1)"),
        (new DateTime(2026, 2, 18), "Tết Nguyên đán (mùng 2)"),
        (new DateTime(2026, 2, 19), "Tết Nguyên đán (mùng 3)"),
        (new DateTime(2026, 2, 20), "Tết Nguyên đán (mùng 4)"),
        (new DateTime(2026, 4, 26), "Giỗ Tổ Hùng Vương (10/3 âm lịch)"),
        (new DateTime(2026, 4, 27), "Nghỉ bù Giỗ Tổ Hùng Vương"),
        (new DateTime(2026, 4, 30), "Ngày Giải phóng miền Nam"),
        (new DateTime(2026, 5, 1), "Ngày Quốc tế Lao động"),
        (new DateTime(2026, 9, 1), "Quốc khánh (ngày liền kề)"),
        (new DateTime(2026, 9, 2), "Quốc khánh"),
    };

    /// <summary>Mật khẩu chung của mọi tài khoản mẫu và PIN quản lý (CONTRACTS mục 11). Lưu dạng băm.</summary>
    public const string SamplePassword = "123456";
    public const string SampleManagerPin = "246810";

    public const string KhachEmail = "khach@fpolycinema.vn";
    public const string Khach2Email = "khach2@fpolycinema.vn";

    public const int ExtraCustomerCount = 150;

    // Khai báo trước Accounts vì trường static được khởi tạo theo thứ tự trong file
    private static readonly string[] FamilyNames =
        { "Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Huỳnh", "Phan", "Vũ", "Võ", "Đặng", "Bùi", "Đỗ", "Hồ", "Ngô", "Dương", "Lý", "Trịnh", "Mai" };

    private static readonly string[] MiddleNames =
        { "Văn", "Thị", "Minh", "Thu", "Quốc", "Ngọc", "Hoàng", "Thanh", "Gia", "Bảo", "Đức", "Phương" };

    private static readonly string[] GivenNames =
        { "An", "Bình", "Châu", "Dũng", "Giang", "Hải", "Hạnh", "Khoa", "Linh", "Long", "Mai", "Nam", "Ngân", "Phong", "Quỳnh", "Sơn", "Tâm", "Thảo", "Trung", "Vy", "Yến", "Tuấn", "Hiếu" };

    /// <summary>Tài khoản theo CONTRACTS mục 11, cộng nhân viên rạp 2 và khách phụ để có lịch sử bán online.</summary>
    public static readonly AccountSpec[] Accounts = BuildAccounts();

    private static AccountSpec[] BuildAccounts()
    {
        var list = new List<AccountSpec>
        {
            new("manager@fpolycinema.vn", Roles.Manager, null, "Trần Minh Quân", "0901000001", new DateTime(1985, 3, 14), true),
            new("shift@fpolycinema.vn", Roles.ShiftManager, 0, "Phạm Thu Trang", "0901000002", new DateTime(1990, 7, 2), true),
            new("cashier@fpolycinema.vn", Roles.POSCashier, 0, "Đỗ Văn Hùng", "0901000003", new DateTime(1999, 11, 23), false),
            new("gate@fpolycinema.vn", Roles.TicketInspector, 0, "Vũ Thị Lan", "0901000004", new DateTime(2000, 1, 9), false),
            new(KhachEmail, Roles.Customer, null, "Nguyễn Thu Hà", "0912000001", new DateTime(1998, 5, 20), false),
            new(Khach2Email, Roles.Customer, null, "Lê Hoàng Nam", "0912000002", new DateTime(2001, 9, 12), false),
            new("shift2@fpolycinema.vn", Roles.ShiftManager, 1, "Hoàng Minh Châu", "0901000005", new DateTime(1988, 4, 18), true),
            new("cashier2@fpolycinema.vn", Roles.POSCashier, 1, "Ngô Gia Bảo", "0901000006", new DateTime(2000, 6, 30), false),
            new("gate2@fpolycinema.vn", Roles.TicketInspector, 1, "Bùi Ngọc Ánh", "0901000007", new DateTime(2001, 2, 5), false),
        };
        // Khách phụ có tài khoản để lịch sử bán online trải đều (mỗi người vài lần đặt vé trong 30 ngày, đa số hạng MEMBER)
        for (int i = 0; i < ExtraCustomerCount; i++)
        {
            int n = i + 3;
            var name = $"{FamilyNames[i % FamilyNames.Length]} {MiddleNames[(i / FamilyNames.Length + i) % MiddleNames.Length]} {GivenNames[(i * 7) % GivenNames.Length]}";
            list.Add(new($"khach{n:D3}@fpolycinema.vn", Roles.Customer, null, name, $"0912{n:D6}",
                new DateTime(1975 + (i * 7) % 32, 1 + i % 12, 1 + (i * 5) % 27), false));
        }
        return list.ToArray();
    }
}
