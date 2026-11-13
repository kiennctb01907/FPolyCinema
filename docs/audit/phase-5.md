# Kiểm tra độc lập – Phase 5

Agent spec-auditor kiểm 5 lô mã (mỗi lô không quá 18 mã, theo nhóm của lệnh `list 5`): đọc spec, truy vết, code thật, chạy test liên quan và xem ảnh chụp giao diện trong artifacts/ui/phase-5. Ngày 14/11/2026.
Cột "Đã sửa" ghi việc đã làm cho các dòng Thiếu hoặc Sai, ghi chú nhỏ đã xử lý, và kết quả của lượt kiểm lại.

| Mã | Kết luận | Lý do | Đã sửa |
|---|---|---|---|
| S3.1-04 | Đạt | Giữ ghế chặn AGE_RESTRICTED theo ngày sinh; thanh toán kiểm lại bằng OnlineAgeRules.Check, phim T13 trở lên chưa tích xác nhận thì AGE_CONFIRM_REQUIRED; có test | – |
| S3.1-08 | Đạt | Phim K có ô xác nhận người giám hộ ở bước 4, server chặn GUARDIAN_REQUIRED; có test | – |
| S3.1-11 | Đạt | Vé online mặc định người lớn; lời nhắc giờ chiếu dưới 13, dưới 16 đọc từ cấu hình, hiện ở lịch chiếu và trang đặt vé | – |
| S3.1-16 | Đạt | Web không có ô chọn đối tượng giảm giá; server chặn loại vé chỉ bán tại quầy ở kênh khác POS | – |
| S3.1-24 | Thiếu | Màn hình QR không có nhãn tuổi; trang đặt vé tự dựng nhãn bằng JS thay vì partial _AgeBadge và chỉ nằm trong khối tóm tắt ẩn trên điện thoại | Pay.cshtml thêm _AgeBadge; Booking/Index thêm dòng tên phim kèm _AgeBadge cho điện thoại và template dùng partial cho khối tóm tắt (BUG-036) |
| S3.5-03 | Đạt | BookingController CustomerOnly; Ensure chỉ cho khách, hóa đơn kênh ONLINE, CustomerId là khách | – |
| S3.6-12 | Sai | Server trừ ví đúng, nhưng pos.js dòng 116 dùng $ thay cho $$ nên renderCart lỗi TypeError, nút "Ví thành viên" không bao giờ bật | Đổi lại $$, kiểm trên trình duyệt: nút hiện khi tra thành viên, không lỗi console (BUG-035) |
| S3.7-01 | Đạt | Trang /payment-sim/pay có QR và hai nút, gửi webhook ký HMAC-SHA256 bằng Payment:WebhookSecret, ProviderTxnId mới mỗi lần mở | – |
| S3.7-02 | Đạt | Sai chữ ký 401; trùng ProviderTxnId trả 200 không xử lý lại, kiểm cả trong transaction; unique index | – |
| S3.7-03 | Đạt | Finalize trong transaction: vé có QR, ghế BOOKED, bắp nước RESERVE và PENDING_PICKUP, cộng điểm; sau commit phát SEAT_MAP_UPDATED, PAYMENT_STATUS_CHANGED | – |
| S3.7-04 | Đạt | Hóa đơn đã hết hạn được giữ lại đúng các ghế cũ từ ảnh chụp trong Payments PENDING, trong cùng transaction | – |
| S3.7-05 | Đạt | Giữ đủ ghế thì gán lại đối tượng vé và chuyển finish; có test webhook đến muộn | – |
| S3.7-06 | Đạt | Không giữ đủ thì OVERBOOKED_REFUNDED, hoàn tiền phương thức gốc, voucher bù, thông báo, sự kiện; có test | – |
| S3.7-07 | Đạt | Webhook thất bại ghi Payment FAILED, hóa đơn vẫn waiting, thử lại được; có test | – |
| S3.7-08 | Đạt | Màn hình QR đếm ngược theo HoldExpiresAt, JoinBill, nghe PAYMENT_STATUS_CHANGED rồi tự chuyển sang vé; mất kết nối thì hỏi trạng thái mỗi 5 giây | – |
| S3.9-01 | Đạt | OwnBill lọc chủ đơn và kênh ONLINE; SelfCancelRules chặn không phải chủ, CORPORATE, chưa finish; có unit test | – |
| S3.9-02 | Đạt | Hạn chót = StartTime − SelfCancelMinMinutesBeforeStart, đúng mốc vẫn hủy được; có test | – |
| S3.9-03 | Đạt | Chặn khi có vé đã vào cửa hoặc bắp nước đã làm; kiểm lại trong transaction | – |
| S3.9-04 | Đạt | Đếm lần tự hủy theo CancelledAt trong tháng dương lịch theo IClock; có unit test | – |
| S3.9-05 | Đạt | Chuyển cancel CANCELLED_BY_USER qua BillStateMachine; không đủ điều kiện thì SELF_CANCEL_NOT_ALLOWED | – |
| S3.9-06 | Đạt | Hoàn đúng số tiền đã trả vào ví qua WalletService.Credit, trả lượt voucher và điểm đã đổi; có test | – |
| S3.9-07 | Đạt | Trả ghế về AVAILABLE và phát SEAT_MAP_UPDATED, RELEASE kho, thu hồi điểm đã cộng; có test | – |
| S3.9-08 | Đạt | Trang vé ghi đủ điều kiện, hạn chót, số lần còn lại; không đủ thì nút khóa và ghi lý do; hộp xác nhận dùng ui.confirm | – |
| S3.11-03 | Đạt | Finalize đặt PENDING_PICKUP và ReserveForBill (khóa nguyên liệu, RESERVE, tăng ReservedStock) | – |
| S3.11-08 | Đạt | ReleaseForVoid chỉ khi PENDING_PICKUP, ghi RELEASE và giảm ReservedStock; có test | – |
| S3.11-13 | Đạt | /menu và bước 3 hiện "Tạm hết" theo kho rạp, JS khóa nút, server chặn STOCK_INSUFFICIENT | – |
| S3.13-04 | Đạt | Mọi luồng đổi ví qua WalletService.Debit, Credit kèm WalletTransactions; ghi chú: UserDAL.AddWallet không dùng nhưng đổi số dư không ghi lịch sử | Xóa UserDAL.AddWallet |
| S4.1-09 | Đạt | Segments chia đoạn liền cột, cửa sổ trượt K ghế, ghế đôi đủ cặp; có unit test | – |
| S4.1-10 | Đạt | Bỏ cụm không qua OrphanSeatValidator; có unit test | – |
| S4.1-11 | Đạt | Xếp theo điểm rồi khoảng cách tới r_ideal, tối đa 3; có unit test | – |
| S4.1-12 | Sai | Logic đúng nhưng câu gợi ý viết thường cả loại ghế: "cụm ghế vip trung tâm" thay vì "ghế VIP" | Chỉ viết thường chữ đầu (LowerFirst) (BUG-039) |
| S7-04 | Đạt | ClusterFinder là lớp tĩnh thuần có 5 unit test | – |
| S8.5-01 | Sai | Chip "Tất cả rạp" ở /showtimes không gửi cinemaId, controller thay bằng rạp đang chọn nên không xem được mọi rạp | cinemaId=0 là tất cả rạp (CustomerController.CinemaFilter, ScheduleFilterVm.Url) (BUG-040) |
| S8.5-02 | Đạt | Bước 1 liệt kê suất cùng phim, rạp, ngày còn bán; hết vé bị khóa; đổi suất trả ghế cũ | – |
| S8.5-03 | Đạt | SeatSuggestService theo persona, banner 3 cụm, bản đồ điểm, gợi ý chuyển suất; sơ đồ cập nhật kênh 1 | – |
| S8.5-04 | Đạt | Bước 3 có bắp nước, voucher, điểm, ví; thanh toán xong giữ kho | – |
| S8.5-05 | Đạt | Webhook ký HMAC, chống lặp, xử lý đến muộn và OVERBOOKED_REFUNDED, JoinBill kiểm quyền | – |
| S8.5-06 | Thiếu | Tab Ví ghi cứng "1 điểm = 1.000 ₫, tối đa 50%" thay vì đọc cấu hình | Đọc PointValueVnd, MaxPointsRedeemPercent qua AccountVm |
| U7.C1-01 | Đạt | Route /, AllowAnonymous; đủ hero, mua vé nhanh, tab phim, combo, ưu đãi, trạng thái rỗng | – |
| U7.C1-02 | Sai | Tên phim 30–36px thay vì 36–48px; phần thứ ba của dòng phụ là ngôn ngữ chứ không phải định dạng; phim không có trailer vẫn hiện "Xem trailer" nhưng mở trang chi tiết | Cỡ chữ clamp(--fs-4xl, 4vw, --fs-display); dòng phụ dùng định dạng phòng của suất sắp tới; nút "Xem chi tiết" khi không có trailer (BUG-037) |
| U7.C1-03 | Thiếu | Thanh mua vé nhanh nuốt lỗi tải, không có trạng thái đang tải | Ô kế tiếp hiện "Đang tải…", lỗi hiện dòng báo kèm nút "thử lại" |
| U7.C1-04 | Đạt | Tab Đang chiếu, Sắp chiếu; lưới 2/3/4/5 cột; khoảng cách 16px, 24px | – |
| U7.C1-05 | Đạt | 4 combo có "Tạm hết" theo kho; ưu đãi công khai; trạng thái rỗng | – |
| U7.C2-01 | Đạt | Route /movies/{id}; ảnh nền mờ, poster 280px; phim không bán trả 404 | – |
| U7.C2-02 | Đạt | Tên phim và _AgeBadge kèm câu giải thích | – |
| U7.C2-03 | Đạt | Chip cảnh báo nội dung từ ContentWarnings | – |
| U7.C2-04 | Đạt | Lưới 6 thông tin đủ | – |
| U7.C2-05 | Đạt | .movie-desc max-width 65ch | – |
| U7.C2-06 | Đạt | 7 chip ngày theo ngày vận hành; định dạng, ngôn ngữ dạng segmented | – |
| U7.C2-07 | Đạt | Nhóm theo rạp rồi "định dạng · ngôn ngữ"; giờ 16px đậm 600, "còn N ghế" | – |
| U7.C2-08 | Đạt | Suất hết vé khóa kèm "Hết vé"; suất quá giờ bán bị ẩn | – |
| U7.C2-09 | Thiếu | Chip "Hết vé" không có icon mặt trăng dù suất kết thúc sau 22:00 | Chip hết vé cũng có icon và tooltip |
| U7.C3-01 | Đạt | Route /booking/{showtimeId}; xem sơ đồ không cần đăng nhập, ghi dữ liệu cần CustomerOnly | – |
| U7.C3-02 | Đạt | Stepper 4 bước, bản gọn "Bước x/4" trên điện thoại | – |
| U7.C3-03 | Đạt | 8/12 và 4/12 từ 992px, tóm tắt sticky top 88px | – |
| U7.C3-04 | Đạt | Thanh dính đáy có tổng tiền và nút chính, chạm mở bottom sheet | – |
| U7.C3-05 | Sai | Bấm ghế mở hộp đăng nhập kèm returnUrl, nhưng AfterLogin xét khu vực theo vai trò Manager nên mọi returnUrl của khách bị đổi thành "/" | AfterLogin dùng RoleRouting.CanReturnTo; test Login_CustomerReturnsToBookingOfSameShowtime_ButNotToStaffArea (BUG-034) |
| U7.C3-06 | Đạt | Segmented 4 persona, công tắc bản đồ điểm, bộ chọn số vé, "Chọn giúp tôi" | – |
| U7.C3-07 | Đạt | Banner kính mờ viền vàng "AI đề xuất … · 91/100" kèm "Chọn nhanh" | – |
| U7.C3-08 | Đạt | _SeatMap có chú thích dưới sơ đồ | – |
| U7.C3-09 | Đạt | Thẻ món 3 cột, voucher, công tắc điểm và ví | – |
| U7.C3-10 | Đạt | Thẻ radio "VietQR (mô phỏng)", "Ví FPoly" | – |
| U7.C3-11 | Đạt | Câu xác nhận độ tuổi đúng quy định, server kiểm lại | – |
| U7.C3-12 | Đạt | Nút "Thanh toán 245.000 ₫" định dạng vi-VN | – |
| U7.C3-13 | Đạt | QR 360px, đồng hồ, trạng thái trực tiếp rồi tự chuyển sang vé | – |
| U7.C3-14 | Đạt | Khối tóm tắt đủ poster, nhãn tuổi, suất, ghế kèm điểm, bắp nước, giảm giá, tổng 24px, đồng hồ, nút chính | – |
| U7.C4-01 | Đạt | /tickets/{billCode} CustomerOnly, chỉ chủ đơn xem được | – |
| U7.C4-02 | Thiếu | Cuống vé có răng cưa, QR 240px, mã vé nhưng thông tin suất chỉ ở khối đầu trang, không nằm trên thẻ vé | Mỗi cuống vé thêm tên phim, nhãn tuổi, ngày giờ, phòng; bỏ chữ định dạng lặp khi tên phòng đã có (BUG-038) |
| U7.C4-04 | Đạt | Khối bắp nước dùng _StatusBadge theo UI mục 6 | – |
| U7.C4-06 | Đạt | Nút "Hủy vé" kèm điều kiện, khóa và ghi lý do khi không đủ | – |
| U7.C5-01 | Đạt | /account CustomerOnly, 5 tab | – |
| U7.C5-02 | Đạt | Thẻ hạng có thanh tiến độ và QR mã thành viên; TierProgressCalculator có unit test | – |
| U7.C5-03 | Đạt | Đủ 5 tab, vé sắp tới và đã xem, ví và điểm có lịch sử, trạng thái rỗng | – |
| U8-03 | Đạt | Ghế 28px dưới 992px, sơ đồ cuộn ngang, nút phóng to 3 mức | – |
| U8-09 | Thiếu | Nhấn giữ bị hủy bởi bất kỳ pointermove nào, cờ suppressClick có thể nuốt lần chạm kế tiếp khi trình duyệt không phát click, chưa chặn menu chọn chữ | Nhấn giữ chịu rung dưới 10px, bỏ qua click trong 400ms sau khi nhả, .seat chặn touch-callout và user-select |
| U8-10 | Đạt | Bản đồ điểm heat-1..5 chỉ tô ghế trống; cụm AI viền vàng kèm nhãn | – |
| U9-16 | Đạt | Câu tự hủy quá hạn theo cấu hình, unit test so nguyên văn | – |
| U9-17 | Đạt | PAYMENT_EXPIRED theo SeatHoldMinutes, test so nguyên văn; ghi chú: hộp hết giờ giữ ghế ở booking.js ghi cứng "10 phút" | Hộp hết giờ và câu hướng dẫn đọc SeatHoldMinutes |
| C6-03 | Đạt | JoinBill kiểm quyền; PAYMENT_STATUS_CHANGED gửi nhóm bill_{id} | – |
| C7-06 | Đạt | Payment:WebhookSecret để trống, bind qua PaymentOptions, dùng để ký và kiểm | – |
| C8-08 | Đạt | Đủ 4 mã trong ErrorCodes và đều được ném thật | – |
| UC03 | Đạt | Đổi mật khẩu từ tab Thông tin cá nhân, đăng xuất phiên khác | – |
| UC04 | Đạt | Cập nhật thông tin cá nhân có Anti-Forgery, quay về /account?tab=profile | – |
| UC06 | Đạt | Phim, lịch chiếu, thực đơn không cần đăng nhập, lọc đủ, trạng thái rỗng | – |
| UC07 | Đạt | Đặt vé 4 bước trong transaction, kiểm lại ở server | – |
| UC08 | Đạt | Gợi ý theo persona, tối đa 3 cụm, gợi ý chuyển suất | – |
| UC09 | Đạt | Webhook HMAC, chống lặp, đến muộn, màn hình QR trực tiếp | – |
| UC11 | Đạt | Tự hủy đủ điều kiện và hệ quả trong transaction | – |
| UC12 | Đạt | Ví, điểm, voucher, thông báo và đánh dấu đã đọc | – |

Tổng lượt đầu: 78 Đạt, 6 Thiếu, 5 Sai (89 mã).
Đã sửa 11 mã Thiếu hoặc Sai (BUG-034..040, quyết định 57–62 trong PROGRESS.md) và các ghi chú nhỏ (xóa UserDAL.AddWallet không dùng, số phút giữ ghế đọc từ cấu hình). Lượt kiểm lại U7.C3-05, U7.C4-02, S3.1-24, S3.6-12, S8.5-06, U7.C1-02, U7.C1-03, U7.C2-09, S4.1-12, S8.5-01, U8-09, S3.13-04: 12 Đạt, 0 Thiếu, 0 Sai (agent chạy 6 test liên quan, đều qua). Ghi chú còn lại (lưu hồ sơ của khách bị lỗi kiểm tra dữ liệu thì hiện form ở Views/Login/Profile thay vì tab Thông tin cá nhân) đã sửa sau đó: BUG-041, POST /account/profile, có test.

Kết quả: Đạt
