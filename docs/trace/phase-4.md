# Truy vết yêu cầu – Phase 4 – POS, hóa đơn, thống kê

File do `node tools/spec-check/check.mjs sync` tạo và giữ đồng bộ với spec. Chỉ sửa 3 cột Trạng thái, Bằng chứng, Ghi chú, nên dùng lệnh `node tools/spec-check/check.mjs mark MÃ --ev "…"` thay vì sửa tay. Không thêm, xóa hay sửa cột Mã và Yêu cầu.

- Trạng thái: Chưa, Xong, Bỏ. Chỉ dùng Bỏ khi yêu cầu đã được đổi và PROGRESS.md mục "Thay đổi mô hình" có nhắc mã này, hoặc mã nằm trong tools/spec-check/scope.json. Ghi lý do ở cột Ghi chú.
- Bằng chứng: nhiều mục cách nhau bằng dấu chấm phẩy.
  - Đường dẫn file tính từ gốc repo, có thể thêm #chuỗi phải có trong file. Ví dụ: src/FPolyCinema.Web/Services/SeatHoldService.cs#Hold
  - test:TênHàmTest hoặc test:TênLớp.TênHàm
  - sql:TênBảng hoặc sql:TênStoredProcedure
  - route:/duong-dan
  - ui:MãMànHình (đã có ảnh chụp trong artifacts/ui)
- Dòng Xong phải có ít nhất một bằng chứng là file code, test: hoặc sql:. Yêu cầu có chữ "logic thuần" hoặc "unit test" phải có test:.

| Mã | Yêu cầu | Trạng thái | Bằng chứng | Ghi chú |
|---|---|---|---|---|
| S2.1-03 | SignalR có sẵn trong ASP.NET Core; client @microsoft/signalr 8.0.x. | Chưa |  |  |
| S2.1-05 | QRCoder 1.6.x sinh mã QR dạng SVG ở server. | Chưa |  |  |
| S2.3-09 | Thao tác nhiều bước (giữ ghế, thanh toán, đổi ghế, hủy, kho) nằm trong Services và chạy trong DBUtil.InTransaction. | Chưa |  |  |
| S2.5-12 | PIN quản lý (ShiftManager, Manager): 6 chữ số, lưu dạng băm. Sai ManagerPinMaxAttempts lần thì khóa ManagerPinLockMinut… | Chưa |  |  |
| S2.7-06 | Hủy hóa đơn đã thanh toán; bỏ qua luật ghế mồ côi. | Chưa |  |  |
| S3.1-03 | POS: không bán vé CHILD_U13 cho phim T13 trở lên; không bán vé CHILD_13_15 cho phim T16, T18 (AGE_RESTRICTED). | Chưa |  |  |
| S3.1-07 | POS: hóa đơn có vé CHILD_U13 cho phim K phải có ít nhất một vé ADULT hoặc SENIOR, hoặc thu ngân tích "Có người giám hộ … | Chưa |  |  |
| S3.1-15 | Các loại vé giảm theo luật chỉ bán tại quầy để nhân viên kiểm tra giấy tờ (luật áp dụng khi khách trực tiếp xem tại rạp… | Chưa |  |  |
| S3.1-19 | Dashboard hiện tỷ lệ lũy kế từ đầu năm theo từng rạp và toàn chuỗi (chỉ tính suất không bị hủy), có vạch mốc 20%. | Chưa |  |  |
| S3.1-20 | Tỷ lệ phim Việt trong khung 18:00–22:00 hiện để tham khảo. | Chưa |  |  |
| S3.1-23 | POS: danh sách suất, hóa đơn tạm tính, vé in. | Chưa |  |  |
| S3.2-02 | Đoạn ghế: dãy ghế liền nhau trong một hàng, ngăn cách bởi ô trống hoặc tường. | Chưa |  |  |
| S3.2-03 | SWEETBOX luôn theo cặp cố định (PairGroupId): hai ghế liền nhau cùng hàng. Chọn, giữ, bán, đổi, hủy luôn đi cả cặp (SWE… | Chưa |  |  |
| S3.2-04 | WHEELCHAIR: khi chọn phải xác nhận "Vị trí dành cho khách dùng xe lăn" (WHEELCHAIR_CONFIRM_REQUIRED). | Chưa |  |  |
| S3.2-09 | Ghế đang bảo trì không giữ được ở mọi suất. | Chưa |  |  |
| S3.2-10 | Nếu ghế đã được bán ở một suất tương lai, dashboard quản lý (UI A1) đưa ghế đó vào danh sách "Cần đổi ghế". | Chưa |  |  |
| S3.4-05 | Voucher và điểm giảm ở cấp hóa đơn, rồi được phân bổ theo tỷ lệ giá trị cho từng vé và từng dòng bắp nước (phần dư dồn … | Chưa |  |  |
| S3.5-01 | Hóa đơn là đơn vị giữ ghế: mỗi hóa đơn waiting có HoldExpiresAt, và ShowtimeSeats.HoldBillId trỏ về hóa đơn đó. | Chưa |  |  |
| S3.5-02 | Lần giữ ghế đầu tiên tạo hóa đơn waiting; tại quầy là hóa đơn waiting của thu ngân trên máy đó. | Chưa |  |  |
| S3.5-04 | Kiểm tra suất còn mở bán: online đến FeatureStartTime + OnlineSalesCutoffMinutes, tại quầy đến FeatureStartTime + PosSa… | Chưa |  |  |
| S3.5-05 | Trong DBUtil.InTransaction, chạy SELECT … FROM ShowtimeSeats WITH (UPDLOCK, HOLDLOCK) WHERE ShowtimeId = @1 để khóa the… | Chưa |  |  |
| S3.5-06 | Tính trạng thái hiệu lực: ghế HELD có HoldExpiresAt ≤ @Now coi là AVAILABLE; ghế IsMaintenance coi là MAINTENANCE. | Chưa |  |  |
| S3.5-07 | Mỗi ghế yêu cầu phải đang AVAILABLE hoặc đang HELD bởi chính hóa đơn này. Nếu không thì trả SEAT_TAKEN kèm danh sách gh… | Chưa |  |  |
| S3.5-08 | Kiểm tra cặp SWEETBOX, xác nhận ghế xe lăn, rồi chạy OrphanSeatValidator (3.5.1). PosStaff được bỏ qua luật ghế mồ côi … | Chưa |  |  |
| S3.5-09 | UPDATE ghế mới thành HELD với HoldBillId và HoldExpiresAt của hóa đơn; ghế cũ của hóa đơn không còn trong lựa chọn thì … | Chưa |  |  |
| S3.5-10 | Commit, rồi phát SEAT_MAP_UPDATED chỉ gồm các ghế thay đổi. | Chưa |  |  |
| S3.5-11 | HoldExpiresAt được đặt ở lần giữ đầu tiên bằng now + SeatHoldMinutes. Đổi ghế không làm tăng thời gian giữ. Thu ngân đư… | Chưa |  |  |
| S3.5-12 | SeatHoldSweeper (BackgroundService, chạy mỗi HoldSweepSeconds) trả các ghế HELD đã hết hạn về AVAILABLE, hủy hóa đơn wa… | Chưa |  |  |
| S3.5-13 | Mọi truy vấn đọc sơ đồ ghế, cùng bước 3 ở trên, luôn coi ghế HELD đã hết hạn là AVAILABLE. Vì vậy ghế không bao giờ bị … | Chưa |  |  |
| S3.5.1-01 | Ghế bị chiếm: HELD còn hạn, BOOKED, HOUSE_HOLD, MAINTENANCE, hoặc đang được chọn. Ghế WHEELCHAIR và SWEETBOX không xét,… | Chưa |  |  |
| S3.5.1-02 | Vi phạm khi, sau lựa chọn, có một ghế trống S mà hai bên S đều là ghế bị chiếm hoặc biên (lối đi, tường), và ít nhất mộ… | Chưa |  |  |
| S3.5.1-03 | Ngoại lệ: trong đoạn đó không còn vị trí nào khác để đặt cùng số ghế liền nhau mà không tạo ghế lẻ (thử mọi vị trí); ho… | Chưa |  |  |
| S3.5.1-04 | Lỗi ORPHAN_SEAT trả về ghế bị bỏ lẻ và gợi ý cụ thể: chọn thêm ghế đó, hoặc dời lựa chọn sang vị trí hợp lệ gần nhất. | Chưa |  |  |
| S3.5.1-05 | Ghế lẻ giữa hai ghế đã bán. | Chưa |  |  |
| S3.5.1-06 | Ghế lẻ sát lối đi. | Chưa |  |  |
| S3.5.1-07 | Ghế lẻ sát tường. | Chưa |  |  |
| S3.5.1-08 | Chọn cách quãng G5 và G7. | Chưa |  |  |
| S3.5.1-09 | Ghế lẻ đã có sẵn từ trước. | Chưa |  |  |
| S3.5.1-10 | Ngoại lệ đoạn ngắn. | Chưa |  |  |
| S3.5.1-11 | Ghế xe lăn và ghế đôi nằm kề bên. | Chưa |  |  |
| S3.6-01 | Mọi giao dịch (quầy, online, doanh nghiệp) đều là một bản ghi Bills. | Chưa |  |  |
| S3.6-02 | Vé lưu ở Tickets: mỗi ghế một vé, mỗi vé một mã QR. | Chưa |  |  |
| S3.6-03 | Bắp nước lưu ở BillDetails như bài mẫu. | Chưa |  |  |
| S3.6-04 | Mỗi hóa đơn có tối đa một suất chiếu (Bills.ShowtimeId); để trống nếu chỉ mua bắp nước. | Chưa |  |  |
| S3.6-05 | Trạng thái và chuyển trạng thái theo CONTRACTS mục 5, luôn kiểm tra qua BillStateMachine. | Chưa |  |  |
| S3.6-06 | Chỉ hóa đơn waiting mới được thêm, sửa, xóa dòng. Mỗi lần thay đổi, BillService.Recalculate tính lại TicketSubtotal, Fn… | Chưa |  |  |
| S3.6-07 | PosController gồm các route của slide (CONTRACTS mục 9), thêm: /pos/select-showtime, /pos/hold-seats, /pos/set-audience… | Chưa |  |  |
| S3.6-08 | Mở ca: mọi thao tác bán yêu cầu người dùng có ca OPEN tại máy đó (SHIFT_NOT_OPEN). TerminalCode được chọn khi mở ca và … | Chưa |  |  |
| S3.6-09 | CASH: nhập tiền khách đưa, hiện tiền thối. | Chưa |  |  |
| S3.6-10 | VIETQR_SIM: POS hiện QR mô phỏng; sau khi khách quét, thu ngân bấm "Đã nhận tiền". | Chưa |  |  |
| S3.6-13 | Có thể dùng kèm voucher, điểm. | Chưa |  |  |
| S3.6-14 | Tạo Tickets và mã QR; ShowtimeSeats chuyển BOOKED. | Chưa |  |  |
| S3.6-15 | Bắp nước chuyển PREPARING ngay và trừ ActualStock; cộng điểm. | Chưa |  |  |
| S3.6-16 | Phát SEAT_MAP_UPDATED; mở trang in vé khổ 80mm. | Chưa |  |  |
| S3.6-18 | Quét QR hóa đơn online tại quầy để in vé giấy. | Chưa |  |  |
| S3.6-20 | Thu ngân nhập lý do; quản lý nhập PIN ngay trên máy để duyệt (bàn phím PIN ở UI mục 5). | Chưa |  |  |
| S3.6-23 | Khi được duyệt: hoàn tiền theo phương thức gốc (tiền mặt trừ vào két của ca hiện tại); vé VOIDED; ghế AVAILABLE; bắp nư… | Chưa |  |  |
| S3.6-24 | Không cho hủy nếu đã có vé vào cửa. | Chưa |  |  |
| S3.6-25 | Danh sách tất cả hóa đơn: lọc theo khoảng ngày, trạng thái, kênh, rạp, nhân viên; tìm theo mã hóa đơn. | Chưa |  |  |
| S3.6-26 | Chi tiết hóa đơn: danh sách vé và món (tên, số lượng, đơn giá, thành tiền), giảm giá, tổng tiền, trạng thái và lý do hủ… | Chưa |  |  |
| S3.6-27 | Thao tác: in lại và hủy theo đúng quy tắc chuyển trạng thái. Không sửa trực tiếp số tiền. | Chưa |  |  |
| S3.6-28 | Ca không còn hóa đơn waiting nào (SHIFT_HAS_WAITING_BILLS). | Chưa |  |  |
| S3.6-29 | Nhân viên đếm tiền theo mệnh giá và nhập tổng, không nhìn thấy con số hệ thống tính. | Chưa |  |  |
| S3.6-30 | Sau khi xác nhận, hệ thống mới tính ExpectedCash = OpeningCashFloat + tiền mặt thu − tiền mặt hoàn trong ca, và Discrep… | Chưa |  |  |
| S3.6-31 | Hiện biên bản thừa hoặc thiếu gắn với nhân viên, in được, không sửa được sau khi chốt. | Chưa |  |  |
| S3.11-02 | Khả dụng = ActualStock − ReservedStock. Một món bán được n phần khi mọi nguyên liệu có khả dụng ≥ n × định mức (STOCK_I… | Chưa |  |  |
| S3.11-05 | Mua tại quầy: khi hóa đơn finish, trừ thẳng ActualStock (CONSUME). | Chưa |  |  |
| S3.11-12 | POS hiện "Tạm hết" và không cho chọn món không đủ nguyên liệu; API cũng chặn (STOCK_INSUFFICIENT). | Chưa |  |  |
| S3.12-01 | StatisticsController dùng policy ManagerOnly; StatisticDAL gọi stored procedure bằng EXEC. | Chưa |  |  |
| S3.12-10 | Các báo cáo khác: tỷ lệ phim Việt (3.1 mục 6), tỷ lệ lấp đầy theo suất, biên bản ca. | Chưa |  |  |
| S3.12-12 | Biểu đồ dùng Chart.js; bộ chọn khoảng ngày có mốc nhanh; xuất được CSV. | Chưa |  |  |
| S3.13-01 | Cộng điểm khi hóa đơn có CustomerId chuyển finish: phần trả bằng tiền mặt, chuyển khoản hoặc ví × PointEarnPercent[hạng… | Chưa |  |  |
| S3.13-02 | Hạng được xét lại mỗi lần cộng điểm, theo tổng chi tiêu 12 tháng gần nhất (TierVipSpend12Months, TierVvipSpend12Months). | Chưa |  |  |
| S3.13-03 | Đổi điểm: 1 điểm = PointValueVnd, tối đa MaxPointsRedeemPercent giá trị hóa đơn (POINTS_INVALID). | Chưa |  |  |
| S3.13-05 | Voucher: kiểm tra Active, thời hạn, giá trị đơn tối thiểu, số lượt dùng, chủ sở hữu. Không hợp lệ thì trả VOUCHER_INVAL… | Chưa |  |  |
| S5-01 | Tên nhóm, sự kiện, quyền tham gia theo CONTRACTS mục 6. Mục tiêu độ trễ dưới 200ms trên máy cục bộ. | Chưa |  |  |
| S5-02 | Client dùng chung một module realtime.js: withAutomaticReconnect; kết nối lại xong thì tham gia lại các nhóm và tải lại… | Chưa |  |  |
| S5-03 | Màn hình POS có chỉ báo trạng thái kết nối. | Chưa |  |  |
| S7-03 | OrphanSeatValidator, SweetboxRule, SettlementCalculator, CashDropCalculator (đóng ca mù), LoyaltyCalculator. | Chưa |  |  |
| S8.4-01 | Mở ca; chọn suất. | Chưa |  |  |
| S8.4-02 | Sơ đồ ghế dùng SeatHoldService, OrphanSeatValidator, luật cặp SWEETBOX; CinemaHub và kênh 1. | Chưa |  |  |
| S8.4-03 | Chọn đối tượng vé cho từng ghế, áp các quy tắc ở 3.1. | Chưa |  |  |
| S8.4-04 | Bắp nước, trừ kho khi bán và chặn khi không đủ (InventoryService phần cơ bản). | Chưa |  |  |
| S8.4-05 | Voucher, điểm, tra cứu thành viên. | Chưa |  |  |
| S8.4-06 | Thanh toán CASH và VIETQR_SIM; tạo vé và QR; in vé. | Chưa |  |  |
| S8.4-07 | Hủy hóa đơn waiting; trang hóa đơn của tôi. | Chưa |  |  |
| S8.4-08 | Hủy hóa đơn đã thanh toán bằng PIN (cách duyệt từ xa làm ở Phase 6). Đóng ca mù. | Chưa |  |  |
| S8.4-09 | SeatHoldSweeper. | Chưa |  |  |
| S8.4-10 | BillsController quản lý hóa đơn (3.6, UI A8). StatisticsController với 4 stored procedure, có biểu đồ Chart.js cho top … | Chưa |  |  |
| U3-04 | Chart.js: màu lấy từ token, lưới mờ, tooltip định dạng tiền Việt, không dùng hiệu ứng 3D. Mỗi biểu đồ có tiêu đề nói rõ… | Chưa |  |  |
| U7.P1-01 | P1. POS /pos/index (theme ops, cao 100vh) | Chưa |  |  |
| U7.P1-02 | Cột 1 (300px): ô tìm phim hoặc suất; danh sách suất hôm nay nhóm theo phim (poster 40px, tên, nhãn tuổi). Mỗi suất là c… | Chưa |  |  |
| U7.P1-03 | Cột 2 (co giãn): hai tab lớn "Sơ đồ ghế" và "Bắp nước". Sơ đồ ghế đặt trong panel tối. Lưới món 4 cột thẻ lớn (ảnh 64px… | Chưa |  |  |
| U7.P1-04 | Cột 3 (380px): "Hóa đơn tạm tính". | Chưa |  |  |
| U7.P1-05 | Thành viên: tra cứu bằng F1, hiện tên, hạng, điểm. | Chưa |  |  |
| U7.P1-06 | Danh sách vé: mỗi vé có ghế, loại ghế, dropdown chọn đối tượng (giá cập nhật ngay), cảnh báo luật hiện ngay trên dòng; … | Chưa |  |  |
| U7.P1-07 | Danh sách món với bộ − số + và nút xóa; voucher và điểm. | Chưa |  |  |
| U7.P1-08 | Tổng tiền 32px đậm; hai nút 64px "Tiền mặt (F4)" và "Chuyển khoản QR"; nút phụ "Hủy hóa đơn (Esc)". | Chưa |  |  |
| U7.P1-09 | Từ 1024px đến 1279px, cột 1 thu vào drawer. | Chưa |  |  |
| U7.P1-10 | Phím tắt hiện dạng <kbd> ngay trên nút. | Chưa |  |  |
| U7.P1-11 | Hủy hóa đơn đã thanh toán: hộp nhập lý do và bàn phím PIN của quản lý (mục 5). | Chưa |  |  |
| U7.P7-01 | P7. Mở ca và đóng ca (ops) | Chưa |  |  |
| U7.P7-02 | Mở ca: wizard ngắn; nhập số tiền bằng bàn phím số lớn. | Chưa |  |  |
| U7.P7-03 | Đếm tiền theo mệnh giá (500.000, 200.000, 100.000, 50.000, 20.000, 10.000, 5.000, 2.000, 1.000), hệ thống tự cộng. | Chưa |  |  |
| U7.P7-04 | Xác nhận. | Chưa |  |  |
| U7.P7-05 | Biên bản: so sánh số đếm được với số hệ thống tính, chênh lệch tô xanh hoặc đỏ, có nút in. | Chưa |  |  |
| U7.A1-01 | A1. Dashboard /admin | Chưa |  |  |
| U7.A1-02 | Hàng thẻ KPI: doanh thu hôm nay, vé bán, tỷ lệ lấp đầy, doanh thu bắp nước, tỷ lệ phim Việt từ đầu năm. | Chưa |  |  |
| U7.A1-03 | Hàng biểu đồ: doanh thu 30 ngày dạng đường (2 series vé và bắp nước, rộng 8/12); top phim dạng thanh ngang (rộng 4/12). | Chưa |  |  |
| U7.A1-04 | Hàng dưới: khối tỷ lệ phim Việt có vạch 20%; danh sách cảnh báo vận hành (KDM sắp hết hạn, nguyên liệu dưới ngưỡng, ghế… | Chưa |  |  |
| U7.A6-01 | A6. Báo cáo: bộ lọc khoảng ngày có mốc nhanh; thẻ KPI; biểu đồ; bảng chi tiết; nút "Xuất CSV". Trang đối soát nhà phát … | Chưa |  |  |
| U7.A8-01 | A8. Quản lý hóa đơn /admin/bills (ManagerOnly) | Chưa |  |  |
| U7.A8-02 | Danh sách: lọc theo khoảng ngày (mốc nhanh Hôm nay, 7 ngày, Tháng này), trạng thái (Chờ thanh toán, Hoàn thành, Đã hủy)… | Chưa |  |  |
| U7.A8-03 | Chi tiết /admin/bills/{id}: đầu trang ghi mã, badge trạng thái, lý do hủy nếu có. Khối thông tin: nhân viên lập, ca, th… | Chưa |  |  |
| U8-01 | Mỗi ghế là một <button>, dùng được bằng bàn phím và có aria. | Chưa |  |  |
| U8-02 | Desktop 32px, tablet 28px. Không bao giờ nhỏ hơn 24px. | Chưa |  |  |
| U8-04 | Ô trống (lối đi) chừa khoảng rộng bằng một ghế. Nhãn hàng (A, B, …) hiện ở cả hai đầu mỗi hàng. | Chưa |  |  |
| U8-05 | Màn hình chiếu: đường cong SVG ở phía trên, có quầng sáng trắng xanh nhạt, chữ "Màn hình" màu muted. | Chưa |  |  |
| U8-06 | Hiển thị theo trạng thái (dùng cả màu lẫn ký hiệu, không chỉ dựa vào màu): | Chưa |  |  |
| U8-07 | Ghế đôi: hai nút dính nhau; hover và chọn luôn áp cho cả cặp. | Chưa |  |  |
| U8-08 | Tooltip khi rê chuột hoặc focus, ví dụ: "Ghế VIP G7 · Điểm góc nhìn và âm thanh 96/100 (tọa độ vàng) · 95.000 ₫". | Chưa |  |  |
| U8-11 | Khi vi phạm luật (ORPHAN_SEAT, SWEETBOX_PAIR, GUARDIAN_REQUIRED…): ghế liên quan rung nhẹ 320ms và nháy viền đỏ 2 lần, … | Chưa |  |  |
| U8-12 | Cập nhật thời gian thực: ghế đổi trạng thái chuyển màu mờ dần trong 200ms. Nếu ghế khách vừa định chọn bị người khác lấ… | Chưa |  |  |
| U8-13 | Bàn phím: phím mũi tên để di chuyển giữa các ghế, Enter hoặc Space để chọn. | Chưa |  |  |
| U9-05 | ORPHAN_SEAT: "Cách chọn này để trống ghế G8 ở giữa. Bạn chọn thêm G8 hoặc dời sang G6–G7 nhé." | Chưa |  |  |
| U9-06 | SWEETBOX_PAIR: "Ghế đôi M1–M2 được bán theo cặp. Bạn chọn cả hai ghế nhé." | Chưa |  |  |
| U9-07 | SEAT_TAKEN: "Ghế G7 vừa có người chọn trước nên đã được bỏ khỏi lựa chọn của bạn." | Chưa |  |  |
| U9-08 | AGE_RESTRICTED: "Phim T16 dành cho khán giả từ đủ 16 tuổi. Vé trẻ em không áp dụng cho phim này." | Chưa |  |  |
| U9-09 | CHILD_CURFEW: "Suất này kết thúc lúc 22:35. Theo quy định, trẻ em dưới 13 tuổi chỉ xem suất kết thúc trước 22:00." | Chưa |  |  |
| U9-10 | GUARDIAN_REQUIRED: "Phim K: trẻ dưới 13 tuổi cần cha, mẹ hoặc người giám hộ đi cùng. Thêm 1 vé người lớn hoặc xác nhận … | Chưa |  |  |
| U9-14 | SHIFT_NOT_OPEN: "Bạn cần mở ca trước khi bán hàng." | Chưa |  |  |
| U9-15 | STOCK_INSUFFICIENT: "Combo bắp phô mai lớn tạm hết nguyên liệu." | Chưa |  |  |
| U10-04 | Mỗi ghế có aria-label đầy đủ, ví dụ "Ghế G7, VIP, còn trống, 96 điểm, 95.000 ₫". | Chưa |  |  |
| C5-03 | Nhân viên chuyển finish sang cancel luôn cần quản lý duyệt (đúng slide bài 6). Ngoài bảng trên không có chuyển trạng th… | Chưa |  |  |
| C6-01 | CinemaHub được map tại /hubs/cinema; hub chỉ có hàm tham gia và rời nhóm, mỗi hàm tham gia kiểm tra quyền theo cột "Ai … | Chưa |  |  |
| C9-07 | Route POS giữ kiểu slide bài 6: /pos/index, /pos/init, /pos/update-quantity (action là incr, decr hoặc delete), /pos/ch… | Chưa |  |  |
| UC14 | Mở ca, đóng ca mù (màn hình P7) | Chưa |  |  |
| UC15 | Bán vé và bắp nước tại quầy (màn hình P1, P2) | Chưa |  |  |
| UC21 | Xem hóa đơn của tôi (màn hình P1) | Chưa |  |  |
| UC37 | Quản lý hóa đơn (màn hình A8) | Chưa |  |  |
| UC38 | Thống kê, báo cáo (màn hình A1, A6) | Chưa |  |  |
| UC39 | Đối soát nhà phát hành (màn hình A6) | Chưa |  |  |
