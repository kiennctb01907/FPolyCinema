# Truy vết yêu cầu – Phase 2 – Giao diện nền, đăng nhập, CRUD cơ bản

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
| S2.1-06 | Giao diện: Bootstrap 5.3.3, Bootstrap Icons 1.11.3, Chart.js 4.4.x, html5-qrcode 2.3.8, font Be Vietnam Pro tự host (wo… | Chưa |  |  |
| S2.1-07 | Thư viện front-end đặt trong wwwroot/lib (tải một lần bằng libman hoặc tải tay), khi chạy không phụ thuộc CDN. | Chưa |  |  |
| S2.5-01 | Cookie Authentication: ExpireTimeSpan 24 giờ, LoginPath /Login/Login, AccessDeniedPath /Home/AccessDenied. UseAuthentic… | Chưa |  |  |
| S2.5-02 | Claims: NameIdentifier, Name, Email, Role, CinemaId (với nhân viên). | Chưa |  |  |
| S2.5-03 | UserDAL.Authenticate(email, password) lấy người dùng theo email và Active = 1, rồi kiểm tra mật khẩu bằng PasswordHashe… | Chưa |  |  |
| S2.5-04 | LoginController có: Login, Logout, Register (cho khách), ForgotPassword, ChangePassword, Profile. Đăng nhập xong chuyển… | Chưa |  |  |
| S2.5-05 | Người dùng nhập email. | Chưa |  |  |
| S2.5-06 | Hệ thống gửi mã OTP 6 số qua email; mã có hạn 5 phút và cho nhập sai tối đa 5 lần (OTP_INVALID). | Chưa |  |  |
| S2.5-07 | OTP đúng thì hệ thống đặt lại mật khẩu bằng một chuỗi ngẫu nhiên, lưu dạng băm vào CSDL, rồi gửi mật khẩu mới qua email. | Chưa |  |  |
| S2.5-08 | Users.MustChangePassword = 1, nên lần đăng nhập kế tiếp bắt buộc phải đổi mật khẩu. | Chưa |  |  |
| S2.5-09 | Có cấu hình Email:SmtpHost thì gửi bằng SMTP thật. | Chưa |  |  |
| S2.5-10 | Không có cấu hình (mặc định khi chạy Development) thì ghi thư vào trang /dev/mailbox để xem. | Chưa |  |  |
| S2.5-11 | Bộ lọc toàn cục AutoValidateAntiforgeryToken. AJAX gửi token qua header RequestVerificationToken. | Chưa |  |  |
| S2.6-04 | FileController có [HttpPost] Upload và Delete, gắn [ValidateAntiForgeryToken] và [Authorize(Policy = "ManagerOnly")]. | Chưa |  |  |
| S2.6-05 | Form Đồ uống gọi Upload và Delete của FileController bằng fetch. | Chưa |  |  |
| S2.7-02 | Mọi controller có [Authorize] hoặc [AllowAnonymous] rõ ràng. Không đưa bí mật vào mã nguồn. | Chưa |  |  |
| S2.7-03 | Xử lý lỗi tập trung: request thường trả trang lỗi thân thiện, AJAX trả JSON theo CONTRACTS mục 8. Lỗi nghiệp vụ ném Bus… | Chưa |  |  |
| S2.7-04 | Khóa hoặc mở tài khoản. | Chưa |  |  |
| S2.7-08 | UseForwardedHeaders đặt đầu pipeline. | Chưa |  |  |
| S3.1-22 | Hiển thị phân loại (Thông tư 05/2023): nhãn độ tuổi kèm câu giải thích dùng chung partial _AgeBadge và phải hiện ở mọi … | Chưa |  |  |
| S3.11-01 | RawMaterials quản lý theo từng rạp. ProductRecipes là định mức nguyên liệu và bao bì cho từng món Drinks; combo cũng là… | Chưa |  |  |
| S3.14-01 | Loại đồ uống: chỉ xóa khi chưa có đồ uống nào. | Chưa |  |  |
| S3.14-02 | Đồ uống: chỉ xóa cứng khi chưa xuất hiện trong hóa đơn. Nếu đã xuất hiện thì chỉ được ngưng (Active = 0). | Chưa |  |  |
| S3.14-04 | Nhân viên: chỉ xóa cứng khi chưa tạo hóa đơn nào. Nếu đã có thì chỉ được khóa hoặc mở (SetActive). Không ai được tự khó… | Chưa |  |  |
| S3.14-06 | Bị chặn thì trả DELETE_BLOCKED kèm lý do, và gợi ý chuyển sang ngưng hoạt động. | Chưa |  |  |
| S5-05 | Nút nổi, thu gọn được; đổi phiên sang một trong 5 tài khoản mẫu chỉ bằng một lần bấm, qua /demo/switch-role?role=…&retu… | Chưa |  |  |
| S5-07 | Khi không bật Demo:EnableTools, mọi đường dẫn bắt đầu bằng demo/ hoặc dev/ trả 404. | Chưa |  |  |
| S8.2-01 | Hệ thống thiết kế theo UI mục 2–6: tokens.css, app.css, 4 layout, các partial dùng chung, ui.js, StatusDisplay và trang… | Chưa |  |  |
| S8.2-02 | Program.cs: xác thực, policy, Anti-Forgery toàn cục, xử lý lỗi, UseForwardedHeaders. LoginController đủ chức năng ở 2.5… | Chưa |  |  |
| S8.2-03 | FileController; CategoriesController; DrinksController (upload ảnh bằng fetch, nhập định mức nguyên liệu, lọc theo loại… | Chưa |  |  |
| S8.2-04 | Thanh đổi vai trò và /demo/switch-role (mục 5). | Chưa |  |  |
| S8.2-05 | Script tools/ui-snapshots (UI mục 13). | Chưa |  |  |
| U2-02 | Có hai theme: data-theme="ops" (nền sáng) cho quản trị và POS; data-theme="cinema" (nền tối) cho web khách, màn hình ph… | Chưa |  |  |
| U2-03 | Nút primary của quản trị và POS màu xanh dương; màu đỏ thương hiệu chỉ dùng cho logo và nút chính của web khách. Nhờ vậ… | Chưa |  |  |
| U2-04 | Chữ trên nhãn tuổi luôn dùng --age-fg (riêng nhãn C dùng chữ trắng). Không đặt chữ trắng lên nền vàng hoặc xanh lá sáng. | Chưa |  |  |
| U2-07 | Be Vietnam Pro các độ đậm 400, 500, 600, 700, tự host. | Chưa |  |  |
| U2-08 | Thân bài 16px ở web khách, 14px trong bảng quản trị. Không có chữ nào nhỏ hơn 12px. | Chưa |  |  |
| U2-09 | line-height tối thiểu 1.4 cho mọi chữ tiếng Việt để không bị cắt dấu. Không đặt height cố định cho phần tử chứa chữ. | Chưa |  |  |
| U2-10 | Tiền, giờ, số lượng dùng font-variant-numeric: tabular-nums. | Chưa |  |  |
| U2-11 | Đoạn văn tối đa khoảng 70 ký tự mỗi dòng trên desktop. | Chưa |  |  |
| U2-12 | Không viết hoa toàn bộ, trừ mã như IMAX, 2D. Tiêu đề viết hoa chữ đầu câu. | Chưa |  |  |
| U3-01 | Bootstrap 5.3.3. Theme cinema đặt thêm data-bs-theme="dark". | Chưa |  |  |
| U3-02 | Bản dist của Bootstrap không tự đổi màu nút theo --bs-primary. Trong app.css phải ghi đè --bs-btn-* cho từng biến thể n… | Chưa |  |  |
| U3-03 | Icon chỉ dùng Bootstrap Icons, cỡ 16, 20 hoặc 24px, luôn đi kèm chữ, hoặc tooltip cộng aria-label. | Chưa |  |  |
| U4-01 | Header dính, cao 64px, nền kính mờ (backdrop-filter blur 12px, --glass). | Chưa |  |  |
| U4-02 | Logo FPoly Cinema gồm chữ và một biểu tượng SVG tự vẽ. | Chưa |  |  |
| U4-03 | Menu: Phim, Lịch chiếu, Bắp nước, Ưu đãi; ô chọn rạp; tài khoản. Trên điện thoại, menu nằm trong offcanvas. | Chưa |  |  |
| U4-04 | Nội dung đặt trong container-xl; các khối cách nhau 48–64px. | Chưa |  |  |
| U4-05 | Footer gồm thông tin rạp, liên hệ, điều khoản, chính sách hủy vé. | Chưa |  |  |
| U4-06 | Sidebar rộng 248px, thu gọn còn 72px (chỉ icon kèm tooltip); trên điện thoại là offcanvas. | Chưa |  |  |
| U4-07 | Nhóm mục của sidebar: Tổng quan; Bán hàng; Phim và lịch chiếu; Rạp và phòng; Kho bắp nước; Nhân sự; Báo cáo; Cấu hình. | Chưa |  |  |
| U4-08 | Topbar cao 56px: breadcrumb, chọn rạp, chuông phê duyệt (kèm số yêu cầu đang chờ), menu người dùng. | Chưa |  |  |
| U4-09 | Vùng nội dung padding 24px; form rộng tối đa 960px. | Chưa |  |  |
| U4-10 | Không có sidebar. Topbar cao 48px: mã quầy, thông tin ca, người dùng, đồng hồ, chỉ báo kết nối, nút đóng ca. | Chưa |  |  |
| U4-11 | Cao đúng 100vh; trang không cuộn, từng cột tự cuộn bên trong. | Chưa |  |  |
| U4-12 | _LayoutDisplay (cinema): toàn màn hình, không có thanh điều hướng; ẩn con trỏ sau 3 giây không dùng; hiện lớp phủ "Đang… | Chưa |  |  |
| U4-13 | Viên thuốc nổi ở góc trái dưới, thu gọn được. | Chưa |  |  |
| U4-14 | Hiện vai trò hiện tại và 5 nút đổi vai trò. | Chưa |  |  |
| U4-16 | Không được che nút chính của trang. | Chưa |  |  |
| U5-01 | Tất cả thành phần dưới đây đều phải có mặt trên /dev/style-guide, hiển thị ở cả hai theme. | Chưa |  |  |
| U5-02 | Cao 32, 40, 48 hoặc 56px; nút thanh toán của POS cao 64px. | Chưa |  |  |
| U5-03 | Các kiểu: primary, secondary (viền), ghost, danger, success. | Chưa |  |  |
| U5-04 | Khi đang xử lý: spinner thay icon, nút bị khóa, giữ nguyên chiều rộng. | Chưa |  |  |
| U5-05 | Mỗi vùng có tối đa một nút primary. | Chưa |  |  |
| U5-06 | Cao 40px (POS 48px). Nhãn đặt ở trên (14px, đậm 500); placeholder không thay cho nhãn. | Chưa |  |  |
| U5-07 | Chữ trợ giúp 12px màu muted. Lỗi 13px màu danger có icon, hiện ngay dưới ô. Trường bắt buộc có dấu *. | Chưa |  |  |
| U5-08 | Ô tiền có hậu tố ₫ và tự chấm phân cách hàng nghìn. Ngày giờ dùng input date, time gốc nhưng hiển thị dd/MM/yyyy. | Chưa |  |  |
| U5-09 | Công tắc Active: bật tắt ngay tại chỗ, đổi trạng thái lạc quan; nếu lỗi thì trả về như cũ và báo. | Chưa |  |  |
| U5-10 | Trạng thái: dạng viên thuốc 12px, nền nhạt, chữ đậm hơn nền. | Chưa |  |  |
| U5-11 | Nhãn tuổi (_AgeBadge): bo góc 6px, chữ đậm 700, rộng tối thiểu 36px, chữ màu --age-fg, có tooltip giải thích. | Chưa |  |  |
| U5-12 | Định dạng phòng và ngôn ngữ: dạng viền. | Chưa |  |  |
| U5-13 | Poster tỉ lệ 2:3 (aspect-ratio), bo góc 10px, nhãn tuổi ở góc trên trái. | Chưa |  |  |
| U5-14 | Tên phim tối đa 2 dòng; dòng phụ dạng "Hành động · 2 giờ 05 phút"; nút "Đặt vé". | Chưa |  |  |
| U5-15 | Khi hover thẻ nâng lên 2px. | Chưa |  |  |
| U5-16 | Thẻ KPI: icon trong vòng tròn màu nhạt; nhãn 13px muted; giá trị 28px đậm 600 dạng tabular; chip tăng hoặc giảm so với … | Chưa |  |  |
| U5-17 | Thẻ món bắp nước: ảnh tỉ lệ 1:1, tên, giá, bộ tăng giảm (− số +) với nút 40px (POS 48px); khi hết hàng có lớp phủ "Tạm … | Chưa |  |  |
| U5-18 | Tiêu đề cột 13px đậm 600 màu muted; mỗi dòng cao 48px, đổi nền khi hover. | Chưa |  |  |
| U5-19 | Số canh phải; trạng thái hiển thị bằng nhãn. | Chưa |  |  |
| U5-20 | Cột thao tác nằm bên phải, dùng nút icon có tooltip. Tiêu đề dính khi cuộn. | Chưa |  |  |
| U5-21 | Phân trang dạng "1–20 trên 135". Trên điện thoại, bảng chuyển thành danh sách thẻ. | Chưa |  |  |
| U5-22 | Ô tìm kiếm có icon và nút xóa, tìm sau 300ms ngừng gõ. | Chưa |  |  |
| U5-23 | Chọn trạng thái; chọn khoảng ngày với các mốc nhanh (Hôm nay, 7 ngày, 30 ngày, Tháng này); nút "Xóa bộ lọc". | Chưa |  |  |
| U5-24 | Đầu trang (_PageHeader): breadcrumb, tiêu đề 24px đậm 600, mô tả 14px muted, các nút đặt bên phải. | Chưa |  |  |
| U5-25 | Stepper 4 bước: số trong vòng tròn, bước hiện tại màu primary, bước đã xong có dấu ✓. Trên điện thoại hiện gọn "Bước 2/… | Chưa |  |  |
| U5-26 | Toast: góc phải trên (điện thoại: giữa phía trên); 4 loại; tự ẩn sau 4 giây (lỗi 6 giây); hiện tối đa 3 cái. | Chưa |  |  |
| U5-27 | Hộp xác nhận: tiêu đề, nội dung, danh sách hậu quả nếu có, hai nút Hủy và Xác nhận. Thao tác hàng loạt nguy hiểm yêu cầ… | Chưa |  |  |
| U5-28 | Bàn phím PIN: 6 chấm, phím số bố trí 3 × 4 cỡ 64px. Nhập sai thì rung và báo số lần còn lại, hoặc thời gian bị khóa. | Chưa |  |  |
| U5-29 | Trạng thái rỗng: icon 48px trong vòng tròn màu nhạt, tiêu đề, một câu giải thích, một nút hành động. | Chưa |  |  |
| U5-30 | Skeleton: khối nhấp nháy đúng theo bố cục thật; chỉ hiện khi tải lâu hơn 250ms. | Chưa |  |  |
| U5-31 | Dạng viên thuốc mm:ss. | Chưa |  |  |
| U5-32 | Còn từ 2 phút trở xuống thì chuyển màu warning; còn từ 1 phút trở xuống thì màu danger và nhấp nhẹ. | Chưa |  |  |
| U5-33 | Về 0 thì mở hộp "Hết thời gian giữ ghế" với nút "Chọn lại". | Chưa |  |  |
| U5-34 | Chỉ báo kết nối: chấm xanh, vàng hoặc đỏ, kèm chữ "Đã kết nối", "Đang kết nối lại", "Mất kết nối". | Chưa |  |  |
| U5-35 | Khối tổng tiền: liệt kê từng dòng, tạm tính, giảm giá; tổng 24px đậm 700 (POS 32px). | Chưa |  |  |
| U5-36 | Khối QR: nền trắng bo góc 16px, QR 240px (màn hình phụ 360px), mã ghi bên dưới, có đồng hồ đếm ngược. | Chưa |  |  |
| U5-37 | Ảnh: luôn có width và height hoặc aspect-ratio cố định, object-fit cover, loading lazy. Khi lỗi, onerror chuyển sang /i… | Chưa |  |  |
| U5-38 | Partial: _PageHeader, _AgeBadge, _StatusBadge, _EmptyState, _Pagination, _FilterBar, _MoneySummary, _Stepper, _SeatMap. | Chưa |  |  |
| U5-39 | C#: Fmt.Money, Fmt.Date, Fmt.Time, Fmt.DateTime, Fmt.Duration, dùng CultureInfo vi-VN. | Chưa |  |  |
| U5-40 | ui.toast.success, ui.toast.error, ui.toast.warning, ui.toast.info. | Chưa |  |  |
| U5-41 | ui.confirm({...}) trả Promise<boolean>; ui.pin({...}) trả Promise<string/null>. | Chưa |  |  |
| U5-42 | ui.api(url, { method, body }): gửi kèm Anti-Forgery, bật trạng thái đang tải, chuyển lỗi dạng CONTRACTS mục 8 thành toa… | Chưa |  |  |
| U5-43 | ui.money(n), ui.dateTime(d), ui.shake(el). | Chưa |  |  |
| U6-01 | Mọi vùng tải dữ liệu đều đủ 4 trạng thái: đang tải (skeleton), rỗng (_EmptyState), lỗi (thông báo kèm nút Thử lại), có … | Chưa |  |  |
| U6-02 | Nút bị khóa luôn có lý do, ghi trong tooltip hoặc dòng chữ nhỏ cạnh nút. | Chưa |  |  |
| U6-03 | Không bao giờ hiện thẳng giá trị enum. Dùng StatusDisplay (C#), xuất ra JSON trong layout để JS dùng chung: | Chưa |  |  |
| U7.C6-01 | C6. Đăng nhập và đăng ký: desktop chia đôi màn hình (bên trái ảnh không khí rạp phim, bên phải form rộng 400px); điện t… | Chưa |  |  |
| U7.C6-02 | Quên mật khẩu gồm 2 bước trên cùng thẻ form: bước 1 nhập email; bước 2 nhập mã OTP 6 số (6 ô, tự nhảy ô, dán được cả ch… | Chưa |  |  |
| U7.C6-03 | Đăng nhập bằng mật khẩu được cấp mà MustChangePassword = 1 thì chuyển ngay tới trang Đổi mật khẩu bắt buộc (không có me… | Chưa |  |  |
| U7.A2-01 | A2. Danh sách (mẫu chung cho Loại đồ uống, Đồ uống, Phim, Nhân viên, Nhà phát hành, Nguyên liệu) | Chưa |  |  |
| U7.A2-02 | Gồm _PageHeader, thanh lọc, bảng dữ liệu, phân trang, trạng thái rỗng. | Chưa |  |  |
| U7.A2-03 | Thanh lọc tối thiểu: ô tìm theo tên (debounce 300ms, giữ trên query string) và bộ lọc theo danh mục cha. Ví dụ Đồ uống … | Chưa |  |  |
| U7.A2-04 | Bấm xóa thì mở hộp xác nhận nêu rõ hậu quả. Nếu không được xóa cứng, hộp thoại giải thích lý do và gợi ý "Ngưng hoạt độ… | Chưa |  |  |
| U7.A3-01 | A3. Form thêm và sửa | Chưa |  |  |
| U7.A3-02 | Thẻ rộng tối đa 960px, chia thành các phần có tiêu đề; từ 992px chia 2 cột. | Chưa |  |  |
| U7.A3-03 | Vùng kéo thả ảnh món tỉ lệ 1:1; xem trước ngay; có nút xóa ảnh. | Chưa |  |  |
| U7.A3-05 | Thanh nút dính chân form: "Hủy" (ghost) và "Lưu" (primary). Cảnh báo khi rời trang mà chưa lưu. | Chưa |  |  |
| U7.A9-01 | A9. Trang cá nhân nhân viên /me (mọi nhân viên, dùng layout của vai trò đang đăng nhập) | Chưa |  |  |
| U7.A9-02 | Thẻ thông tin: ảnh đại diện chữ cái đầu, họ tên, email (chỉ đọc), số điện thoại, vai trò, rạp. Nút "Lưu". | Chưa |  |  |
| U7.A9-03 | Thẻ đổi mật khẩu: mật khẩu hiện tại, mật khẩu mới, nhập lại; có thước đo độ mạnh và nút hiện hoặc ẩn. Đổi xong thì đăng… | Chưa |  |  |
| U7.D1-01 | D1. Style guide /dev/style-guide (chỉ khi Demo:EnableTools): mọi thành phần ở mục 5 trên cả hai theme; mỗi thành phần c… | Chưa |  |  |
| U7.D2-01 | D2. Hộp thư thử /dev/mailbox (chỉ khi Demo:EnableTools): danh sách thư trong EmailOutbox, mới nhất ở trên; bấm vào thì … | Chưa |  |  |
| U7.D3-01 | D3. Trang từ chối truy cập /Home/AccessDenied và trang lỗi /Home/Error: dùng layout của khu vực đang đứng, icon 48px, m… | Chưa |  |  |
| U9-01 | Với khách: lịch sự, ngắn gọn, xưng "bạn". Với nhân viên: viết thẳng việc cần làm. | Chưa |  |  |
| U9-02 | Không dùng thuật ngữ kỹ thuật. Không hiện mã lỗi; chỉ ghi mã nhỏ ở cuối khi cần liên hệ hỗ trợ. | Chưa |  |  |
| U9-03 | Lời báo lỗi gồm hai ý: chuyện gì xảy ra, và cách sửa. | Chưa |  |  |
| U9-04 | Định dạng: tiền "125.000 ₫"; ngày "06/11/2026"; giờ "19:30"; ngày trong tuần "T7, 07/11"; thời lượng "2 giờ 05 phút"; g… | Chưa |  |  |
| U9-18 | OTP_INVALID: "Mã xác nhận chưa đúng. Bạn còn 3 lần thử." | Chưa |  |  |
| U9-19 | OTP_EXPIRED: "Mã xác nhận đã hết hạn. Bấm Gửi lại mã để nhận mã mới." | Chưa |  |  |
| U9-20 | MUST_CHANGE_PASSWORD: "Bạn đang dùng mật khẩu được cấp. Đặt mật khẩu mới để tiếp tục." | Chưa |  |  |
| U9-21 | Mất mạng: "Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại." | Chưa |  |  |
| U9-22 | 403: "Tài khoản của bạn không có quyền dùng chức năng này." | Chưa |  |  |
| U9-23 | Trạng thái rỗng mẫu: "Chưa có hóa đơn nào hôm nay"; "Không có suất chiếu phù hợp – thử đổi ngày hoặc bỏ bớt bộ lọc". | Chưa |  |  |
| U9-24 | Toast thành công mẫu: "Đã lưu thay đổi"; "Đã giữ 3 ghế trong 10 phút". | Chưa |  |  |
| U10-01 | Độ tương phản: chữ thường ít nhất 4,5:1; chữ lớn và viền thành phần ít nhất 3:1. Các cặp màu trong token đã đạt mức này… | Chưa |  |  |
| U10-02 | Vòng focus 2px màu --focus, cách 2px, hiện khi dùng bàn phím (:focus-visible). | Chưa |  |  |
| U10-03 | Nút chỉ có icon phải có aria-label. | Chưa |  |  |
| U10-05 | Không truyền thông tin chỉ bằng màu: trạng thái ghế và kết quả soát vé luôn có icon hoặc chữ đi kèm. | Chưa |  |  |
| U10-06 | Vùng chạm tối thiểu 44px (POS 48px, máy soát vé 56px). | Chưa |  |  |
| U10-07 | Tôn trọng prefers-reduced-motion: tắt hiệu ứng rung, nhấp nháy và chuyển động trang trí. | Chưa |  |  |
| U10-08 | Hộp thoại giữ focus bên trong và đóng được bằng Esc, trừ hộp PIN và hộp xác nhận thao tác nguy hiểm. | Chưa |  |  |
| U11-01 | tools/ui-snapshots/routes.json ghi mã màn hình, vai trò, đường dẫn và đúng các kích thước trong bảng trên cho từng khu … | Chưa |  |  |
| U12-01 | Dùng bảng hoặc form HTML mặc định thay cho thành phần chung; để tiêu đề scaffold kiểu "Index", "Create", "Edit"; để sót… | Chưa |  |  |
| U12-02 | Dùng thuộc tính style inline (trừ biến CSS động, như vị trí khối trên Gantt); dùng mã màu ngoài tokens.css; dùng cỡ chữ… | Chưa |  |  |
| U12-03 | Dùng alert(), confirm(), prompt() của trình duyệt. | Chưa |  |  |
| U12-04 | Dùng lorem ipsum, ảnh tải từ internet; để ảnh vỡ hoặc ảnh méo tỉ lệ. | Chưa |  |  |
| U12-05 | Hiện mã enum, stack trace hay lỗi 500 thô cho người dùng. | Chưa |  |  |
| U12-06 | Trộn nhiều bộ icon hoặc nhiều font. | Chưa |  |  |
| U12-07 | Đặt hơn một nút primary trong một vùng; để nút chính của POS hay bước đặt vé phải cuộn mới thấy. | Chưa |  |  |
| U12-08 | Để cả trang có thanh cuộn ngang trên điện thoại (chỉ vùng sơ đồ ghế được cuộn ngang). | Chưa |  |  |
| U12-09 | Đặt chữ xám nhạt trên nền xám; chữ trắng trên nền vàng hoặc xanh lá sáng. | Chưa |  |  |
| U13-01 | Phase 2 dựng /dev/style-guide trước tiên. Mọi màn hình sau chỉ dùng thành phần có ở đó. Cần thành phần mới thì thêm vào… | Chưa |  |  |
| U13-02 | Đọc routes.json: mã màn hình, vai trò, đường dẫn, kích thước theo mục 11. | Chưa |  |  |
| U13-03 | Đăng nhập bằng /demo/switch-role; chờ trang tải xong và font sẵn sàng. | Chưa |  |  |
| U13-04 | Chụp toàn trang vào artifacts/ui/phase-N/{mãMànHình}-{vai-tro}-{rong}x{cao}.png, ví dụ artifacts/ui/phase-4/A8-manager-… | Chưa |  |  |
| U13-05 | Cài một lần: npm i -D playwright, rồi npx playwright install chromium. | Chưa |  |  |
| U13-06 | Claude Code mở từng ảnh, đối chiếu với danh sách bên dưới, sửa rồi chụp lại cho đến khi đạt, và ghi kết quả vào PROGRES… | Chưa |  |  |
| U13-07 | Chữ không tràn, không bị cắt dấu, không chồng lên nhau; không có thanh cuộn ngang trên điện thoại. | Chưa |  |  |
| U13-08 | Lề thẳng theo lưới; khoảng cách theo thang token; các thẻ cùng loại cao bằng nhau. | Chưa |  |  |
| U13-09 | Mỗi vùng có đúng một nút chính và nhìn thấy ngay. | Chưa |  |  |
| U13-10 | Tương phản đủ đọc, nhất là chữ muted trên nền tối. | Chưa |  |  |
| U13-11 | Có trạng thái rỗng, lỗi, đang tải; không có ảnh vỡ; không sót chữ tiếng Anh. | Chưa |  |  |
| U13-12 | Tiền, ngày, giờ đúng định dạng Việt Nam; trạng thái là nhãn tiếng Việt. | Chưa |  |  |
| U13-13 | Nhãn độ tuổi xuất hiện ở đủ những nơi liệt kê trong SPEC 3.1 mục 7. | Chưa |  |  |
| U13-14 | Màn hình hiển thị đọc rõ từ xa: chữ chính tối thiểu 28px. | Chưa |  |  |
| U13-15 | Dữ liệu dài hoặc rỗng không làm vỡ bố cục: thử với tên phim khoảng 40 ký tự, giỏ 20 món, danh sách 0 kết quả. | Chưa |  |  |
| C1-02 | Giao diện hiển thị vai trò bằng cột Hiển thị, không hiện giá trị gốc. | Chưa |  |  |
| C2-02 | Mỗi controller và action dùng đúng policy theo cột Dùng cho; chức năng thuộc phase sau dùng đúng policy của nó khi được… | Chưa |  |  |
| C8-01 | Thành công: { "ok": true, "data": ... } | Chưa |  |  |
| C8-02 | Lỗi: { "ok": false, "error": { "code": "...", "message": "câu tiếng Việt cho người dùng", "details": { ... } } } | Chưa |  |  |
| C8-03 | HTTP: 200 thành công; 400 dữ liệu không hợp lệ; 401, 403 lỗi quyền; 404 không tìm thấy; 409 xung đột; 422 vi phạm quy t… | Chưa |  |  |
| C9-08 | Route của mọi màn hình trong UI mục 7 khai báo bằng attribute routing ([Route], [HttpGet("…")]) đúng chuỗi đường dẫn gh… | Chưa |  |  |
| UC01 | Đăng nhập, đăng xuất (màn hình C6) | Chưa |  |  |
| UC02 | Quên mật khẩu (OTP, cấp mật khẩu mới qua email) (màn hình C6) | Chưa |  |  |
| UC05 | Đăng ký tài khoản (màn hình C6) | Chưa |  |  |
| UC28 | Quản lý loại đồ uống (màn hình A2, A3) | Chưa |  |  |
| UC29 | Quản lý đồ uống và định mức (màn hình A2, A3) | Chưa |  |  |
| UC36 | Quản lý nhân viên (màn hình A2, A3) | Chưa |  |  |
