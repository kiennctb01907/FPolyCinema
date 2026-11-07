# Giao diện FPolyCinema – mục 7: bản mô tả từng màn hình

Mục lục và cách đọc mã yêu cầu như [S3.6-04 P4]: docs/README.md. Tên chuẩn: docs/CONTRACTS.md. Giao diện: docs/ui.

## 7. Bản mô tả màn hình

### Web khách (theme cinema)
- [U7.C1-01 P5] C1. Trang chủ `/`
  - [U7.C1-02 P5] Hero cao 420px (điện thoại 320px) giới thiệu 1 phim nổi bật: nền là poster phóng to làm mờ, phủ gradient tối từ dưới lên. Bên trái có nhãn tuổi, tên phim 36–48px, dòng "thể loại · thời lượng · định dạng", nút "Đặt vé" (primary) và nút "Xem trailer" (ghost).
  - [U7.C1-03 P5] Thanh mua vé nhanh dạng thẻ kính mờ nằm ngay dưới hero: chọn Rạp, Phim, Ngày, Suất, rồi bấm "Mua vé".
  - [U7.C1-04 P5] Tab "Đang chiếu" và "Sắp chiếu" với lưới thẻ phim: 5 cột từ 1200px, 4 cột từ 992px, 3 cột từ 768px, 2 cột trên điện thoại; khoảng cách 24px (điện thoại 16px).
  - [U7.C1-05 P5] Khối 4 combo bắp nước nổi bật; khối ưu đãi công khai.
  - [U7.C1-06 P7] Nút trợ lý AI nổi ở góc phải dưới: hình tròn 56px.
- [U7.C2-01 P5] C2. Chi tiết phim `/movies/{id}`
  - Phần đầu có ảnh nền. Cột trái là poster rộng 280px. Cột phải gồm:
    - [U7.C2-02 P5] Tên phim và nhãn tuổi, kèm câu giải thích, ví dụ "T16: Phim dành cho khán giả từ đủ 16 tuổi trở lên".
    - [U7.C2-03 P5] Chip cảnh báo nội dung.
    - [U7.C2-04 P5] Lưới thông tin: thời lượng, khởi chiếu, thể loại, ngôn ngữ, nhà phát hành, số giấy phép.
    - [U7.C2-05 P5] Mô tả phim, tối đa 65 ký tự mỗi dòng.
  - Lịch chiếu:
    - [U7.C2-06 P5] Dải chip 7 ngày, ví dụ "Hôm nay 06/11", "T7 07/11"; bộ lọc định dạng và ngôn ngữ dạng segmented.
    - [U7.C2-07 P5] Nhóm theo rạp, rồi theo "2D · Phụ đề"; mỗi suất là một chip giờ: giờ 16px đậm 600 và dòng nhỏ "còn 42 ghế".
    - [U7.C2-08 P5] Suất hết vé hiện bị khóa kèm chữ "Hết vé"; suất đã qua thì ẩn.
    - [U7.C2-09 P5] Suất kết thúc từ 22:00 có icon mặt trăng, tooltip nhắc quy định giờ chiếu cho trẻ em.
- [U7.C3-01 P5] C3. Đặt vé `/booking/{showtimeId}`
  - Bố cục chung:
    - [U7.C3-02 P5] Stepper 4 bước ở trên cùng.
    - [U7.C3-03 P5] Từ 992px chia 2 cột: nội dung bước chiếm 8/12, khối tóm tắt dính bên phải chiếm 4/12 (cách đỉnh 88px).
    - [U7.C3-04 P5] Trên điện thoại: một cột, thêm thanh dính đáy hiện tổng tiền và nút chính; chạm vào thanh thì mở bottom sheet chi tiết.
    - [U7.C3-05 P5] Khi chưa đăng nhập vẫn xem được sơ đồ ghế. Bấm chọn ghế thì mở hộp đăng nhập hoặc đăng ký, xong quay lại đúng suất.
  - Bước 2, chọn ghế:
    - [U7.C3-06 P5] Thanh điều khiển trên sơ đồ: segmented 4 persona (Đẹp nhất, Gần màn hình, Cặp đôi, Gần lối đi); công tắc "Bản đồ điểm AI"; bộ chọn số vé; nút "Chọn giúp tôi".
    - [U7.C3-07 P5] Banner gợi ý (kính mờ, viền vàng), ví dụ "AI đề xuất G6–G8 · 96/100", kèm nút "Chọn nhanh".
    - [U7.C3-08 P5] Sơ đồ ghế theo mục 8; chú thích nằm dưới sơ đồ.
  - [U7.C3-09 P5] Bước 3, bắp nước và ưu đãi: lưới thẻ món 3 cột; ô voucher kèm nút áp dụng; công tắc dùng điểm (hiện số điểm và số tiền quy đổi); công tắc dùng ví.
  - Bước 4, thanh toán:
    - [U7.C3-10 P5] Chọn phương thức bằng thẻ radio: "VietQR (mô phỏng)", "Ví FPoly".
    - [U7.C3-11 P5] Ô tích xác nhận độ tuổi hoặc giờ chiếu cho trẻ em khi cần, ghi đúng câu theo quy định.
    - [U7.C3-12 P5] Nút "Thanh toán 245.000 ₫".
    - [U7.C3-13 P5] Màn hình QR: QR lớn ở giữa, đồng hồ đếm ngược, trạng thái cập nhật trực tiếp ("Đang chờ thanh toán…" → "Thanh toán thành công" kèm dấu ✓), rồi tự chuyển sang vé.
  - [U7.C3-14 P5] Khối tóm tắt: poster nhỏ, tên phim và nhãn tuổi; rạp và phòng; ngày giờ; định dạng và ngôn ngữ; chip ghế kèm điểm và giá; bắp nước; giảm giá; tổng tiền 24px đậm 700; đồng hồ giữ ghế; nút chính.
- [U7.C4-01 P5] C4. Vé điện tử `/tickets/{billCode}`
  - [U7.C4-02 P5] Thẻ vé có đường răng cưa như cuống vé; QR 240px trên nền trắng; mã vé; thông tin suất chiếu.
  - [U7.C4-03 P6] Danh sách ghế, mỗi ghế có trạng thái vào cửa cập nhật trực tiếp. Ghế đã vào hiện dấu xác nhận động: đồng hồ HH:mm:ss chạy và dải gradient chuyển động.
  - [U7.C4-04 P5] Khối bắp nước: trạng thái hiển thị theo mục 6.
  - [U7.C4-05 P6] Nút "Tôi đã tới rạp – làm bắp nước ngay" (chỉ hiện trong khung giờ cho phép); số gọi hiện lớn khi món đã sẵn sàng.
  - [U7.C4-06 P5] Nút "Hủy vé" kèm giải thích điều kiện; khi không đủ điều kiện thì nút bị khóa và ghi lý do.
- [U7.C5-01 P5] C5. Tài khoản `/account`
  - [U7.C5-02 P5] Thẻ hạng thành viên có thanh tiến độ tới hạng kế tiếp, kèm mã thành viên dạng QR để quét ở quầy.
  - [U7.C5-03 P5] Các tab: Vé của tôi (sắp tới, đã xem); Ví và điểm (số dư, lịch sử); Voucher; Thông báo; Thông tin cá nhân.
- [U7.C6-01 P2] C6. Đăng nhập và đăng ký: desktop chia đôi màn hình (bên trái ảnh không khí rạp phim, bên phải form rộng 400px); điện thoại chỉ hiện form. Có nút hiện hoặc ẩn mật khẩu, lỗi ghi cụ thể, liên kết quên mật khẩu.
  - [U7.C6-02 P2] Quên mật khẩu gồm 2 bước trên cùng thẻ form: bước 1 nhập email; bước 2 nhập mã OTP 6 số (6 ô, tự nhảy ô, dán được cả chuỗi), có đếm ngược 5 phút và nút "Gửi lại mã" sau 60 giây. Xong thì báo "Mật khẩu mới đã được gửi tới email của bạn" và về trang đăng nhập. Lời nhắn chung cho email không tồn tại để không lộ tài khoản.
  - [U7.C6-03 P2] Đăng nhập bằng mật khẩu được cấp mà MustChangePassword = 1 thì chuyển ngay tới trang Đổi mật khẩu bắt buộc (không có menu, chỉ có form và nút Đăng xuất).
- [U7.C7-01 P7] C7. Trợ lý đặt vé (widget)
  - [U7.C7-02 P7] Khung: panel 380 × 600px trên desktop, dạng sheet toàn màn hình trên điện thoại. Đầu panel có tiêu đề "Trợ lý đặt vé" và nút đóng.
  - [U7.C7-03 P7] Hội thoại: tin nhắn dạng bong bóng; chip câu hỏi gợi ý; ô nhập kèm nút gửi; skeleton "Đang tìm suất phù hợp…" khi đang xử lý.
  - [U7.C7-04 P7] Thẻ đặt vé trong khung chat: poster nhỏ; tên phim và nhãn tuổi; rạp, phòng, giờ, định dạng; sơ đồ thu nhỏ quanh cụm ghế (ghế chọn màu đỏ); danh sách ghế kèm điểm; combo; tổng tiền; nút "Giữ ghế & thanh toán ngay" (primary) và nút "Đổi lựa chọn" (ghost).

### Quầy và vận hành
- [U7.P1-01 P4] P1. POS `/pos/index` (theme ops, cao 100vh)
  - Từ 1280px chia 3 cột:
    - [U7.P1-02 P4] Cột 1 (300px): ô tìm phim hoặc suất; danh sách suất hôm nay nhóm theo phim (poster 40px, tên, nhãn tuổi). Mỗi suất là chip giờ kèm số ghế trống, màu theo mức còn nhiều, sắp hết, hết. Có nút "Chỉ bán bắp nước".
    - [U7.P1-03 P4] Cột 2 (co giãn): hai tab lớn "Sơ đồ ghế" và "Bắp nước". Sơ đồ ghế đặt trong panel tối. Lưới món 4 cột thẻ lớn (ảnh 64px, tên, giá, nút + lớn), lọc theo danh mục bằng chip.
    - [U7.P1-04 P4] Cột 3 (380px): "Hóa đơn tạm tính".
      - [U7.P1-05 P4] Thành viên: tra cứu bằng F1, hiện tên, hạng, điểm.
      - [U7.P1-06 P4] Danh sách vé: mỗi vé có ghế, loại ghế, dropdown chọn đối tượng (giá cập nhật ngay), cảnh báo luật hiện ngay trên dòng; có nút áp một đối tượng cho tất cả vé.
      - [U7.P1-07 P4] Danh sách món với bộ − số + và nút xóa; voucher và điểm.
      - [U7.P1-08 P4] Tổng tiền 32px đậm; hai nút 64px "Tiền mặt (F4)" và "Chuyển khoản QR"; nút phụ "Hủy hóa đơn (Esc)".
  - [U7.P1-09 P4] Từ 1024px đến 1279px, cột 1 thu vào drawer.
  - [U7.P1-10 P4] Phím tắt hiện dạng <kbd> ngay trên nút.
  - [U7.P1-11 P4] Hủy hóa đơn đã thanh toán: hộp nhập lý do và bàn phím PIN của quản lý (mục 5).
  - [U7.P1-12 P6] Trong hộp hủy có thêm nút "Gửi quản lý duyệt từ xa"; kết quả duyệt hiện ngay trên POS.
- [U7.P2-01 P6] P2. Màn hình phụ của quầy `/pos/customer-display?terminal=POS-01` (cinema, 1920 × 1080)
  - [U7.P2-02 P6] Lúc chờ: chạy slideshow poster cùng logo.
  - [U7.P2-03 P6] Khi thu ngân chọn suất: trái là sơ đồ ghế lớn (ghế đang chọn sáng lên kèm điểm); phải là giỏ hàng chữ 24–28px, tổng tiền 56px.
  - [U7.P2-04 P6] Khi thanh toán QR: QR 360px ở giữa, số tiền 56px, đồng hồ đếm ngược.
  - [U7.P2-05 P6] Khi xong: "Cảm ơn quý khách", kèm mã vé và QR vé.
- [U7.P3-01 P6] P3. Máy soát vé `/pos/gate-inspector` (cinema, ưu tiên điện thoại, chạy được trên desktop)
  - [U7.P3-02 P6] Phần trên: chọn phòng hoặc cửa; ô nhập mã luôn autofocus và tự lấy lại focus sau mỗi lần quét; nút bật camera.
  - [U7.P3-03 P6] Phần còn lại là thẻ kết quả, đổ nền đặc theo màu ở mục 2: xanh là hợp lệ, vàng là cần xác nhận, đỏ là từ chối.
    - [U7.P3-04 P6] Icon 64px, dòng chính 28–32px (ví dụ "Hợp lệ – Phòng 3"), dòng phụ là suất và giờ.
    - [U7.P3-05 P6] Danh sách ghế với checkbox 32px; nút "Cho vào tất cả" cao 56px.
    - [U7.P3-06 P6] Cảnh báo độ tuổi nằm trong khung vàng nổi bật.
  - [U7.P3-07 P6] Sau 6 giây tự quay về trạng thái chờ, có thanh đếm. Khung "Phòng đang dọn" có nút "Đã dọn xong". Có rung và âm báo ngắn nếu thiết bị hỗ trợ.
- [U7.P4-01 P6] P4. Bếp `/pos/fnb-kds` (cinema)
  - [U7.P4-02 P6] Hai cột "Đang chuẩn bị" và "Sẵn sàng giao".
  - [U7.P4-03 P6] Thẻ đơn gồm: số gọi 40px; kênh (Quầy hoặc Online); thời gian chờ đổi màu (xanh dưới 5 phút, vàng 5–10 phút, đỏ trên 10 phút); danh sách món chữ 18px; nút "Sẵn sàng giao" cao 56px. Thẻ ở cột hai có nút "Đã giao".
  - [U7.P4-04 P6] Đơn mới trượt vào kèm âm báo nhẹ.
- [U7.P5-01 P6] P5. Bảng gọi số `/lobby/pickup-board` (cinema, 1920 × 1080)
  - [U7.P5-02 P6] Hai cột: "Đang chuẩn bị" với số 64px màu xám; "Mời nhận đồ" với số 96px màu vàng, nhấp nháy nhẹ 3 lần khi vừa xuất hiện, có âm báo.
  - [U7.P5-03 P6] Kèm đồng hồ và logo.
- [U7.P6-01 P6] P6. Bảng sảnh `/lobby/digital-signage` (cinema, 1920 × 1080)
  - [U7.P6-02 P6] Bảng các suất sắp chiếu, gồm các cột: giờ (48px, tabular); phim (tên, nhãn tuổi, định dạng, ngôn ngữ); phòng; trạng thái.
  - [U7.P6-03 P6] Trạng thái hiện bằng nhãn lớn: "Mời vào rạp" màu xanh nhấp nháy chậm, "Đang chiếu", "Đang dọn".
  - [U7.P6-04 P6] Bảng tự cuộn khi nhiều dòng. Chân trang có dòng chữ chạy, ví dụ nhắc quy định độ tuổi.
- [U7.P7-01 P4] P7. Mở ca và đóng ca (ops)
  - [U7.P7-02 P4] Mở ca: wizard ngắn; nhập số tiền bằng bàn phím số lớn.
  - Đóng ca mù, 3 bước:
    1. [U7.P7-03 P4] Đếm tiền theo mệnh giá (500.000, 200.000, 100.000, 50.000, 20.000, 10.000, 5.000, 2.000, 1.000), hệ thống tự cộng.
    2. [U7.P7-04 P4] Xác nhận.
    3. [U7.P7-05 P4] Biên bản: so sánh số đếm được với số hệ thống tính, chênh lệch tô xanh hoặc đỏ, có nút in.

### Quản trị (theme ops)
- [U7.A1-01 P4] A1. Dashboard `/admin`
  - [U7.A1-02 P4] Hàng thẻ KPI: doanh thu hôm nay, vé bán, tỷ lệ lấp đầy, doanh thu bắp nước, tỷ lệ phim Việt từ đầu năm.
  - [U7.A1-03 P4] Hàng biểu đồ: doanh thu 30 ngày dạng đường (2 series vé và bắp nước, rộng 8/12); top phim dạng thanh ngang (rộng 4/12).
  - [U7.A1-04 P4] Hàng dưới: khối tỷ lệ phim Việt có vạch 20%; danh sách cảnh báo vận hành (KDM sắp hết hạn, nguyên liệu dưới ngưỡng, ghế cần đổi).
  - [U7.A1-05 P6] Danh sách cảnh báo thêm các yêu cầu phê duyệt đang chờ; thêm bảng sĩ số trực tiếp các suất đang chạy.
- [U7.A2-01 P2] A2. Danh sách (mẫu chung cho Loại đồ uống, Đồ uống, Phim, Nhân viên, Nhà phát hành, Nguyên liệu)
  - [U7.A2-02 P2] Gồm _PageHeader, thanh lọc, bảng dữ liệu, phân trang, trạng thái rỗng.
  - [U7.A2-03 P2] Thanh lọc tối thiểu: ô tìm theo tên (debounce 300ms, giữ trên query string) và bộ lọc theo danh mục cha. Ví dụ Đồ uống lọc theo Loại đồ uống, Phim lọc theo trạng thái và nhãn tuổi, Nhân viên lọc theo vai trò và rạp.
  - [U7.A2-04 P2] Bấm xóa thì mở hộp xác nhận nêu rõ hậu quả. Nếu không được xóa cứng, hộp thoại giải thích lý do và gợi ý "Ngưng hoạt động".
- [U7.A3-01 P2] A3. Form thêm và sửa
  - [U7.A3-02 P2] Thẻ rộng tối đa 960px, chia thành các phần có tiêu đề; từ 992px chia 2 cột.
  - [U7.A3-03 P2] Vùng kéo thả ảnh món tỉ lệ 1:1; xem trước ngay; có nút xóa ảnh.
  - [U7.A3-04 P3] Vùng kéo thả poster phim tỉ lệ 2:3, cùng cách hoạt động.
  - [U7.A3-05 P2] Thanh nút dính chân form: "Hủy" (ghost) và "Lưu" (primary). Cảnh báo khi rời trang mà chưa lưu.
- [U7.A4-01 P3] A4. Thiết kế phòng chiếu
  - [U7.A4-02 P3] Trái là công cụ tô: ghế thường, VIP, cặp sweetbox, xe lăn, ô trống hoặc lối đi, ghế nội bộ, bảo trì.
  - [U7.A4-03 P3] Giữa là lưới chỉnh sửa: bấm hoặc kéo để tô.
  - [U7.A4-04 P3] Phải là thống kê (số ghế theo loại, sức chứa) và công tắc xem heatmap BaseViewScore.
  - [U7.A4-05 P3] Có nút "Tự đánh số" và "Gợi ý ghế nội bộ".
- [U7.A5-01 P3] A5. Lịch chiếu `/scheduler`
  - [U7.A5-02 P3] Thanh trên: chọn rạp, chọn ngày (‹ Hôm nay ›).
  - [U7.A5-03 P7] Thanh trên thêm nút chuyển giữa chế độ Bảng và Gantt, và nút "AI tự động xếp lịch".
  - [U7.A5-04 P3] Chế độ Bảng: mỗi phòng một nhóm, các suất xếp theo giờ, mỗi dòng có giờ, phim, nhãn tuổi, ngôn ngữ, số ghế đã bán, thao tác.
  - Chế độ Gantt:
    - [U7.A5-05 P7] Trục thời gian 08:00–02:00, vạch mờ mỗi 30 phút, nhãn mỗi giờ. Mỗi hàng là một phòng; cột trái rộng 160px ghi tên phòng, định dạng, sức chứa.
    - [U7.A5-06 P7] Khối suất: nền màu nhãn tuổi nhạt 20%, viền trái 4px màu nhãn tuổi. Bên trong ghi tên phim (1 dòng) và "19:15–21:40 · Phụ đề". Phần quảng cáo đầu khối và phần dọn phòng cuối khối vẽ sọc nhạt. Suất bị khóa có icon khóa.
    - [U7.A5-07 P7] Lỗi chặn tô viền đỏ 2px kèm icon, cảnh báo tô viền vàng; tooltip ghi lý do. Bản nháp AI vẽ viền nét đứt, kèm nút "Áp dụng" và "Bỏ".
    - [U7.A5-08 P7] Chân trang hiện tỷ lệ phim Việt của ngày và của tuần.
- [U7.A6-01 P4] A6. Báo cáo: bộ lọc khoảng ngày có mốc nhanh; thẻ KPI; biểu đồ; bảng chi tiết; nút "Xuất CSV". Trang đối soát nhà phát hành: chọn phim và tuần, bảng kết quả ghi rõ phần rạp tự chịu do giá sàn.
- [U7.A7-01 P6] A7. Phê duyệt (quản lý ca)
  - [U7.A7-02 P6] Popup thời gian thực ở góc phải, không chặn thao tác khác. Nội dung: hóa đơn, lý do, người yêu cầu, hai nút lớn Duyệt và Từ chối.
  - [U7.A7-03 P6] Dashboard có thêm danh sách các yêu cầu đang chờ duyệt.

- [U7.A8-01 P4] A8. Quản lý hóa đơn `/admin/bills` (ManagerOnly)
  - [U7.A8-02 P4] Danh sách: lọc theo khoảng ngày (mốc nhanh Hôm nay, 7 ngày, Tháng này), trạng thái (Chờ thanh toán, Hoàn thành, Đã hủy), kênh (Quầy, Online, Doanh nghiệp), rạp, nhân viên; ô tìm theo mã hóa đơn. Cột: mã, thời gian tạo, kênh, nhân viên hoặc khách, số vé, số món, tổng tiền (căn phải, số đơn sắc), trạng thái dạng badge theo mục 6. Có phân trang và "Xuất CSV".
  - [U7.A8-03 P4] Chi tiết `/admin/bills/{id}`: đầu trang ghi mã, badge trạng thái, lý do hủy nếu có. Khối thông tin: nhân viên lập, ca, thời gian tạo, thời gian thanh toán, khách hàng. Bảng vé (phim, suất, phòng, ghế, đối tượng, đơn giá, giảm, thành tiền) và bảng bắp nước (món, kích cỡ, số lượng, đơn giá, thành tiền). Khối tổng: tạm tính, giảm giá, VAT, tổng cộng. Lịch sử thanh toán, hoàn tiền và phê duyệt dạng dòng thời gian. Nút "In lại hóa đơn".

- [U7.A9-01 P2] A9. Trang cá nhân nhân viên `/me` (mọi nhân viên, dùng layout của vai trò đang đăng nhập)
  - [U7.A9-02 P2] Thẻ thông tin: ảnh đại diện chữ cái đầu, họ tên, email (chỉ đọc), số điện thoại, vai trò, rạp. Nút "Lưu".
  - [U7.A9-03 P2] Thẻ đổi mật khẩu: mật khẩu hiện tại, mật khẩu mới, nhập lại; có thước đo độ mạnh và nút hiện hoặc ẩn. Đổi xong thì đăng xuất các phiên khác.

### Ảnh mẫu (SvgArtGenerator)
- [U7.ART-01 P1] Poster 600 × 900: nền gradient hai màu theo thể loại, họa tiết hình học nhẹ, tên phim chữ lớn (đậm 600–700, tự xuống dòng), dòng phụ ghi thể loại và năm, nhãn tuổi ở góc trên.
- [U7.ART-02 P1] Ảnh món 600 × 600: hình phẳng đơn giản (xô bắp, ly nước, combo) trên nền màu nhạt.
- [U7.ART-03 P1] Không dùng ảnh tải từ internet.

### Trang hệ thống
- [U7.D1-01 P2] D1. Style guide `/dev/style-guide` (chỉ khi Demo:EnableTools): mọi thành phần ở mục 5 trên cả hai theme; mỗi thành phần có tiêu đề và đủ các trạng thái (thường, hover, đang xử lý, lỗi, bị khóa).
- [U7.D2-01 P2] D2. Hộp thư thử `/dev/mailbox` (chỉ khi Demo:EnableTools): danh sách thư trong EmailOutbox, mới nhất ở trên; bấm vào thì xem nội dung; có nút xóa hết.
- [U7.D3-01 P2] D3. Trang từ chối truy cập `/Home/AccessDenied` và trang lỗi `/Home/Error`: dùng layout của khu vực đang đứng, icon 48px, một câu giải thích tiếng Việt, nút quay về trang chính của vai trò. Không hiện stack trace.
- [U7.D4-01 P8] D4. Trung tâm demo `/demo/multi-screen` (chỉ khi Demo:EnableTools): 5 kịch bản ở SPEC mục 5, mỗi kịch bản một thẻ có mô tả và nút "Mở các màn hình".
