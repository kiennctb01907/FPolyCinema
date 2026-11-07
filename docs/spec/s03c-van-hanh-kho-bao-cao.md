# Đặc tả FPolyCinema – mục 3.8 đến 3.14: soát vé, tự hủy, nghiệp vụ khác, kho, thống kê, điểm và ví, quy tắc xóa

Mục lục và cách đọc mã yêu cầu như [S3.6-04 P4]: docs/README.md. Tên chuẩn: docs/CONTRACTS.md. Giao diện: docs/ui.

### 3.8 Soát vé và vận hành phòng
- [S3.8-01 P6] Có thể quét QR hóa đơn (hiện mọi vé trong hóa đơn) hoặc QR vé (hiện vé đó cùng các vé khác trong hóa đơn).
- Kiểm tra theo thứ tự sau, dừng ở lỗi đầu tiên, mỗi lỗi một thông điệp riêng:
  1. [S3.8-02 P6] Mã tồn tại.
  2. [S3.8-03 P6] Hóa đơn đang finish.
  3. [S3.8-04 P6] Vé không ở trạng thái VOIDED hay SWAPPED.
  4. [S3.8-05 P6] Đúng rạp.
  5. [S3.8-06 P6] Suất đang BOARDING, ADS_PLAYING hoặc FEATURE_PLAYING. Phòng chưa sẵn sàng thì báo "Phòng đang dọn vệ sinh, chưa thể đón khách"; các trường hợp còn lại báo "Chưa tới giờ vào" hoặc "Suất đã kết thúc".
  6. [S3.8-07 P6] Vé chưa CHECKED_IN.
- [S3.8-08 P6] Các cảnh báo vàng sau vẫn cho vào, nhưng nhân viên phải xác nhận: phim T13 trở lên (kiểm tra CCCD hoặc VNeID); phim K có vé trẻ em (kiểm tra người giám hộ); suất kết thúc từ 22:00 hoặc 23:00 (giờ chiếu cho trẻ em); vé có RequiresDocument.
- [S3.8-09 P6] Cho vào toàn bộ hoặc từng ghế. Bills.CheckInStatus tự tính thành NOT_CHECKED_IN, PARTIALLY_CHECKED_IN hoặc FULLY_CHECKED_IN. Phát CHECKIN_UPDATED và OCCUPANCY_UPDATED.
- [S3.8-10 P6] Vé điện tử đã vào cửa hiện dấu xác nhận động (đồng hồ chạy theo giây, hoa văn chuyển động) để phân biệt với ảnh chụp màn hình.
- [S3.8-11 P6] Nút "Đã dọn xong" (GateStaff) chuyển suất đang CLEANING_IN_PROGRESS sang COMPLETED. Bảng sảnh hiện "Mời vào rạp" khi suất kế tiếp chuyển BOARDING.
- [S3.8-12 P6] Dashboard vận hành hiện sĩ số trực tiếp (đã vào trên đã bán) của các suất đang chạy.

### 3.9 Khách tự hủy vé
- Điều kiện:
  - [S3.9-01 P5] Hóa đơn ONLINE ở trạng thái finish, của chính khách đó, không phải hóa đơn CORPORATE.
  - [S3.9-02 P5] now ≤ StartTime − SelfCancelMinMinutesBeforeStart.
  - [S3.9-03 P5] Mọi vé đều NOT_CHECKED_IN; FnbStatus là NONE hoặc PENDING_PICKUP.
  - [S3.9-04 P5] Số lần CANCELLED_BY_USER trong tháng dương lịch hiện tại nhỏ hơn SelfCancelMaxPerMonth.
- Kết quả:
  - [S3.9-05 P5] Hóa đơn chuyển cancel với CANCELLED_BY_USER.
  - [S3.9-06 P5] Hoàn 100% phần đã trả vào ví (WalletTransactions); voucher và điểm đã dùng được trả lại.
  - [S3.9-07 P5] Ghế về AVAILABLE (phát SEAT_MAP_UPDATED); giải phóng ReservedStock; thu hồi điểm đã cộng.
- [S3.9-08 P5] Giao diện hiện rõ điều kiện và số lần hủy còn lại trong tháng. Khi không đủ điều kiện, nút bị khóa và có ghi lý do.

### 3.10 Nghiệp vụ quầy và quản lý khác
1. Đổi hoặc nâng hạng ghế (PosStaff):
   - [S3.10-01 P6] Quét vé, rồi chọn ghế mới trong cùng suất qua SeatHoldService (route /pos/swap-seat).
   - [S3.10-02 P6] Chênh lệch = giá mới − giá cũ. Dương thì thu thêm (Payments IN); không dương thì đổi ngang, không hoàn tiền.
   - [S3.10-03 P6] Vé cũ chuyển SWAPPED (QR cũ hết hiệu lực); vé mới có QR mới, nằm trong cùng hóa đơn.
   - [S3.10-04 P6] Chỉ đổi được khi vé NOT_CHECKED_IN và trước FeatureStartTime. Ghi AuditLog.
2. Hủy suất do sự cố (SupervisorOrManager):
   - [S3.10-05 P6] Chọn suất, nhập lý do, gõ lại mã suất để xác nhận.
   - [S3.10-06 P6] Trong một transaction: OperationalStatus chuyển CANCELLED; mọi hóa đơn finish của suất chuyển INCIDENT_COMPENSATED, hoàn toàn bộ số đã trả theo phương thức gốc, vé VOIDED, bắp nước chưa làm thì giải phóng kho, hóa đơn có tài khoản được tặng voucher bồi thường kèm Notifications; hóa đơn waiting chuyển EXPIRED; giải phóng ghế; phát sự kiện.
   - [S3.10-07 P6] Cuối cùng hiện tổng kết: số hóa đơn, số vé, tổng tiền đã hoàn.
3. Doanh nghiệp (Manager):
   - [S3.10-08 P6] Thuê trọn suất: chỉ với suất chưa bán vé nào. Suất được đánh dấu IsPrivate (ẩn khỏi web khách) và có một hóa đơn CORPORATE cho toàn bộ ghế, giá theo hợp đồng.
   - [S3.10-09 P6] Phát hành loạt voucher FREE_TICKET: nhập số lượng và hạn dùng, gắn CorporateBookingId. Mỗi mã trả cho một vé ghế STANDARD hoặc VIP, không áp cho SWEETBOX.

### 3.11 Kho bắp nước theo định mức (BOM)
- [S3.11-01 P2] RawMaterials quản lý theo từng rạp. ProductRecipes là định mức nguyên liệu và bao bì cho từng món Drinks; combo cũng là một Drinks có IsCombo = 1 với công thức gộp.
- [S3.11-02 P4] Khả dụng = ActualStock − ReservedStock. Một món bán được n phần khi mọi nguyên liệu có khả dụng ≥ n × định mức (STOCK_INSUFFICIENT).
- [S3.11-03 P5] Online thanh toán thành công: ReservedStock tăng theo định mức (RESERVE), FnbStatus = PENDING_PICKUP.
- [S3.11-04 P6] Bắt đầu làm món cho đơn online: khách bấm "Tôi đã tới rạp – làm bắp nước ngay" trên vé điện tử (nút chỉ hiện từ StartTime − ImHereWindowMinutesBeforeStart đến EndTime), hoặc thu ngân quét QR ở quầy bắp nước. Khi đó ReservedStock và ActualStock cùng giảm (RELEASE + CONSUME), FnbStatus = PREPARING, đơn hiện trên màn hình bếp.
- [S3.11-05 P4] Mua tại quầy: khi hóa đơn finish, trừ thẳng ActualStock (CONSUME).
- [S3.11-06 P6] Đơn bắp nước mua tại quầy hiện ngay trên màn hình bếp.
- [S3.11-07 P6] Bếp: PREPARING chuyển READY_FOR_PICKUP khi bấm "Sẵn sàng giao" (số gọi lên bảng, có âm báo, gửi FNB_STATUS_CHANGED tới khách), rồi chuyển FULFILLED khi bấm "Đã giao".
- [S3.11-08 P5] Đơn hủy hợp lệ trước khi làm thì giải phóng ReservedStock (RELEASE).
- Sau mỗi giao dịch kho, InventoryService tính lại các món liên quan:
  - [S3.11-09 P6] Món không còn đủ một phần thì đặt IsSoldOut = 1 và phát PRODUCT_SOLD_OUT.
  - [S3.11-10 P6] Nhập hàng làm món khả dụng lại thì phát PRODUCT_AVAILABLE.
  - [S3.11-11 P6] Nguyên liệu xuống dưới MinThreshold thì phát STOCK_LOW.
  - [S3.11-12 P4] POS hiện "Tạm hết" và không cho chọn món không đủ nguyên liệu; API cũng chặn (STOCK_INSUFFICIENT).
  - [S3.11-13 P5] Web khách hiện "Tạm hết" và không cho chọn.
- [S3.11-14 P6] Nhập kho (Manager): phiếu nhập đơn giản (IMPORT).
- [S3.11-15 P6] Hủy hàng hỏng: PosStaff lập WastageLogs (PENDING), SupervisorOrManager duyệt, khi đó ActualStock giảm (WASTAGE).

### 3.12 Thống kê và đối soát nhà phát hành
- [S3.12-01 P4] StatisticsController dùng policy ManagerOnly; StatisticDAL gọi stored procedure bằng EXEC.
- [S3.12-02 P1] Quy tắc chung: chỉ tính hóa đơn finish; vé SWAPPED và VOIDED không tính; doanh thu dùng FinalPrice và LineFinalAmount (đã gồm VAT, đã phân bổ giảm giá); ngày tính theo Bills.PaidAt.
- [S3.12-03 P1] sp_top_5_best_selling_drinks trả DrinkId, DrinkName, TotalQuantitySold, TotalRevenue, sắp theo số lượng giảm dần.
- [S3.12-04 P1] sp_top_5_best_selling_movies trả MovieId, Title, TicketsSold, TicketRevenue, sắp theo doanh thu giảm dần.
- [S3.12-05 P1] sp_revenue_by_day trả RevenueDate, TotalBills, TicketRevenue, FnbRevenue, TotalRevenue. Ngày nào trong khoảng cũng có một dòng (ngày không bán ghi 0); sắp theo ngày tăng dần.
- sp_box_office_settlement:
  - [S3.12-06 P1] Tuần n là khoảng [ReleaseDate + 7(n − 1), ReleaseDate + 7n), tính theo ngày vận hành của suất.
  - [S3.12-07 P1] Tỷ lệ của nhà phát hành lấy từ FilmRentalRules của tuần n; tuần vượt quá quy tắc cuối cùng thì dùng quy tắc cuối cùng.
  - [S3.12-08 P1] Với mỗi vé: Net = FinalPrice × 100 / (100 + VatRate); FloorNet = MinimumTicketPrice × 100 / (100 + VatRate); Base = max(Net, FloorNet).
  - [S3.12-09 P1] Trả về: số vé, doanh thu gộp, doanh thu thuần, số vé dưới giá sàn, phần rạp tự chịu do giá sàn, tiền trả nhà phát hành (tổng Base × tỷ lệ), tiền rạp giữ lại.
- [S3.12-10 P4] Các báo cáo khác: tỷ lệ phim Việt (3.1 mục 6), tỷ lệ lấp đầy theo suất, biên bản ca.
- [S3.12-11 P6] Báo cáo hủy hàng.
- [S3.12-12 P4] Biểu đồ dùng Chart.js; bộ chọn khoảng ngày có mốc nhanh; xuất được CSV.

### 3.13 Điểm, hạng, ví, voucher
- [S3.13-01 P4] Cộng điểm khi hóa đơn có CustomerId chuyển finish: phần trả bằng tiền mặt, chuyển khoản hoặc ví × PointEarnPercent[hạng] / 100 / PointValueVnd, làm tròn xuống. Hóa đơn bị hủy thì thu hồi điểm.
- [S3.13-02 P4] Hạng được xét lại mỗi lần cộng điểm, theo tổng chi tiêu 12 tháng gần nhất (TierVipSpend12Months, TierVvipSpend12Months).
- [S3.13-03 P4] Đổi điểm: 1 điểm = PointValueVnd, tối đa MaxPointsRedeemPercent giá trị hóa đơn (POINTS_INVALID).
- [S3.13-04 P5] Ví: Users.WalletBalance chỉ thay đổi cùng với một bản ghi WalletTransactions, trong cùng transaction (WALLET_INSUFFICIENT).
- [S3.13-05 P4] Voucher: kiểm tra Active, thời hạn, giá trị đơn tối thiểu, số lượt dùng, chủ sở hữu. Không hợp lệ thì trả VOUCHER_INVALID kèm lý do cụ thể.

### 3.14 Quy tắc xóa (theo slide bài 1, 5)
- [S3.14-01 P2] Loại đồ uống: chỉ xóa khi chưa có đồ uống nào.
- [S3.14-02 P2] Đồ uống: chỉ xóa cứng khi chưa xuất hiện trong hóa đơn. Nếu đã xuất hiện thì chỉ được ngưng (Active = 0).
- [S3.14-03 P3] Phim: chỉ xóa cứng khi chưa có suất chiếu hay vé. Nếu đã có thì chỉ được ngưng (Active = 0).
- [S3.14-04 P2] Nhân viên: chỉ xóa cứng khi chưa tạo hóa đơn nào. Nếu đã có thì chỉ được khóa hoặc mở (SetActive). Không ai được tự khóa hay xóa tài khoản mình đang đăng nhập.
- [S3.14-05 P3] Phòng chiếu: chỉ xóa khi chưa có suất chiếu nào. Ghế: theo 3.2.
- [S3.14-06 P2] Bị chặn thì trả DELETE_BLOCKED kèm lý do, và gợi ý chuyển sang ngưng hoạt động.
