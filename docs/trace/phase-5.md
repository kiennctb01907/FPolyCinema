# Truy vết yêu cầu – Phase 5 – Web khách và đặt vé online

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
| S3.1-04 | Online: tài khoản có ngày sinh mà chưa đủ tuổi thì chặn. Phim T13 trở lên luôn yêu cầu tích "Tôi xác nhận người xem đủ … | Chưa |  |  |
| S3.1-08 | Online: hiện lưu ý và yêu cầu tích xác nhận. | Chưa |  |  |
| S3.1-11 | Online không bán vé trẻ em. Suất kết thúc từ 22:00 hiện lưu ý "Trẻ em dưới 13 tuổi không vào được suất này", suất kết t… | Chưa |  |  |
| S3.1-16 | Các loại vé này bị ẩn trên web khách. | Chưa |  |  |
| S3.1-24 | Web khách: thẻ phim, trang phim, chọn suất, chọn ghế, thanh toán, vé điện tử. Trang phim hiện thêm cảnh báo nội dung (M… | Chưa |  |  |
| S3.5-03 | Online: hóa đơn có Channel ONLINE và khách phải đăng nhập. | Chưa |  |  |
| S3.6-12 | WALLET: dành cho thành viên đã tra cứu; trừ ví theo 3.13. | Chưa |  |  |
| S3.7-01 | Không tích hợp cổng thanh toán thật. Trang mô phỏng /payment-sim/pay?billId= hiện QR và hai nút "Thanh toán thành công"… | Chưa |  |  |
| S3.7-02 | Webhook xử lý trùng lặp an toàn: ProviderTxnId là duy nhất; nhận lại cùng mã thì trả OK mà không xử lý lần nữa. Sai chữ… | Chưa |  |  |
| S3.7-03 | Thanh toán thành công khi hóa đơn còn waiting và ghế vẫn đang giữ: chuyển finish trong transaction (tạo vé, ghế BOOKED,… | Chưa |  |  |
| S3.7-04 | Thanh toán thành công nhưng đến muộn (hóa đơn đã EXPIRED): giữ lại đúng các ghế cũ trong transaction. | Chưa |  |  |
| S3.7-05 | Giữ được hết thì chuyển finish. | Chưa |  |  |
| S3.7-06 | Không giữ được thì chuyển OVERBOOKED_REFUNDED: hoàn tiền về phương thức gốc, tặng voucher CompensationVoucherAmount, tạ… | Chưa |  |  |
| S3.7-07 | Thanh toán thất bại: hóa đơn vẫn waiting đến khi hết hạn; khách thử lại được. | Chưa |  |  |
| S3.7-08 | Trang chờ thanh toán đếm ngược theo HoldExpiresAt và nghe PAYMENT_STATUS_CHANGED để tự chuyển sang vé điện tử. | Chưa |  |  |
| S3.9-01 | Hóa đơn ONLINE ở trạng thái finish, của chính khách đó, không phải hóa đơn CORPORATE. | Chưa |  |  |
| S3.9-02 | now ≤ StartTime − SelfCancelMinMinutesBeforeStart. | Chưa |  |  |
| S3.9-03 | Mọi vé đều NOT_CHECKED_IN; FnbStatus là NONE hoặc PENDING_PICKUP. | Chưa |  |  |
| S3.9-04 | Số lần CANCELLED_BY_USER trong tháng dương lịch hiện tại nhỏ hơn SelfCancelMaxPerMonth. | Chưa |  |  |
| S3.9-05 | Hóa đơn chuyển cancel với CANCELLED_BY_USER. | Chưa |  |  |
| S3.9-06 | Hoàn 100% phần đã trả vào ví (WalletTransactions); voucher và điểm đã dùng được trả lại. | Chưa |  |  |
| S3.9-07 | Ghế về AVAILABLE (phát SEAT_MAP_UPDATED); giải phóng ReservedStock; thu hồi điểm đã cộng. | Chưa |  |  |
| S3.9-08 | Giao diện hiện rõ điều kiện và số lần hủy còn lại trong tháng. Khi không đủ điều kiện, nút bị khóa và có ghi lý do. | Chưa |  |  |
| S3.11-03 | Online thanh toán thành công: ReservedStock tăng theo định mức (RESERVE), FnbStatus = PENDING_PICKUP. | Chưa |  |  |
| S3.11-08 | Đơn hủy hợp lệ trước khi làm thì giải phóng ReservedStock (RELEASE). | Chưa |  |  |
| S3.11-13 | Web khách hiện "Tạm hết" và không cho chọn. | Chưa |  |  |
| S3.13-04 | Ví: Users.WalletBalance chỉ thay đổi cùng với một bản ghi WalletTransactions, trong cùng transaction (WALLET_INSUFFICIE… | Chưa |  |  |
| S4.1-09 | Duyệt từng đoạn bằng cửa sổ trượt K ghế trống liền nhau; với SWEETBOX chỉ nhận nguyên cặp. | Chưa |  |  |
| S4.1-10 | Bỏ các cụm vi phạm luật ghế mồ côi. | Chưa |  |  |
| S4.1-11 | Xếp theo điểm trung bình; nếu bằng điểm thì ưu tiên cụm gần r_ideal hơn. Trả tối đa 3 phương án. | Chưa |  |  |
| S4.1-12 | Gợi ý chuyển suất: nếu phương án tốt nhất dưới GoodClusterScore, tìm suất cùng phim, cùng rạp, cùng LanguageFormat, Sta… | Chưa |  |  |
| S7-04 | ClusterFinder. | Chưa |  |  |
| S8.5-01 | Trang chủ, danh sách và chi tiết phim, lịch chiếu lọc theo rạp, ngày, định dạng, ngôn ngữ; thực đơn bắp nước. | Chưa |  |  |
| S8.5-02 | Chọn suất. | Chưa |  |  |
| S8.5-03 | Chọn ghế: SeatScoring, cụm ghế gợi ý, heatmap, gợi ý chuyển suất, cập nhật thời gian thực qua kênh 1. | Chưa |  |  |
| S8.5-04 | Bắp nước và ưu đãi; giữ kho khi thanh toán (RESERVE). | Chưa |  |  |
| S8.5-05 | Thanh toán qua trang mô phỏng và webhook (kênh 3), xử lý webhook đến muộn. | Chưa |  |  |
| S8.5-06 | Vé điện tử; tài khoản (vé của tôi, ví và điểm, voucher, thông báo, thông tin cá nhân, mã thành viên); tự hủy vé. | Chưa |  |  |
| U7.C1-01 | C1. Trang chủ / | Chưa |  |  |
| U7.C1-02 | Hero cao 420px (điện thoại 320px) giới thiệu 1 phim nổi bật: nền là poster phóng to làm mờ, phủ gradient tối từ dưới lê… | Chưa |  |  |
| U7.C1-03 | Thanh mua vé nhanh dạng thẻ kính mờ nằm ngay dưới hero: chọn Rạp, Phim, Ngày, Suất, rồi bấm "Mua vé". | Chưa |  |  |
| U7.C1-04 | Tab "Đang chiếu" và "Sắp chiếu" với lưới thẻ phim: 5 cột từ 1200px, 4 cột từ 992px, 3 cột từ 768px, 2 cột trên điện tho… | Chưa |  |  |
| U7.C1-05 | Khối 4 combo bắp nước nổi bật; khối ưu đãi công khai. | Chưa |  |  |
| U7.C2-01 | C2. Chi tiết phim /movies/{id} | Chưa |  |  |
| U7.C2-02 | Tên phim và nhãn tuổi, kèm câu giải thích, ví dụ "T16: Phim dành cho khán giả từ đủ 16 tuổi trở lên". | Chưa |  |  |
| U7.C2-03 | Chip cảnh báo nội dung. | Chưa |  |  |
| U7.C2-04 | Lưới thông tin: thời lượng, khởi chiếu, thể loại, ngôn ngữ, nhà phát hành, số giấy phép. | Chưa |  |  |
| U7.C2-05 | Mô tả phim, tối đa 65 ký tự mỗi dòng. | Chưa |  |  |
| U7.C2-06 | Dải chip 7 ngày, ví dụ "Hôm nay 06/11", "T7 07/11"; bộ lọc định dạng và ngôn ngữ dạng segmented. | Chưa |  |  |
| U7.C2-07 | Nhóm theo rạp, rồi theo "2D · Phụ đề"; mỗi suất là một chip giờ: giờ 16px đậm 600 và dòng nhỏ "còn 42 ghế". | Chưa |  |  |
| U7.C2-08 | Suất hết vé hiện bị khóa kèm chữ "Hết vé"; suất đã qua thì ẩn. | Chưa |  |  |
| U7.C2-09 | Suất kết thúc từ 22:00 có icon mặt trăng, tooltip nhắc quy định giờ chiếu cho trẻ em. | Chưa |  |  |
| U7.C3-01 | C3. Đặt vé /booking/{showtimeId} | Chưa |  |  |
| U7.C3-02 | Stepper 4 bước ở trên cùng. | Chưa |  |  |
| U7.C3-03 | Từ 992px chia 2 cột: nội dung bước chiếm 8/12, khối tóm tắt dính bên phải chiếm 4/12 (cách đỉnh 88px). | Chưa |  |  |
| U7.C3-04 | Trên điện thoại: một cột, thêm thanh dính đáy hiện tổng tiền và nút chính; chạm vào thanh thì mở bottom sheet chi tiết. | Chưa |  |  |
| U7.C3-05 | Khi chưa đăng nhập vẫn xem được sơ đồ ghế. Bấm chọn ghế thì mở hộp đăng nhập hoặc đăng ký, xong quay lại đúng suất. | Chưa |  |  |
| U7.C3-06 | Thanh điều khiển trên sơ đồ: segmented 4 persona (Đẹp nhất, Gần màn hình, Cặp đôi, Gần lối đi); công tắc "Bản đồ điểm A… | Chưa |  |  |
| U7.C3-07 | Banner gợi ý (kính mờ, viền vàng), ví dụ "AI đề xuất G6–G8 · 96/100", kèm nút "Chọn nhanh". | Chưa |  |  |
| U7.C3-08 | Sơ đồ ghế theo mục 8; chú thích nằm dưới sơ đồ. | Chưa |  |  |
| U7.C3-09 | Bước 3, bắp nước và ưu đãi: lưới thẻ món 3 cột; ô voucher kèm nút áp dụng; công tắc dùng điểm (hiện số điểm và số tiền … | Chưa |  |  |
| U7.C3-10 | Chọn phương thức bằng thẻ radio: "VietQR (mô phỏng)", "Ví FPoly". | Chưa |  |  |
| U7.C3-11 | Ô tích xác nhận độ tuổi hoặc giờ chiếu cho trẻ em khi cần, ghi đúng câu theo quy định. | Chưa |  |  |
| U7.C3-12 | Nút "Thanh toán 245.000 ₫". | Chưa |  |  |
| U7.C3-13 | Màn hình QR: QR lớn ở giữa, đồng hồ đếm ngược, trạng thái cập nhật trực tiếp ("Đang chờ thanh toán…" → "Thanh toán thàn… | Chưa |  |  |
| U7.C3-14 | Khối tóm tắt: poster nhỏ, tên phim và nhãn tuổi; rạp và phòng; ngày giờ; định dạng và ngôn ngữ; chip ghế kèm điểm và gi… | Chưa |  |  |
| U7.C4-01 | C4. Vé điện tử /tickets/{billCode} | Chưa |  |  |
| U7.C4-02 | Thẻ vé có đường răng cưa như cuống vé; QR 240px trên nền trắng; mã vé; thông tin suất chiếu. | Chưa |  |  |
| U7.C4-04 | Khối bắp nước: trạng thái hiển thị theo mục 6. | Chưa |  |  |
| U7.C4-06 | Nút "Hủy vé" kèm giải thích điều kiện; khi không đủ điều kiện thì nút bị khóa và ghi lý do. | Chưa |  |  |
| U7.C5-01 | C5. Tài khoản /account | Chưa |  |  |
| U7.C5-02 | Thẻ hạng thành viên có thanh tiến độ tới hạng kế tiếp, kèm mã thành viên dạng QR để quét ở quầy. | Chưa |  |  |
| U7.C5-03 | Các tab: Vé của tôi (sắp tới, đã xem); Ví và điểm (số dư, lịch sử); Voucher; Thông báo; Thông tin cá nhân. | Chưa |  |  |
| U8-03 | Điện thoại tối thiểu 28px, đặt trong vùng cuộn ngang, có nút phóng to và thu nhỏ 3 mức. | Chưa |  |  |
| U8-09 | Trên điện thoại: chạm để chọn, nhấn giữ để xem tooltip. | Chưa |  |  |
| U8-10 | Heatmap bật: ghế trống tô theo thang --heat-*; ghế đã bán hoặc đang giữ vẫn hiện đúng trạng thái. Cụm AI đề xuất có viề… | Chưa |  |  |
| U9-16 | SELF_CANCEL_NOT_ALLOWED: "Vé chỉ tự hủy được trước giờ chiếu 2 tiếng. Suất này bắt đầu lúc 19:30." | Chưa |  |  |
| U9-17 | PAYMENT_EXPIRED: "Đã hết 10 phút giữ ghế. Bạn chọn lại ghế để tiếp tục nhé." | Chưa |  |  |
| UC03 | Đổi mật khẩu (màn hình C5, A9) | Chưa |  |  |
| UC04 | Cập nhật thông tin cá nhân (màn hình C5, A9) | Chưa |  |  |
| UC06 | Xem phim, lịch chiếu, thực đơn bắp nước (màn hình C1, C2) | Chưa |  |  |
| UC07 | Đặt vé online (màn hình C3) | Chưa |  |  |
| UC08 | Chọn ghế với gợi ý AI (màn hình C3, P1) | Chưa |  |  |
| UC09 | Thanh toán online (màn hình C3) | Chưa |  |  |
| UC11 | Tự hủy vé (màn hình C4, C5) | Chưa |  |  |
| UC12 | Quản lý ví, điểm, voucher, thông báo (màn hình C5) | Chưa |  |  |
