# Kiểm tra độc lập – Phase 1

Agent spec-auditor kiểm từng nhóm mã (không quá 20 mã một lượt, theo nhóm của lệnh `list 1`), đọc spec, truy vết và code thật.
Cột "Đã sửa" ghi việc đã làm cho các dòng Thiếu hoặc Sai và kết quả của lượt kiểm lại.

| Mã | Kết luận | Lý do | Đã sửa |
|---|---|---|---|
| S2.1-01 | Đạt | net8.0, Microsoft.NET.Sdk.Web, AddControllersWithViews; không có gói Entity Framework | – |
| S2.1-02 | Đạt | Microsoft.Data.SqlClient 5.2.2; SQL không dùng hàm chỉ có từ SQL Server 2022 | – |
| S2.1-04 | Đạt | Identity.Core 8.0.11, chỉ dùng PasswordHasher cho mật khẩu và PIN | – |
| S2.1-08 | Đạt | xunit 2.9.2; dotnet test 193/193 | – |
| S2.2-01 | Đạt | Entities, DAL, Utils/DBUtil; lớp dữ liệu không gọi lên trên | – |
| S2.2-02 | Đạt | Services/BillStatusService, Domain/Rules thuần, Domain/Constants | – |
| S2.2-03 | Đạt | Có Controllers, Views, ViewModels, wwwroot; màn hình làm từ Phase 2 | – |
| S2.2-04 | Sai | Code đúng, nhưng test Layers_ServicesCallDalNotDBUtil cấm cả DBUtil.InTransaction trong Service, chặt hơn spec (S2.3-09 bắt Service dùng InTransaction) | Test cho Service gọi DBUtil.InTransaction, chỉ cấm câu SQL trực tiếp. Kiểm lại 07/11: Đạt |
| S2.2-05 | Đạt | Đúng tên solution, project, cấu trúc; tokens.css, ui.js, realtime.js, tools/ui-snapshots thuộc phase sau | – |
| S2.3-01 | Đạt | QueryDataTable và bí danh queryDataTable, có bản conn/tx; DBUtil.Init lúc khởi động | – |
| S2.3-02 | Đạt | ExecuteScalar và executeScalar, DBNull thành null; test SCOPE_IDENTITY và @@ROWCOUNT | – |
| S2.3-03 | Đạt | ExecuteNonQuery trả số dòng | – |
| S2.3-04 | Đạt | Mọi hàm có bản nạp chồng (conn, tx) | – |
| S2.3-05 | Đạt | InTransaction commit, rollback rồi ném lại; có test | – |
| S2.3-06 | Đạt | @1, @2 theo thứ tự, DBNull, DateTime2, BigInt, InParams; agent gợi ý ghi quyết định cho danh sách rỗng | Đã ghi quyết định 22 ở PROGRESS.md |
| S2.3-07 | Đạt | 35 Entity, 35 DAL đủ 6 hàm, hàm ghi nhận conn và tx | – |
| S2.3-08 | Đạt | Đủ lớp tên slide; Authenticate lọc Active = 1 rồi kiểm PasswordHasher; SetActive | – |
| S2.4-01 | Đạt | Kết nối master, tạo DB nếu chưa có, chạy script theo tên, tách GO, ghi SchemaVersions, procedure CREATE OR ALTER | – |
| S2.4-02 | Đạt | SeedIfEmpty chỉ chạy khi Users rỗng; test lần nạp thứ hai không chạy | – |
| S2.4-03 | Đạt | --reset-db chỉ trong Development, ngoài Development ném lỗi trước khi xóa DB | – |
| S2.4-04 | Đạt | --init-only thoát 0 khi thành công, 1 khi lỗi | – |
| S2.6-01 | Đạt | FileUtil là lớp tĩnh | – |
| S2.6-02 | Đạt | 5 đuôi, kiểm chữ ký đầu file, tối đa 5MB, tên {timestamp}_{shortGuid}{ext} theo IClock | – |
| S2.6-03 | Đạt | Delete chỉ xóa file nằm trực tiếp trong uploads | – |
| S2.7-01 | Đạt | SystemClock theo TimeZoneId; mọi nơi dùng IClock; test dùng FakeClock | – |
| S2.7-09 | Đạt | Không System.Drawing; ảnh mẫu SVG | – |
| S2.7-10 | Đạt | Path.Combine; tên file đúng hoa thường, có test | – |
| S2.7-11 | Đạt | AddEnvironmentVariables; test ghi đè bằng biến môi trường | – |
| S3.1-14 | Đạt | AudienceDiscounts đúng CONTRACTS mục 4, CHECK mức tối thiểu | – |
| S3.2-01 | Đạt | Hàng 1 là A, số ghế bỏ lối đi; UNIQUE vị trí và nhãn | – |
| S3.3-01 | Đạt | Làm tròn đến bội số gần nhất; ghi chú: làm tròn theo ticks từ năm 0001 chỉ khớp 00:00 khi bước chia hết 1440 phút | Đã đổi sang làm tròn tính từ 00:00 của ngày, thêm test Rounding_IsMeasuredFromMidnightForAnyStep |
| S3.3-02 | Đạt | Feature = Start + AdsMinutes | – |
| S3.3-03 | Đạt | End = Feature + DurationMinutes | – |
| S3.3-04 | Đạt | CleaningEnd làm tròn lên; ghi chú như S3.3-01 | Như S3.3-01 |
| S3.4-01 | Đạt | PricingRules có CinemaId, Amount BIGINT không chặn số âm, UNIQUE(CinemaId, RuleType, RuleKey); seeder nạp 18 dòng mỗi rạp, có HAPPY_TUESDAY −20.000 | – |
| S3.4-02 | Đạt | OperatingDate (trước 06:00 thuộc hôm trước), DayTypeOf xét HOLIDAY trước rồi thứ Ba, cuối tuần, ngày thường; có test cả 4 loại và suất 00:30 | – |
| S3.4-03 | Đạt | TimeSlotOf theo FeatureStartTime, mốc lấy từ CinemaRules, trước 06:00 là LATE; test đủ các mốc biên | – |
| S3.4-04 | Đạt | RoundPrice Floor(x/1000 + 0,5) × 1000, không âm; test 85.500→86.000, 85.499→85.000 | – |
| S3.4-06 | Đạt | PreTaxAmount đúng công thức; dữ liệu mẫu lưu VatRate = TicketVatPercent và FnbVatPercent; test seed không có dòng lệch | – |
| S3.4-07 | Đạt | PricingCalculator tĩnh, thuần; unit test phủ đủ 6 chiều và 9 AudienceType | – |
| S3.12-02 | Đạt | Ba SP lọc finish, PaidAt gồm cả hai đầu, bỏ vé SWAPPED/VOIDED, doanh thu từ FinalPrice và LineFinalAmount; test số tính tay | – |
| S3.12-03 | Đạt | TOP 5, đúng 4 cột, sắp TotalQuantitySold giảm dần; StatisticDAL gọi bằng EXEC | – |
| S3.12-04 | Đạt | TOP 5, đúng 4 cột, sắp TicketRevenue giảm dần; test phim ít vé doanh thu cao đứng trước | – |
| S3.12-05 | Đạt | CTE ngày đủ mọi ngày, ngày không bán ghi 0, sắp ngày tăng dần; test 4 ngày có 2 ngày 0 | – |
| S3.12-06 | Đạt | Tuần n nửa mở [Release + 7(n−1), Release + 7n), theo ngày vận hành; test suất 00:30 | – |
| S3.12-07 | Đạt | TOP 1 quy tắc WeekNumber ≤ n giảm dần; tuần 5 dùng quy tắc tuần 3 | – |
| S3.12-08 | Đạt | Net, FloorNet theo VatRate từng vé, Base = max | – |
| S3.12-09 | Đạt | Trả đủ 7 đại lượng kèm tuần, tỷ lệ; agent gợi ý ghi rõ công thức "phần rạp tự chịu" | Đã ghi quyết định 21 ở PROGRESS.md |
| S4.1-01 | Đạt | RawScore nhận R, C, r, c; c là cột lưới tính cả lối đi | – |
| S4.1-02 | Đạt | IdealRow (NEAR_SCREEN 0,40), IdealCol (C+1)/2; mặc định 0,65 ở hằng và DEFAULT của cột | – |
| S4.1-03 | Đạt | Công thức S_raw đúng; test tính tay 47,571 | – |
| S4.1-04 | Đạt | r ≤ 2 trừ 25, NEAR_SCREEN chỉ trừ 10 ở r = 1 | – |
| S4.1-05 | Đạt | PRIVATE_COUPLE +20 khi r ≥ 0,85R hoặc SWEETBOX | – |
| S4.1-06 | Đạt | NEAR_AISLE +15 khi kề lối đi hoặc tường | – |
| S4.1-07 | Đạt | Làm tròn AwayFromZero rồi giới hạn 0–100 | – |
| S4.1-08 | Đạt | Lớp tĩnh thuần, đủ 3 test spec yêu cầu | – |
| S7-01 | Đạt | Có test cho BillStateMachine, PricingCalculator, SeatScoring và các lớp Rules còn lại | – |
| S7-06 | Thiếu | Cột "Commit sửa lỗi" chưa có mã commit (repo chưa có commit); ghi chú truy vết nói 4 lỗi trong khi log có 5 | Cột Commit sửa lỗi ghi defe3cf; ghi chú truy vết sửa thành mọi lỗi (BUG-001 đến BUG-006). Kiểm lại 07/11: Đạt |
| S7-07 | Thiếu | "Kết quả thực tế" chưa khớp lần chạy cuối (số test, thời gian, top 5 món, số hóa đơn); agent gặp 28 test CSDL lỗi khi nhiều lượt `dotnet test` chạy cùng lúc | Cập nhật TEST-SCENARIOS theo lần chạy cuối (mã băm 41bc72904895, 195/195); sửa lỗi chạy test song song (BUG-006). Kiểm lại 07/11: Đạt |
| U7.ART-01 | Đạt | Poster 600×900, gradient theo thể loại, họa tiết mờ, tên đậm 700 tự xuống dòng, dòng phụ thể loại • năm, nhãn tuổi góc trên | – |
| U7.ART-02 | Đạt | Ảnh món 600×600 nền nhạt, 4 kiểu hình phẳng | – |
| U7.ART-03 | Đạt | Không có ảnh hay href ngoài | – |
| C4-01 | Đạt | 9 hằng AudienceType, CHECK của Tickets và AudienceDiscounts đúng đủ | – |
| C4-02 | Đạt | SeedCatalog khớp từng dòng bảng CONTRACTS mục 4 | – |
| C5-01 | Đạt | Chữ ký đúng, đường đổi trạng thái duy nhất qua BillStatusService; chú thích nhắc nhầm BillDAL.ChangeStatus | Đã sửa chú thích trong BillStateMachine.cs |
| C5-02 | Đạt | 8 dòng bảng có test, 9 chuyển ngoài bảng bị từ chối với INVALID_TRANSITION | – |
| S8.1-01 | Thiếu | Máy chỉ có SDK 10.0.301 (có runtime 8.0.28); spec yêu cầu dotnet --list-sdks có .NET 8 | Đã hỏi người dùng 07/11: người dùng chọn giữ SDK 10. Kiểm lại 07/11: vẫn Thiếu vì điều kiện chưa đúng và mã chưa có trong scope.json; chờ người dùng quyết định |
| S8.1-02 | Đạt | MSSQLSERVER đang chạy, chuỗi kết nối localhost trong appsettings.Development.json; test tích hợp kết nối được | – |
| S8.1-03 | Đạt | Git 2.54, Node chạy được check.mjs | – |
| S8.1-04 | Sai | Thiếu SDK .NET 8 nhưng không dừng hỏi người dùng mà tự chốt quyết định 1 | Đã hỏi người dùng 07/11 (muộn, sau khi đã làm phase; ghi rõ ở PROGRESS.md). Kiểm lại 07/11: Đạt |
| S8.1-05 | Đạt | git init, .gitignore đủ mục, uploads bỏ qua trừ seed | – |
| S8.1-06 | Đạt | FPolyCinema.sln hai project net8.0, cấu trúc 2.2, launchSettings 5080 | – |
| S8.1-07 | Đạt | PROGRESS.md đúng mẫu, TEST-SCENARIOS và BUG-LOG đúng cấu trúc mục 7 | – |
| S8.1-08 | Đạt | Constants đủ mục 3–4, 35 bảng có khóa, UNIQUE, CHECK, index; 4 procedure; DbInitializer, IClock | – |
| S8.1-09 | Đạt | DBUtil, FileUtil, 35 Entity và 35 DAL khớp schema | – |
| S8.1-10 | Đạt | BillStateMachine và PricingCalculator kèm unit test | – |
| S8.1-11 | Đạt | 2 rạp × 3 phòng IMAX 12×18 (SWEETBOX hàng cuối 8 cặp), 3D 10×14, 2D 8×12; lối đi, VIP hàng giữa, 2 xe lăn, 4 nội bộ, 1 bảo trì, BaseViewScore | – |
| S8.1-12 | Sai | Đủ 3 phim Việt, đủ nhãn, 1 phim C; nhưng tên gốc "Forbidden Zone", "Night Stalker" trùng phim có thật trong khi spec yêu cầu phim hư cấu | Đổi các tên có thể trùng phim thật (quyết định 24), sinh lại poster. Kiểm lại 07/11: Đạt |
| S8.1-13 | Đạt | 2 phim hoạt hình có DUBBED và có suất lồng tiếng; tên 41 ký tự | – |
| S8.1-14 | Đạt | Mỗi phim có giấy phép, cảnh báo, 3 quy tắc chia doanh thu, giá sàn, KDM 2 rạp; 1 KDM hết hạn sau khoảng 20 giờ, không suất nào vượt hạn | – |
| S8.1-15 | Đạt | 10 poster, 18 ảnh món SVG ở uploads/seed, đường dẫn DB khớp từng ký tự | – |
| S8.1-16 | Đạt | 4 danh mục, 14 món, 4 combo, 12 nguyên liệu mỗi rạp, định mức đủ; Bột phô mai rạp 1 gần ngưỡng | – |
| S8.1-17 | Đạt | 18 dòng giá mỗi rạp, AudienceDiscounts đúng 9 dòng, 12 ngày lễ 2026 | – |
| S8.1-18 | Đạt | 6 suất mỗi phòng mỗi ngày từ hôm nay đến 6 ngày sau; truy vấn 0 lỗi mọi ràng buộc; nhiều suất sắp chiếu đã có vé | – |
| S8.1-19 | Đạt | Hóa đơn finish đủ 30 ngày cả hai kênh, có vé và bắp nước; cuối tuần, buổi tối đông hơn; 29 hóa đơn hủy với 4 lý do | – |
| S8.1-20 | Đạt | Đủ tài khoản CONTRACTS mục 11, ca mở POS-01, 2 voucher công khai, 1 voucher bồi thường; khach@ VIP có điểm, ví, vé sắp chiếu | – |
| S8.1-21 | Đạt | PROGRESS.md ghi kết quả build, test, --reset-db, 4 stored procedure; chạy lại đạt | – |
| S6-01 | Đạt | Users đủ 18 cột, Email UNIQUE, MemberCode UNIQUE lọc khác NULL, CHECK Role và Tier; Entity và DAL đủ cột | – |
| S6-02 | Đạt | PasswordResetOtps đủ cột, FK UserId; Entity và DAL đủ cột | – |
| S6-03 | Đạt | EmailOutbox đủ cột, SentAt cho phép NULL; Entity và DAL đủ cột | – |
| S6-04 | Đạt | Cinemas đủ cột; Entity và DAL đủ cột | – |
| S6-05 | Đạt | Categories, Name UNIQUE; Entity và DAL đủ cột | – |
| S6-06 | Đạt | Drinks đủ 10 cột, Price BIGINT; Entity và DAL đủ cột | – |
| S6-07 | Đạt | RawMaterials DECIMAL(18,3), không âm; Entity và DAL đủ cột | – |
| S6-08 | Đạt | ProductRecipes UNIQUE(DrinkId, RawMaterialId); Entity và DAL đủ cột | – |
| S6-09 | Đạt | InventoryTransactions đủ FK, CHECK Type; Entity và DAL đủ cột | – |
| S6-10 | Đạt | WastageLogs CHECK Status; Entity và DAL đủ cột | – |
| S6-11 | Đạt | WastageLogItems DECIMAL(18,3); Entity và DAL đủ cột | – |
| S6-12 | Đạt | Distributors đủ cột; Entity và DAL đủ cột | – |
| S6-13 | Đạt | Movies đủ 20 cột, CHECK AgeRating; Entity và DAL đủ cột | – |
| S6-14 | Đạt | FilmRentalRules UNIQUE(MovieId, WeekNumber), DECIMAL(5,2); Entity và DAL đủ cột | – |
| S6-15 | Đạt | KdmLicenses DATETIME2(0), ValidUntil > ValidFrom; Entity và DAL đủ cột | – |
| S6-16 | Đạt | Auditoriums CHECK Format, IdealRowRatio mặc định 0.65; Entity và DAL đủ cột | – |
| S6-17 | Đạt | Seats UNIQUE vị trí, CHECK SeatType, BaseViewScore 0–100; Entity và DAL đủ cột | – |
| S6-18 | Đạt | Showtimes đủ 13 cột, index (AuditoriumId, StartTime), CHECK LanguageFormat, OperationalStatus; Entity và DAL đủ cột | – |
| S6-19 | Đạt | ShowtimeSeats khóa (ShowtimeId, SeatId), FK Showtimes, Seats, Bills, Tickets, CHECK Status | – |
| S6-20 | Đạt | Holidays khóa HolidayDate DATE | – |
| S6-21 | Đạt | PricingRules UNIQUE(CinemaId, RuleType, RuleKey), Amount BIGINT cho phép âm | – |
| S6-22 | Đạt | AudienceDiscounts khóa AudienceType, DECIMAL(5,2), CHECK mức tối thiểu | – |
| S6-23 | Đạt | Bills đủ 27 cột, Code UNIQUE, index (Status, PaidAt), CHECK 5 cột trạng thái, tiền BIGINT | – |
| S6-24 | Đạt | BillDetails FK Bills, Drinks; tiền BIGINT, VatRate DECIMAL(5,2) | – |
| S6-25 | Đạt | Tickets đủ 14 cột, QrCode UNIQUE, CHECK AudienceType, CheckInStatus, SwappedFromTicketId tự tham chiếu | – |
| S6-26 | Đạt | Payments ProviderTxnId UNIQUE khi khác NULL, CHECK Method, Direction, Status | – |
| S6-27 | Đạt | Vouchers đủ cột, Code UNIQUE, CHECK Type, Source; gợi ý giới hạn Value của PERCENT | Đã thêm CK_Vouchers_PercentValue (PERCENT ≤ 100) và quyết định 23 |
| S6-28 | Đạt | WalletTransactions BIGINT, BalanceAfter không âm | – |
| S6-29 | Đạt | PointTransactions đủ cột | – |
| S6-30 | Đạt | CorporateBookings CHECK Type, FK BillId thêm bằng ALTER | – |
| S6-31 | Đạt | PosShifts tiền BIGINT, CHECK Status, index một ca mở | – |
| S6-32 | Đạt | SupervisorApprovals CHECK Type, Status, Method | – |
| S6-33 | Đạt | Notifications đủ cột | – |
| S6-34 | Đạt | AuditLogs đủ cột, UserId cho phép NULL | – |
| S6-35 | Thiếu | Schema có thêm Id khóa chính đúng quy ước, nhưng dòng spec vẫn ghi SchemaVersions(Version, AppliedAt), chưa sửa spec rồi sync | Sửa dòng S6-35 thành SchemaVersions(Id, Version UNIQUE, AppliedAt), chạy sync. Kiểm lại 07/11: Đạt |
| C1-01 | Đạt | Roles đủ 5 hằng, CK_Users_Role đúng và đủ | – |
| C3-01 | Đạt | AgeRating đủ 6, CHECK đúng | – |
| C3-02 | Đạt | ContentWarning đủ 7; cột danh sách, dữ liệu hợp lệ | – |
| C3-03 | Đạt | Genre đủ 11; cột danh sách, dữ liệu hợp lệ | – |
| C3-04 | Đạt | LanguageFormat đủ 3, CHECK Showtimes.LanguageFormat đúng | – |
| C3-05 | Đạt | AuditoriumFormat F2D, F3D, IMAX, F4DX, CHECK đúng | – |
| C3-06 | Đạt | SeatType đủ 4, CHECK đúng | – |
| C3-07 | Đạt | ShowtimeSeatStatus đủ 4, CHECK đúng, DAL truyền hằng qua tham số | – |
| C3-08 | Đạt | SeatDisplayStatus đủ 5, không có SELECTED | – |
| C3-09 | Đạt | OperationalStatus đủ 8, CHECK đúng | – |
| C3-10 | Đạt | BillStatus chữ thường, CHECK đúng | – |
| C3-11 | Đạt | CancelReason đủ 6, CHECK có IS NULL OR | – |
| C3-12 | Đạt | BillChannel đủ 3, CHECK đúng | – |
| C3-13 | Đạt | BillCheckInStatus đủ 5, CHECK đúng | – |
| C3-14 | Đạt | TicketCheckInStatus đủ 4, CHECK đúng | – |
| C3-15 | Đạt | FnbStatus đủ 6, CHECK đúng | – |
| C3-16 | Đạt | PaymentMethod đủ 3, CHECK đúng | – |
| C3-17 | Đạt | PaymentDirection đủ 2, CHECK đúng | – |
| C3-18 | Đạt | PaymentStatus đủ 3, CHECK đúng | – |
| C3-19 | Đạt | VoucherType đủ 3, CK_Vouchers_Type đúng | – |
| C3-20 | Đạt | VoucherSource đủ 3, CHECK đúng | – |
| C3-21 | Thiếu | LoyaltyTier và CK_Users_Tier đúng, nhưng khóa mặc định của CinemaRules.PointEarnPercent viết tay "MEMBER", "VIP", "VVIP" thay vì dùng hằng | CinemaRules.PointEarnPercent dùng khóa LoyaltyTier.*, có test. Kiểm lại 07/11: Đạt (gợi ý tách phép kiểm khóa thành test riêng) |
| C3-22 | Đạt | ShiftStatus đủ 2, CHECK đúng | – |
| C3-23 | Đạt | ApprovalType VOID_BILL, CHECK đúng | – |
| C3-24 | Đạt | ApprovalStatus đủ 4, CHECK đúng | – |
| C3-25 | Đạt | ApprovalMethod đủ 2, CHECK có IS NULL OR | – |
| C3-26 | Đạt | WastageStatus đủ 3, CHECK đúng | – |
| C3-27 | Đạt | InventoryTxnType đủ 6, CHECK đúng, DAL truyền hằng qua tham số | – |
| C3-28 | Đạt | CorporateType đủ 2, CHECK đúng | – |
| C3-29 | Đạt | SeatPersona đủ 4 | – |
| C3-30 | Đạt | PricingRuleType đủ 6, CHECK đúng | – |
| C3-31 | Đạt | BASE: DEFAULT; CHECK RuleKey là hợp các khóa hợp lệ (quyết định 10); gợi ý thêm test cặp RuleType–RuleKey sai | Đã thêm test PricingRuleKey_RejectsKeysOfAnotherRuleType |
| C3-32 | Đạt | DAY_TYPE đủ 4 | – |
| C3-33 | Đạt | TIME_SLOT đủ 4 | – |
| C3-34 | Đạt | SEAT_TYPE dùng SeatType.All | – |
| C3-35 | Đạt | FORMAT dùng AuditoriumFormat.All | – |
| C3-36 | Đạt | BLOCKBUSTER: YES | – |
| C7-01 | Đạt | ConnectionStrings:Default rỗng trong appsettings.json, giá trị thật ở Development hoặc biến môi trường | – |
| C7-08 | Đạt | Đủ 45 khóa CinemaRules đúng mặc định ở appsettings.json và CinemaRules.cs, đăng ký dùng chung | – |
| C8-04 | Đạt | INVALID_TRANSITION trả 409, lời nhắn tiếng Việt | – |
| C9-01 | Đạt | Bảng số nhiều PascalCase, khóa Id INT IDENTITY trừ 3 ngoại lệ; gợi ý ghi quyết định cho EmailOutbox và các cột *By | Đã ghi quyết định 25 ở PROGRESS.md |
| C9-02 | Đạt | Tiền BIGINT, phần trăm DECIMAL(5,2), tồn kho DECIMAL(18,3), Entity dùng long; gợi ý ghi quyết định Vouchers.Value và tạo lại DB | Đã ghi quyết định 23; DB tạo lại bằng test --reset-db |
| C9-03 | Đạt | DATETIME2(0) và DATE; không có hàm lấy giờ của SQL Server; thời gian qua tham số từ IClock | – |
| C9-04 | Đạt | Tham số @1..@n theo thứ tự, không ghép dữ liệu vào SQL | – |
| C9-05 | Đạt | 35 Entity, 35 DAL đủ 6 hàm, Domain/Rules thuần | – |
| C9-06 | Đạt | Định dạng HD, TK, TV, số gọi, POS-NN đúng; dữ liệu mẫu đúng định dạng | – |
| C10-01 | Đạt | Chữ ký sp_top_5_best_selling_drinks đúng | – |
| C10-02 | Đạt | Chữ ký sp_top_5_best_selling_movies đúng | – |
| C10-03 | Đạt | Chữ ký sp_revenue_by_day đúng | – |
| C10-04 | Đạt | Chữ ký sp_box_office_settlement, @CinemaId mặc định NULL | – |
| C11-01 | Đạt | Đủ 6 tài khoản, mật khẩu 123456 và PIN 246810 lưu dạng băm, đúng vai trò, rạp và ghi chú | – |

## Lượt kiểm lại (07/11/2026)

Agent spec-auditor kiểm lại 8 mã có kết luận Thiếu hoặc Sai ở lượt đầu, trên commit defe3cf và các sửa đổi sau đó.

| Mã | Kết luận | Lý do | Đã sửa |
|---|---|---|---|
| S2.2-04 | Đạt | Test kiến trúc cho Service dùng DBUtil.InTransaction, cấm mọi hàm chạy SQL của DBUtil; đủ 4 hướng gọi sai bị chặn | – |
| S7-06 | Đạt | 6 lỗi đủ các cột và vòng đời; commit defe3cf có thật | Tag lab-3 gắn ở bước commit cuối |
| S7-07 | Đạt | 9 cột, 6 kịch bản khớp dấu kết quả test (mã băm 41bc72904895); chạy lại hai lượt test song song đều 195/195 | – |
| S8.1-01 | Thiếu | Máy vẫn chỉ có SDK 10.0.301; người dùng chọn giữ SDK 10 nhưng mã chưa được người dùng bỏ qua scope.json | Người dùng cài .NET 8 SDK 8.0.427; thêm global.json ghim 8.0; chạy lại test --reset-db bằng SDK 8 |
| S8.1-04 | Đạt | Đã hỏi người dùng (ghi rõ là hỏi muộn, ngày 07/11) | – |
| S8.1-12 | Đạt | Tên phim không còn trùng tên phim có thật đã biết; poster sinh lại | – |
| S6-35 | Đạt | Spec, schema, Entity, DAL, docs/generated khớp | – |
| C3-21 | Đạt | Khóa PointEarnPercent dùng hằng LoyaltyTier, có test | – |

## Lượt kiểm lại lần 2 (07/11/2026)

| Mã | Kết luận | Lý do | Đã sửa |
|---|---|---|---|
| S8.1-01 | Đạt | dotnet --list-sdks có 8.0.427, dotnet --version trong thư mục dự án là 8.0.427 nhờ global.json; dấu kết quả test ghi sau khi có global.json; dotnet test 195/195 trên .NETCoreApp v8.0 | – |
| S8.1-04 | Đạt | PROGRESS.md ghi trung thực việc hỏi muộn; người dùng đã cài SDK 8; không còn thiếu .NET 8 hay SQL Server | – |

## Tổng hợp

- Lượt đầu: 173 mã, 165 Đạt, 5 Thiếu (S7-06, S7-07, S8.1-01, S6-35, C3-21), 3 Sai (S2.2-04, S8.1-04, S8.1-12).
- Lượt kiểm lại lần 1: 7/8 Đạt, còn S8.1-01 Thiếu (chờ người dùng); lần 2: S8.1-01, S8.1-04 Đạt.
- Sau khi sửa và kiểm lại: 173/173 Đạt.
- Gợi ý chưa làm (không ảnh hưởng kết luận): tách phép kiểm khóa PointEarnPercent thành test riêng (đang nằm trong PricingRuleKey_RejectsKeysOfAnotherRuleType); ghi ở PROGRESS.md mục "Lỗi biết trước và việc còn lại".

Kết quả: Đạt
