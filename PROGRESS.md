# Tiến độ
## Trạng thái
- [x] Phase 1  - [ ] Phase 2  - [ ] Phase 3  - [ ] Phase 4
- [ ] Phase 5  - [ ] Phase 6  - [ ] Phase 7  - [ ] Phase 8
## Phase đang làm
Phase 1 – Nền tảng dữ liệu: đã xong ngày 07/11/2026 (verify 1 --final ĐẠT, commit "Phase 1: …", tag lab-3). Phase kế tiếp: Phase 2 – Giao diện nền, đăng nhập, CRUD cơ bản.

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

## Thay đổi mô hình (ngày – bảng/cột/use case/màn hình – trước – sau)
- 06/11/2026 – SchemaVersions – (Version, AppliedAt) – (Id INT IDENTITY khóa chính, Version UNIQUE, AppliedAt), theo quy ước khóa chính Id của CONTRACTS mục 9. Đã sửa dòng S6-35 trong docs/spec/s05-07-realtime-csdl-kiem-thu.md, chạy sync, và ghi ở CONTRACTS mục 12.
- 06/11/2026 – Ràng buộc thêm (không đổi cột): UNIQUE(CinemaId, Name) của RawMaterials; UNIQUE(AuditoriumId, Label) của Seats; index lọc UX_PosShifts_OpenTerminal, UX_PosShifts_OpenUser (một ca mở cho mỗi máy và mỗi thu ngân), UX_Tickets_ActiveSeat (một vé còn hiệu lực cho mỗi ghế của suất), UX_Payments_ProviderTxnId, UX_Users_MemberCode; CHECK CK_Vouchers_PercentValue (voucher PERCENT không quá 100).

## Lỗi biết trước và việc còn lại
- Phase 1 không có màn hình; thư mục Controllers, Views, ViewModels, Hubs, Background, wwwroot/css, wwwroot/js đã tạo (chứa .gitkeep), nội dung làm từ Phase 2.
- OperationalStatus của suất trong dữ liệu mẫu chỉ là COMPLETED (đã dọn xong) hoặc SCHEDULED; ShowtimeClockService cập nhật theo giờ ở Phase 6.
- QR vé dạng SVG (QRCoder) làm ở Phase 4.
- Gợi ý của kiểm tra độc lập chưa làm: tách phép kiểm khóa CinemaRules.PointEarnPercent (đang nằm trong test PricingRuleKey_RejectsKeysOfAnotherRuleType) thành test riêng ở lần sửa test tới.
- Kiểm tra độc lập: lượt đầu 165/173 Đạt; sau khi sửa và kiểm lại 173/173 Đạt (docs/audit/phase-1.md).

## Đang bị chặn (câu hỏi cần người dùng trả lời)
Không có. (07/11/2026: câu hỏi về SDK .NET 8 của S8.1-01 đã xong, người dùng cài SDK 8.0.427.)
## Kết quả kiểm tra giao diện (phase – màn hình – kích thước – đạt/chưa)
- Phase 1 – không có màn hình (ảnh mẫu SVG được kiểm bằng unit test SvgArtGeneratorTests).
