# Kiểm tra độc lập – Phase 4

Agent spec-auditor kiểm 9 lô mã (mỗi lô không quá 20 mã, theo nhóm của lệnh `list 4`): đọc spec, truy vết, code thật, chạy test liên quan và xem ảnh chụp giao diện trong artifacts/ui/phase-4. Ngày 13/11/2026.
Cột "Đã sửa" ghi việc đã làm cho các dòng Thiếu hoặc Sai, ghi chú nhỏ đã xử lý, và kết quả của lượt kiểm lại.

| Mã | Kết luận | Lý do | Đã sửa |
|---|---|---|---|
| S2.1-03 | Đạt | AddSignalR, MapHub<CinemaHub>; client @microsoft/signalr 8.0.7 trong libman và wwwroot/lib/signalr; realtime.js withAutomaticReconnect | – |
| S2.1-05 | Đạt | QRCoder 1.6.0, QrSvg dùng SvgQRCode ở server cho vé in và VietQR mô phỏng | – |
| S2.3-09 | Đạt | Giữ ghế, thanh toán, hủy, hết hạn giữ, mở và đóng ca chạy trong DBUtil.InTransaction ở Services; kho dùng chung transaction | – |
| S2.5-12 | Đạt | PIN 6 số chỉ cho ShiftManager, Manager, lưu băm; sai ManagerPinMaxAttempts lần khóa ManagerPinLockMinutes, PIN_LOCKED/PIN_INVALID; có unit test | – |
| S2.7-06 | Đạt | AuditLogs VOID_BILL (người duyệt, người yêu cầu, lý do) và ORPHAN_SEAT_OVERRIDE trong cùng transaction | – |
| S3.1-03 | Đạt | AgeRules.CheckTicket chặn CHILD_U13, CHILD_13_15 theo nhãn phim (AGE_RESTRICTED) khi chọn đối tượng và khi thanh toán | – |
| S3.1-07 | Đạt | CheckGuardian phim K; ô "Có người giám hộ đi cùng" gọi /pos/set-guardian; kiểm lại khi thanh toán | – |
| S3.1-15 | Đạt | POS bán mọi loại vé AudienceDiscounts; server chặn loại PosOnly ở kênh khác | – |
| S3.1-19 | Đạt | Lũy kế từ 01/01 theo ngày vận hành, bỏ suất hủy, từng rạp và toàn chuỗi, vạch mốc VietnameseFilmMinShare | – |
| S3.1-20 | Đạt | Tỷ lệ khung 18:00–22:00 theo VietnameseFilmPriorityStart/End, hiện "(tham khảo)" | – |
| S3.1-23 | Đạt | Danh sách suất, vé in và hóa đơn tạm tính dùng nhãn tuổi age-badge kèm giải thích | – |
| S3.2-02 | Đạt | OrphanSeatValidator.Segments chia đoạn theo hàng; ô trống, tường, xe lăn, ghế đôi là biên; có unit test | – |
| S3.2-03 | Đạt | SweetboxRule theo PairGroupId trả SWEETBOX_PAIR; JS chọn cả cặp; giá từng ghế; hủy trả cả cặp | – |
| S3.2-04 | Đạt | WHEELCHAIR_CONFIRM_REQUIRED ở server; POS mở hộp xác nhận rồi gửi wheelchairConfirmed | – |
| S3.2-09 | Đạt | EffectiveStatus ưu tiên MAINTENANCE nên CanHold từ chối ở mọi suất; có test | – |
| S3.2-10 | Đạt | Ghế bảo trì đã bán ở suất tương lai đứng đầu cảnh báo dashboard A1 "Ghế cần đổi" | – |
| S3.4-05 | Đạt | DiscountAllocator phân bổ voucher và điểm theo tỷ lệ, dư vào dòng cuối, lưu Tickets.FinalPrice, BillDetails.LineFinalAmount; có unit test | – |
| S3.5-01 | Đạt | Hóa đơn waiting là đơn vị giữ ghế: ShowtimeSeatDAL.Hold gán HoldBillId, HoldExpiresAt; Bills.HoldExpiresAt đặt ở lần giữ đầu | – |
| S3.5-02 | Đạt | /pos/hold-seats gọi BillService.Ensure lấy hoặc tạo hóa đơn waiting của ca tại máy | – |
| S3.5-04 | Đạt | SeatHoldRules.SalesDeadline, IsSalesOpen theo kênh (PosSalesCutoffMinutes, OnlineSalesCutoffMinutes), SALES_CLOSED; có test | – |
| S3.5-05 | Đạt | LockForShowtime WITH (UPDLOCK, HOLDLOCK) trong InTransaction; test đồng thời một bên thắng, bên kia SEAT_TAKEN | – |
| S3.5-06 | Đạt | EffectiveStatus: MAINTENANCE trước, HELD hết hạn coi là AVAILABLE; có unit test | – |
| S3.5-07 | Đạt | CanHold chỉ nhận AVAILABLE hoặc HELD của chính hóa đơn; SEAT_TAKEN kèm seats, seatIds | – |
| S3.5-08 | Đạt | Thứ tự SWEETBOX_PAIR → WHEELCHAIR_CONFIRM_REQUIRED → OrphanSeatValidator; bỏ qua chỉ PosStaff ở POS có lý do, ghi AuditLogs | – |
| S3.5-09 | Đạt | UPDATE HELD có điều kiện, so số dòng, sai thì SEAT_TAKEN và rollback; ghế bỏ chọn về AVAILABLE | – |
| S3.5-10 | Đạt | SEAT_MAP_UPDATED phát sau commit, chỉ ghế thay đổi; rỗng thì không gửi; có test | – |
| S3.5-11 | Đạt | HoldExpiry giữ hạn cũ khi đổi ghế; ExtendHold một lần (HoldExtended) cộng SeatHoldExtendMinutes; có test | – |
| S3.5-12 | Đạt | SeatHoldSweeper BackgroundService theo HoldSweepSeconds; SweepExpired hủy EXPIRED, trả ghế, phát sự kiện | – |
| S3.5-13 | Đạt | Sơ đồ ghế và đếm ghế trống coi HELD hết hạn là trống; có test | – |
| S3.11-02 | Đạt | Khả dụng = ActualStock − ReservedStock; StockCalculator.MaxPortions có unit test; EnsureAvailable gộp theo nguyên liệu, STOCK_INSUFFICIENT | – |
| S3.11-05 | Đạt | Checkout finish gọi ConsumeForBill cùng transaction: khóa, kiểm lại, ghi CONSUME, trừ ActualStock | – |
| S3.11-12 | Đạt | Lưới POS hiện "Tạm hết", khóa nút; /pos/drink-stock cập nhật; API chặn | – |
| S3.12-01 | Đạt | StatisticsController ManagerOnly; StatisticDAL gọi 4 stored procedure với tham số @1, @2 | – |
| S3.12-10 | Đạt | Tab tỷ lệ phim Việt, lấp đầy theo suất, biên bản ca; trạng thái rỗng; CSV riêng mỗi tab | – |
| S3.12-12 | Đạt | Chart.js 4.4.9, _Chart có trạng thái rỗng, 5 mốc nhanh, Xuất CSV. Ghi chú: mốc nhanh tính ngày ở trình duyệt; tham số preset= | Mốc nhanh chuyển sang server tính theo IClock qua tham số preset (StatisticsService.Range) |
| S3.5.1-01 | Đạt | Ghế chiếm gồm BOOKED, HOUSE_HOLD, MAINTENANCE, HELD còn hạn của hóa đơn khác và ghế đang chọn; WHEELCHAIR, SWEETBOX làm biên (SeatHoldService, OrphanSeatValidator) | – |
| S3.5.1-02 | Đạt | OrphansOf chỉ bắt ghế trống kẹp giữa hai bên chiếm/biên và có ít nhất một bên thuộc lựa chọn | – |
| S3.5.1-03 | Đạt | Thử mọi vị trí k ghế liền trong đoạn, ngoại lệ đoạn ngắn; PosStaff bỏ qua kèm lý do, ghi AuditLogs ORPHAN_SEAT_OVERRIDE | – |
| S3.5.1-04 | Đạt | ORPHAN_SEAT kèm orphans, suggestion, canOverride; lời nhắn "Bạn chọn thêm X hoặc dời sang Y–Z nhé"; pos.js xử lý | – |
| S3.5.1-05 | Đạt | Test Orphan_SeatLeftBetweenTwoSoldSeats đạt | – |
| S3.5.1-06 | Đạt | Test Orphan_SeatNextToAisle đạt, kiểm lời nhắn "sát lối đi" | – |
| S3.5.1-07 | Đạt | Test Orphan_SeatNextToWall đạt, kiểm lời nhắn và gợi ý | – |
| S3.5.1-08 | Đạt | Test Orphan_SkippingOneSeat_G5AndG7 đạt, gợi ý G5–G6 | – |
| S3.5.1-09 | Đạt | Test Orphan_PreExistingSingleSeatIsNotTheSelectorsFault đạt | – |
| S3.5.1-10 | Đạt | Test Orphan_ShortSegmentException_NoOtherPlacement đạt với đoạn 3 và 2 ghế | – |
| S3.5.1-11 | Đạt | Test Orphan_WheelchairAndSweetboxNextToSelectionAreBoundaries đạt | – |
| S3.13-01 | Đạt | LoyaltyCalculator.EarnPoints làm tròn xuống trên Total sau giảm; cộng khi finish trong transaction, thu hồi khi hủy | – |
| S3.13-02 | Đạt | TierFor theo chi tiêu 12 tháng (CustomerSpend), xét lại mỗi lần cộng điểm | – |
| S3.13-03 | Đạt | 1 điểm = PointValueVnd, tối đa MaxPointsRedeemPercent, vượt trả POINTS_INVALID; tính lại hạ số điểm đổi | – |
| S3.13-05 | Đạt | Kiểm Active, hạn, lượt dùng, chủ sở hữu, MinBillAmount với lý do riêng; VOUCHER_INVALID; kiểm lại khi thanh toán | – |
| S5-01 | Đạt | Hub /hubs/cinema, nhóm showtime_{id}, SEAT_MAP_UPDATED chỉ gửi ghế đổi đúng CONTRACTS mục 6 | – |
| S5-02 | Đạt | realtime.js withAutomaticReconnect, onreconnected join lại nhóm và tải lại bằng HTTP | – |
| S5-03 | Đạt | _ConnIndicator trong _LayoutPos, ui.connection.set cho 3 trạng thái, có CSS | – |
| S7-03 | Đạt | Phase4RulesTests có test OrphanSeatValidator, SweetboxRule, LoyaltyCalculator, CashDropCalculator, SettlementCalculator; 18/18 đạt | – |
| S3.6-01 | Đạt | Hóa đơn quầy Channel POS qua BillDAL.Insert; schema cho POS, ONLINE, CORPORATE | – |
| S3.6-02 | Đạt | Mỗi ghế một Ticket có QR riêng chống trùng; UQ_Tickets_QrCode, UX_Tickets_ActiveSeat | – |
| S3.6-03 | Đạt | Bắp nước thêm, sửa, xóa qua BillDetailDAL (incr, decr, delete) | – |
| S3.6-04 | Đạt | Bills.ShowtimeId NULL được; về NULL khi hóa đơn không còn ghế | – |
| S3.6-05 | Đạt | Mọi đổi Status qua BillStatusService.ChangeStatus → BillStateMachine.EnsureTransition; finish → VOIDED_BY_POS chỉ SupervisorOrManager | – |
| S3.6-06 | Đạt | Mutate khóa hóa đơn, chặn khi không waiting, chạy thay đổi và Recalculate trong cùng InTransaction | – |
| S3.6-07 | Đạt | Đủ route của slide (index, init, update-quantity, checkout, cancel) và route thêm; my-bills lọc người tạo, ngày | – |
| S3.6-08 | Thiếu | (1) Trình duyệt chưa có cookie mã máy vẫn nhận ca OPEN ở máy khác và tự gán cookie; (2) OpeningCashFloat không bắt buộc (ô ẩn mặc định 0) | Resolve chỉ nhận ca khi cookie đúng mã máy của ca (ShiftService.Resolve, PosController.TryContext); đóng ca dùng ResolveOwn; OpeningCashFloat kiểu long?, bỏ trống báo lỗi "Bạn nhập tiền đầu ca"; thanh đổi vai trò demo đặt cookie mã máy của ca đang mở; thêm test Shift_RequiresTerminalCookieAndOpeningFloat (BUG-027). Kiểm lại: Đạt |
| S3.6-09 | Đạt | Hộp tiền mặt có ô tiền khách đưa, bàn phím số, tiền thối tính trực tiếp; server chặn thiếu tiền | – |
| S3.6-10 | Đạt | /pos/payment-qr QR mô phỏng; "Đã nhận tiền" checkout VIETQR_SIM, ProviderTxnId SIM | – |
| S3.6-13 | Đạt | Voucher và điểm tính trong Recalculate; thanh toán tăng lượt voucher, trừ điểm | – |
| S3.6-14 | Đạt | Finish tạo Ticket kèm QR; ShowtimeSeatDAL.Book HELD → BOOKED, kiểm đúng 1 dòng | – |
| S3.6-15 | Đạt | FnbStatus PREPARING kèm số gọi; ConsumeForBill ghi CONSUME, trừ ActualStock; cộng điểm, cùng transaction | – |
| S3.6-16 | Đạt | Checkout phát SEAT_MAP_UPDATED sau commit, trả printUrl; trang in khổ 80mm (@page 80mm); có test | – |
| S3.6-18 | Đạt | Ô "In vé đơn online" gọi /pos/scan, FindByScan theo mã hóa đơn hoặc vé, kiểm rạp và trạng thái finish | – |
| S3.6-20 | Đạt | _VoidModal có lý do và chọn quản lý; void-bill.js mở bàn phím PIN; APPROVAL_REQUIRED, PIN_INVALID, PIN_LOCKED | – |
| S3.6-23 | Thiếu | Hủy từ /admin/bills khi quản lý không có ca mở: khoản hoàn tiền mặt ghi PosShiftId NULL, không trừ vào két ca nào | VoidPaid chặn hoàn tiền mặt khi không có ca OPEN (SHIFT_NOT_OPEN), cả transaction rollback; test Void_NeedsSupervisorPin… kiểm thêm trường hợp này (BUG-028). Kiểm lại: Đạt |
| S3.6-24 | Đạt | VoidPaid chặn khi CountCheckedIn > 0 trong transaction; Details ẩn nút; test Void_BlockedWhenATicketCheckedIn | – |
| S3.6-25 | Đạt | Lọc ngày, trạng thái, kênh, rạp (ô chọn rạp), nhân viên, mã; SQL tham số; phân trang; có test | – |
| S3.6-26 | Đạt | Chi tiết có bảng vé, bảng món, giảm giá, tổng, badge, lý do hủy, nhân viên, thời gian, dòng thời gian thanh toán và phê duyệt | – |
| S3.6-27 | Thiếu | Print và Cancel ở /admin/bills không giới hạn theo rạp của quản lý gắn rạp | BillsController.InScope dùng cho Details, Print, Cancel (BUG-029). Kiểm lại: Đạt |
| S3.6-28 | Đạt | Close đếm hóa đơn waiting trong transaction, SHIFT_HAS_WAITING_BILLS; view khóa nút; có test | – |
| S3.6-29 | Đạt | Đếm số tờ theo mệnh giá, shift.js tự cộng; không hiện ExpectedCash trước khi xác nhận | – |
| S3.6-30 | Đạt | CashDropCalculator.Close logic thuần: Expected = đầu ca + thu − hoàn; Discrepancy = đếm − Expected; có test | – |
| S3.6-31 | Đạt | Biên bản hiện thừa/thiếu, có nút in; PosShiftDAL.Close chỉ khi OPEN, không có route sửa | – |
| C5-03 | Đạt | BillStateMachine chỉ cho ShiftManager, Manager chuyển finish → VOIDED_BY_POS; cả hai đường hủy bắt buộc PIN | – |
| C6-01 | Đạt | Hub /hubs/cinema chỉ có JoinShowtime, LeaveShowtime; kênh 1 cho mọi người | – |
| C6-02 | Đạt | RealtimeNotifier phát SEAT_MAP_UPDATED; seatmap.js JoinShowtime và nhận sự kiện | – |
| C8-07 | Đạt | Đủ mã lỗi trong ErrorCodes, mã nào cũng được dùng; SEAT_TAKEN trả 409; lời nhắn tiếng Việt | – |
| C9-07 | Đạt | PosController [Route("pos")] có index, init, update-quantity, checkout, cancel; action incr/decr/delete | – |
| S8.4-01 | Đạt | Mở ca kiểm máy, tiền đầu ca 0–50 triệu, không trùng ca, transaction, cookie mã máy; suất theo ngày vận hành lọc IsSalesOpen | – |
| S8.4-02 | Đạt | SeatHoldService.Hold đủ bước khóa, SEAT_TAKEN, SWEETBOX_PAIR, xe lăn, ghế mồ côi; phát SEAT_MAP_UPDATED sau commit; có test | – |
| S8.4-03 | Đạt | Đối tượng từng ghế hoặc cả hóa đơn; AGE_RESTRICTED, CHILD_CURFEW, GUARDIAN_REQUIRED; khóa thanh toán khi còn cảnh báo | – |
| S8.4-04 | Đạt | Kiểm kho gộp nguyên liệu, STOCK_INSUFFICIENT; CONSUME khi thanh toán; "Tạm hết"; StockCalculator có test | – |
| S8.4-05 | Đạt | Tra thành viên (F1), VOUCHER_INVALID có lý do, POINTS_INVALID; trừ lượt voucher, cộng trừ điểm khi thanh toán | – |
| S8.4-06 | Đạt | CASH và VIETQR_SIM; tạo vé QR, ghế BOOKED, Payments, finish qua state machine, trang in 80mm | – |
| S8.4-07 | Đạt | Hủy hóa đơn waiting trả ghế; /pos/my-bills lọc ngày, in lại, hủy. Ghi chú: trạng thái rỗng luôn ghi "hôm nay" | Trạng thái rỗng ghi đúng ngày đang xem |
| S8.4-08 | Đạt | Hủy đã thanh toán cần lý do và PIN, chặn khi có vé vào cửa, hoàn tiền, VOIDED, kho, điểm, AuditLogs; đóng ca mù | – |
| S8.4-09 | Đạt | SeatHoldSweeper theo HoldSweepSeconds, hủy EXPIRED, trả ghế, phát sự kiện, lỗi chỉ ghi log | – |
| S8.4-10 | Thiếu | /admin/bills Print, Cancel không giới hạn rạp của quản lý gắn rạp; đối soát cho chọn rạp bất kỳ | BillsController.InScope; Settlement và SettlementExport ép cinemaId theo rạp gắn với quản lý, ẩn "Toàn chuỗi" khi chỉ có một rạp; tab doanh thu theo sp_RevenueByDay toàn chuỗi (quyết định 52). Kiểm lại: Đạt |
| U3-04 | Đạt | Màu từ token, lưới --border, tooltip "125.000 ₫", không 3D, figcaption ghi đo gì và khoảng ngày, trạng thái rỗng | – |
| U7.P7-01 | Đạt | /pos/open-shift, /pos/close-shift, /pos/shift-report/{id} dùng layout ops; nút Đóng ca trên thanh POS | – |
| U7.P7-02 | Đạt | Wizard 2 bước, bàn phím số lớn có 000 và xóa, ô tiền tự định dạng | – |
| U7.P7-03 | Đạt | Bảng 9 mệnh giá, JS tự cộng, server tính lại và từ chối mệnh giá lạ | – |
| U7.P7-04 | Đạt | Bước xác nhận nêu hậu quả, có Đếm lại và Xác nhận và đóng ca; không hiện số hệ thống trước | – |
| U7.P7-05 | Đạt | Biên bản so sánh, chênh lệch tô token success/danger, nút In biên bản | – |
| U7.A6-01 | Đạt | 5 mốc nhanh, KPI, 3 biểu đồ, bảng, Xuất CSV; đối soát nổi phần rạp tự chịu. Ghi chú: mốc nhanh tính ngày ở trình duyệt | Mốc nhanh gửi tham số preset, server tính theo IClock |
| U7.P1-01 | Đạt | /pos, /pos/index PosStaff, layout theme ops, cao 100vh, 3 cột ở 1366 và 1920 | – |
| U7.P1-02 | Đạt | Cột 300px, ô tìm, suất nhóm theo phim có poster 40px và nhãn tuổi, chip giờ 3 mức màu, "Chỉ bán bắp nước" | – |
| U7.P1-03 | Đạt | Tab Sơ đồ ghế (panel tối) và Bắp nước (lưới 4 cột, ảnh 64px, lọc danh mục) | – |
| U7.P1-04 | Đạt | Cột 380px, tiêu đề "Hóa đơn tạm tính" | – |
| U7.P1-05 | Đạt | F1 vào ô tra cứu; tra theo mã, điện thoại, email; thẻ thành viên tên, hạng, điểm | – |
| U7.P1-06 | Đạt | Dòng vé có dropdown đối tượng, giá vẽ lại ngay, cảnh báo luật trên dòng, "Áp cho tất cả vé"; có test | – |
| U7.P1-07 | Đạt | Món có − số + và xóa; form voucher, form đổi điểm khi có thành viên | – |
| U7.P1-08 | Đạt | Tổng 32px đậm; hai nút 64px Tiền mặt F4, Chuyển khoản QR; nút ghost Hủy hóa đơn Esc | – |
| U7.P1-09 | Đạt | Dưới 1280px còn 2 cột, cột 1 thành drawer có scrim, Esc đóng | – |
| U7.P1-10 | Đạt | kbd F1, F4, Esc trên nút | – |
| U7.P1-11 | Đạt | Hộp hủy có lý do, chọn quản lý, bàn phím PIN, PIN_INVALID, PIN_LOCKED; duyệt từ xa TODO(Phase 6) | – |
| U7.A1-01 | Đạt | /admin ManagerOnly, _LayoutAdmin | – |
| U7.A1-02 | Đạt | 5 thẻ KPI: doanh thu, vé bán, lấp đầy, bắp nước, tỷ lệ phim Việt | – |
| U7.A1-03 | Đạt | Biểu đồ đường 2 series col-xl-8, top phim thanh ngang col-xl-4 | – |
| U7.A1-04 | Đạt | Khối tỷ lệ phim Việt vạch 20%; cảnh báo KDM, nguyên liệu, ghế bảo trì đã bán; trạng thái rỗng | – |
| U7.A8-01 | Đạt | /admin/bills ManagerOnly, tiêu đề "Hóa đơn" | – |
| U7.A8-02 | Đạt | Lọc đủ, mốc nhanh, cột đủ, tiền căn phải tabular-nums, badge, phân trang, CSV. Ghi chú: nhãn "Đã thanh toán" thay "Hoàn thành" | Ghi quyết định 54 (dùng nhãn của UI mục 6) |
| U7.A8-03 | Thiếu | Cột "Kích cỡ" bảng bắp nước in "Combo"/"Tiêu chuẩn" theo IsCombo vì schema không có kích cỡ, chưa ghi quyết định | Ghi quyết định 55: Drinks không có cột kích cỡ (SPEC 6), mỗi cỡ là một món riêng có cỡ trong tên; cột hiện "Combo" hoặc "Tiêu chuẩn". Kiểm lại: Đạt |
| U8-01 | Đạt | Ghế là button có aria-label, aria-pressed, aria-disabled, roving tabindex. Ghi chú: role grid chưa có row/gridcell | – |
| U8-02 | Đạt | --seat-size 32px, 28px dưới 992px, không nhỏ hơn 24px | – |
| U8-04 | Đạt | Ô lối đi rộng bằng một ghế, nhãn hàng hai đầu | – |
| U8-05 | Đạt | Màn hình cong SVG có quầng, chữ "Màn hình" màu --text-muted | – |
| U8-06 | Đạt | 9 trạng thái có cả màu và ký hiệu | – |
| U8-07 | Đạt | Ghế đôi hai nút dính nhau, hover và chọn cả cặp | – |
| U8-08 | Đạt | Tooltip khi rê và khi focus: loại, nhãn, điểm, giá | – |
| U8-11 | Thiếu | Ghế không rung: animation seat-flash của .seat.is-violation đè animation shake | app.css: .seat.is-violation gộp hai animation (shake, seat-flash); giảm chuyển động vẫn tắt theo khối reduced-motion (BUG-030). Kiểm lại: Đạt |
| U8-12 | Đạt | Đổi màu trong --dur-base 200ms; ghế đang chờ bị lấy thì toast "vừa có người chọn trước" | – |
| U8-13 | Đạt | Phím mũi tên tới ghế gần nhất, Enter/Space chọn | – |
| U9-05 | Đạt | Câu ghế mồ côi đúng mẫu, có test | – |
| U9-06 | Đạt | Câu ghế đôi đúng nguyên văn, có test | – |
| U9-07 | Đạt | Câu ghế bị lấy đúng nguyên văn, POS toast và tải lại sơ đồ, có test | – |
| U9-08 | Đạt | Câu AGE_RESTRICTED đúng nguyên văn, có test | – |
| U9-09 | Đạt | Câu CHILD_CURFEW đúng, giờ từ cấu hình, có test | – |
| U9-10 | Đạt | Câu GUARDIAN_REQUIRED đúng nguyên văn, có test | – |
| U9-14 | Đạt | Câu SHIFT_NOT_OPEN đúng nguyên văn, có test | – |
| U9-15 | Đạt | Câu "{món} tạm hết nguyên liệu.", có test | – |
| U10-04 | Đạt | aria-label đủ mã ghế, loại, trạng thái, điểm, giá. Ghi chú: ra "Ghế G7, Ghế VIP" | Bỏ chữ "Ghế" đầu loại ghế trong aria-label (_SeatMap, seatmap.js) |
| UC14 | Đạt | Mở ca wizard 2 bước (máy, tiền đầu ca bắt buộc), một ca OPEN mỗi người và mỗi máy; đóng ca mù 3 bước, biên bản; có test | – |
| UC15 | Đạt | P1 đủ 3 cột, chip suất, sơ đồ ghế, bắp nước, thành viên, đối tượng, voucher, điểm; checkout một transaction tạo vé QR, ghế BOOKED, kho, điểm, phát sự kiện, trang in; có test | – |
| UC21 | Đạt | /pos/my-bills lọc theo người tạo và ngày, in lại, hủy waiting, hủy đã thanh toán bằng PIN, trạng thái rỗng; có test | – |
| UC37 | Đạt | /admin/bills ManagerOnly: lọc, bảng, CSV, chi tiết, hủy theo state machine với PIN, không sửa số tiền; có test | – |
| UC38 | Đạt | A1 đủ KPI, biểu đồ, phim Việt, cảnh báo; A6 mốc nhanh, 4 tab, CSV; có test | – |
| UC39 | Đạt | Đối soát chọn phim, tuần, rạp; sp_box_office_settlement kiểm chéo SettlementCalculator; phần rạp tự chịu tô nổi; CSV; có test | – |

Tổng lượt đầu: 138 Đạt, 6 Thiếu, 0 Sai. Đã sửa 6 mã Thiếu (BUG-027..030, quyết định 49–56 trong PROGRESS.md) và các ghi chú nhỏ (mốc nhanh tính theo IClock, trạng thái rỗng Hóa đơn của tôi, aria-label ghế). Lượt kiểm lại S3.6-08, S3.6-23, S3.6-27, S8.4-10, U8-11, U7.A8-03: 6 Đạt, 0 Thiếu, 0 Sai.

Kết quả: Đạt
