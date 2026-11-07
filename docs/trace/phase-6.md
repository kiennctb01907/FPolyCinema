# Truy vết yêu cầu – Phase 6 – Vận hành thời gian thực

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
| S2.7-07 | Đổi ghế; giải phóng ghế nội bộ; hủy suất; duyệt hủy hàng. | Chưa |  |  |
| S3.1-05 | Máy soát vé hiện cảnh báo kiểm tra CCCD hoặc VNeID với phim T13 trở lên. | Chưa |  |  |
| S3.1-12 | Máy soát vé hiện cảnh báo tương tự với các suất đó. | Chưa |  |  |
| S3.1-25 | Máy soát vé và bảng sảnh. | Chưa |  |  |
| S3.2-07 | SupervisorOrManager giải phóng được ghế nội bộ của từng suất vào bất kỳ lúc nào; dashboard quản lý ca nhắc trước giờ ch… | Chưa |  |  |
| S3.3-18 | SCHEDULED chuyển BOARDING từ StartTime − BoardingLeadMinutes, nếu phòng đã sẵn sàng. | Chưa |  |  |
| S3.3-19 | ADS_PLAYING từ StartTime; FEATURE_PLAYING từ FeatureStartTime; CREDITS_ROLLING từ EndTime − CreditsMinutes; CLEANING_IN… | Chưa |  |  |
| S3.3-20 | COMPLETED khi nhân viên bấm "Đã dọn xong". | Chưa |  |  |
| S3.3-21 | Phòng sẵn sàng khi suất trước trong phòng đã COMPLETED hoặc không có suất trước. Nếu phòng chưa sẵn sàng thì suất sau đ… | Chưa |  |  |
| S3.3-22 | Mỗi lần đổi trạng thái đều phát SHOWTIME_OPS_CHANGED. | Chưa |  |  |
| S3.6-11 | VIETQR_SIM: QR mô phỏng hiện thêm trên màn hình phụ của quầy (UI P2). | Chưa |  |  |
| S3.6-17 | Phát POS_PAYMENT_DONE cho màn hình phụ. | Chưa |  |  |
| S3.6-19 | Quét QR hóa đơn online tại quầy bắp nước để nhận bắp nước (3.11). | Chưa |  |  |
| S3.6-21 | Thu ngân có thể chọn gửi yêu cầu duyệt từ xa thay cho nhập PIN. | Chưa |  |  |
| S3.6-22 | Yêu cầu từ xa: phát APPROVAL_REQUESTED tới manager_alerts; quản lý duyệt hoặc từ chối trên dashboard; kết quả APPROVAL_… | Chưa |  |  |
| S3.8-01 | Có thể quét QR hóa đơn (hiện mọi vé trong hóa đơn) hoặc QR vé (hiện vé đó cùng các vé khác trong hóa đơn). | Chưa |  |  |
| S3.8-02 | Mã tồn tại. | Chưa |  |  |
| S3.8-03 | Hóa đơn đang finish. | Chưa |  |  |
| S3.8-04 | Vé không ở trạng thái VOIDED hay SWAPPED. | Chưa |  |  |
| S3.8-05 | Đúng rạp. | Chưa |  |  |
| S3.8-06 | Suất đang BOARDING, ADS_PLAYING hoặc FEATURE_PLAYING. Phòng chưa sẵn sàng thì báo "Phòng đang dọn vệ sinh, chưa thể đón… | Chưa |  |  |
| S3.8-07 | Vé chưa CHECKED_IN. | Chưa |  |  |
| S3.8-08 | Các cảnh báo vàng sau vẫn cho vào, nhưng nhân viên phải xác nhận: phim T13 trở lên (kiểm tra CCCD hoặc VNeID); phim K c… | Chưa |  |  |
| S3.8-09 | Cho vào toàn bộ hoặc từng ghế. Bills.CheckInStatus tự tính thành NOT_CHECKED_IN, PARTIALLY_CHECKED_IN hoặc FULLY_CHECKE… | Chưa |  |  |
| S3.8-10 | Vé điện tử đã vào cửa hiện dấu xác nhận động (đồng hồ chạy theo giây, hoa văn chuyển động) để phân biệt với ảnh chụp mà… | Chưa |  |  |
| S3.8-11 | Nút "Đã dọn xong" (GateStaff) chuyển suất đang CLEANING_IN_PROGRESS sang COMPLETED. Bảng sảnh hiện "Mời vào rạp" khi su… | Chưa |  |  |
| S3.8-12 | Dashboard vận hành hiện sĩ số trực tiếp (đã vào trên đã bán) của các suất đang chạy. | Chưa |  |  |
| S3.10-01 | Quét vé, rồi chọn ghế mới trong cùng suất qua SeatHoldService (route /pos/swap-seat). | Chưa |  |  |
| S3.10-02 | Chênh lệch = giá mới − giá cũ. Dương thì thu thêm (Payments IN); không dương thì đổi ngang, không hoàn tiền. | Chưa |  |  |
| S3.10-03 | Vé cũ chuyển SWAPPED (QR cũ hết hiệu lực); vé mới có QR mới, nằm trong cùng hóa đơn. | Chưa |  |  |
| S3.10-04 | Chỉ đổi được khi vé NOT_CHECKED_IN và trước FeatureStartTime. Ghi AuditLog. | Chưa |  |  |
| S3.10-05 | Chọn suất, nhập lý do, gõ lại mã suất để xác nhận. | Chưa |  |  |
| S3.10-06 | Trong một transaction: OperationalStatus chuyển CANCELLED; mọi hóa đơn finish của suất chuyển INCIDENT_COMPENSATED, hoà… | Chưa |  |  |
| S3.10-07 | Cuối cùng hiện tổng kết: số hóa đơn, số vé, tổng tiền đã hoàn. | Chưa |  |  |
| S3.10-08 | Thuê trọn suất: chỉ với suất chưa bán vé nào. Suất được đánh dấu IsPrivate (ẩn khỏi web khách) và có một hóa đơn CORPOR… | Chưa |  |  |
| S3.10-09 | Phát hành loạt voucher FREE_TICKET: nhập số lượng và hạn dùng, gắn CorporateBookingId. Mỗi mã trả cho một vé ghế STANDA… | Chưa |  |  |
| S3.11-04 | Bắt đầu làm món cho đơn online: khách bấm "Tôi đã tới rạp – làm bắp nước ngay" trên vé điện tử (nút chỉ hiện từ StartTi… | Chưa |  |  |
| S3.11-06 | Đơn bắp nước mua tại quầy hiện ngay trên màn hình bếp. | Chưa |  |  |
| S3.11-07 | Bếp: PREPARING chuyển READY_FOR_PICKUP khi bấm "Sẵn sàng giao" (số gọi lên bảng, có âm báo, gửi FNB_STATUS_CHANGED tới … | Chưa |  |  |
| S3.11-09 | Món không còn đủ một phần thì đặt IsSoldOut = 1 và phát PRODUCT_SOLD_OUT. | Chưa |  |  |
| S3.11-10 | Nhập hàng làm món khả dụng lại thì phát PRODUCT_AVAILABLE. | Chưa |  |  |
| S3.11-11 | Nguyên liệu xuống dưới MinThreshold thì phát STOCK_LOW. | Chưa |  |  |
| S3.11-14 | Nhập kho (Manager): phiếu nhập đơn giản (IMPORT). | Chưa |  |  |
| S3.11-15 | Hủy hàng hỏng: PosStaff lập WastageLogs (PENDING), SupervisorOrManager duyệt, khi đó ActualStock giảm (WASTAGE). | Chưa |  |  |
| S3.12-11 | Báo cáo hủy hàng. | Chưa |  |  |
| S5-04 | Màn hình bếp, soát vé, bảng sảnh có chỉ báo trạng thái kết nối. | Chưa |  |  |
| S8.6-01 | ShowtimeClockService, máy soát vé (kênh 4), bảng sảnh (kênh 6), nút "Đã dọn xong". | Chưa |  |  |
| S8.6-02 | Bếp, bảng gọi số, nút "Tôi đã tới rạp", kho hai tầng, báo hết món, nhập kho, hủy hàng hỏng (kênh 5, 7). | Chưa |  |  |
| S8.6-03 | Màn hình phụ của quầy (kênh 2); duyệt hủy từ xa (kênh 7); giải phóng ghế nội bộ; đổi và nâng hạng ghế; hủy suất do sự c… | Chưa |  |  |
| U2-05 | Màn hình kết quả soát vé dùng nền đặc: hợp lệ #15803D, cảnh báo #B45309, từ chối #B91C1C, chữ trắng. | Chưa |  |  |
| U7.C4-03 | Danh sách ghế, mỗi ghế có trạng thái vào cửa cập nhật trực tiếp. Ghế đã vào hiện dấu xác nhận động: đồng hồ HH:mm:ss ch… | Chưa |  |  |
| U7.C4-05 | Nút "Tôi đã tới rạp – làm bắp nước ngay" (chỉ hiện trong khung giờ cho phép); số gọi hiện lớn khi món đã sẵn sàng. | Chưa |  |  |
| U7.P1-12 | Trong hộp hủy có thêm nút "Gửi quản lý duyệt từ xa"; kết quả duyệt hiện ngay trên POS. | Chưa |  |  |
| U7.P2-01 | P2. Màn hình phụ của quầy /pos/customer-display?terminal=POS-01 (cinema, 1920 × 1080) | Chưa |  |  |
| U7.P2-02 | Lúc chờ: chạy slideshow poster cùng logo. | Chưa |  |  |
| U7.P2-03 | Khi thu ngân chọn suất: trái là sơ đồ ghế lớn (ghế đang chọn sáng lên kèm điểm); phải là giỏ hàng chữ 24–28px, tổng tiề… | Chưa |  |  |
| U7.P2-04 | Khi thanh toán QR: QR 360px ở giữa, số tiền 56px, đồng hồ đếm ngược. | Chưa |  |  |
| U7.P2-05 | Khi xong: "Cảm ơn quý khách", kèm mã vé và QR vé. | Chưa |  |  |
| U7.P3-01 | P3. Máy soát vé /pos/gate-inspector (cinema, ưu tiên điện thoại, chạy được trên desktop) | Chưa |  |  |
| U7.P3-02 | Phần trên: chọn phòng hoặc cửa; ô nhập mã luôn autofocus và tự lấy lại focus sau mỗi lần quét; nút bật camera. | Chưa |  |  |
| U7.P3-03 | Phần còn lại là thẻ kết quả, đổ nền đặc theo màu ở mục 2: xanh là hợp lệ, vàng là cần xác nhận, đỏ là từ chối. | Chưa |  |  |
| U7.P3-04 | Icon 64px, dòng chính 28–32px (ví dụ "Hợp lệ – Phòng 3"), dòng phụ là suất và giờ. | Chưa |  |  |
| U7.P3-05 | Danh sách ghế với checkbox 32px; nút "Cho vào tất cả" cao 56px. | Chưa |  |  |
| U7.P3-06 | Cảnh báo độ tuổi nằm trong khung vàng nổi bật. | Chưa |  |  |
| U7.P3-07 | Sau 6 giây tự quay về trạng thái chờ, có thanh đếm. Khung "Phòng đang dọn" có nút "Đã dọn xong". Có rung và âm báo ngắn… | Chưa |  |  |
| U7.P4-01 | P4. Bếp /pos/fnb-kds (cinema) | Chưa |  |  |
| U7.P4-02 | Hai cột "Đang chuẩn bị" và "Sẵn sàng giao". | Chưa |  |  |
| U7.P4-03 | Thẻ đơn gồm: số gọi 40px; kênh (Quầy hoặc Online); thời gian chờ đổi màu (xanh dưới 5 phút, vàng 5–10 phút, đỏ trên 10 … | Chưa |  |  |
| U7.P4-04 | Đơn mới trượt vào kèm âm báo nhẹ. | Chưa |  |  |
| U7.P5-01 | P5. Bảng gọi số /lobby/pickup-board (cinema, 1920 × 1080) | Chưa |  |  |
| U7.P5-02 | Hai cột: "Đang chuẩn bị" với số 64px màu xám; "Mời nhận đồ" với số 96px màu vàng, nhấp nháy nhẹ 3 lần khi vừa xuất hiện… | Chưa |  |  |
| U7.P5-03 | Kèm đồng hồ và logo. | Chưa |  |  |
| U7.P6-01 | P6. Bảng sảnh /lobby/digital-signage (cinema, 1920 × 1080) | Chưa |  |  |
| U7.P6-02 | Bảng các suất sắp chiếu, gồm các cột: giờ (48px, tabular); phim (tên, nhãn tuổi, định dạng, ngôn ngữ); phòng; trạng thá… | Chưa |  |  |
| U7.P6-03 | Trạng thái hiện bằng nhãn lớn: "Mời vào rạp" màu xanh nhấp nháy chậm, "Đang chiếu", "Đang dọn". | Chưa |  |  |
| U7.P6-04 | Bảng tự cuộn khi nhiều dòng. Chân trang có dòng chữ chạy, ví dụ nhắc quy định độ tuổi. | Chưa |  |  |
| U7.A1-05 | Danh sách cảnh báo thêm các yêu cầu phê duyệt đang chờ; thêm bảng sĩ số trực tiếp các suất đang chạy. | Chưa |  |  |
| U7.A7-01 | A7. Phê duyệt (quản lý ca) | Chưa |  |  |
| U7.A7-02 | Popup thời gian thực ở góc phải, không chặn thao tác khác. Nội dung: hóa đơn, lý do, người yêu cầu, hai nút lớn Duyệt v… | Chưa |  |  |
| U7.A7-03 | Dashboard có thêm danh sách các yêu cầu đang chờ duyệt. | Chưa |  |  |
| C3-38 | Hóa đơn "đã hoàn tất": Status finish, CheckInStatus là NONE hoặc FULLY_CHECKED_IN, FnbStatus là NONE hoặc FULFILLED. | Chưa |  |  |
| UC10 | Xem vé điện tử, báo "Tôi đã tới rạp" (màn hình C4) | Chưa |  |  |
| UC16 | In vé, nhận đơn online tại quầy (màn hình P1) | Chưa |  |  |
| UC17 | Đổi, nâng hạng ghế (màn hình P1) | Chưa |  |  |
| UC18 | Yêu cầu hủy hóa đơn đã thanh toán (màn hình P1) | Chưa |  |  |
| UC19 | Xử lý đơn bắp nước tại bếp, gọi số (màn hình P4, P5) | Chưa |  |  |
| UC20 | Lập phiếu hủy hàng hỏng (màn hình P4) | Chưa |  |  |
| UC22 | Soát vé, check-in từng phần (màn hình P3) | Chưa |  |  |
| UC23 | Xác nhận dọn phòng xong (màn hình P3, P6) | Chưa |  |  |
| UC24 | Duyệt hủy hóa đơn (PIN hoặc từ xa) (màn hình P1, A7) | Chưa |  |  |
| UC25 | Giải phóng ghế nội bộ (màn hình A7) | Chưa |  |  |
| UC26 | Duyệt phiếu hủy hàng (màn hình A7) | Chưa |  |  |
| UC27 | Hủy suất do sự cố (màn hình A5) | Chưa |  |  |
| UC30 | Quản lý nguyên liệu, nhập kho (màn hình A2, A3) | Chưa |  |  |
| UC40 | Bán vé doanh nghiệp (màn hình A2, A3) | Chưa |  |  |
