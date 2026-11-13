# Tiến độ
## Trạng thái
- [x] Phase 1  - [x] Phase 2  - [x] Phase 3  - [x] Phase 4
- [x] Phase 5  - [ ] Phase 6  - [ ] Phase 7  - [ ] Phase 8
## Phase đang làm
Phase 5 – Web khách và đặt vé online: xong ngày 14/11/2026 (verify 5 --final ĐẠT, commit "Phase 5: …"; Phase 5 không có tag lab). Phase kế tiếp: Phase 6 – Vận hành thời gian thực.

### Việc đã làm ở Phase 5
- Domain/Rules (logic thuần, có unit test, mỗi lớp một file): ClusterFinder (điểm ghế theo persona, cụm ghế liền nhau, bỏ cụm vi phạm ghế mồ côi và ghế đôi, tối đa 3 cụm), SelfCancelRules, OnlineAgeRules (câu xác nhận độ tuổi, người giám hộ phim K, nhắc giờ chiếu cho trẻ em), WebhookSignature (HMAC-SHA256), TierProgressCalculator.
- Services: OnlineBookingService (giỏ online, giữ ghế, bắp nước, voucher, điểm, xác nhận tuổi, thanh toán VietQR mô phỏng hoặc ví, webhook có chữ ký, chống lặp, webhook đến muộn giữ lại đúng ghế hoặc OVERBOOKED_REFUNDED kèm voucher bù, tự hủy hoàn vào ví), SeatSuggestService (gợi ý theo persona, gợi ý chuyển suất), WalletService; CinemaHub.JoinBill và sự kiện PAYMENT_STATUS_CHANGED.
- Màn hình: C1 trang chủ (hero, mua vé nhanh, tab phim, combo, ưu đãi), /movies, /showtimes, /menu; C2 chi tiết phim; C3 đặt vé 4 bước (persona, bản đồ điểm AI, Chọn nhanh, bắp nước và ưu đãi, thanh toán, màn hình QR trực tiếp); trang mô phỏng cổng thanh toán; C4 vé điện tử có tự hủy; C5 tài khoản 5 tab. Menu web khách mở khóa.
- Kiểm tra giao diện: 13 đường dẫn, 39 ảnh; sửa BUG-031, BUG-032. Test kiến trúc phát hiện BUG-033.
- Kiểm tra độc lập: lượt đầu 78/89 Đạt, 6 Thiếu, 5 Sai; đã sửa (BUG-034..040, quyết định 57–62); sau đó sửa thêm BUG-041, BUG-042 và tách file quy tắc (quyết định 63–65) và kiểm lại (docs/audit/phase-5.md).

Phase 4 – POS, hóa đơn, thống kê: xong ngày 13/11/2026 (verify 4 --final ĐẠT, commit "Phase 4: …", tag lab-6).

### Việc đã làm ở Phase 4
- Domain/Rules (logic thuần, có unit test): SeatHoldRules (trạng thái hiệu lực, hạn giữ, giờ ngừng bán theo kênh), SweetboxRule, OrphanSeatValidator (đoạn ghế, ngoại lệ đoạn ngắn, gợi ý dời), CashDropCalculator (đếm mệnh giá, Expected, Discrepancy, PIN), StockCalculator, LoyaltyCalculator (điểm, hạng, voucher, phân bổ giảm giá), SettlementCalculator.
- Services: SeatHoldService (giữ ghế trong transaction có UPDLOCK, HOLDLOCK; bỏ qua luật ghế mồ côi có lý do và AuditLogs), BillService (hóa đơn tạm tính, đối tượng vé, bắp nước theo định mức, thành viên, voucher, điểm, gia hạn giữ ghế một lần, thanh toán tiền mặt và VietQR mô phỏng, hủy waiting, hủy đã thanh toán bằng PIN quản lý), ShiftService (mở ca tại máy, đóng ca mù), InventoryService, LoyaltyService, ManagerPinService, StatisticsService, BillQueryService; SeatHoldSweeper (BackgroundService dọn ghế hết hạn); CinemaHub /hubs/cinema với SEAT_MAP_UPDATED.
- Màn hình: P1 POS 3 cột (sơ đồ ghế thời gian thực, bắp nước, hóa đơn tạm tính, phím tắt F1, F4, Esc), trang in vé 80mm có QR SVG, Hóa đơn của tôi, P7 mở ca, đóng ca mù, biên bản ca; A1 dashboard, A6 báo cáo 4 tab có CSV và đối soát nhà phát hành, A8 quản lý hóa đơn.
- Kiểm tra độc lập: lượt đầu 138/144 Đạt, 6 Thiếu (S3.6-08, S3.6-23, S3.6-27, S8.4-10, U8-11, U7.A8-03); đã sửa (BUG-027..030, quyết định 49–56) và kiểm lại (docs/audit/phase-4.md).

Phase 3 – Phim, phòng chiếu, giá, suất chiếu: xong ngày 13/11/2026 (verify 3 --final ĐẠT, commit "Phase 3: …"; Phase 3 không có tag lab). Phase kế tiếp: Phase 4 – POS, hóa đơn, thống kê.

### Việc đã làm ở Phase 3
- Domain/Rules (logic thuần, có unit test): AgeRules (tuổi tối thiểu, giấy phép và phim C, vé trẻ em AGE_RESTRICTED, giờ chiếu trẻ em CHILD_CURFEW, suất khuya, người giám hộ GUARDIAN_REQUIRED, cảnh báo xếp lịch phim K, T13 kết thúc muộn); ScheduleConstraintChecker (chồng giờ, giấy phép, KDM, khung giờ, ngôn ngữ, SHOWTIME_LOCKED; cảnh báo cùng phút, kết thúc muộn, KDM sắp hết hạn, tỷ lệ phim Việt), VietnameseFilmQuota, ShowtimeLock (LOCKED_HAS_TICKETS); AuditoriumLayout (nhãn, cặp ghế đôi, BaseViewScore, mức heatmap, gợi ý ghế nội bộ, phân biệt đổi cấu trúc với bật tắt bảo trì); DeleteRules thêm Movie, Distributor, Auditorium, Showtime.
- Màn hình: Phim (A2, A3 có poster 2:3 tải bằng fetch, chia doanh thu, KDM theo rạp, chi tiết), Nhà phát hành, Phòng chiếu và công cụ thiết kế A4, Bảng giá 6 chiều (ghi AuditLogs PRICING_UPDATED), Đối tượng ưu đãi (chặn dưới mức tối thiểu), Ngày lễ, Thử tính giá, Lịch chiếu /scheduler chế độ Bảng (A5) và form suất kiểm tra trực tiếp. Sidebar mở các mục Phase 3; style guide thêm tab, chọn ngày, nhóm ô tích, danh sách lỗi và cảnh báo, công cụ tô, heatmap.
- Lưu suất: kiểm tra lại trong transaction sau khi khóa (UPDLOCK, HOLDLOCK) các suất cùng phòng quanh khoảng giờ; tạo suất sinh ShowtimeSeats AVAILABLE hoặc HOUSE_HOLD. Sơ đồ ghế _SeatMap khóa ghế nội bộ (khách thấy nhưng không chọn được).
- Test: 35 test mới (26 quy tắc thuần, 6 dịch vụ trên CSDL mẫu gồm kiểm mọi suất của DbSeeder qua ScheduleConstraintChecker, 3 HTTP); tổng 268/268 đạt.


### Việc đã làm ở Phase 2
- Hệ thống thiết kế: tokens.css (đúng 109 biến của UI mục 2 và vài bí danh), app.css (ghi đè Bootstrap về token, mọi thành phần mục 5), 4 layout (_LayoutCustomer, _LayoutAdmin, _LayoutPos, _LayoutDisplay), 9 partial bắt buộc và partial phụ, ui.js, StatusDisplay xuất JSON trong layout, trang /dev/style-guide hai theme.
- Thư viện tự host trong wwwroot/lib (Bootstrap 5.3.3, Bootstrap Icons 1.11.3, Chart.js 4.4.9, html5-qrcode 2.3.8, Be Vietnam Pro, jQuery validate), nguồn ghi ở libman.json; QRCoder 1.6.0 cho khối QR.
- Program.cs: cookie 24 giờ, 6 policy, Anti-Forgery toàn cục (header RequestVerificationToken), xử lý lỗi tập trung (trang lỗi thân thiện, JSON CONTRACTS mục 8), UseForwardedHeaders đầu pipeline, 404 cho demo/ dev/ khi tắt công cụ, bắt đổi mật khẩu khi MustChangePassword = 1, đăng xuất phiên khác khi đổi mật khẩu (PasswordStamp).
- LoginController (đăng nhập, đăng xuất, đăng ký, quên mật khẩu OTP 2 bước, đổi mật khẩu, hồ sơ, /me), IEmailSender (SMTP hoặc hộp thư thử /dev/mailbox).
- CRUD: Loại đồ uống, Đồ uống (ảnh 1:1 tải bằng fetch qua FileController, định mức theo từng rạp, lọc theo loại, tìm theo tên), Nhân viên (cấp mật khẩu qua email, khóa hoặc mở ghi AuditLogs, quy tắc xóa SPEC 3.14).
- Thanh đổi vai trò và /demo/switch-role; script tools/ui-snapshots (snap.mjs, routes.json, Playwright).
- Test: 38 test mới (quy tắc thuần, dịch vụ trên CSDL thử, HTTP qua WebApplicationFactory); tổng 233/233 đạt.

### Kiểm tra môi trường (06/11/2026, cập nhật 07/11/2026)
- 06/11: `dotnet --list-sdks` chỉ có 10.0.301 (thiếu SDK .NET 8 theo S8.1-01); `dotnet --list-runtimes` có Microsoft.NETCore.App 8.0.28, Microsoft.AspNetCore.App 8.0.28 (và 10.0.9). Ngày 06/11 đã tự chốt dùng SDK 10 và làm tiếp, không dừng hỏi ngay như S8.1-04 yêu cầu.
- 07/11: sau khi agent kiểm tra độc lập chỉ ra, đã hỏi người dùng theo S8.1-04; người dùng cài .NET 8 SDK. Hiện `dotnet --list-sdks` có 8.0.427 và 10.0.301. global.json ghim SDK 8.0 (8.0.100, rollForward latestFeature) nên `dotnet --version` trong thư mục dự án là 8.0.427; build, test và tạo lại DB đã chạy lại bằng SDK này (quyết định 1).
- SQL Server: dịch vụ MSSQLSERVER đang Running (`Get-Service MSSQL*`), Microsoft SQL Server 2022 (16.0.1000.6) Developer Edition, instance mặc định `localhost`, đăng nhập Windows. Đã ghi vào appsettings.Development.json.
- Git 2.54.0.windows.1, Node.js v24.21.0.

### Kết quả kiểm tra Phase 1 (build, test, CSDL, stored procedure) – lần chạy cuối sáng 07/11/2026 bằng SDK 8.0.427
- `node tools/spec-check/check.mjs test --reset-db` (mã băm code 41bc72904895): build ĐẠT, test ĐẠT, resetDb ĐẠT.
- `dotnet build`: đạt, 0 cảnh báo, 0 lỗi.
- `dotnet test`: 195/195 đạt (unit test cho Domain/Rules, FileUtil, SvgArtGenerator, cấu hình, kiến trúc; test tích hợp trên CSDL thử tên ngẫu nhiên tự tạo và tự xóa). Hai lượt chạy cùng lúc đều đạt.
- `dotnet run --project src/FPolyCinema.Web -- --reset-db --init-only`: thoát mã 0, nạp dữ liệu mẫu khoảng 15 giây.
  Dữ liệu: 2 rạp, 6 phòng, 784 ghế, 159 tài khoản (152 khách: 90 MEMBER, 62 VIP; khach@ VIP, 262 điểm, ví 359.000đ), 10 phim, 1.339 suất chiếu (30 ngày qua đến 6 ngày tới, phim Việt 50,6%),
  175.056 dòng ShowtimeSeats, 4.803 hóa đơn (4.774 finish, 29 cancel), 9.728 vé, 2.533 dòng bắp nước, 4.780 giao dịch thanh toán, 13.933 giao dịch kho, 49 suất sắp chiếu đã có vé.
- Gọi thử 4 stored procedure (khoảng 09/10–07/11/2026):
  - sp_top_5_best_selling_drinks: Combo bắp phô mai 352 phần (33.383.480đ), Combo Gia đình 346, Combo Couple 331, Combo Solo 322, Nước suối 128 – sắp theo số lượng giảm dần.
  - sp_top_5_best_selling_movies: Giao Lộ Tốc Độ 2.819 vé (376.186.805đ), Mùa Hè Trên Đồi Gió 2.638 vé (284.183.826đ), Chú Mèo Phi Hành Gia, Đội Đặc Nhiệm Sông Hồng, Căn Gác Số 9 Phố Cũ – sắp theo doanh thu giảm dần.
  - sp_revenue_by_day 04/11–09/11: 04–06/11 có 137–148 hóa đơn và 37–40 triệu đồng mỗi ngày; 07/11 (đến lúc nạp, 07:26) 15 hóa đơn; 08/11 và 09/11 ghi 0.
  - sp_box_office_settlement: phim 6 tuần 3 toàn chuỗi: 749 vé, gộp 101.048.343đ, thuần 93.563.281đ, 5 vé dưới giá sàn, rạp tự chịu 249.074đ, trả nhà phát hành 46.906.177đ (50%), rạp giữ 46.657.104đ.
    Phim 1 tuần 5 rạp 1 (vượt quy tắc 3 tuần): dùng tỷ lệ tuần 3 là 40%, 186 vé, trả nhà phát hành 7.379.259đ.
- Bộ truy vấn kiểm dữ liệu mẫu (22 điều kiện, TC-P1-05 trong docs/TEST-SCENARIOS.md): đều đạt.

## Quyết định đã chốt (ngày – nội dung – lý do)
1. 07/11/2026 – Dùng .NET 8 SDK (8.0.427) do người dùng cài, global.json ghim SDK 8.0 với rollForward latestFeature (máy khác có bất kỳ SDK 8.0.x nào cũng build được) – máy ban đầu chỉ có SDK 10; theo S8.1-04 đã hỏi người dùng, lúc đầu người dùng chọn giữ SDK 10 nhưng S8.1-01 vẫn chưa đạt, nên người dùng cài SDK 8.
2. 06/11/2026 – Chuỗi kết nối `Server=localhost;Database=FPolyCinema;Trusted_Connection=True;TrustServerCertificate=True` – máy có instance mặc định MSSQLSERVER, đăng nhập Windows.
3. 06/11/2026 – Tác nhân "hệ thống" khi đổi trạng thái hóa đơn là hằng `Actors.System = "System"` (đã ghi vào CONTRACTS mục 5) – bảng chuyển trạng thái có cột "Hệ thống" nhưng CONTRACTS chưa có tên.
4. 06/11/2026 – `BillStateMachine.CanTransition(from, to, reason, actorRole, out errorCode, currentReason = null)`: thêm tham số tùy chọn currentReason sau out errorCode – hai dòng "cancel (EXPIRED) → …" cần biết lý do hủy hiện tại; chữ ký 5 tham số của CONTRACTS giữ nguyên.
5. 06/11/2026 – Mọi thay đổi Bills.Status đi qua `BillStatusService.ChangeStatus` (gọi BillStateMachine rồi `BillDAL.UpdateStatus` có điều kiện trạng thái cũ); `BillDAL.Insert` chỉ nhận hóa đơn waiting; `BillDAL.Update` không đổi Status, CancelReason, PaidAt, CancelledAt – để không có đường nào đổi trạng thái mà không qua máy trạng thái. Điều kiện nghiệp vụ của từng dòng (đã thu đủ tiền, chưa vào cửa…) do service của từng phase kiểm.
6. 06/11/2026 – `BusinessRuleException(code, message, details)` tạo ngay ở Phase 1 (dùng cho INVALID_TRANSITION, 409); xử lý lỗi tập trung và JSON lỗi làm ở Phase 2 – máy trạng thái cần ném lỗi có mã.
7. 06/11/2026 – DbInitializer: mỗi file Database/*.sql chạy trong một transaction; file đã ghi trong SchemaVersions thì bỏ qua, riêng file chỉ gồm CREATE OR ALTER (02_procedures.sql) chạy lại mỗi lần khởi động – để sửa stored procedure không cần tạo lại DB.
8. 06/11/2026 – StartTime làm tròn đến bội số ScheduleRoundMinutes gần nhất (đúng nửa thì lên); CleaningEndTime làm tròn lên – spec chỉ nói "làm tròn theo bội số" cho StartTime.
9. 06/11/2026 – Ngày vận hành: suất bắt đầu trước 06:00 thuộc ngày hôm trước (`PricingCalculator.NightCutoff`), dùng cho DAY_TYPE và tuần đối soát (SQL: `CAST(DATEADD(HOUR, -6, StartTime) AS DATE)`) – theo ghi chú của OperatingDayStart ở CONTRACTS mục 7.
10. 06/11/2026 – CHECK của PricingRules.RuleKey liệt kê hợp mọi RuleKey hợp lệ; việc ghép đúng RuleKey với RuleType kiểm ở server bằng `PricingRuleKey.IsValid` – CHECK bảng nhắc cả hai cột sẽ làm CHECK của RuleType có giá trị lạ.
11. 06/11/2026 – Trong 01_schema.sql, các cột CheckInStatus, CheckedInAt, CheckedInBy viết trong ngoặc vuông – script kiểm tự động hiểu nhầm dòng bắt đầu bằng "Check" là ràng buộc CHECK; SQL Server hiểu hai cách viết như nhau.
12. 06/11/2026 – FileUtil.Upload trả đường dẫn web `/uploads/{timestamp}_{shortGuid}{ext}` để lưu DB; timestamp lấy từ `FileUtil.Clock` (Program.cs gán IClock); FileUtil.Delete chỉ xóa file nằm trực tiếp trong uploads, không xóa ảnh mẫu uploads/seed – ảnh mẫu được commit vào git.
13. 06/11/2026 – Dữ liệu mẫu: thêm nhân viên rạp 2 (shift2@, cashier2@, gate2@) và 150 khách phụ khach003@ … khach152@ (mật khẩu 123456) – rạp 2 cần người bán; lịch sử bán online cần đủ tài khoản để hạng thành viên hợp lý. Mật khẩu và PIN mẫu băm một lần bằng PasswordHasher rồi dùng chung cho các tài khoản mẫu để nạp nhanh.
14. 06/11/2026 – Ca hôm nay mở cho cả cashier@ (rạp 1, POS-01) và cashier2@ (rạp 2, POS-01); các ca trước đã đóng, một ca thiếu 20.000đ để có chênh lệch – hai rạp đều bán trong ngày.
15. 06/11/2026 – Hóa đơn online: Bills.UserId là khách tạo hóa đơn (cột "người tạo, như slide"), CustomerId cũng là khách. Hóa đơn chưa thanh toán không có vé (vé tạo lúc thanh toán).
16. 06/11/2026 – Số gọi bắp nước lưu INT 1–999, hiển thị 3 chữ số (`CodeGenerator.FormatPickupNumber`); mã thành viên TV + số thứ tự 8 chữ số.
17. 06/11/2026 – Ngày lễ 2026: 01/01; Tết Nguyên đán 16–20/02 (mùng 1 là 17/02); Giỗ Tổ 26/04 và nghỉ bù 27/04; 30/04; 01/05; Quốc khánh 01–02/09.
18. 06/11/2026 – Mỗi phim có 3 quy tắc chia doanh thu (tuần 1–3); KDM mỗi phim tại mỗi rạp có hạn từ ngày trước ngày khởi chiếu đến 46 ngày sau; riêng phim "Lời Hứa Dưới Mưa Sao Băng" tại rạp 1 hết hạn sau 20 giờ kể từ lúc nạp, và không có suất nào sau thời điểm đó.
19. 06/11/2026 – Kết quả 4 stored procedure trả về dạng lớp TopDrinkStat, TopMovieStat, RevenueByDayStat, BoxOfficeSettlement đặt trong DAL/StatisticDAL.cs (không phải Entity vì không ứng với bảng).
20. 06/11/2026 – Test tích hợp tạo CSDL thử riêng (tên DB + "_Test_" hoặc "_SeedTest_" + 8 ký tự ngẫu nhiên) rồi xóa sau khi chạy, không đụng DB phát triển – để `dotnet test` chạy được trước bước `--reset-db` và nhiều lượt chạy cùng lúc không đụng nhau (BUG-006).
21. 06/11/2026 – Đối soát: "phần rạp tự chịu do giá sàn" = Σ(FloorNet − Net) trên các vé có Net < FloorNet; tiền trả nhà phát hành = làm tròn(ΣBase × tỷ lệ / 100); tiền rạp giữ lại = doanh thu thuần − tiền trả nhà phát hành – spec liệt kê các đại lượng nhưng chưa cho công thức của phần rạp tự chịu.
22. 06/11/2026 – `DBUtil.InParams` với danh sách rỗng trả "NULL" để `IN (NULL)` không khớp dòng nào thay vì sinh câu SQL sai cú pháp `IN ()`; Service được gọi `DBUtil.InTransaction` để gom nhiều bước, còn câu SQL luôn nằm trong DAL.
23. 07/11/2026 – Vouchers.Value dùng chung: AMOUNT lưu số đồng, PERCENT lưu số phần trăm nguyên (CHECK không quá 100), FREE_TICKET lưu 0 – một cột Value như SPEC mục 6.
24. 07/11/2026 – Rà lại tên phim mẫu để không trùng phim thật: đổi "Bóng Ma Phố Cổ" thành "Căn Gác Số 9 Phố Cũ", "Vương Quốc Kẹo Ngọt" thành "Cô Bé Mây Và Xứ Sở Kẹo Bông", "Kẻ Săn Bóng Đêm" thành "Thanh Tra Đêm Mất Điện", "Vùng Cấm" thành "Phòng Thí Nghiệm Số 19", và các tên gốc tiếng Anh thành Orbit Paws, Candy Cloud Kingdom, Northbound Ice Chronicle, Blackout Inspector, Lab Nineteen – agent kiểm tra độc lập phát hiện "Forbidden Zone" và "Night Stalker" là tên phim có thật.
25. 07/11/2026 – Quy ước đặt tên CONTRACTS mục 9 có hai chỗ giữ theo SPEC mục 6: bảng EmailOutbox (tên spec đặt, không số nhiều) và các cột người thực hiện CreatedBy, DecidedBy, RequestedBy, CheckedInBy (khóa ngoại tới Users nhưng không có đuôi Id) – SPEC mục 6 là nguồn tên bảng và cột.

26. 08/11/2026 – Trang chính sau đăng nhập ở Phase 2: Manager, ShiftManager → /admin/drinks; POSCashier, TicketInspector → /me; khách → / (RoleRouting.HomePath, đổi khi có /admin ở Phase 4, /pos/index ở Phase 4, /pos/gate-inspector ở Phase 6) – các trang chính theo vai trò chưa tồn tại ở Phase 2.
27. 08/11/2026 – Menu web khách (Phim, Lịch chiếu, Bắp nước, Ưu đãi), các mục sidebar chưa có màn hình và nút Đóng ca hiện ở trạng thái khóa có lý do trong tooltip, ghi TODO(Phase k) – UI mục 4 yêu cầu có các mục này từ Phase 2 nhưng màn hình thuộc phase sau; khóa có lý do theo UI U6-02 thay vì nút chết.
28. 08/11/2026 – Route quản trị đặt chữ thường gạch ngang: /admin/categories, /admin/drinks, /admin/users, …/create, …/{id}/edit, …/{id}/delete-check, …/{id}/delete, …/{id}/set-active – UI mục 7 không ghi đường dẫn A2, A3; theo kiểu route ở CONTRACTS C9-07.
29. 08/11/2026 – Mật khẩu người dùng tự đặt: ít nhất 8 ký tự, có cả chữ và số, khác mật khẩu hiện tại (PasswordRules) – spec không ghi; tài khoản mẫu vẫn dùng 123456 vì do DbSeeder tạo.
30. 08/11/2026 – Mã OTP: gửi lại chỉ sau 60 giây (kiểm cả ở server); mã mới vô hiệu mã cũ; sai đủ 5 lần thì mã bị vô hiệu (OTP_INVALID kèm câu "đã hết lượt thử", lần sau OTP_EXPIRED); email không có tài khoản trả cùng lời nhắn nhưng không gửi thư.
31. 08/11/2026 – Thêm nhân viên: hệ thống cấp mật khẩu ngẫu nhiên Auth:NewPasswordLength ký tự, gửi qua email, MustChangePassword = 1; nhân viên không phải Manager phải gắn rạp; không ai tự đổi vai trò của mình. Ngoài Bills, nhân viên có dữ liệu vận hành khác (ca, phê duyệt, nhật ký, kho…) cũng không xóa cứng được vì khóa ngoại.
32. 08/11/2026 – Định mức món nhập theo từng rạp (RawMaterials theo rạp) với nút chép định mức sang rạp khác theo tên nguyên liệu; lưu món thay toàn bộ định mức trong một transaction.
33. 08/11/2026 – Ảnh món: tải lên ngay khi chọn (FileController.Upload); nút Xóa ảnh gọi FileController.Delete cho ảnh vừa tải trong lần sửa; ảnh đã lưu chỉ xóa khi bấm Lưu (DrinkService xóa ảnh cũ không còn món, phim nào dùng); FileController.Delete từ chối ảnh đang được dùng (DELETE_BLOCKED). Uploads:Path phải là thư mục tên "uploads".
34. 08/11/2026 – Ô chọn rạp lưu trong cookie fpc_cinema; nhân viên gắn rạp chỉ xem rạp của mình; quản lý toàn chuỗi chọn được "Toàn chuỗi".
35. 08/11/2026 – Cỡ chữ 13, 28, 32, 56px (UI mục 5 ghi đích danh) là token bí danh --fs-13, --fs-28, --fs-32, --fs-56 trong tokens.css; bí danh màu --white, --ink, --scrim lấy đúng giá trị có sẵn.
36. 08/11/2026 – Ảnh chụp của màn hình có nhiều trang thêm biến thể sau vai trò, ví dụ A2-manager-loai-1440x900.png – mẫu tên UI U13-04 chỉ cho một trang mỗi mã màn hình.
37. 08/11/2026 – Bản ghi đọc (CategoryRow, DrinkRow, RecipeLine, PendingApprovalRow) đặt trong namespace Entities.ReadModels – không ứng với bảng nên không tính là Entity (test Phase 1 đếm đúng 35 entity).
38. 08/11/2026 – HTML và JSON giữ nguyên chữ tiếng Việt (WebEncoderOptions, JsonOptions cho toàn bộ Unicode).
39. 08/11/2026 – Model binding dùng InvariantCulture (ô number gửi dấu chấm); hiển thị luôn qua Fmt với vi-VN.

40. 13/11/2026 – Giờ bắt đầu ngoài khung OperatingDayStart–LastShowStartLatest và bản ngôn ngữ không thuộc Movies.LanguageFormats dùng mã VALIDATION_FAILED gắn với ô StartTime, LanguageFormat – SPEC 3.3 ghi là lỗi chặn nhưng CONTRACTS mục 8 không có mã riêng.
41. 13/11/2026 – Cảnh báo không chặn của ScheduleConstraintChecker có tên nội bộ ScheduleWarningCode (SAME_MINUTE_STARTS, LATE_END_FOR_AUDIENCE, KDM_EXPIRING_SOON, VIETNAMESE_SHARE_LOW), không gửi mã cho giao diện, chỉ gửi mức độ và câu tiếng Việt – không phải mã lỗi nên không thêm vào CONTRACTS.
42. 13/11/2026 – "KDM hết hạn trong 24 giờ tới mà vẫn còn suất sau thời điểm đó": lấy hạn cuối cùng của mọi KDM của phim tại rạp; cảnh báo khi hạn này nằm trong (bây giờ, bây giờ + 24 giờ] và có suất chưa hủy kết thúc sau hạn.
43. 13/11/2026 – Tỷ lệ phim Việt tính theo từng rạp, mọi suất không bị hủy có ngày vận hành trong năm (kể cả suất đã xếp cho tương lai); ở lịch chiếu hiện thêm tỷ lệ trong ngày và trong khung 18:00–22:00 để tham khảo. Cảnh báo chỉ áp dụng từ 01/01/2026.
44. 13/11/2026 – Không xếp suất mới hoặc dời suất vào thời điểm đã qua; suất đã bắt đầu hoặc đã hủy không sửa được; xóa suất chỉ khi chưa bán vé, không có ghế đang giữ, chưa bắt đầu và không gắn hợp đồng doanh nghiệp (suất đã bán dùng Hủy suất do sự cố ở Phase 6). Sửa suất đã bán vé vẫn đổi được bản ngôn ngữ.
45. 13/11/2026 – Phòng khóa cấu trúc (có suất tương lai đã bán hoặc đang giữ ghế) trả VALIDATION_FAILED kèm lý do; ghế từng có vé (kể cả vé cũ) không xóa khỏi sơ đồ được, gợi ý đặt bảo trì. Đổi cấu trúc khi phòng chưa khóa thì thêm, bớt ShowtimeSeats của các suất tương lai và đặt lại HOUSE_HOLD theo ghế nội bộ. Ghế đôi ghép cặp theo các ô liền nhau trong hàng từ trái sang; PairGroupId mới = PairGroupId lớn nhất hiện có + số thứ tự cặp.
46. 13/11/2026 – Phòng mới tạo với lưới đầy ghế thường; số hàng, số cột đổi trong công cụ thiết kế. Nhãn, cặp ghế đôi, điểm và gợi ý ghế nội bộ của công cụ do server tính (POST /admin/auditoriums/{id}/preview) để công cụ và lúc lưu dùng chung AuditoriumLayout. Gợi ý ghế nội bộ: hàng gần r_ideal nhất có đủ ghế thường hoặc VIP liền nhau không bảo trì, đoạn gần giữa phòng nhất.
47. 13/11/2026 – Route Phase 3 theo kiểu chữ thường gạch ngang: /admin/movies, /admin/distributors, /admin/auditoriums (…/{id}/design, …/{id}/preview, …/{id}/layout), /admin/pricing (…/audiences, …/holidays, …/try), /scheduler (…/create, …/{id}/edit, …/check, …/save). Phim xem được với EmployeeOrManager; nhà phát hành, phòng chiếu, bảng giá, lịch chiếu là ManagerOnly (cấu hình, xếp lịch theo CONTRACTS mục 2).
48. 13/11/2026 – Mức giảm đối tượng dùng chung cho mọi rạp (bảng AudienceDiscounts không có CinemaId); trang chỉ sửa phần trăm giảm, cột bán online và cần giấy tờ giữ theo luật (CONTRACTS mục 4).
49. 13/11/2026 – Máy POS nhận biết bằng cookie fpc_terminal đặt lúc mở ca; mọi thao tác bán chỉ chạy khi cookie đúng mã máy của ca OPEN (trình duyệt chưa có cookie hoặc mang mã máy khác bị chuyển về Mở ca, SHIFT_NOT_OPEN). Đóng ca tìm ca OPEN của chính người dùng ở bất kỳ máy nào (đóng ca không phải thao tác bán). Thanh đổi vai trò demo đặt cookie mã máy của ca đang mở để bán được ngay – SPEC 3.6 "có ca OPEN tại máy đó".
50. 13/11/2026 – Tiền đầu ca bắt buộc nhập (két trống thì nhập 0), từ 0 đến 50.000.000 ₫ – SPEC 3.6 "khi mở ca phải nhập OpeningCashFloat".
51. 13/11/2026 – Hủy hóa đơn đã thanh toán có khoản tiền mặt cần người hủy có ca OPEN tại rạp của hóa đơn, tiền hoàn ghi vào ca đó (SHIFT_NOT_OPEN nếu không có ca); chuyển khoản và ví không cần ca – SPEC 3.6 "tiền mặt trừ vào két của ca hiện tại".
52. 13/11/2026 – Quản lý gắn một rạp chỉ xem, in, hủy hóa đơn và đối soát của rạp mình; quản lý toàn chuỗi (CinemaId trống) chọn rạp ở ô rạp. Tab doanh thu, top đồ uống, top phim theo sp_RevenueByDay, sp_TopDrinks, sp_TopMovies của SPEC 3.12 (tham số khoảng ngày, không có rạp) nên là số toàn chuỗi, tiêu đề ghi "toàn chuỗi".
53. 13/11/2026 – Mốc nhanh của báo cáo (Hôm nay, 7 ngày, 30 ngày, Tháng này, Tháng trước) gửi tham số preset, server tính khoảng ngày theo IClock (giờ Việt Nam); sửa tay ô ngày thì bỏ mốc nhanh.
54. 13/11/2026 – Bộ lọc trạng thái ở /admin/bills dùng nhãn của UI mục 6 ("Đã thanh toán" cho finish) thay cho chữ "Hoàn thành" ở UI A8 – một trạng thái chỉ có một nhãn trên toàn hệ thống.
55. 13/11/2026 – Drinks không có cột kích cỡ (SPEC 6); mỗi cỡ là một món riêng có cỡ trong tên (ví dụ "Bắp ngọt L"). Cột "Kích cỡ" ở chi tiết hóa đơn hiện "Combo" hoặc "Tiêu chuẩn".
56. 13/11/2026 – Chip giờ suất ở POS tô "sắp hết" khi còn từ 10 ghế trống trở xuống – UI P1 không nêu ngưỡng.
57. 14/11/2026 – Lớp dựng ViewModel cho trang khách (trang chủ, phim, lịch chiếu, thực đơn) đặt ở Controllers/CustomerCatalogPages.cs thuộc lớp giao diện, không đặt trong Services, vì Services không được phụ thuộc ViewModels (SPEC 2.2, test Layers_ServicesCallDalNotDBUtil).
58. 14/11/2026 – Bộ lọc rạp của /showtimes và /movies/{id}: cinemaId=0 là "Tất cả rạp"; không gửi cinemaId thì dùng rạp đang chọn ở đầu trang.
59. 14/11/2026 – Dòng phụ của hero ghi định dạng phòng (2D, 3D, IMAX, 4DX) của các suất còn bán trong 7 ngày tới ở rạp đang chọn; phim không có TrailerUrl thì nút phụ là "Xem chi tiết" thay cho "Xem trailer".
60. 14/11/2026 – Khách đăng nhập hoặc đăng ký xong quay lại returnUrl nếu RoleRouting.CanReturnTo cho phép (mọi trang web khách); returnUrl vào /admin, /pos, /scheduler, /me thì về trang chủ.
61. 14/11/2026 – Poster mẫu (SvgArtGenerator) đặt chữ "FPoly Cinema" ở đáy, giữ nhãn tuổi góc trên phải theo U7.ART-01; góc trên trái để trống cho nhãn tuổi của thẻ phim (U5-13).
62. 14/11/2026 – tools/ui-snapshots: route có đường dẫn sinh ngẫu nhiên hoặc phụ thuộc ngày (vé, suất chiếu) khai báo resolveFrom và resolveLink (một selector hoặc mảng selector); snap.mjs mở trang bắt đầu rồi theo liên kết đầu tiên khớp từng selector.
63. 14/11/2026 – Form Thông tin cá nhân của khách gửi POST /account/profile (AccountController); dữ liệu sai thì hiện lại tab profile của /account kèm lỗi từng ô. /Login/Profile giữ cho nhân viên (trang /me).
64. 14/11/2026 – Mỗi lớp quy tắc thuần một file: ClusterFinder, SelfCancelRules, OnlineAgeRules, WebhookSignature, TierProgressCalculator (trước đây chung ClusterFinder.cs).
65. 14/11/2026 – Hộp xác nhận có nút chính là một hành động hủy thì nút đóng không ghi "Hủy" (dễ nhầm): hủy vé dùng "Giữ vé", hủy hóa đơn dùng "Không hủy".

## Thay đổi mô hình (ngày – bảng/cột/use case/màn hình – trước – sau)
- 13/11/2026 – Bills thêm cột HoldExtended BIT NOT NULL DEFAULT 0 (thu ngân đã gia hạn giữ ghế một lần, SPEC 3.5); ShowtimeSeats thêm HoldAudienceType VARCHAR(20) NULL (đối tượng vé đã chọn cho ghế đang giữ, vé thật tạo khi thanh toán). Đã sửa S6-19, S6-23 trong docs/spec/s05-07-realtime-csdl-kiem-thu.md, chạy sync. CONTRACTS C3-39 thêm AuditAction VOID_BILL, ORPHAN_SEAT_OVERRIDE; C8-07 mã lỗi Phase 4 khai báo trong ErrorCodes.
- 13/11/2026 – CONTRACTS C3-39 – thêm AuditAction PRICING_UPDATED (sửa bảng giá và mức giảm đối tượng, SPEC 2.7) theo quy tắc "mỗi phase thêm hành động của mình". Không đổi bảng, cột, use case hay màn hình.
- 11/11/2026 – CONTRACTS – thêm C3-39 AuditAction (USER_LOCKED, USER_UNLOCKED) và C8-09 mã lỗi chung (AUTH_REQUIRED, ACCESS_DENIED, NOT_FOUND, SERVER_ERROR) theo quy tắc "cần tên mới thì thêm vào CONTRACTS trước". Không đổi bảng, cột, use case hay màn hình.
- 06/11/2026 – SchemaVersions – (Version, AppliedAt) – (Id INT IDENTITY khóa chính, Version UNIQUE, AppliedAt), theo quy ước khóa chính Id của CONTRACTS mục 9. Đã sửa dòng S6-35 trong docs/spec/s05-07-realtime-csdl-kiem-thu.md, chạy sync, và ghi ở CONTRACTS mục 12.
- 06/11/2026 – Ràng buộc thêm (không đổi cột): UNIQUE(CinemaId, Name) của RawMaterials; UNIQUE(AuditoriumId, Label) của Seats; index lọc UX_PosShifts_OpenTerminal, UX_PosShifts_OpenUser (một ca mở cho mỗi máy và mỗi thu ngân), UX_Tickets_ActiveSeat (một vé còn hiệu lực cho mỗi ghế của suất), UX_Payments_ProviderTxnId, UX_Users_MemberCode; CHECK CK_Vouchers_PercentValue (voucher PERCENT không quá 100).

## Lỗi biết trước và việc còn lại
- Phase 5: ảnh chụp lúc rạng sáng (ngày vận hành vẫn là hôm trước) có trang lịch chiếu và chi tiết phim "Hôm nay" rỗng vì mọi suất đã qua – đúng nghiệp vụ; routes.json chụp C3 bằng ngày thứ hai của dải ngày.
- Phase 5: lời nhắc giờ chiếu cho trẻ em ở chip suất là tooltip trên icon mặt trăng (đã có trong aria-label của chip); trên điện thoại câu nhắc đầy đủ hiện ở trang đặt vé.
- Phase 5: nút "Tôi đã tới rạp", trạng thái vào cửa trực tiếp trên vé (UI C4-03, C4-05) làm ở Phase 6; nút trợ lý AI (C1-06) ở Phase 7.
- Phase 2: DeveloperExceptionPage của .NET vẫn tự bật trong Development nhưng chỉ hiện khi chính trang lỗi /Home/Error hỏng; mọi lỗi thường đi qua UseExceptionHandler, không hiện stack trace.
- Phase 2: _SeatMap mới hiển thị trạng thái; chọn ghế, bàn phím, tooltip giá, thời gian thực làm ở Phase 4. Nút Đặt vé trên thẻ phim khóa đến Phase 5.
- Phase 2: chỉ báo kết nối ở POS phản ánh mạng của trình duyệt cho tới khi có realtime.js (Phase 4).
- Phase 1 không có màn hình; thư mục Controllers, Views, ViewModels, Hubs, Background, wwwroot/css, wwwroot/js đã tạo (chứa .gitkeep), nội dung làm từ Phase 2.
- OperationalStatus của suất trong dữ liệu mẫu chỉ là COMPLETED (đã dọn xong) hoặc SCHEDULED; ShowtimeClockService cập nhật theo giờ ở Phase 6.
- QR vé dạng SVG (QRCoder) làm ở Phase 4.
- Gợi ý của kiểm tra độc lập chưa làm: tách phép kiểm khóa CinemaRules.PointEarnPercent (đang nằm trong test PricingRuleKey_RejectsKeysOfAnotherRuleType) thành test riêng ở lần sửa test tới.
- Kiểm tra độc lập: lượt đầu 165/173 Đạt; sau khi sửa và kiểm lại 173/173 Đạt (docs/audit/phase-1.md).

## Đang bị chặn (câu hỏi cần người dùng trả lời)
Không có. (07/11/2026: câu hỏi về SDK .NET 8 của S8.1-01 đã xong, người dùng cài SDK 8.0.427.)
## Kết quả kiểm tra giao diện (phase – màn hình – kích thước – đạt/chưa)
- Phase 5 – 14/11/2026, `node tools/ui-snapshots/snap.mjs --phase 5`: 13 đường dẫn, 39 ảnh trong artifacts/ui/phase-5, 0 lỗi HTTP 5xx, 0 cuộn ngang, 0 ảnh vỡ, 0 lỗi JS. Mở từng ảnh đối chiếu UI mục 13 và sửa: C1 (nhãn tuổi che chữ trên poster – BUG-031; hero – BUG-037), C5 (tab xuống 3 dòng ở 390px – BUG-032), C3 (nhãn tuổi trên điện thoại – BUG-036), C4 (thông tin suất trên cuống vé – BUG-038). Màn hình QR và trang mô phỏng thanh toán kiểm bằng tay trên trình duyệt (TC-P5-01). Kết quả cuối: C1–C5 đạt ở 390, 768, 1440.
- Phase 4 – 13/11/2026, `node tools/ui-snapshots/snap.mjs --phase 4`: 16 đường dẫn, 42 ảnh trong artifacts/ui/phase-4, 0 lỗi HTTP 5xx, 0 cuộn ngang, 0 ảnh vỡ, 0 lỗi JS. Mở từng ảnh đối chiếu UI mục 13 và sửa: P1 (chữ nút thanh toán và tên món bị cắt ở 1366 – BUG-022), A1, A6 (biểu đồ không vẽ – BUG-023; KPI tràn chữ, lề bảng – BUG-026), P1 Hóa đơn của tôi (ô ngày trống – BUG-024). Kết quả cuối: P1, P7 đạt ở 1366 và 1920; A1, A6, A8 đạt ở 1440, 1280, dùng được ở 768.
- Phase 3 – 13/11/2026, `node tools/ui-snapshots/snap.mjs --phase 3`: 17 đường dẫn, 51 ảnh trong artifacts/ui/phase-3, 0 lỗi HTTP 5xx, 0 cuộn ngang, 0 ảnh vỡ, 0 lỗi JS. Mở từng ảnh đối chiếu UI mục 13 và sửa: A4 (ô mẫu và chú thích heatmap mất màu – BUG-015; lưới bị cắt – BUG-019), A3 phim (ô ngày giờ kiểu Mỹ – BUG-016; ô rạp KDM bị cắt – BUG-017), A5 (nhóm phòng cuộn dọc riêng – BUG-018), A2 bảng giá (số tiền không dấu phân cách – BUG-020), A3 đối tượng (dải trống đầu thẻ). Kết quả cuối: A2, A3, A4, A5 đạt ở 1440, 1280; ở 768 dùng được, bảng lịch chiếu và lưới thiết kế chỉ cuộn ngang trong vùng của chúng.
- Phase 2 – 11/11/2026, `node tools/ui-snapshots/snap.mjs --phase 2`: 19 đường dẫn, 56 ảnh trong artifacts/ui/phase-2, 0 lỗi HTTP 5xx, 0 cuộn ngang, 0 ảnh vỡ, 0 lỗi JS. Đã mở từng ảnh đối chiếu UI mục 13 và sửa: D1 (thẻ phim lệch nút do dòng phụ), C6 (đạt ở 390, 768, 1440), A2 (cột phụ và nhãn công tắc ở 768px, link trong bảng), A3 (đạt, form 960px hai cột), A9 (lỗi 500 ở layout POS – BUG-007; thanh demo che nút – BUG-012), D2 (cuộn ngang ở 390px – BUG-009), D3 (đạt, layout theo khu vực). Kết quả cuối: D1, C6, A2, A3, A9, D2, D3 đạt ở mọi kích thước của UI mục 11.
- Phase 1 – không có màn hình (ảnh mẫu SVG được kiểm bằng unit test SvgArtGeneratorTests).
