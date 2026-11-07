# Đặc tả FPolyCinema – mục 3.5 đến 3.7: giữ ghế, hóa đơn và POS, thanh toán online

Mục lục và cách đọc mã yêu cầu như [S3.6-04 P4]: docs/README.md. Tên chuẩn: docs/CONTRACTS.md. Giao diện: docs/ui.

### 3.5 Giữ ghế và chống tranh chấp
- [S3.5-01 P4] Hóa đơn là đơn vị giữ ghế: mỗi hóa đơn waiting có HoldExpiresAt, và ShowtimeSeats.HoldBillId trỏ về hóa đơn đó.
- [S3.5-02 P4] Lần giữ ghế đầu tiên tạo hóa đơn waiting; tại quầy là hóa đơn waiting của thu ngân trên máy đó.
- [S3.5-03 P5] Online: hóa đơn có Channel ONLINE và khách phải đăng nhập.
- Các bước của SeatHoldService.Hold(billId, showtimeId, seatIds, override):
  1. [S3.5-04 P4] Kiểm tra suất còn mở bán: online đến FeatureStartTime + OnlineSalesCutoffMinutes, tại quầy đến FeatureStartTime + PosSalesCutoffMinutes (SALES_CLOSED).
  2. [S3.5-05 P4] Trong DBUtil.InTransaction, chạy `SELECT … FROM ShowtimeSeats WITH (UPDLOCK, HOLDLOCK) WHERE ShowtimeId = @1` để khóa theo suất. Nhờ vậy hai yêu cầu đến cùng lúc sẽ được xử lý lần lượt.
  3. [S3.5-06 P4] Tính trạng thái hiệu lực: ghế HELD có HoldExpiresAt ≤ @Now coi là AVAILABLE; ghế IsMaintenance coi là MAINTENANCE.
  4. [S3.5-07 P4] Mỗi ghế yêu cầu phải đang AVAILABLE hoặc đang HELD bởi chính hóa đơn này. Nếu không thì trả SEAT_TAKEN kèm danh sách ghế.
  5. [S3.5-08 P4] Kiểm tra cặp SWEETBOX, xác nhận ghế xe lăn, rồi chạy OrphanSeatValidator (3.5.1). PosStaff được bỏ qua luật ghế mồ côi nếu nhập lý do (ghi AuditLog).
  6. [S3.5-09 P4] UPDATE ghế mới thành HELD với HoldBillId và HoldExpiresAt của hóa đơn; ghế cũ của hóa đơn không còn trong lựa chọn thì trả về AVAILABLE. Số dòng được cập nhật phải đúng bằng số ghế; sai thì rollback và trả SEAT_TAKEN.
  7. [S3.5-10 P4] Commit, rồi phát SEAT_MAP_UPDATED chỉ gồm các ghế thay đổi.
- [S3.5-11 P4] HoldExpiresAt được đặt ở lần giữ đầu tiên bằng now + SeatHoldMinutes. Đổi ghế không làm tăng thời gian giữ. Thu ngân được gia hạn một lần thêm SeatHoldExtendMinutes.
- Giải phóng ghế theo hai lớp:
  - [S3.5-12 P4] SeatHoldSweeper (BackgroundService, chạy mỗi HoldSweepSeconds) trả các ghế HELD đã hết hạn về AVAILABLE, hủy hóa đơn waiting hết hạn (EXPIRED) và phát sự kiện.
  - [S3.5-13 P4] Mọi truy vấn đọc sơ đồ ghế, cùng bước 3 ở trên, luôn coi ghế HELD đã hết hạn là AVAILABLE. Vì vậy ghế không bao giờ bị treo, kể cả khi sweeper chưa kịp chạy.

#### 3.5.1 Luật ghế mồ côi (OrphanSeatValidator, logic thuần)
- [S3.5.1-01 P4] Ghế bị chiếm: HELD còn hạn, BOOKED, HOUSE_HOLD, MAINTENANCE, hoặc đang được chọn. Ghế WHEELCHAIR và SWEETBOX không xét, được coi như biên.
- [S3.5.1-02 P4] Vi phạm khi, sau lựa chọn, có một ghế trống S mà hai bên S đều là ghế bị chiếm hoặc biên (lối đi, tường), và ít nhất một ghế kề S thuộc lựa chọn hiện tại. Ghế lẻ đã có sẵn từ trước không tính là lỗi của người đang chọn.
- [S3.5.1-03 P4] Ngoại lệ: trong đoạn đó không còn vị trí nào khác để đặt cùng số ghế liền nhau mà không tạo ghế lẻ (thử mọi vị trí); hoặc PosStaff chủ động bỏ qua luật.
- [S3.5.1-04 P4] Lỗi ORPHAN_SEAT trả về ghế bị bỏ lẻ và gợi ý cụ thể: chọn thêm ghế đó, hoặc dời lựa chọn sang vị trí hợp lệ gần nhất.
- Unit test tối thiểu:
  - [S3.5.1-05 P4] Ghế lẻ giữa hai ghế đã bán.
  - [S3.5.1-06 P4] Ghế lẻ sát lối đi.
  - [S3.5.1-07 P4] Ghế lẻ sát tường.
  - [S3.5.1-08 P4] Chọn cách quãng G5 và G7.
  - [S3.5.1-09 P4] Ghế lẻ đã có sẵn từ trước.
  - [S3.5.1-10 P4] Ngoại lệ đoạn ngắn.
  - [S3.5.1-11 P4] Ghế xe lăn và ghế đôi nằm kề bên.

### 3.6 Hóa đơn và bán tại quầy (theo slide bài 6, mở rộng)
- [S3.6-01 P4] Mọi giao dịch (quầy, online, doanh nghiệp) đều là một bản ghi Bills.
  - [S3.6-02 P4] Vé lưu ở Tickets: mỗi ghế một vé, mỗi vé một mã QR.
  - [S3.6-03 P4] Bắp nước lưu ở BillDetails như bài mẫu.
  - [S3.6-04 P4] Mỗi hóa đơn có tối đa một suất chiếu (Bills.ShowtimeId); để trống nếu chỉ mua bắp nước.
- [S3.6-05 P4] Trạng thái và chuyển trạng thái theo CONTRACTS mục 5, luôn kiểm tra qua BillStateMachine.
- [S3.6-06 P4] Chỉ hóa đơn waiting mới được thêm, sửa, xóa dòng. Mỗi lần thay đổi, BillService.Recalculate tính lại TicketSubtotal, FnbSubtotal, DiscountTotal, Total trong cùng transaction.
- [S3.6-07 P4] PosController gồm các route của slide (CONTRACTS mục 9), thêm: /pos/select-showtime, /pos/hold-seats, /pos/set-audience, /pos/lookup-member, /pos/apply-voucher, /pos/request-void, /pos/print, /pos/my-bills (hóa đơn do chính thu ngân tạo, lọc theo ngày).
- [S3.6-08 P4] Mở ca: mọi thao tác bán yêu cầu người dùng có ca OPEN tại máy đó (SHIFT_NOT_OPEN). TerminalCode được chọn khi mở ca và lưu trong cookie của máy. Khi mở ca phải nhập OpeningCashFloat.
- Thanh toán tại quầy:
  - [S3.6-09 P4] CASH: nhập tiền khách đưa, hiện tiền thối.
  - [S3.6-10 P4] VIETQR_SIM: POS hiện QR mô phỏng; sau khi khách quét, thu ngân bấm "Đã nhận tiền".
  - [S3.6-11 P6] VIETQR_SIM: QR mô phỏng hiện thêm trên màn hình phụ của quầy (UI P2).
  - [S3.6-12 P5] WALLET: dành cho thành viên đã tra cứu; trừ ví theo 3.13.
  - [S3.6-13 P4] Có thể dùng kèm voucher, điểm.
- Khi hóa đơn chuyển finish:
  - [S3.6-14 P4] Tạo Tickets và mã QR; ShowtimeSeats chuyển BOOKED.
  - [S3.6-15 P4] Bắp nước chuyển PREPARING ngay và trừ ActualStock; cộng điểm.
  - [S3.6-16 P4] Phát SEAT_MAP_UPDATED; mở trang in vé khổ 80mm.
  - [S3.6-17 P6] Phát POS_PAYMENT_DONE cho màn hình phụ.
- [S3.6-18 P4] Quét QR hóa đơn online tại quầy để in vé giấy.
- [S3.6-19 P6] Quét QR hóa đơn online tại quầy bắp nước để nhận bắp nước (3.11).
- Hủy hóa đơn đã thanh toán (VOIDED_BY_POS):
  - [S3.6-20 P4] Thu ngân nhập lý do; quản lý nhập PIN ngay trên máy để duyệt (bàn phím PIN ở UI mục 5).
  - [S3.6-21 P6] Thu ngân có thể chọn gửi yêu cầu duyệt từ xa thay cho nhập PIN.
  - [S3.6-22 P6] Yêu cầu từ xa: phát APPROVAL_REQUESTED tới manager_alerts; quản lý duyệt hoặc từ chối trên dashboard; kết quả APPROVAL_DECIDED gửi về máy POS. Yêu cầu hết hạn sau ApprovalRequestExpireMinutes.
  - [S3.6-23 P4] Khi được duyệt: hoàn tiền theo phương thức gốc (tiền mặt trừ vào két của ca hiện tại); vé VOIDED; ghế AVAILABLE; bắp nước chưa làm thì giải phóng kho, đã làm thì không hoàn kho; thu hồi điểm; ghi AuditLog.
  - [S3.6-24 P4] Không cho hủy nếu đã có vé vào cửa.
- Quản lý hóa đơn (BillsController, ManagerOnly; Lab 6 bài 1):
  - [S3.6-25 P4] Danh sách tất cả hóa đơn: lọc theo khoảng ngày, trạng thái, kênh, rạp, nhân viên; tìm theo mã hóa đơn.
  - [S3.6-26 P4] Chi tiết hóa đơn: danh sách vé và món (tên, số lượng, đơn giá, thành tiền), giảm giá, tổng tiền, trạng thái và lý do hủy, tên nhân viên tạo, ngày giờ tạo, các lần thanh toán và hoàn tiền.
  - [S3.6-27 P4] Thao tác: in lại và hủy theo đúng quy tắc chuyển trạng thái. Không sửa trực tiếp số tiền.
- Đóng ca mù:
  1. [S3.6-28 P4] Ca không còn hóa đơn waiting nào (SHIFT_HAS_WAITING_BILLS).
  2. [S3.6-29 P4] Nhân viên đếm tiền theo mệnh giá và nhập tổng, không nhìn thấy con số hệ thống tính.
  3. [S3.6-30 P4] Sau khi xác nhận, hệ thống mới tính ExpectedCash = OpeningCashFloat + tiền mặt thu − tiền mặt hoàn trong ca, và Discrepancy = tiền thực đếm − ExpectedCash.
  4. [S3.6-31 P4] Hiện biên bản thừa hoặc thiếu gắn với nhân viên, in được, không sửa được sau khi chốt.

### 3.7 Thanh toán online
- [S3.7-01 P5] Không tích hợp cổng thanh toán thật. Trang mô phỏng /payment-sim/pay?billId= hiện QR và hai nút "Thanh toán thành công", "Thất bại". Trang này gọi POST /payments/webhook giống cổng thật, kèm chữ ký HMAC-SHA256 (Payment:WebhookSecret) và ProviderTxnId.
- [S3.7-02 P5] Webhook xử lý trùng lặp an toàn: ProviderTxnId là duy nhất; nhận lại cùng mã thì trả OK mà không xử lý lần nữa. Sai chữ ký thì trả 401.
- [S3.7-03 P5] Thanh toán thành công khi hóa đơn còn waiting và ghế vẫn đang giữ: chuyển finish trong transaction (tạo vé, ghế BOOKED, giữ kho bắp nước với FnbStatus PENDING_PICKUP, cộng điểm), rồi phát PAYMENT_STATUS_CHANGED và SEAT_MAP_UPDATED.
- [S3.7-04 P5] Thanh toán thành công nhưng đến muộn (hóa đơn đã EXPIRED): giữ lại đúng các ghế cũ trong transaction.
  - [S3.7-05 P5] Giữ được hết thì chuyển finish.
  - [S3.7-06 P5] Không giữ được thì chuyển OVERBOOKED_REFUNDED: hoàn tiền về phương thức gốc, tặng voucher CompensationVoucherAmount, tạo Notifications và phát PAYMENT_STATUS_CHANGED.
- [S3.7-07 P5] Thanh toán thất bại: hóa đơn vẫn waiting đến khi hết hạn; khách thử lại được.
- [S3.7-08 P5] Trang chờ thanh toán đếm ngược theo HoldExpiresAt và nghe PAYMENT_STATUS_CHANGED để tự chuyển sang vé điện tử.
