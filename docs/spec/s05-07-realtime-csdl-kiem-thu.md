# Đặc tả FPolyCinema – mục 5 đến 7: thời gian thực, cơ sở dữ liệu, kiểm thử

Mục lục và cách đọc mã yêu cầu như [S3.6-04 P4]: docs/README.md. Tên chuẩn: docs/CONTRACTS.md. Giao diện: docs/ui.

## 5. Thời gian thực và công cụ demo
- [S5-01 P4] Tên nhóm, sự kiện, quyền tham gia theo CONTRACTS mục 6. Mục tiêu độ trễ dưới 200ms trên máy cục bộ.
- [S5-02 P4] Client dùng chung một module realtime.js: withAutomaticReconnect; kết nối lại xong thì tham gia lại các nhóm và tải lại trạng thái bằng HTTP.
- [S5-03 P4] Màn hình POS có chỉ báo trạng thái kết nối.
- [S5-04 P6] Màn hình bếp, soát vé, bảng sảnh có chỉ báo trạng thái kết nối.
- Thanh đổi vai trò (chỉ khi Demo:EnableTools):
  - [S5-05 P2] Nút nổi, thu gọn được; đổi phiên sang một trong 5 tài khoản mẫu chỉ bằng một lần bấm, qua /demo/switch-role?role=…&returnUrl=….
  - [S5-06 P8] Có nút mở trung tâm demo.
  - [S5-07 P2] Khi không bật Demo:EnableTools, mọi đường dẫn bắt đầu bằng demo/ hoặc dev/ trả 404.
- Trung tâm demo (UI D4):
  - [S5-08 P8] Trang /demo/multi-screen chỉ có khi bật Demo:EnableTools.
  - [S5-09 P8] Mỗi màn hình mở trong một cửa sổ riêng, trên một tên miền phụ khác nhau của localhost: cashier.localhost, manager.localhost, gate.localhost, khach-a.localhost, khach-b.localhost. Mỗi cửa sổ vì thế có phiên đăng nhập riêng (Chrome và Edge tự trỏ *.localhost về máy). Mỗi cửa sổ tự đăng nhập bằng /demo/switch-role. Cửa sổ được mở bằng window.open với kích thước và vị trí sao cho chia đôi màn hình.
  - [S5-10 P8] Các màn hình không cần đăng nhập (màn hình phụ, bảng gọi số, bảng sảnh) được nhúng bằng iframe.
  - 5 kịch bản dựng sẵn:
    1. [S5-11 P8] Hai khách cùng giành một ghế.
    2. [S5-12 P8] POS và màn hình phụ của khách.
    3. [S5-13 P8] Khách đặt bắp nước, bếp và bảng gọi số.
    4. [S5-14 P8] Máy soát vé, vé điện tử và bảng sảnh.
    5. [S5-15 P8] Thu ngân xin hủy hóa đơn, quản lý ca duyệt từ xa.

## 6. Cơ sở dữ liệu
Kiểu dữ liệu và quy ước theo CONTRACTS mục 9. Mọi cột trạng thái có CHECK theo CONTRACTS mục 3. Dưới đây là bảng và cột chính; Claude Code được thêm cột phụ khi cần, nhưng phải ghi lại vào CONTRACTS.md mục 12.
- [S6-01 P1 auto:table] Users(Id, Email UNIQUE, PasswordHash, FullName, Phone, BirthDate, Role, CinemaId, Active, MustChangePassword, MemberCode UNIQUE, LoyaltyPoints, Tier, WalletBalance, ManagerPinHash, PinFailedCount, PinLockedUntil, CreatedAt)
- [S6-02 P1 auto:table] PasswordResetOtps(Id, UserId, OtpHash, ExpiresAt, FailedAttempts, UsedAt)
- [S6-03 P1 auto:table] EmailOutbox(Id, ToEmail, Subject, Body, SentAt, CreatedAt): thư đã gửi, hoặc thư hiện ở hộp thư thử của công cụ dev khi chưa cấu hình SMTP
- [S6-04 P1 auto:table] Cinemas(Id, Name, Address, Phone, Active)
- [S6-05 P1 auto:table] Categories(Id, Name UNIQUE, Active): đúng slide
- [S6-06 P1 auto:table] Drinks(Id, CategoryId, Name, Price, Image, Description, Active, IsCombo, IsSoldOut, CreatedAt): đúng slide, thêm cột
- [S6-07 P1 auto:table] RawMaterials(Id, CinemaId, Name, Unit, ActualStock, ReservedStock, MinThreshold, Active)
- [S6-08 P1 auto:table] ProductRecipes(Id, DrinkId, RawMaterialId, Quantity), UNIQUE(DrinkId, RawMaterialId)
- [S6-09 P1 auto:table] InventoryTransactions(Id, RawMaterialId, Type, Quantity, BillId, WastageLogId, Note, CreatedBy, CreatedAt)
- [S6-10 P1 auto:table] WastageLogs(Id, CinemaId, Status, Note, CreatedBy, CreatedAt, DecidedBy, DecidedAt)
- [S6-11 P1 auto:table] WastageLogItems(Id, WastageLogId, RawMaterialId, Quantity, Reason)
- [S6-12 P1 auto:table] Distributors(Id, Name, Email, Phone, Active)
- [S6-13 P1 auto:table] Movies(Id, Title, OriginalTitle, Genres, DurationMinutes, AgeRating, ClassificationLicenseNo, ContentWarnings, IsVietnameseFilm, LanguageFormats, ReleaseDate, DistributorId, MinimumTicketPrice, IsBlockbuster, PosterUrl, TrailerUrl, Description, AiDemandScore, Active, CreatedAt)
- [S6-14 P1 auto:table] FilmRentalRules(Id, MovieId, WeekNumber, DistributorSharePercent), UNIQUE(MovieId, WeekNumber)
- [S6-15 P1 auto:table] KdmLicenses(Id, MovieId, CinemaId, ValidFrom, ValidUntil, Note)
- [S6-16 P1 auto:table] Auditoriums(Id, CinemaId, Name, Format, TotalRows, TotalCols, IdealRowRatio, Active)
- [S6-17 P1 auto:table] Seats(Id, AuditoriumId, RowIndex, ColIndex, Label, SeatType, PairGroupId, IsHouseSeat, IsMaintenance, BaseViewScore), UNIQUE(AuditoriumId, RowIndex, ColIndex)
- [S6-18 P1 auto:table] Showtimes(Id, MovieId, AuditoriumId, StartTime, FeatureStartTime, EndTime, CleaningEndTime, LanguageFormat, OperationalStatus, IsAiGenerated, IsPrivate, CancelNote, CreatedAt), index (AuditoriumId, StartTime)
- [S6-19 P1 auto:table] ShowtimeSeats(ShowtimeId, SeatId, Status, HoldBillId, HoldExpiresAt, HoldAudienceType, TicketId), khóa chính (ShowtimeId, SeatId)
- [S6-20 P1 auto:table] Holidays(HolidayDate, Name)
- [S6-21 P1 auto:table] PricingRules(Id, CinemaId, RuleType, RuleKey, Amount), UNIQUE(CinemaId, RuleType, RuleKey)
- [S6-22 P1 auto:table] AudienceDiscounts(AudienceType, DiscountPercent, LegalMinPercent, PosOnly, RequiresDocument)
- [S6-23 P1 auto:table] Bills(Id, Code UNIQUE, Channel, CinemaId, ShowtimeId, UserId (người tạo, như slide), CustomerId, PosShiftId, TerminalCode, Status, CancelReason, CreatedAt, PaidAt, CancelledAt, TicketSubtotal, FnbSubtotal, DiscountTotal, Total, VoucherId, PointsUsed, HoldExpiresAt, HoldExtended, CheckInStatus, FnbStatus, PickupNumber, GuardianConfirmed, AgeConfirmed, CorporateBookingId), index (Status, PaidAt)
- [S6-24 P1 auto:table] BillDetails(Id, BillId, DrinkId, Quantity, Price, VatRate, LineFinalAmount): đúng slide, thêm cột
- [S6-25 P1 auto:table] Tickets(Id, BillId, ShowtimeId, SeatId, AudienceType, ListPrice, FinalPrice, VatRate, QrCode UNIQUE, CheckInStatus, CheckedInAt, CheckedInBy, SwappedFromTicketId, CreatedAt)
- [S6-26 P1 auto:table] Payments(Id, BillId, PosShiftId, Method, Direction, Amount, Status, ProviderTxnId (UNIQUE khi khác NULL), RawPayload, CreatedAt)
- [S6-27 P1 auto:table] Vouchers(Id, Code UNIQUE, Type, Value, MinBillAmount, ValidFrom, ValidUntil, UsageLimit, UsedCount, OwnerUserId, Source, CorporateBookingId, Active)
- [S6-28 P1 auto:table] WalletTransactions(Id, UserId, Amount, BalanceAfter, Reason, BillId, CreatedAt)
- [S6-29 P1 auto:table] PointTransactions(Id, UserId, Points, Reason, BillId, CreatedAt)
- [S6-30 P1 auto:table] CorporateBookings(Id, CompanyName, ContactName, Phone, Email, Type, ShowtimeId, VoucherCount, ContractAmount, BillId, CreatedBy, CreatedAt)
- [S6-31 P1 auto:table] PosShifts(Id, UserId, CinemaId, TerminalCode, OpenedAt, OpeningCashFloat, ClosedAt, ActualCashCounted, ExpectedCash, Discrepancy, Status)
- [S6-32 P1 auto:table] SupervisorApprovals(Id, Type, BillId, RequestedBy, Reason, Status, Method, DecidedBy, DecidedAt, ExpiresAt, CreatedAt)
- [S6-33 P1 auto:table] Notifications(Id, UserId, Title, Body, Link, IsRead, CreatedAt)
- [S6-34 P1 auto:table] AuditLogs(Id, UserId, Action, EntityType, EntityId, Detail, CreatedAt)
- [S6-35 P1 auto:table] SchemaVersions(Id, Version UNIQUE, AppliedAt)

## 7. Kiểm thử và tiêu chuẩn hoàn thành
- Unit test (tests/FPolyCinema.Tests) cho mọi lớp trong Domain/Rules, viết cùng phase với lớp đó:
  - [S7-01 P1] BillStateMachine, PricingCalculator, SeatScoring.
  - [S7-02 P3] AgeRules (độ tuổi, giờ chiếu cho trẻ em, người giám hộ), ScheduleConstraintChecker.
  - [S7-03 P4] OrphanSeatValidator, SweetboxRule, SettlementCalculator, CashDropCalculator (đóng ca mù), LoyaltyCalculator.
  - [S7-04 P5] ClusterFinder.
  - [S7-05 P7] NlpParser (ít nhất 20 câu).
- [S7-06 P1] docs/BUG-LOG.md: mọi lỗi thật phát hiện khi chạy thử (không tính lỗi biên dịch) đều được ghi lại, gồm: mã lỗi, mô tả, các bước tái hiện, kết quả thực tế, kết quả mong đợi, Severity (Critical, Major, Minor, Trivial), Priority, phân loại (chức năng, giao diện, hiệu năng, bảo mật), vòng đời (New → In Progress → Fixed → Retest → Closed, ghi ngày từng bước), commit sửa lỗi. File này dùng cho Lab 7.
- [S7-07 P1] docs/TEST-SCENARIOS.md: mỗi phase thêm kịch bản kiểm thử theo cấu trúc test case của slide bài 7 (Test case ID, Chức năng, Mô tả, Dữ liệu, Các bước, Kết quả mong đợi, Kết quả thực tế, Pass/Fail, Ghi chú) và ghi kết quả đã chạy thật. File này dùng lại được cho Lab 7.

### 7.1 Tiêu chuẩn hoàn thành phase
<!-- spec-check: không gắn mã -->
Một phase chỉ được đánh dấu xong khi đủ các điều sau. Lệnh `node tools/spec-check/check.mjs verify N --final` kiểm tự động từng điều; chỉ báo xong khi lệnh này ĐẠT.
1. Mọi yêu cầu của phase (lệnh `list N`) có dòng Xong kèm bằng chứng hợp lệ trong docs/trace/phase-N.md, các luật tự động và quy tắc code (lint) đều đạt.
2. dotnet build không lỗi, cảnh báo mới được sửa hoặc ghi lý do; dotnet test đạt toàn bộ; chạy `--reset-db --init-only` thành công. Lệnh `node tools/spec-check/check.mjs test --reset-db` chạy cả ba và ghi dấu kết quả theo mã băm code.
3. Các trang của phase trả về đúng với từng vai trò liên quan; đã kiểm tra giao diện theo UI mục 13 và có ảnh artifacts/ui/phase-N/{mãMànHình}-*.png cho mọi màn hình có yêu cầu thuộc phase.
4. Trong phần đã làm không còn nút chết, dữ liệu giả cứng trên giao diện, TODO hay hàm rỗng.
5. Agent spec-auditor đã kiểm độc lập mọi yêu cầu của phase; docs/audit/phase-N.md ghi "Kết quả: Đạt".
6. Đã cập nhật PROGRESS.md (đánh dấu [x] Phase N, quyết định, lỗi biết trước, kết quả kiểm tra giao diện), CONTRACTS.md mục 12, docs/TEST-SCENARIOS.md (mục "## Phase N", ít nhất 3 kịch bản đã chạy thật), docs/BUG-LOG.md, và chạy lệnh `facts` để cập nhật docs/generated.
7. Nếu có đổi tên hoặc thêm, bớt bảng, cột, use case hay màn hình so với SPEC mục 6, mục 10 và UI mục 7 thì đã sửa ngay trong docs/spec hoặc docs/ui, chạy `sync`, và ghi vào PROGRESS.md mục "Thay đổi mô hình". Tài liệu lab dựa vào mục này để vẽ lại sơ đồ.
8. Đã git commit "Phase N: <tóm tắt>", không còn file chưa commit, và gắn git tag nếu phase ứng với bài lab (mục 8).

Xong các bước trên thì dừng và báo cáo.
