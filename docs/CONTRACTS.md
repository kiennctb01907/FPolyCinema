# Hợp đồng đặt tên – FPolyCinema

File này là nguồn duy nhất cho tên vai trò, policy, giá trị trạng thái, nhóm và sự kiện SignalR, khóa cấu hình, mã lỗi và quy ước dữ liệu. Code C#, SQL (kể cả CHECK constraint) và JavaScript dùng đúng các chuỗi ở đây. Cần tên mới thì thêm vào file này trước rồi mới dùng. Mục 12 do Claude Code cập nhật cuối mỗi phase.

Các dòng có mã dạng [C3-05 P1 auto:enum] là yêu cầu được script `node tools/spec-check/check.mjs` kiểm tự động từ phase ghi trong mã trở đi (xem docs/README.md).

## 1. Vai trò (Users.Role)

| Giá trị | Hiển thị | Ghi chú |
|---|---|---|
| Customer | Khách hàng | Tự đăng ký trên web |
| POSCashier | Thu ngân và quầy bắp nước | Gắn với một rạp (CinemaId) |
| TicketInspector | Soát vé và điều phối phòng chiếu | Gắn với một rạp |
| ShiftManager | Quản lý ca | Có mọi quyền của POSCashier và TicketInspector, thêm quyền phê duyệt |
| Manager | Quản lý rạp (quản trị) | Toàn quyền; CinemaId để trống nghĩa là quản lý toàn chuỗi |

- [C1-01 P1 auto:enum-table] Các giá trị ở cột đầu bảng trên là hằng trong Domain/Constants (lớp Roles) và là CHECK của cột (cột: Users.Role).
- [C1-02 P2] Giao diện hiển thị vai trò bằng cột Hiển thị, không hiện giá trị gốc.

## 2. Policy (Program.cs)

| Policy | Vai trò được phép | Dùng cho |
|---|---|---|
| EmployeeOrManager | POSCashier, TicketInspector, ShiftManager, Manager | Giữ đúng tên slide bài 4. Xem danh sách và chi tiết loại đồ uống, đồ uống, phim; trang cá nhân nhân viên |
| ManagerOnly | Manager | Giữ đúng tên slide bài 4. Thêm, sửa, xóa danh mục; nhân viên; cấu hình; báo cáo; xếp lịch; upload ảnh |
| PosStaff | POSCashier, ShiftManager, Manager | Màn hình POS, bếp, mở và đóng ca |
| GateStaff | TicketInspector, ShiftManager, Manager | Soát vé, xác nhận dọn phòng |
| SupervisorOrManager | ShiftManager, Manager | Duyệt hủy hóa đơn, giải phóng ghế nội bộ, duyệt hủy hàng, hủy suất do sự cố |
| CustomerOnly | Customer | Vé của tôi, ví, điểm, tự hủy vé |

- [C2-01 P2 auto:policies] Mỗi policy ở bảng trên được khai báo bằng AddPolicy trong Program.cs với đúng tên và đúng các vai trò được phép.
- [C2-02 P2] Mỗi controller và action dùng đúng policy theo cột Dùng cho; chức năng thuộc phase sau dùng đúng policy của nó khi được làm.

## 3. Giá trị trạng thái và danh mục

Lưu trong DB đúng chuỗi ở đây. Trong C# khai báo ở Domain/Constants dạng `public static class SeatType { public const string Standard = "STANDARD"; ... }`. Cột ghi trong ngoặc "cột:" phải có CHECK constraint liệt kê đúng và đủ các giá trị (cột cho phép NULL thì thêm `IS NULL OR`).

- [C3-01 P1 auto:enum] AgeRating (cột: Movies.AgeRating): P, K, T13, T16, T18, C
- [C3-02 P1 auto:enum] ContentWarning: THEME, VIOLENCE, NUDITY_SEX, DRUGS, HORROR, LANGUAGE, DANGEROUS_BEHAVIOR (Thông tư 05/2023; Movies.ContentWarnings lưu danh sách cách nhau dấu phẩy)
- [C3-03 P1 auto:enum] Genre: ACTION, ADVENTURE, ANIMATION, COMEDY, DRAMA, FAMILY, HORROR, ROMANCE, SCIFI, THRILLER, DOCUMENTARY (Movies.Genres lưu danh sách cách nhau dấu phẩy)
- [C3-04 P1 auto:enum] LanguageFormat (cột: Showtimes.LanguageFormat): SUBTITLED, DUBBED, ORIGINAL (Movies.LanguageFormats lưu danh sách cách nhau dấu phẩy)
- [C3-05 P1 auto:enum] AuditoriumFormat (cột: Auditoriums.Format): 2D, 3D, IMAX, 4DX (tên hằng C#: F2D, F3D, IMAX, F4DX)
- [C3-06 P1 auto:enum] SeatType (cột: Seats.SeatType): STANDARD, VIP, SWEETBOX, WHEELCHAIR
- [C3-07 P1 auto:enum] ShowtimeSeatStatus (cột: ShowtimeSeats.Status): AVAILABLE, HELD, BOOKED, HOUSE_HOLD
- [C3-08 P1 auto:enum] SeatDisplayStatus: AVAILABLE, HELD, BOOKED, HOUSE_HOLD, MAINTENANCE (tính khi đọc, gửi cho giao diện; giao diện tự hiển thị SELECTED cho ghế có holdBillId trùng hóa đơn của mình)
- [C3-09 P1 auto:enum] OperationalStatus (cột: Showtimes.OperationalStatus): SCHEDULED, BOARDING, ADS_PLAYING, FEATURE_PLAYING, CREDITS_ROLLING, CLEANING_IN_PROGRESS, COMPLETED, CANCELLED
- [C3-10 P1 auto:enum] BillStatus (cột: Bills.Status): waiting, finish, cancel (chữ thường, đúng slide bài 6)
- [C3-11 P1 auto:enum] CancelReason (cột: Bills.CancelReason): EXPIRED, CANCELLED_BEFORE_PAYMENT, CANCELLED_BY_USER, VOIDED_BY_POS, OVERBOOKED_REFUNDED, INCIDENT_COMPENSATED
- [C3-12 P1 auto:enum] BillChannel (cột: Bills.Channel): POS, ONLINE, CORPORATE
- [C3-13 P1 auto:enum] BillCheckInStatus (cột: Bills.CheckInStatus): NONE, NOT_CHECKED_IN, PARTIALLY_CHECKED_IN, FULLY_CHECKED_IN, VOIDED
- [C3-14 P1 auto:enum] TicketCheckInStatus (cột: Tickets.CheckInStatus): NOT_CHECKED_IN, CHECKED_IN, SWAPPED, VOIDED
- [C3-15 P1 auto:enum] FnbStatus (cột: Bills.FnbStatus): NONE, PENDING_PICKUP, PREPARING, READY_FOR_PICKUP, FULFILLED, VOIDED
- [C3-16 P1 auto:enum] PaymentMethod (cột: Payments.Method): CASH, VIETQR_SIM, WALLET (voucher và điểm là giảm giá, không phải phương thức thanh toán)
- [C3-17 P1 auto:enum] PaymentDirection (cột: Payments.Direction): IN, REFUND
- [C3-18 P1 auto:enum] PaymentStatus (cột: Payments.Status): PENDING, SUCCEEDED, FAILED
- [C3-19 P1 auto:enum] VoucherType (cột: Vouchers.Type): AMOUNT, PERCENT, FREE_TICKET
- [C3-20 P1 auto:enum] VoucherSource (cột: Vouchers.Source): PROMO, COMPENSATION, CORPORATE
- [C3-21 P1 auto:enum] LoyaltyTier (cột: Users.Tier): MEMBER, VIP, VVIP
- [C3-22 P1 auto:enum] ShiftStatus (cột: PosShifts.Status): OPEN, CLOSED
- [C3-23 P1 auto:enum] ApprovalType (cột: SupervisorApprovals.Type): VOID_BILL
- [C3-24 P1 auto:enum] ApprovalStatus (cột: SupervisorApprovals.Status): PENDING, APPROVED, REJECTED, EXPIRED
- [C3-25 P1 auto:enum] ApprovalMethod (cột: SupervisorApprovals.Method): PIN, REMOTE
- [C3-26 P1 auto:enum] WastageStatus (cột: WastageLogs.Status): PENDING, APPROVED, REJECTED
- [C3-27 P1 auto:enum] InventoryTxnType (cột: InventoryTransactions.Type): IMPORT, RESERVE, RELEASE, CONSUME, WASTAGE, ADJUST
- [C3-28 P1 auto:enum] CorporateType (cột: CorporateBookings.Type): PRIVATE_SCREENING, VOUCHER_BATCH
- [C3-29 P1 auto:enum] SeatPersona: BEST_VIEW_AUDIO, NEAR_SCREEN, PRIVATE_COUPLE, NEAR_AISLE
- [C3-30 P1 auto:enum] PricingRuleType (cột: PricingRules.RuleType): BASE, DAY_TYPE, TIME_SLOT, SEAT_TYPE, FORMAT, BLOCKBUSTER
- RuleKey hợp lệ theo từng RuleType:
  - [C3-31 P1 auto:enum] BASE: DEFAULT
  - [C3-32 P1 auto:enum] DAY_TYPE: WEEKDAY, WEEKEND, HOLIDAY, HAPPY_TUESDAY
  - [C3-33 P1 auto:enum] TIME_SLOT: MORNING, AFTERNOON, GOLDEN, LATE
  - [C3-34 P1 auto:enum] SEAT_TYPE: STANDARD, VIP, SWEETBOX, WHEELCHAIR
  - [C3-35 P1 auto:enum] FORMAT: 2D, 3D, IMAX, 4DX
  - [C3-36 P1 auto:enum] BLOCKBUSTER: YES
- Cờ tính toán, không lưu DB:
  - [C3-37 P3] LOCKED_HAS_TICKETS: suất có ít nhất một ghế BOOKED hoặc HELD còn hạn.
  - [C3-38 P6] Hóa đơn "đã hoàn tất": Status finish, CheckInStatus là NONE hoặc FULLY_CHECKED_IN, FnbStatus là NONE hoặc FULFILLED.
- [C3-39 P2 auto:enum] AuditAction: USER_LOCKED, USER_UNLOCKED, PRICING_UPDATED, VOID_BILL, ORPHAN_SEAT_OVERRIDE, SEAT_SWAPPED, HOUSE_SEAT_RELEASED, SHOWTIME_CANCELLED, WASTAGE_APPROVED, WASTAGE_REJECTED (hằng AuditAction ghi vào AuditLogs.Action; mỗi phase thêm hành động của mình theo SPEC 2.7)

## 4. Đối tượng vé (AudienceType)

| Giá trị | Ý nghĩa | Giảm mặc định | Mức tối thiểu theo luật | Bán online | Cần giấy tờ khi soát vé |
|---|---|---|---|---|---|
| ADULT | Người lớn | 0% | – | Có | Không |
| U22 | Khách dưới 22 tuổi (ưu đãi của rạp) | 10% | – | Có | Có |
| CHILD_U13 | Trẻ em dưới 13 tuổi | 20% | 20% | Không | Có |
| CHILD_13_15 | Trẻ em từ đủ 13 đến dưới 16 tuổi | 20% | 20% | Không | Có |
| SENIOR | Người cao tuổi (từ đủ 60 tuổi) | 20% | 20% | Không | Có |
| MERIT | Người có công với cách mạng | 20% | 20% | Không | Có |
| HARDSHIP | Người có hoàn cảnh đặc biệt khó khăn | 20% | 20% | Không | Có |
| DISABLED_SEVERE | Người khuyết tật nặng | 50% | 50% | Không | Có |
| DISABLED_EXTREME | Người khuyết tật đặc biệt nặng | 100% | 100% | Không | Có |

Căn cứ: Điều 10 Nghị định 131/2022/NĐ-CP; "trẻ em" là người dưới 16 tuổi theo Luật Trẻ em. Được tăng mức giảm nhưng không được thấp hơn cột tối thiểu.

- [C4-01 P1 auto:enum-table] Các giá trị ở cột đầu bảng trên là hằng AudienceType trong Domain/Constants và là CHECK của các cột (cột: Tickets.AudienceType, AudienceDiscounts.AudienceType).
- [C4-02 P1] DbSeeder nạp bảng AudienceDiscounts đúng các cột giảm mặc định, mức tối thiểu, bán online (PosOnly) và cần giấy tờ (RequiresDocument) của bảng trên.

## 5. Chuyển trạng thái hóa đơn

- [C5-01 P1] Mọi thay đổi Bills.Status đi qua một hàm duy nhất `BillStateMachine.CanTransition(from, to, reason, actorRole, out string? errorCode)`, là logic thuần có unit test.

| Từ | Sang | Ai thực hiện | Điều kiện |
|---|---|---|---|
| waiting | finish | PosStaff (quầy); hệ thống khi webhook báo thành công (online) | Đã thu đủ tiền |
| waiting | cancel, CANCELLED_BEFORE_PAYMENT | PosStaff; khách với hóa đơn online của mình | |
| waiting | cancel, EXPIRED | Hệ thống | Hết hạn giữ ghế mà chưa thanh toán |
| finish | cancel, VOIDED_BY_POS | Thu ngân yêu cầu; ShiftManager hoặc Manager duyệt bằng PIN hoặc từ xa | Chưa có vé nào đã vào cửa |
| finish | cancel, CANCELLED_BY_USER | Khách | Đủ điều kiện tự hủy (SPEC 3.9) |
| finish | cancel, INCIDENT_COMPENSATED | SupervisorOrManager | Hủy suất do sự cố (SPEC 3.10) |
| cancel (EXPIRED) | finish | Hệ thống | Webhook thành công đến muộn, giữ lại được đủ ghế cũ |
| cancel (EXPIRED) | cancel, OVERBOOKED_REFUNDED | Hệ thống | Webhook thành công đến muộn, ghế đã bán cho người khác |

Tham số actorRole nhận một giá trị ở mục 1 hoặc `System` (hằng `Actors.System` trong Domain/Constants) khi người thực hiện là hệ thống (webhook thanh toán, bộ quét giữ ghế). Hai dòng "cancel (EXPIRED)" cần biết lý do hủy hiện tại nên CanTransition có thêm tham số tùy chọn `currentReason` đặt sau `out errorCode`.

- [C5-02 P1] Unit test cho từng dòng của bảng trên, và cho ít nhất 3 chuyển trạng thái không có trong bảng (phải bị từ chối với INVALID_TRANSITION).
- [C5-03 P4] Nhân viên chuyển finish sang cancel luôn cần quản lý duyệt (đúng slide bài 6). Ngoài bảng trên không có chuyển trạng thái nào khác.

## 6. SignalR – CinemaHub tại /hubs/cinema

Hub chỉ để tham gia, rời nhóm và nhận sự kiện. Mọi thao tác thay đổi dữ liệu đi qua Controller (POST, có Anti-Forgery và phân quyền); service phát sự kiện bằng IHubContext<CinemaHub>. Payload là JSON camelCase. Kênh 3 gộp phần khách theo dõi thanh toán, vào cửa và bắp nước vào một nhóm theo hóa đơn.

| Kênh | Nhóm | Hàm tham gia | Ai được tham gia | Sự kiện |
|---|---|---|---|---|
| 1. Sơ đồ ghế | showtime_{showtimeId} | JoinShowtime(showtimeId) | Mọi người | SEAT_MAP_UPDATED { showtimeId, changes: [{ seatId, status, holdExpiresAt, holdBillId }] } |
| 2. Màn hình phụ của quầy | pos_terminal_{terminalCode} | JoinTerminal(code, isDisplay) | PosStaff; màn hình phụ tham gia ẩn danh, chỉ nhận | POS_SHOWTIME_SELECTED, POS_CART_UPDATED, POS_PAYMENT_QR, POS_PAYMENT_DONE, POS_RESET, APPROVAL_DECIDED |
| 3. Theo dõi một hóa đơn | bill_{billId} | JoinBill(billId) | Chủ hóa đơn hoặc nhân viên | PAYMENT_STATUS_CHANGED, CHECKIN_UPDATED, FNB_STATUS_CHANGED |
| 4. Sĩ số phòng | auditorium_{auditoriumId} | JoinAuditorium(auditoriumId) | EmployeeOrManager | OCCUPANCY_UPDATED { showtimeId, checkedIn, sold } |
| 5. Bếp và bảng gọi số | fnb_kds_{cinemaId} | JoinKds(cinemaId, isBoard) | PosStaff; bảng gọi số ẩn danh, chỉ nhận | KDS_ORDER_QUEUED, KDS_ORDER_UPDATED |
| 6. Vận hành phòng và bảng sảnh | cinema_ops_{cinemaId} | JoinOps(cinemaId) | Mọi người, chỉ nhận | SHOWTIME_OPS_CHANGED { showtimeId, operationalStatus } |
| 7a. Cảnh báo quản lý | manager_alerts_{cinemaId} | JoinManagerAlerts(cinemaId) | SupervisorOrManager | APPROVAL_REQUESTED, APPROVAL_DECIDED, STOCK_LOW |
| 7b. Hết món | cinema_{cinemaId} | JoinCinema(cinemaId) | Mọi người | PRODUCT_SOLD_OUT { drinkIds }, PRODUCT_AVAILABLE { drinkIds } |

- [C6-01 P4] CinemaHub được map tại /hubs/cinema; hub chỉ có hàm tham gia và rời nhóm, mỗi hàm tham gia kiểm tra quyền theo cột "Ai được tham gia".
- Phase phải có hàm tham gia và sự kiện (script kiểm chuỗi tên có trong code C# phát sự kiện và trong JavaScript nhận sự kiện):
  - [C6-02 P4 auto:events] JoinShowtime, SEAT_MAP_UPDATED
  - [C6-03 P5 auto:events] JoinBill, PAYMENT_STATUS_CHANGED
  - [C6-04 P6 auto:events] JoinTerminal, POS_SHOWTIME_SELECTED, POS_CART_UPDATED, POS_PAYMENT_QR, POS_PAYMENT_DONE, POS_RESET, APPROVAL_DECIDED
  - [C6-05 P6 auto:events] CHECKIN_UPDATED, FNB_STATUS_CHANGED
  - [C6-06 P6 auto:events] JoinAuditorium, OCCUPANCY_UPDATED
  - [C6-07 P6 auto:events] JoinKds, KDS_ORDER_QUEUED, KDS_ORDER_UPDATED
  - [C6-08 P6 auto:events] JoinOps, SHOWTIME_OPS_CHANGED
  - [C6-09 P6 auto:events] JoinManagerAlerts, APPROVAL_REQUESTED, STOCK_LOW
  - [C6-10 P6 auto:events] JoinCinema, PRODUCT_SOLD_OUT, PRODUCT_AVAILABLE

## 7. Cấu hình (appsettings.json)

Khóa nào chứa bí mật thì để chuỗi rỗng trong appsettings.json và đặt qua biến môi trường (dấu hai chấm đổi thành hai dấu gạch dưới).

- [C7-01 P1 auto:config] ConnectionStrings:Default – chuỗi kết nối SQL Server. appsettings.json có khóa này (để trống), giá trị chạy thật nằm ở appsettings.Development.json hoặc biến môi trường ConnectionStrings__Default.
- [C7-02 P2 auto:config] Demo:EnableTools – true chỉ trong Development (thanh đổi vai trò, các trang demo và dev).
- [C7-03 P2 auto:config] Uploads:Path – mặc định wwwroot/uploads.
- [C7-04 P2 auto:config] Email:SmtpHost, Email:SmtpPort, Email:SmtpUser, Email:SmtpPassword, Email:From – để trống thì IEmailSender ghi thư vào bảng EmailOutbox và xem ở hộp thư thử (UI D2). SmtpPassword chỉ đặt qua biến môi trường Email__SmtpPassword.
- [C7-05 P2 auto:config] Auth:OtpLength = 6, Auth:OtpValidMinutes = 5, Auth:OtpMaxAttempts = 5, Auth:NewPasswordLength = 10.
- [C7-06 P5 auto:config] Payment:WebhookSecret – khóa ký webhook của trang mô phỏng thanh toán.
- [C7-07 P7 auto:config] Llm:BaseUrl, Llm:Model, Llm:ApiKey – ApiKey chỉ đặt qua biến môi trường Llm__ApiKey.
- [C7-08 P1 auto:config-table] CinemaRules: có đủ mọi khóa trong bảng dưới với đúng giá trị mặc định, đọc qua lớp options tên CinemaRules (Infrastructure/CinemaRules.cs) dùng chung cho mọi service.

| Khóa | Mặc định | Ghi chú |
|---|---|---|
| TimeZoneId | Asia/Ho_Chi_Minh | Dự phòng "SE Asia Standard Time" trên Windows cũ |
| SeatHoldMinutes | 10 | Đổi ghế không gia hạn |
| SeatHoldExtendMinutes | 5 | Thu ngân gia hạn được một lần |
| HoldSweepSeconds | 20 | |
| OnlineSalesCutoffMinutes | 0 | Bán online đến FeatureStartTime cộng số phút này |
| PosSalesCutoffMinutes | 15 | |
| AdsMinutes | 15 | |
| CreditsMinutes | 5 | |
| CleaningMinutes | 15 | |
| ScheduleRoundMinutes | 5 | |
| BoardingLeadMinutes | 10 | |
| OperatingDayStart | 08:00 | Suất bắt đầu trước 06:00 thuộc ngày vận hành hôm trước |
| LastShowStartLatest | 23:30 | |
| ChildU13LatestEnd | 22:00 | Điều 9 khoản 4 NĐ 131/2022 |
| ChildU16LatestEnd | 23:00 | Điều 9 khoản 4 NĐ 131/2022 |
| VietnameseFilmMinShare | 0.20 | Điều 9 khoản 3 NĐ 131/2022, từ 01/01/2026 |
| VietnameseFilmTargetShare | 0.22 | Mục tiêu của bộ xếp lịch |
| VietnameseFilmPriorityStart, VietnameseFilmPriorityEnd | 18:00, 22:00 | Điều 9 khoản 2 (ưu tiên) |
| PriceSlotAfternoonStart, PriceSlotGoldenStart, PriceSlotLateStart | 12:00, 17:00, 22:00 | Khung giá |
| PriceRoundTo | 1000 | |
| MaxSameMinuteStarts | 2 | |
| MinStartStaggerMinutes | 10 | |
| HouseSeatsPerAuditorium | 4 | |
| HouseSeatReminderMinutes | 30 | |
| SelfCancelMinMinutesBeforeStart | 120 | |
| SelfCancelMaxPerMonth | 2 | Tính theo tháng dương lịch, giờ Việt Nam |
| ImHereWindowMinutesBeforeStart | 60 | |
| CrossShowtimeWindowMinutes | 60 | |
| GoodClusterScore | 75 | |
| TicketVatPercent | 8 | Mức 10% giảm còn 8% theo Nghị quyết 204/2025/QH15 đến 31/12/2026; kiểm tra lại trước khi dùng thật |
| FnbVatPercent | 8 | Như trên |
| PointEarnPercent | MEMBER 5, VIP 7, VVIP 10 | Đối tượng JSON theo hạng |
| PointValueVnd | 1000 | |
| MaxPointsRedeemPercent | 50 | |
| TierVipSpend12Months, TierVvipSpend12Months | 4000000, 8000000 | |
| CompensationVoucherAmount | 50000 | |
| CompensationVoucherValidDays | 60 | |
| ApprovalRequestExpireMinutes | 10 | |
| ManagerPinMaxAttempts, ManagerPinLockMinutes | 5, 5 | |

## 8. Phản hồi AJAX và mã lỗi

- [C8-01 P2] Thành công: `{ "ok": true, "data": ... }`
- [C8-02 P2] Lỗi: `{ "ok": false, "error": { "code": "...", "message": "câu tiếng Việt cho người dùng", "details": { ... } } }`
- [C8-03 P2] HTTP: 200 thành công; 400 dữ liệu không hợp lệ; 401, 403 lỗi quyền; 404 không tìm thấy; 409 xung đột; 422 vi phạm quy tắc nghiệp vụ.
- Mã lỗi theo phase phải có trong code (script kiểm chuỗi mã có trong C#; lời nhắn mẫu cho từng mã ở UI mục 9):
  - [C8-04 P1 auto:codes] INVALID_TRANSITION (409)
  - [C8-05 P2 auto:codes] VALIDATION_FAILED (400), DELETE_BLOCKED, OTP_INVALID, OTP_EXPIRED, MUST_CHANGE_PASSWORD
  - [C8-06 P3 auto:codes] KDM_INVALID, LICENSE_MISSING, RATING_C, SHOWTIME_OVERLAP, SHOWTIME_LOCKED, AGE_RESTRICTED, CHILD_CURFEW, GUARDIAN_REQUIRED
  - [C8-07 P4 auto:codes] SEAT_TAKEN (409), ORPHAN_SEAT, SWEETBOX_PAIR, WHEELCHAIR_CONFIRM_REQUIRED, SALES_CLOSED, SHIFT_NOT_OPEN, SHIFT_HAS_WAITING_BILLS, STOCK_INSUFFICIENT, APPROVAL_REQUIRED, PIN_INVALID, PIN_LOCKED, VOUCHER_INVALID, POINTS_INVALID
  - [C8-08 P5 auto:codes] AGE_CONFIRM_REQUIRED, SELF_CANCEL_NOT_ALLOWED, WALLET_INSUFFICIENT, PAYMENT_EXPIRED
  - [C8-09 P2 auto:codes] AUTH_REQUIRED (401), ACCESS_DENIED (403), NOT_FOUND (404), SERVER_ERROR (500) – mã chung khi chưa đăng nhập, không có quyền, không tìm thấy và lỗi máy chủ

## 9. Quy ước dữ liệu và code

- [C9-01 P1] Bảng số nhiều PascalCase, cột PascalCase, khóa chính Id INT IDENTITY, khóa ngoại {Tên}Id. Ngoại lệ: ShowtimeSeats có khóa chính (ShowtimeId, SeatId); Holidays có khóa chính HolidayDate; AudienceDiscounts có khóa chính AudienceType.
- [C9-02 P1] Tiền: BIGINT (đồng), C# long. Phần trăm: DECIMAL(5,2). Tồn kho và định mức: DECIMAL(18,3). Script kiểm kiểu các cột này trong schema.
- [C9-03 P1] Thời gian: DATETIME2(0), giờ Việt Nam; cột chỉ có ngày dùng DATE. SQL nghiệp vụ không gọi GETDATE() hay SYSDATETIME(); luôn nhận @Now từ IClock.
- [C9-04 P1] Tham số SQL theo kiểu slide: @1, @2, … theo thứ tự trong List<object>.
- [C9-05 P1] Entity trong Entities/, DAL trong DAL/ (mỗi DAL có FromDataRow, GetAll, GetById, Insert, Update, Delete), quy tắc thuần trong Domain/Rules/.
- [C9-06 P1] Mã hóa đơn: HD + yyyyMMddHHmmss + 4 ký tự in hoa ngẫu nhiên. Mã QR vé: TK + 12 ký tự ngẫu nhiên khó đoán. Mã thành viên: TV + 8 chữ số. Số gọi bắp nước: 3 chữ số, đếm lại mỗi ngày theo rạp. Mã máy POS: POS-01, POS-02.
- [C9-07 P4] Route POS giữ kiểu slide bài 6: /pos/index, /pos/init, /pos/update-quantity (action là incr, decr hoặc delete), /pos/checkout, /pos/cancel. Route mới cùng kiểu chữ thường, nối bằng gạch ngang.
- [C9-08 P2] Route của mọi màn hình trong UI mục 7 khai báo bằng attribute routing ([Route], [HttpGet("…")]) đúng chuỗi đường dẫn ghi trong UI, để script đối chiếu được.

## 10. Stored procedure (chữ ký đã chốt, chi tiết ở SPEC 3.12)

- [C10-01 P1 auto:sp] sp_top_5_best_selling_drinks @StartDate DATE, @EndDate DATE
- [C10-02 P1 auto:sp] sp_top_5_best_selling_movies @StartDate DATE, @EndDate DATE
- [C10-03 P1 auto:sp] sp_revenue_by_day @StartDate DATE, @EndDate DATE
- [C10-04 P1 auto:sp] sp_box_office_settlement @MovieId INT, @WeekNumber INT, @CinemaId INT = NULL

## 11. Tài khoản mẫu

Mật khẩu của mọi tài khoản: 123456. PIN quản lý (ShiftManager, Manager): 246810.

| Email | Vai trò | Ghi chú |
|---|---|---|
| manager@fpolycinema.vn | Manager | Toàn chuỗi |
| shift@fpolycinema.vn | ShiftManager | Rạp 1 |
| cashier@fpolycinema.vn | POSCashier | Rạp 1, có ca đang mở tại POS-01 |
| gate@fpolycinema.vn | TicketInspector | Rạp 1 |
| khach@fpolycinema.vn | Customer | Hạng VIP, có điểm, ví, voucher và vé sắp chiếu |
| khach2@fpolycinema.vn | Customer | Để thử hai khách cùng giành một ghế |

- [C11-01 P1 auto:accounts] DbSeeder tạo đủ các tài khoản ở bảng trên với mật khẩu 123456 và PIN 246810 (lưu dạng băm), đúng vai trò, rạp và ghi chú.

## 12. Endpoint, lớp và sự kiện đã làm
<!-- spec-check: không gắn mã -->

Claude Code ghi vào đây cuối mỗi phase, mỗi phase một mục "### Phase N": route và quyền của từng action, tên service và DAL chính, sự kiện đã phát. Bảng route đầy đủ do lệnh `facts` sinh ở docs/generated/routes.md.

### Phase 1

Route và quyền: chưa có controller nào (Phase 1 không có màn hình). Program.cs mới đăng ký MVC, CinemaRules, IClock, BillStatusService và khởi tạo CSDL.

Sự kiện SignalR: chưa phát sự kiện nào.

Tham số dòng lệnh: `--reset-db` (chỉ Development: xóa DB, tạo lại, nạp lại dữ liệu mẫu), `--init-only` (khởi tạo xong thì thoát, mã 0 khi thành công, 1 khi lỗi).

Lớp chính:
- Utils: `DBUtil` (QueryDataTable/queryDataTable, ExecuteScalar/executeScalar, ExecuteNonQuery, bản có conn và tx, InTransaction, InParams, Value, BuildParameters), `FileUtil` (Upload, Delete, SignatureMatches, Clock), `PasswordUtil` (Hash, Verify bằng PasswordHasher).
- Entities và DAL: 35 cặp `{Entity}`/`{Entity}DAL` cho 35 bảng, mỗi DAL có FromDataRow, FromDataTable, GetAll, GetById, Insert, Update, Delete nhận conn và tx tùy chọn. Hàm thêm: `UserDAL.GetByEmail`, `Authenticate`, `SetActive`, `Count`; `BillDAL.GetByCode`, `UpdateStatus` (chỉ BillStatusService gọi); `ShowtimeSeatDAL.CreateForShowtime`, `GetByShowtime`, `ReleaseForBill`; `TicketDAL.GetByBill`, `SetCheckInStatusForBill`; `InventoryTransactionDAL.InsertForBill`, `InsertOpeningBalances`; `RawMaterialDAL.RecalculateReservedStock`.
- `StatisticDAL`: TopBestSellingDrinks, TopBestSellingMovies, RevenueByDay, GetBoxOfficeSettlement (gọi 4 stored procedure bằng EXEC), trả các lớp TopDrinkStat, TopMovieStat, RevenueByDayStat, BoxOfficeSettlement.
- Services: `BillStatusService.ChangeStatus` – đường duy nhất đổi Bills.Status, gọi BillStateMachine trước khi ghi.
- Domain/Rules (logic thuần, có unit test): `BillStateMachine` (CanTransition, EnsureTransition), `PricingCalculator` (OperatingDate, DayTypeOf, TimeSlotOf, RoundPrice, Calculate, PreTaxAmount, VatAmount; kèm `PriceTable`, `PriceInput`, `PriceBreakdown`), `SeatScoring` (IdealRow, IdealCol, RawScore, Adjustment, Score, IsSegmentEnd), `ShowtimeTimeCalculator` (Compute, RoundToNearest, RoundUp), `SeatLabeler` (RowLetter, AssignLabels), `CodeGenerator` (BillCode, TicketQrCode, MemberCode, NextPickupNumber, FormatPickupNumber, TerminalCode).
- Domain: `BusinessRuleException(code, message, details)` với HttpStatus theo `ErrorCodes.HttpStatusOf` (INVALID_TRANSITION → 409).
- Domain/Constants: các lớp hằng theo mục 1, 3, 4, thêm `Actors.System` (mục 5), `DayType`, `TimeSlot`, `PricingRuleKey` (For, IsValid), `CsvList`, `ErrorCodes`.
- Infrastructure: `CinemaRules`, `IClock`/`SystemClock`, `DbInitializer`, `DbSeeder` (kèm Seed/SeedCatalog, SeatLayoutPlanner, SchedulePlanner), `SvgArtGenerator`.

Cột và ràng buộc thêm so với SPEC mục 6:
- SchemaVersions có thêm cột `Id INT IDENTITY` làm khóa chính, `Version` UNIQUE (theo quy ước khóa chính Id ở mục 9).
- UNIQUE(CinemaId, Name) của RawMaterials; UNIQUE(AuditoriumId, Label) của Seats.
- Index lọc: UX_Users_MemberCode (MemberCode khác NULL), UX_PosShifts_OpenTerminal (CinemaId, TerminalCode khi OPEN), UX_PosShifts_OpenUser (UserId khi OPEN), UX_Tickets_ActiveSeat (ShowtimeId, SeatId khi vé NOT_CHECKED_IN hoặc CHECKED_IN), UX_Payments_ProviderTxnId (khác NULL).
- CHECK phụ: giá, tổng tiền, tồn kho không âm; DiscountPercent ≥ LegalMinPercent; KDM và voucher ValidUntil > ValidFrom; thứ tự 4 mốc của suất chiếu; PricingRules.RuleKey thuộc hợp các RuleKey hợp lệ.

### Phase 2

Route và quyền (bảng đầy đủ ở docs/generated/routes.md):
- Khách vãng lai ([AllowAnonymous]): `/`, `/policies/terms`, `/policies/cancellation`, `/Home/AccessDenied`, `/Home/Error`, `/Login/Login` (GET, POST), `/Login/Register` (GET, POST), `/Login/ForgotPassword` (GET; POST AJAX gửi OTP), `/Login/VerifyOtp` (POST AJAX), `/cinema/select` (POST).
- Đã đăng nhập ([Authorize]): `/Login/Logout` (POST), `/Login/ChangePassword` (GET, POST, nhận cả AJAX), `/Login/Profile` (khách) và `/me` (nhân viên, UI A9) (GET, POST `/Login/Profile`).
- EmployeeOrManager: `/admin/categories`, `/admin/categories/{id}`, `/admin/drinks`, `/admin/drinks/{id}` (danh sách nhận `?partial=1` để tải lại vùng bảng).
- ManagerOnly: `/admin/categories|drinks/create`, `/{id}/edit` (GET, POST), `/{id}/delete-check` (GET JSON), `/{id}/delete` (POST JSON), `/{id}/set-active` (POST JSON); toàn bộ `/admin/users…` (cùng các action); `/File/Upload`, `/File/Delete` (POST, ValidateAntiForgeryToken).
- Chỉ khi Demo:EnableTools (tắt thì 404): `/demo/switch-role?role=…&returnUrl=…`, `/dev/style-guide` (cùng `/dev/style-guide/ok|fail|slow|broken` để thử thành phần), `/dev/mailbox`, `/dev/mailbox/clear` (POST), `/dev/layouts/pos`, `/dev/layouts/display`.

Tên mới thêm vào hợp đồng: mã lỗi chung C8-09 (AUTH_REQUIRED 401, ACCESS_DENIED 403, NOT_FOUND 404, SERVER_ERROR 500); hành động nhật ký C3-39 (USER_LOCKED, USER_UNLOCKED, EntityType "Users"); claim `CinemaId`, `MustChangePassword`, `PasswordStamp` (lớp AppClaims); cookie `fpc_auth`, `fpc_af` (Anti-Forgery), `fpc_cinema` (rạp đang chọn); header AJAX `RequestVerificationToken`, `X-Requested-With: XMLHttpRequest`.

Lớp chính:
- Services: `AuthService` (Login, Register, ChangePassword, UpdateProfile, BuildPrincipal, PasswordStamp, ValidateSession), `PasswordResetService` (RequestOtp, VerifyOtp), `IEmailSender` với `SmtpEmailSender` / `OutboxEmailSender`, `CategoryService`, `DrinkService` (Save kèm định mức theo rạp, Delete, SetActive), `StaffService` (Create cấp mật khẩu qua email, Update, SetActive ghi AuditLogs, Delete), `UploadService`, `CinemaContextService`, `ShellService`, `DevToolsService`, `DemoAccounts`.
- Domain/Rules (có unit test): `OtpRules`, `PasswordRules`, `DeleteRules` (Category, Drink, Staff, CanSetStaffActive), `Paging`, `RoleRouting` (HomePath, AreaOfPath, LayoutOf, CanReturnTo). `FieldErrors` cho VALIDATION_FAILED theo ô.
- Infrastructure: `Fmt`, `StatusDisplay`, `QrSvg` (QRCoder SVG), `AuthOptions`, `EmailOptions`, `DemoOptions`, `UploadOptions`; Web: `ApiResponse`, `RequestKinds`, `ApiErrorMiddleware`, `AntiforgeryResultFilter`, `DemoToolsMiddleware`, `MustChangePasswordMiddleware`, `Flash`, `QueryUrl`.
- DAL thêm: `UserDAL.SearchStaff, EmailExists, UpdatePassword, UpdateProfile, UpdateStaff, CountBillsCreated, CountOperationalReferences, DeleteDependents, MaxMemberSequence`; `CategoryDAL.Search, GetActive, CountDrinks, NameExists, SetActive`; `DrinkDAL.Search, CountBillLines, SetActive, CountByImage`; `ProductRecipeDAL.GetByDrink, DeleteByDrink, CountByDrink`; `RawMaterialDAL.GetActiveByCinema`; `CinemaDAL.GetActive`; `MovieDAL.CountByPoster, GetActive`; `PasswordResetOtpDAL.GetLatestForUser, ExpireAllForUser, IncrementFailed, MarkUsed`; `EmailOutboxDAL.GetLatest, Count, DeleteAll`; `SupervisorApprovalDAL.CountPending, ListPending`; `PosShiftDAL.GetOpenByUser`. Bản ghi đọc ở `Entities.ReadModels`.
- Giao diện: tokens.css, app.css, 4 layout, partial `_PageHeader, _AgeBadge, _StatusBadge, _EmptyState, _Pagination, _FilterBar, _MoneySummary, _Stepper, _SeatMap` và các partial phụ; `ui.js` (toast, confirm, pin, api, loadRegion, busy, connection, holdTimer, money, dateTime, shake, status).

Sự kiện SignalR: chưa phát sự kiện nào (realtime từ Phase 4).

### Phase 3

Route và quyền (bảng đầy đủ ở docs/generated/routes.md):
- EmployeeOrManager: `/admin/movies` (nhận `?partial=1`), `/admin/movies/{id}`.
- ManagerOnly: `/admin/movies/create`, `/{id}/edit` (GET, POST), `/{id}/delete-check` (GET JSON), `/{id}/delete`, `/{id}/set-active` (POST JSON); toàn bộ `/admin/distributors…` (cùng các action); `/admin/auditoriums` (`?cinemaId=`), `/create`, `/{id}/edit`, `/{id}/design` (UI A4), `/{id}/preview` và `/{id}/layout` (POST JSON, tham số `layout` là chuỗi JSON {rows, cols, cells:[{r, c, t, h, m}]}), `/{id}/delete-check`, `/{id}/delete`, `/{id}/set-active`; `/admin/pricing` (GET, POST, ô `amounts[RuleType.RuleKey]`), `/admin/pricing/audiences` (GET, POST, ô `discounts[AudienceType]`), `/admin/pricing/holidays` (GET, POST thêm), `/admin/pricing/holidays/{yyyy-MM-dd}/delete` (POST JSON), `/admin/pricing/try` (GET); `/scheduler` (UI A5, `?cinemaId=&date=yyyy-MM-dd`), `/scheduler/create`, `/scheduler/{id}/edit`, `/scheduler/check` và `/scheduler/save` (POST JSON: id, auditoriumId, movieId, startTime, languageFormat), `/scheduler/{id}/delete-check`, `/scheduler/{id}/delete`.

Tên mới thêm vào hợp đồng: hành động nhật ký C3-39 `PRICING_UPDATED` (EntityType "PricingRules" với EntityId là rạp, hoặc "AudienceDiscounts"); hằng `ShowtimeLock.LockedHasTickets` cho cờ C3-37. Mã lỗi C8-06 có trong `ErrorCodes` (AGE_RESTRICTED, CHILD_CURFEW, GUARDIAN_REQUIRED dùng ở AgeRules, sẽ được POS gọi ở Phase 4). Lỗi chặn của suất trả mã của lỗi đầu tiên kèm `details.issues` [{severity, code, message, field}] và `details.fields`.

Lớp chính:
- Domain/Rules (có unit test): `AgeRules`, `ScheduleConstraintChecker` (kèm `ScheduleMovie`, `ScheduleSlot`, `KdmWindow`, `ScheduleContext`, `ScheduleIssue`, `ScheduleWarningCode`), `VietnameseFilmQuota`, `ShowtimeLock`, `AuditoriumLayout` (Validate, Build, SuggestHouseSeats, IsStructuralChange, Stats, HeatLevel), `DeleteRules.Movie/Distributor/Auditorium/Showtime`.
- Services: `MovieService`, `DistributorService`, `AuditoriumService` (Create, UpdateInfo, Preview, SaveLayout, Delete), `PricingService` (Table, SaveTable ghi AuditLogs, Discounts, SaveDiscounts, Holidays, AddHoliday, DeleteHoliday, Try), `ScheduleService` (Day, Check, Save trong transaction có khóa, Delete).
- DAL thêm (DAL/CatalogDAL.Phase3.cs): `MovieDAL.Search, GetActiveByTitle, CountShowtimes, CountTickets, SetActive`; `DistributorDAL.Search, GetActiveOrId, CountMovies, NameExists, SetActive`; `FilmRentalRuleDAL.GetByMovie, DeleteByMovie`; `KdmLicenseDAL.GetByMovie, GetByMovieCinema, DeleteByMovie`; `AuditoriumDAL.GetRows, GetActiveByCinema, GetByCinema, CountShowtimes, CountFutureShowtimesWithSales, NameExists, SetActive`; `SeatDAL.GetByAuditorium, IdsWithTickets, MaxPairGroupId, SetTemporaryLabels, SetMaintenance, DeleteByAuditorium`; `ShowtimeSeatDAL.DeleteForSeat, AddSeatToFutureShowtimes, SyncHouseSeatsForFutureShowtimes, DeleteByShowtime, CountLocking`; `ShowtimeDAL.ListForCinema, GetRow, ActiveNear, LockAuditoriumRange, UpcomingForMovie, CountVietnameseShare, CountTickets, CountCorporateBookings, AllRows`; `PricingRuleDAL.GetByCinema`; `HolidayDAL.GetByYear, Years`. Bản ghi đọc `MovieRow, DistributorRow, AuditoriumRow, KdmRow, ShowtimeRow`.
- Giao diện: CSS heatmap `.heat-1…5`, `.page-tabs`, `.date-nav`, `.check-grid`, `.issue-list`, `.schedule-group`, `.sold-meter`, `.price-grid`, `.designer`, `.tool-btn`, `.dcell`; JS `pages/movie-form.js`, `auditorium-designer.js`, `showtime-form.js`, `pricing.js`; ô `date-field` hiển thị cả ngày giờ dd/MM/yyyy HH:mm; vùng kéo thả nhận `data-subject`, `data-noun`.

Sự kiện SignalR: chưa phát sự kiện nào (realtime từ Phase 4).

### Phase 4

Route và quyền (bảng đầy đủ ở docs/generated/routes.md):
- PosStaff: `/pos`, `/pos/index` (UI P1), `/pos/open-shift` (GET, POST; mở ca cần ShiftManager hoặc Manager), `/pos/close-shift` (GET, POST; đóng ca mù), `/pos/shift-report/{id}`, `/pos/print?billId=` (vé khổ 80mm), `/pos/my-bills`; POST JSON: `/pos/init`, `/pos/select-showtime`, `/pos/hold-seats`, `/pos/extend-hold`, `/pos/set-audience`, `/pos/set-guardian`, `/pos/update-quantity`, `/pos/lookup-member`, `/pos/apply-voucher`, `/pos/apply-points`, `/pos/checkout`, `/pos/cancel`, `/pos/request-void` (PIN quản lý), `/pos/scan`; GET JSON: `/pos/cart`, `/pos/seat-map`, `/pos/drink-stock`, `/pos/payment-qr`.
- ManagerOnly: `/admin` (UI A1), `/admin/reports` (UI A6, `?tab=revenue|vietnamese|occupancy|shifts&preset=&from=&to=`), `/admin/reports/export` (CSV), `/admin/reports/settlement` (`?movieId=&week=`), `/admin/reports/settlement/export`; `/admin/bills` (UI A8, lọc ngày, trạng thái, kênh, rạp, nhân viên, mã), `/admin/bills/export`, `/admin/bills/{id}`, `/admin/bills/{id}/print`, `/admin/bills/{id}/cancel` (POST JSON).
- SignalR: hub `/hubs/cinema`, phương thức `JoinShowtime(showtimeId)`, `LeaveShowtime(showtimeId)`, nhóm `showtime_{id}`.

Tên mới thêm vào hợp đồng: AuditAction `VOID_BILL`, `ORPHAN_SEAT_OVERRIDE` (C3-39); mã lỗi C8-07 trong `ErrorCodes` (SEAT_TAKEN, ORPHAN_SEAT, SWEETBOX_PAIR, WHEELCHAIR_CONFIRM_REQUIRED, SALES_CLOSED, SHIFT_NOT_OPEN, SHIFT_HAS_WAITING_BILLS, STOCK_INSUFFICIENT, APPROVAL_REQUIRED, PIN_INVALID, PIN_LOCKED, VOUCHER_INVALID, POINTS_INVALID, PAYMENT_EXPIRED). Cột mới: `Bills.HoldExtended` (BIT, đã gia hạn giữ ghế một lần), `ShowtimeSeats.HoldAudienceType` (đối tượng vé đã chọn cho ghế đang giữ).

Lớp chính:
- Domain/Rules (có unit test): `SeatHoldRules` (EffectiveStatus, CanHold, SalesDeadline, IsSalesOpen, HoldExpiry), `SweetboxRule`, `OrphanSeatValidator`, `BillStateMachine`, `CashDropCalculator`, `LoyaltyCalculator`, `PricingCalculator`, `CodeGenerator`.
- Services: `SeatHoldService` (Hold trong transaction có khóa, SweepExpired), `BillService` (Ensure, Recalculate, SetAudience, SetGuardian, UpdateQuantity, LookupMember, ApplyVoucher, ApplyPoints, ExtendHold, CancelWaiting, Checkout, VoidPaid), `SeatMapService`, `TicketPricer`, `InventoryService`, `LoyaltyService`, `StatisticsService` (Dashboard, Revenue, VietnameseShares, Occupancy, Shifts, Settlement, Csv), `BillQueryService`, `RealtimeNotifier`; nền `Background/SeatHoldSweeper`.
- DAL thêm (DAL/SalesDAL.Phase4.cs, DAL/StatisticDAL.Phase4.cs): `ShowtimeSeatDAL`, `ShowtimeDAL`, `BillDAL`, `BillDetailDAL`, `TicketDAL`, `PaymentDAL`, `PosShiftDAL`, `UserDAL` (PIN, Supervisors), `VoucherDAL`, `PointTransactionDAL`, `RawMaterialDAL`, `SupervisorApprovalDAL`, `StatisticDAL` (Kpi, VietnameseShares, OccupancyByShowtime, LowStock, KdmExpiring); bản ghi đọc ở Entities/ReadModels.Phase4.cs.
- Giao diện: views `Pos/*`, `Bills/*`, `Statistics/*`, partial `_Chart`, `_VietnameseShare`; JS `seatmap.js`, `realtime.js`, `charts.js`, `pages/pos.js`, `shift.js`, `bills.js`, `my-bills.js`, `reports.js`, `void-bill.js`; client SignalR tự host ở `wwwroot/lib/signalr`.

Sự kiện SignalR: `SEAT_MAP_UPDATED` gửi nhóm `showtime_{id}` mỗi khi giữ, nhả, hết hạn giữ, bán hoặc hủy ghế (payload: showtimeId, changes [{seatId, status, holdExpiresAt, holdBillId}]).

### Phase 5

Route và quyền (bảng đầy đủ ở docs/generated/routes.md):
- AllowAnonymous: `/` (UI C1, `?tab=now|soon`), `/movies`, `/movies/{id}` (UI C2, `?date=&format=&language=&cinemaId=`), `/showtimes`, `/menu`, `/quick-buy` (JSON cho thanh mua vé nhanh); `/booking/{showtimeId}` (UI C3, xem được sơ đồ khi chưa đăng nhập), `/booking/{showtimeId}/seat-map`, `/booking/{showtimeId}/suggest` (`?persona=&quantity=`); `/payment-sim/pay?billId=` (trang mô phỏng), `/payments/webhook` (POST, không Anti-Forgery, xác thực bằng header `X-Signature` HMAC-SHA256).
- CustomerOnly: `/booking/{showtimeId}/cart`, POST JSON `/booking/{showtimeId}/hold`, `/quantity`, `/voucher`, `/points`, `/confirm`, `/checkout`, `/cancel`; `/booking/pay/{code}` (màn hình QR), `/booking/pay/{code}/status`; `/tickets/{billCode}` (UI C4), POST `/tickets/{billCode}/cancel` (tự hủy); `/account` (UI C5, `?tab=tickets|wallet|voucher|notifications|profile`), POST `/account/profile` (cập nhật hồ sơ của khách), POST `/account/notifications/read`. Khách vào `/Login/Profile` được chuyển tới `/account?tab=profile`.
- SignalR: thêm `JoinBill(billId)`, `LeaveBill(billId)` (chỉ chủ hóa đơn hoặc nhân viên), nhóm `bill_{id}`.

Tên mới thêm vào hợp đồng: mã lỗi C8-08 trong `ErrorCodes` (AGE_CONFIRM_REQUIRED, SELF_CANCEL_NOT_ALLOWED, WALLET_INSUFFICIENT; PAYMENT_EXPIRED dùng lại từ Phase 4); cấu hình C7-06 `Payment:WebhookSecret` (để trống thì app sinh khóa ngẫu nhiên mỗi lần chạy, xem `Infrastructure/PaymentOptions`); sự kiện C6-03 `PAYMENT_STATUS_CHANGED`. Không thêm bảng hay cột.

Lớp chính:
- Domain/Rules (có unit test, mỗi lớp một file): `ClusterFinder` (điểm từng ghế theo persona, tìm cụm ghế liền nhau, đoạn ghế), `SelfCancelRules` (điều kiện tự hủy, hạn chót, số lần trong tháng), `OnlineAgeRules` (câu xác nhận độ tuổi, giờ chiếu cho trẻ em, nhắc suất muộn), `WebhookSignature` (HMAC-SHA256 hex), `TierProgressCalculator` (tiến độ hạng thành viên).
- Services: `OnlineBookingService` (Ensure, Hold, UpdateQuantity, ApplyVoucher, ApplyPoints, Confirm, Checkout bằng VietQR hoặc ví, HandleWebhook, SelfCancelCheck, SelfCancel), `SeatSuggestService` (gợi ý theo persona, gợi ý suất khác khi suất này không còn cụm đẹp), `WalletService` (Debit, Credit), `RealtimeNotifier.PaymentStatusChanged`; `InventoryService.ReserveForBill` giữ nguyên liệu cho đơn online.
- Lớp giao diện: `Controllers/CustomerCatalogPages` (dựng ViewModel trang chủ, danh sách phim, chi tiết phim, lịch chiếu, thực đơn); controller `CustomerController`, `BookingController`, `PaymentController`, `TicketsController`, `AccountController`.
- DAL thêm (DAL/CustomerDAL.Phase5.cs): lịch chiếu cho khách, đếm lần tự hủy trong tháng, `FailPending` cho thanh toán chờ, thông báo (CountUnread, MarkAllRead), ví; bản ghi đọc ở Entities/ReadModels.Phase5.cs.
- Giao diện: views `Customer/*` (Movies, Movie, Showtimes, Menu, `_Schedule`, `_ScheduleFilter`), `Booking/*`, `Payment/Sim`, `Tickets/*`, `Account/*`; JS `pages/booking.js`, `pay.js`, `payment-sim.js`, `quick-buy.js`, `ticket.js`, `account.js`; menu web khách mở khóa Phim, Lịch chiếu, Bắp nước, Ưu đãi.

Sự kiện SignalR: `PAYMENT_STATUS_CHANGED` gửi nhóm `bill_{id}` khi thanh toán thành công, thất bại hoặc đến muộn phải hoàn tiền (payload: billId, code, status, cancelReason, message, ticketUrl); màn hình QR nhận sự kiện rồi tự chuyển sang vé.

### Phase 6

Route và quyền (bảng đầy đủ ở docs/generated/routes.md):
- AllowAnonymous: `/pos/customer-display?terminal=POS-01&cinemaId=` (UI P2; `/state`, `/seat-map`), `/lobby/pickup-board?cinemaId=` (UI P5; `/data`), `/lobby/digital-signage?cinemaId=` (UI P6; `/rows`).
- GateStaff: `/pos/gate-inspector` (UI P3), POST `/scan`, `/admit`, `/cleaned`, GET `/cleaning`.
- PosStaff: `/pos/fnb-kds` (UI P4), GET `/orders`, POST `/fnb-kds/{billId}/ready`, `/served`, `/fnb-kds/pickup` (quét QR đơn online); `/pos/wastage` (lập phiếu hủy hàng); `/pos/swap-seat` (GET màn hình, POST đổi ghế), POST `/swap-seat/find`, `/swap-seat/quote`, GET `/swap-seat/seat-map`; POST `/pos/request-void-remote`.
- SupervisorOrManager: `/ops` (UI A7), `/ops/live` (khối vận hành của /admin), POST `/ops/approvals/{id}/approve|reject`, GET `/ops/approvals/pending`, POST `/ops/house-seats/{showtimeId}/release`, `/ops/wastage` (`?status=`), `/ops/wastage/{id}`, POST `/approve|reject`, `/ops/incident` (GET, POST).
- ManagerOnly: `/admin/materials` (danh sách, `create`, `{id}/edit`, `{id}/set-active`, `import`), `/admin/corporate` (danh sách, `create?type=PRIVATE_SCREENING|VOUCHER_BATCH`, `{id}`, `{id}/vouchers.csv`), `/admin/reports?tab=wastage`.
- CustomerOnly: POST `/tickets/{billCode}/im-here` ("Tôi đã tới rạp").
- TicketInspector đăng nhập xong về `/pos/gate-inspector`; `/ops` thuộc khu vực quản trị.

SignalR (CinemaHub): `JoinTerminal(code, isDisplay)` (khóa máy `{CinemaId}-{TerminalCode}`), `JoinAuditorium`, `JoinKds(cinemaId, isBoard)`, `JoinOps`, `JoinManagerAlerts`, `JoinCinema` cùng các hàm Leave tương ứng; sự kiện POS_SHOWTIME_SELECTED, POS_CART_UPDATED, POS_PAYMENT_QR, POS_PAYMENT_DONE, POS_RESET, APPROVAL_DECIDED, CHECKIN_UPDATED, FNB_STATUS_CHANGED, OCCUPANCY_UPDATED, KDS_ORDER_QUEUED, KDS_ORDER_UPDATED, SHOWTIME_OPS_CHANGED, APPROVAL_REQUESTED, STOCK_LOW, PRODUCT_SOLD_OUT, PRODUCT_AVAILABLE (đúng tên mục 6).

Tên mới thêm vào hợp đồng: AuditAction SEAT_SWAPPED, HOUSE_SEAT_RELEASED, SHOWTIME_CANCELLED, WASTAGE_APPROVED, WASTAGE_REJECTED (C3-39). Không thêm bảng hay cột.

Lớp chính:
- Domain/Rules (có unit test Phase6RulesTests): `ShowtimeOpsRules`, `GateRules`, `CheckInRules`, `KdsRules`, `InventoryAlertRules`, `WastageRules`, `SeatSwapRules`, `IncidentRules`, `CorporateRules`, `ApprovalRules`, `HouseSeatRules`.
- Background: `ShowtimeClockService` (mỗi 30 giây: ShowtimeOpsService.Tick, ApprovalService.ExpireOverdue).
- Services: `ShowtimeOpsService`, `GateService`, `KdsService`, `ApprovalService`, `OpsManagementService` (ghế nội bộ, hủy suất do sự cố, doanh nghiệp), `SeatSwapService` (giữ ghế mới qua `SeatHoldService.HoldForSwap`), `CustomerDisplayService`, `InventoryService` phần Phase 6 (nhập kho, phiếu hủy hàng, bắt đầu làm món online, PublishAlerts); test tích hợp Phase6ServiceTests.
- Lớp giao diện: `Controllers/OpsDashboardPages`; controller `GateController`, `KitchenController`, `LobbyController`, `OpsController`, `MaterialsController`, `CorporateController`; JS `approvals.js` (popup A7 trong layout quản trị), `pages/gate.js`, `kds.js`, `lobby.js`, `customer-display.js`, `ops.js`, `swap-seat.js`, `stock-form.js`, `incident.js`.

### Phase 7

Route và quyền:
- AllowAnonymous: `/assistant/message` (POST JSON: text, showtimeId), `/assistant/state` (GET), `/assistant/reset` (POST); widget C7 trên mọi trang của layout web khách; liên kết mở thẳng `/#tro-ly`, `/#tro-ly?q=…`.
- CustomerOnly: POST `/assistant/hold` ("Giữ ghế & thanh toán ngay", trả `redirect` tới `/booking/{showtimeId}?step=4`).
- ManagerOnly: `/scheduler?view=table|gantt` (UI A5), POST `/scheduler/validate` (id, auditoriumId, movieId, startTime, languageFormat, `pending` JSON [{id, auditoriumId, movieId, startTime, languageFormat}]), POST `/scheduler/ai/generate` (cinemaId, fromDate, days 1–7, movieIds[], auditoriumIds[]), POST `/scheduler/ai/apply` (cinemaId, `drafts` JSON); liên kết `/scheduler#ai-xep-lich`.

Tên mới thêm vào hợp đồng: cookie `fpc_agent` (mã phiên hội thoại của trợ lý, HttpOnly); khóa sessionStorage `fpc-ai-draft-{cinemaId}` (bản nháp AI ở trình duyệt quản lý); cấu hình C7-07 đọc qua lớp `LlmOptions`. Không thêm bảng hay cột (dùng Movies.AiDemandScore, Showtimes.IsAiGenerated đã có).

Lớp chính:
- Domain/Rules (có unit test): `NlpParser` (NlpParserTests, 24 câu), `AiDemandScore`, `AiScheduler` (Phase7RulesTests).
- Services: `BookingAgentService` (chọn LlmAgent hoặc LocalAgent, HoldCard), `LocalAgent`, `LlmAgent`, `IBookingTools`/`BookingTools`, `AgentSessionStore`, `AiSchedulerService`; `ScheduleService.ApplyAiDrafts`, `ScheduleService.Check(…, pending)`.
- DAL: `MovieDAL.DemandStats`, `MovieDAL.UpdateDemandScore` (DAL/SchedulerDAL.Phase7.cs).
- Giao diện: `Controllers/AssistantController`, partial `_AssistantWidget`, `Scheduler/_Gantt`, `Scheduler/_AiModal`; JS `pages/assistant.js`, `pages/gantt.js`.
