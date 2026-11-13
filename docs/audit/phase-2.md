# Kiểm tra độc lập – Phase 2

Agent spec-auditor kiểm từng nhóm mã (không quá 20 mã một lượt, theo thứ tự của lệnh `list 2`), đọc spec, truy vết, code thật và ảnh chụp giao diện.
Cột "Đã sửa" ghi việc đã làm cho các dòng Thiếu hoặc Sai và kết quả của lượt kiểm lại.

| Mã | Kết luận | Lý do | Đã sửa |
|---|---|---|---|
| S2.1-06 | Đạt | Bootstrap 5.3.3, Icons 1.11.3, Chart.js 4.4.9, html5-qrcode 2.3.8 (libman.json), Be Vietnam Pro tự host 4 độ đậm; không Tailwind, FontAwesome; jQuery chỉ cho validate | – |
| S2.1-07 | Đạt | Mọi thư viện trong wwwroot/lib, nguồn ghi ở libman.json; không URL CDN | – |
| S2.5-01 | Đạt | ExpireTimeSpan 24 giờ, LoginPath, AccessDeniedPath đúng; UseAuthentication trước UseAuthorization; 6 policy khớp CONTRACTS | – |
| S2.5-02 | Đạt | Claim NameIdentifier, Name, Email, Role; CinemaId chỉ cho nhân viên | – |
| S2.5-03 | Đạt | Authenticate lọc Email và Active = 1, kiểm bằng PasswordHasher | – |
| S2.5-04 | Đạt | Đủ Login, Logout, Register, ForgotPassword (thêm VerifyOtp), ChangePassword, Profile; về trang chính của vai trò | – |
| S2.5-05 | Đạt | Ô email, POST /Login/ForgotPassword, RequestOtp | – |
| S2.5-06 | Đạt | Mã 6 số ngẫu nhiên mật mã, hạn 5 phút, sai tối đa 5 lần, OTP_INVALID kèm số lần còn, OTP_EXPIRED; có unit test và test tích hợp | – |
| S2.5-07 | Đạt | Mật khẩu ngẫu nhiên, băm, cập nhật trong transaction, gửi qua IEmailSender | – |
| S2.5-08 | Đạt | MustChangePassword = 1, đăng nhập chuyển trang đổi mật khẩu, middleware chặn mọi trang khác | – |
| S2.5-09 | Đạt | Có SmtpHost thì SmtpEmailSender gửi bằng SmtpClient | – |
| S2.5-10 | Đạt | Không cấu hình thì OutboxEmailSender ghi EmailOutbox, xem ở /dev/mailbox | – |
| S2.5-11 | Đạt | AutoValidateAntiforgeryToken toàn cục, header RequestVerificationToken, ui.js gửi header | – |
| S2.6-04 | Đạt | FileController ManagerOnly, Upload và Delete là HttpPost có ValidateAntiForgeryToken | – |
| S2.6-05 | Đạt | Form món gọi /File/Upload và /File/Delete bằng ui.api (fetch) | – |
| S2.7-02 | Đạt | Mọi controller có Authorize hoặc AllowAnonymous (có test); không bí mật trong mã nguồn | – |
| S2.7-03 | Đạt | UseExceptionHandler cho request thường, ApiErrorMiddleware cho AJAX theo CONTRACTS mục 8, không stack trace | – |
| S2.7-04 | Đạt | SetActive và AuditLogs USER_LOCKED/USER_UNLOCKED trong cùng transaction | – |
| S2.7-08 | Đạt | UseForwardedHeaders đầu pipeline, có test thứ tự | – |
| S3.1-22 | Đạt | _AgeBadge dùng chung, câu giải thích 6 mức; nơi bán vé thuộc P4–P6 | – |
| S3.11-01 | Đạt | Nguyên liệu theo rạp, định mức theo từng rạp trên form món, combo là Drinks IsCombo | – |
| S3.14-01 | Đạt | Chặn xóa loại còn món, kiểm lại trong transaction | – |
| S3.14-02 | Đạt | Chặn xóa món đã có trong BillDetails, gợi ý ngưng bán | – |
| S3.14-04 | Đạt | Chặn xóa nhân viên đã lập hóa đơn, chặn tự khóa và tự xóa | – |
| S3.14-06 | Đạt | DELETE_BLOCKED kèm lý do và suggestion deactivate, hộp thoại có nút Ngưng hoạt động | – |
| S5-05 | Đạt | Thanh nổi thu gọn được, 5 tài khoản mẫu, /demo/switch-role | – |
| S5-07 | Đạt | DemoToolsMiddleware trả 404 cho /demo, /dev khi tắt công cụ (có test) | – |
| S8.2-01 | Đạt | tokens.css, app.css, 4 layout, partial, ui.js, StatusDisplay, style guide hai theme, ảnh D1 | – |
| S8.2-02 | Đạt | Program.cs đủ xác thực, policy, Anti-Forgery, lỗi tập trung, ForwardedHeaders; LoginController; AccessDenied; mailbox | – |
| S8.2-03 | Đạt | FileController, Categories, Drinks (ảnh bằng fetch, định mức, lọc và tìm), Users (SetActive, quy tắc xóa), OTP, /me; phân quyền xem và sửa đúng | – |
| S8.2-04 | Đạt | _DemoBar và /demo/switch-role | – |
| S8.2-05 | Đạt | snap.mjs đọc routes.json, đăng nhập qua switch-role, chụp toàn trang | – |
| U2-01 | Đạt | 109 biến token khớp spec (verify tự động) | – |
| U2-02 | Đạt | Admin, POS dùng ops; khách, hiển thị dùng cinema; sơ đồ ghế luôn cinema | – |
| U2-03 | Đạt | Primary xanh ở ops, đỏ ở cinema; đỏ thương hiệu cho logo | – |
| U2-04 | Đạt | Chữ nhãn tuổi --age-fg, nhãn C chữ trắng | – |
| U2-07 | Đạt | 12 file woff2, 4 độ đậm, tự host | – |
| U2-08 | Đạt | Thân 16px, bảng 14px, nhỏ nhất 12px | – |
| U2-09 | Đạt | line-height chữ ≥ 1.4; chỉ icon và chữ số dùng 1 | – |
| U2-10 | Thiếu | Một số giờ, tiền chưa có tabular-nums: chuông phê duyệt, hộp thư thử, trang chính sách hủy vé | Thêm lớp num, money cho các chỗ đó | Đã sửa: Thêm lớp num, money cho các chỗ đó. Kiểm lại 13/11/2026: Đạt 
| U2-11 | Đạt | Đoạn văn tối đa 70ch | – |
| U2-12 | Đạt | Không viết hoa toàn bộ | – |
| U3-01 | Đạt | Bootstrap 5.3.3; mọi chỗ theme cinema có data-bs-theme="dark" | – |
| U3-02 | Đạt | Ghi đè --bs-btn-* từng biến thể và các biến --bs-* về token | – |
| U3-03 | Thiếu | Một số icon không đặt cỡ nên ra 12–14px (ghế, lỗi dưới ô, thanh demo, phân trang, footer…) | Đặt mặc định .bi 16px, icon ghế và icon lỗi 16px | Đã sửa: Đặt mặc định .bi 16px, icon ghế và icon lỗi 16px. Kiểm lại 13/11/2026: Đạt 
| U4-01 | Đạt | Header dính 64px, kính mờ blur 12px | – |
| U4-02 | Đạt | Logo SVG tự vẽ kèm chữ | – |
| U4-03 | Đạt | Menu 4 mục (khóa có lý do, TODO Phase 5), ô chọn rạp, tài khoản, offcanvas | – |
| U4-04 | Đạt | container-xl, khoảng cách 48–64px | – |
| U4-05 | Đạt | Footer đủ thông tin rạp, liên hệ, điều khoản, chính sách hủy vé | – |
| U4-06 | Đạt | Sidebar 248/72px, tooltip, offcanvas | – |
| U4-07 | Đạt | Đủ 8 nhóm đúng thứ tự | – |
| U4-08 | Đạt | Topbar 56px: breadcrumb, chọn rạp, chuông có số chờ, menu người dùng | – |
| U4-09 | Đạt | Padding 24px, form tối đa 960px | – |
| U4-10 | Đạt | Topbar POS 48px đủ thành phần, nút Đóng ca khóa có lý do | – |
| U4-11 | Thiếu | Cột trong bản xem trước POS nằm trong .pos-preview nên không tự cuộn | Thêm overflow-y: auto cho .pos-preview > .pos-col, chiều cao 100% | Đã sửa: Thêm overflow-y: auto cho .pos-preview > .pos-col, chiều cao 100%. Kiểm lại 13/11/2026: Đạt 
| U4-12 | Thiếu | Lớp phủ mất kết nối nằm ngoài .display-shell nên con trỏ không ẩn trên lớp phủ | Thêm quy tắc ẩn con trỏ cho lớp phủ | Đã sửa: Thêm quy tắc ẩn con trỏ cho lớp phủ. Kiểm lại 13/11/2026: Đạt 
| U4-13 | Đạt | Viên thuốc góc trái dưới, thu gọn được | – |
| U4-14 | Đạt | Hiện vai trò hiện tại và 5 nút | – |
| U4-16 | Thiếu | Ở đúng 768px thanh demo mở che nút Hủy của thanh thao tác form | Thu gọn mặc định dưới 992px và ở POS | Đã sửa: Thu gọn mặc định dưới 992px và ở POS. Kiểm lại 13/11/2026: Đạt 
| U5-01 | Đạt | Style guide hai theme, đủ thành phần mục 5 | – |
| U5-02 | Đạt | Cỡ 32, 40, 48, 56, 64px | – |
| U5-03 | Đạt | Đủ 5 kiểu | – |
| U5-04 | Thiếu | Form POST thường chưa có trạng thái đang xử lý | ui.js: bộ nghe submit chung bật ui.busy cho nút gửi khi form thật sự gửi | Đã sửa: ui.js: bộ nghe submit chung bật ui.busy cho nút gửi khi form thật sự gửi. Kiểm lại 13/11/2026: Đạt 
| U5-05 | Đạt | Mỗi vùng tối đa một nút primary | – |
| U5-06 | Sai | Ô ở trang đăng nhập cao 44px, ô chọn rạp topbar 36px | Bỏ hai ghi đè, mọi ô cao 40px (POS 48px) | Đã sửa: Bỏ hai ghi đè, mọi ô cao 40px (POS 48px). Kiểm lại 13/11/2026: Đạt 
| U5-07 | Thiếu | Lỗi asp-validation-for nằm sau chữ trợ giúp, không ngay dưới ô | Đưa chỗ hiện lỗi lên ngay dưới ô ở Drinks, Categories, Users, Profile | Đã sửa: Đưa chỗ hiện lỗi lên ngay dưới ô ở Drinks, Categories, Users, Profile. Kiểm lại 13/11/2026: Đạt 
| U5-08 | Đạt | Ô tiền ₫ tự phân cách, ô ngày hiển thị dd/MM/yyyy | – |
| U5-09 | Đạt | Công tắc lạc quan, lỗi thì trả về và báo | – |
| U5-10 | Đạt | Nhãn trạng thái viên thuốc 12px | – |
| U5-11 | Đạt | Nhãn tuổi đúng kích thước, màu, tooltip | – |
| U5-12 | Đạt | Nhãn viền cho định dạng phòng và ngôn ngữ | – |
| U5-13 | Đạt | Poster 2:3 bo 10px, nhãn tuổi góc trên trái | – |
| U5-14 | Đạt | Tên 2 dòng, dòng phụ, nút Đặt vé | – |
| U5-15 | Đạt | Hover nâng 2px | – |
| U5-16 | Đạt | Thẻ KPI đúng mô tả | – |
| U5-17 | Đạt | Thẻ món 1:1, bộ tăng giảm 40/48px, lớp phủ Tạm hết | – |
| U5-18 | Đạt | Tiêu đề cột 13px đậm 600 muted, dòng 48px, đổi nền khi hover | – |
| U5-19 | Đạt | Số canh phải tabular, trạng thái bằng nhãn hoặc công tắc | – |
| U5-20 | Đạt | Cột thao tác bên phải, nút icon có tooltip và aria-label, tiêu đề dính | – |
| U5-21 | Đạt | "1–20 trên 135" có unit test; điện thoại thành thẻ | – |
| U5-22 | Đạt | Ô tìm có icon, nút xóa, tìm sau 300ms | – |
| U5-23 | Đạt | Lọc trạng thái, khoảng ngày 4 mốc nhanh, Xóa bộ lọc | – |
| U5-24 | Đạt | _PageHeader đúng cỡ chữ, nút bên phải | – |
| U5-25 | Đạt | Stepper vòng tròn, ✓, bản gọn trên điện thoại | – |
| U5-26 | Đạt | Toast góc phải trên, 4 loại, 4/6 giây, tối đa 3 | – |
| U5-27 | Đạt | Hộp xác nhận có hậu quả, gõ lại mã cho thao tác hàng loạt | – |
| U5-28 | Đạt | PIN 6 chấm, phím 3 × 4 64px, rung và báo lượt còn hoặc khóa | – |
| U5-29 | Đạt | Trạng thái rỗng đúng mô tả | – |
| U5-30 | Thiếu | Một khung skeleton chung không khớp số cột từng bảng; điện thoại vẫn là lưới | _TableSkeleton nhận loại bảng (category, drink, staff) với đúng cột; dưới 768px thành thẻ | Đã sửa: _TableSkeleton nhận loại bảng (category, drink, staff) với đúng cột; dưới 768px thành thẻ. Kiểm lại 13/11/2026: Đạt 
| U5-31 | Đạt | Viên thuốc mm:ss | – |
| U5-32 | Đạt | ≤ 2 phút warning, ≤ 1 phút danger và nhấp nhẹ | – |
| U5-33 | Đạt | Hộp "Hết thời gian giữ ghế" với nút "Chọn lại" | – |
| U5-34 | Thiếu | Chấm "Đang kết nối lại" dùng --warning-fg, ở theme sáng là nâu chứ không vàng | Dùng --brand-gold (vàng) ở cả hai theme | Đã sửa: Dùng --brand-gold (vàng) ở cả hai theme. Kiểm lại 13/11/2026: Đạt 
| U5-35 | Đạt | Khối tổng tiền đúng cỡ 24/32px | – |
| U5-36 | Đạt | Khối QR nền trắng bo 16px, 240/360px, mã và đếm ngược | – |
| U5-37 | Đạt | Ảnh có kích thước hoặc tỉ lệ cố định, object-fit cover, lazy, onerror sang ảnh thay thế | – |
| U5-38 | Đạt | Đủ 9 partial, có nội dung thật | – |
| U5-39 | Đạt | Fmt đủ hàm, vi-VN, có test | – |
| U5-40 | Đạt | ui.toast 4 loại | – |
| U5-41 | Đạt | ui.confirm Promise<boolean>, ui.pin Promise<string\|null> | – |
| U5-42 | Đạt | ui.api gửi Anti-Forgery, bật đang tải, lỗi thành toast rồi ném | – |
| U5-43 | Đạt | ui.money, ui.dateTime, ui.shake | – |
| U6-01 | Đạt | Vùng dữ liệu đủ 4 trạng thái | – |
| U6-02 | Thiếu | Nút "Xóa ảnh" khóa khi chưa có ảnh nhưng không nêu lý do; tooltip "Gửi lại mã" không cập nhật số giây | Thêm dòng chữ nhỏ và title nêu lý do cạnh nút Xóa ảnh; tooltip Gửi lại mã cập nhật bằng setContent | Đã sửa: Thêm dòng chữ nhỏ và title nêu lý do cạnh nút Xóa ảnh; tooltip Gửi lại mã cập nhật bằng setContent. Kiểm lại 13/11/2026: Đạt 
| U6-03 | Đạt | StatusDisplay đủ 8 nhóm, JSON trong layout, ui.status dùng chung | – |
| U7.C6-01 | Đạt | Chia đôi từ 992px, form 400px, nút hiện mật khẩu, lỗi cụ thể, liên kết quên mật khẩu | – |
| U7.C6-02 | Đạt | 2 bước cùng thẻ, 6 ô OTP, đếm ngược 5 phút, gửi lại sau 60 giây, lời nhắn chung | – |
| U7.C6-03 | Đạt | Đổi mật khẩu bắt buộc không menu, chỉ form và Đăng xuất | – |
| U7.A2-01 | Đạt | Mẫu danh sách cho Loại đồ uống, Đồ uống, Nhân viên | – |
| U7.A2-02 | Đạt | Đủ _PageHeader, thanh lọc, bảng, phân trang, trạng thái rỗng | – |
| U7.A2-03 | Đạt | Tìm 300ms giữ trên query string, lọc theo loại, vai trò, rạp | – |
| U7.A2-04 | Đạt | Hộp xác nhận nêu hậu quả; bị chặn thì giải thích và gợi ý ngưng hoạt động | – |
| U7.A3-01 | Đạt | Form thêm, sửa cho 3 danh mục | – |
| U7.A3-02 | Đạt | Thẻ 960px, các phần có tiêu đề, 2 cột từ 992px | – |
| U7.A3-03 | Đạt | Vùng kéo thả 1:1, xem trước, nút xóa ảnh | – |
| U7.A3-05 | Đạt | Thanh nút dính Hủy, Lưu; cảnh báo rời trang | – |
| U7.A9-01 | Đạt | /me với layout theo vai trò | – |
| U7.A9-02 | Đạt | Thẻ thông tin đủ trường, email chỉ đọc, nút Lưu | – |
| U7.A9-03 | Đạt | Đổi mật khẩu có thước đo, nút hiện ẩn, đăng xuất phiên khác | – |
| U7.D1-01 | Thiếu | Chỉ nút có đủ 5 trạng thái; ô nhập, thẻ, dòng bảng, thanh lọc thiếu hover, đang xử lý, lỗi, bị khóa | Thêm khối "Trạng thái của thành phần" (ma trận 5 trạng thái cho ô nhập, thẻ phim, thẻ món, dòng bảng, thanh lọc và phân trang) | Đã sửa: Thêm khối "Trạng thái của thành phần" (ma trận 5 trạng thái cho ô nhập, thẻ phim, thẻ món, dòng bảng, thanh lọc và phân trang). Kiểm lại 13/11/2026: Đạt 
| U7.D2-01 | Đạt | Hộp thư mới nhất ở trên, xem nội dung, xóa hết có xác nhận | – |
| U7.D3-01 | Đạt | Layout theo khu vực, icon 48px, một câu, nút về trang chính, không stack trace | – |
| U9-01 | Đạt | Khách xưng "bạn", nhân viên câu chỉ việc | – |
| U9-02 | Đạt | Không lộ mã lỗi, chỉ mã hỗ trợ nhỏ | – |
| U9-03 | Thiếu | Vài câu chỉ nói chuyện gì xảy ra, chưa nói cách sửa (tự xóa, tự khóa, tự đổi vai trò, rạp không tồn tại, ảnh không hợp lệ, giá âm, câu dự phòng) | Viết lại các câu đó đủ hai ý | Đã sửa: Viết lại các câu đó đủ hai ý. Kiểm lại 13/11/2026: Đạt 
| U9-04 | Đạt | Định dạng tiền, ngày, giờ, thứ, thời lượng có test | – |
| U9-18 | Đạt | Câu OTP_INVALID đúng, có số lần còn lại | – |
| U9-19 | Đạt | Câu OTP_EXPIRED đúng nguyên văn | – |
| U9-20 | Đạt | Câu MUST_CHANGE_PASSWORD đúng nguyên văn | – |
| U9-21 | Đạt | Câu mất mạng đúng nguyên văn | – |
| U9-22 | Đạt | Câu 403 đúng nguyên văn | – |
| U9-23 | Đạt | Hai câu trạng thái rỗng mẫu có trên style guide | – |
| U9-24 | Thiếu | "Đã giữ 3 ghế trong 10 phút" hiện bằng toast info thay vì thành công | Thêm nút "Thành công: giữ ghế" hiện toast success | Đã sửa: Thêm nút "Thành công: giữ ghế" hiện toast success. Kiểm lại 13/11/2026: Đạt 
| U10-01 | Sai | Tiêu đề cột bảng ops #6B7280 trên #F3F4F6 (4,39:1); nút success theme tối chữ trắng trên #16A34A (3,3:1) | Tiêu đề cột trên nền --surface (4,8:1); nút success ở theme cinema dùng chữ --ink trên --success-fg | Đã sửa: Tiêu đề cột trên nền --surface (4,8:1); nút success ở theme cinema dùng chữ --ink trên --success-fg. Kiểm lại 13/11/2026: Đạt 
| U10-02 | Đạt | Vòng focus 2px --focus cách 2px khi dùng bàn phím | – |
| U10-03 | Đạt | Nút chỉ có icon đều có aria-label | – |
| U10-05 | Đạt | Trạng thái ghế có ký hiệu và chữ; kết quả soát vé ở Phase 6 | – |
| U10-06 | Thiếu | Phân trang 40px, nút đóng 24px, nút hiện mật khẩu 40px, mục menu 40px chưa đủ 44px | Phân trang 44px; mở rộng vùng chạm nút đóng, nút hiện mật khẩu; mục menu và thanh demo 44px | Đã sửa: Phân trang 44px; mở rộng vùng chạm nút đóng, nút hiện mật khẩu; mục menu và thanh demo 44px. Kiểm lại 13/11/2026: Đạt 
| U10-07 | Đạt | Tôn trọng prefers-reduced-motion | – |
| U10-08 | Sai | Hộp "Hết thời gian giữ ghế" không đóng được bằng Esc dù không phải hộp PIN hay hộp nguy hiểm | Hộp này đóng được bằng Esc | Đã sửa: Hộp này đóng được bằng Esc. Kiểm lại 13/11/2026: Đạt 
| U11-01 | Đạt | routes.json đủ 6 khu vực, kích thước đúng | – |
| U12-01 | Đạt | Chỉ dùng bảng chung, không tiêu đề scaffold, không chữ tiếng Anh | – |
| U12-02 | Thiếu | Khoảng cách 36px, 44px ngoài thang token; ui.busy gán style width | Đổi sang calc từ token; ui.busy chỉ đặt biến CSS --busy-w | Đã sửa: Đổi sang calc từ token; ui.busy chỉ đặt biến CSS --busy-w. Kiểm lại 13/11/2026: Đạt 
| U12-03 | Đạt | Không alert, confirm, prompt | – |
| U12-04 | Đạt | Không lorem, không ảnh ngoài, không ảnh vỡ | – |
| U12-05 | Đạt | Không hiện enum, stack trace, lỗi 500 thô | – |
| U12-06 | Đạt | Một font, một bộ icon | – |
| U12-07 | Đạt | Mỗi vùng tối đa một nút primary | – |
| U12-08 | Đạt | 56 ảnh không cuộn ngang | – |
| U12-09 | Sai | Chữ xám trên nền xám ở tiêu đề cột; chữ trắng trên xanh lá ở nút success theme tối | Như U10-01 | Đã sửa: Như U10-01. Kiểm lại 13/11/2026: Đạt 
| U13-01 | Thiếu | Màn hình dùng dropzone, alert-ui, detail-list, card-section, form-grid, form-actions-sticky, avatar, mail-list chưa có trên style guide | Thêm khối "Form thêm, sửa và thẻ thông tin" vào style guide | Đã sửa: Thêm khối "Form thêm, sửa và thẻ thông tin" vào style guide. Kiểm lại 13/11/2026: Đạt 
| U13-02 | Đạt | snap.mjs đọc routes.json, kích thước theo mục 11 | – |

Lượt kiểm lại 13/11/2026 (agent spec-auditor, 20 mã Thiếu/Sai): cả 20 mã Đạt. Đã sửa thêm lệch nhỏ ở Drinks/Form.cshtml (lỗi Image đưa lên trước dòng lý do). Câu AccessDenied giữ nguyên văn theo U9-22.

Kết quả: Đạt
