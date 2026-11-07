# Giao diện FPolyCinema – mục 1 đến 6: nguyên tắc, token, thư viện, bố cục, thành phần, trạng thái

Mục lục và cách đọc mã yêu cầu như [S3.6-04 P4]: docs/README.md. Tên chuẩn: docs/CONTRACTS.md. Giao diện: docs/ui.

Mục tiêu: giao diện đẹp, nhất quán, dùng được ngay lần đầu, hợp với từng nhóm người dùng. Mọi màn hình dựng từ token và thành phần trong file này; không tự chế màu, cỡ chữ, khoảng cách hay kiểu nút mới. Được tham khảo bố cục quen thuộc của các trang đặt vé phổ biến, nhưng không sao chép logo, tên, ảnh hay màu thương hiệu của bên khác. Thương hiệu của dự án là FPoly Cinema.

## 1. Nguyên tắc
<!-- spec-check: không gắn mã -->
1. Mỗi màn hình có một việc chính; nút chính nổi bật và luôn nhìn thấy.
2. Nhất quán: cùng loại thông tin thì hiển thị cùng một cách ở mọi nơi (trạng thái, tiền, ngày giờ, nhãn tuổi).
3. Phản hồi ngay: mọi thao tác đều có trạng thái đang xử lý, thành công hoặc lỗi, bằng lời tiếng Việt rõ ràng.
4. Ngăn lỗi tốt hơn báo lỗi: khóa sẵn lựa chọn không hợp lệ và giải thích lý do ngay bên cạnh.
5. Đúng thiết bị: web khách ưu tiên điện thoại; POS, bếp, soát vé ưu tiên chạm nhanh; quản trị ưu tiên màn hình rộng; màn hình hiển thị phải đọc được từ 2–3 m.

## 2. Token thiết kế (wwwroot/css/tokens.css)

- [U2-01 P2 auto:tokens] wwwroot/css/tokens.css khai báo đúng tên và giá trị của mọi biến trong khối dưới đây.
- [U2-02 P2] Có hai theme: `data-theme="ops"` (nền sáng) cho quản trị và POS; `data-theme="cinema"` (nền tối) cho web khách, màn hình phụ, bảng sảnh, bảng gọi số, bếp, máy soát vé. Sơ đồ ghế luôn dùng theme cinema, kể cả khi nằm trong POS.

```css
:root {
  --brand-red: #E50914; --brand-red-hover: #C40812; --brand-gold: #F59E0B;

  --font-sans: "Be Vietnam Pro", system-ui, -apple-system, "Segoe UI", Roboto, Arial, sans-serif;
  --fs-xs: .75rem; --fs-sm: .875rem; --fs-md: 1rem; --fs-lg: 1.125rem; --fs-xl: 1.25rem;
  --fs-2xl: 1.5rem; --fs-3xl: 1.875rem; --fs-4xl: 2.25rem; --fs-display: 3rem;
  --lh-tight: 1.25; --lh-normal: 1.5;

  --sp-1: 4px; --sp-2: 8px; --sp-3: 12px; --sp-4: 16px; --sp-5: 24px; --sp-6: 32px; --sp-7: 48px; --sp-8: 64px;
  --radius-sm: 6px; --radius-md: 10px; --radius-lg: 16px; --radius-pill: 999px;
  --dur-fast: 150ms; --dur-base: 200ms; --ease: cubic-bezier(.2,.8,.2,1);

  --age-fg: #0B0F19;
  --age-P: #10B981; --age-K: #3B82F6; --age-T13: #F59E0B; --age-T16: #F97316; --age-T18: #EF4444;
  --age-C: #6B7280;   /* riêng nhãn C dùng chữ trắng */

  --seat-size: 32px; --seat-gap: 6px;
}

[data-theme="ops"] {
  --bg: #F6F7F9; --surface: #FFFFFF; --surface-2: #F3F4F6; --border: #E5E7EB;
  --text: #111827; --text-muted: #6B7280;
  --primary: #2563EB; --primary-hover: #1D4ED8; --on-primary: #FFFFFF; --focus: #2563EB;
  --success: #15803D; --success-bg: #DCFCE7; --success-fg: #166534;
  --warning: #B45309; --warning-bg: #FEF3C7; --warning-fg: #92400E;
  --danger:  #DC2626; --danger-bg:  #FEE2E2; --danger-fg:  #991B1B;
  --info:    #0369A1; --info-bg:    #E0F2FE; --info-fg:    #075985;
  --neutral-bg: #F3F4F6; --neutral-fg: #374151;
  --shadow-sm: 0 1px 2px rgba(16,24,40,.06); --shadow-md: 0 4px 12px rgba(16,24,40,.08);
}

[data-theme="cinema"] {
  --bg: #0B0F19; --surface: #111827; --surface-2: #1F2937; --border: #374151;
  --text: #F9FAFB; --text-muted: #9CA3AF;
  --primary: #E50914; --primary-hover: #C40812; --on-primary: #FFFFFF; --focus: #F59E0B;
  --glass: rgba(17,24,39,.72);
  --success: #16A34A; --success-bg: rgba(34,197,94,.15);  --success-fg: #4ADE80;
  --warning: #D97706; --warning-bg: rgba(245,158,11,.15); --warning-fg: #FBBF24;
  --danger:  #DC2626; --danger-bg:  rgba(239,68,68,.15);  --danger-fg:  #F87171;
  --info:    #0284C7; --info-bg:    rgba(56,189,248,.15); --info-fg:    #7DD3FC;
  --neutral-bg: rgba(156,163,175,.15); --neutral-fg: #D1D5DB;
  --shadow-md: 0 8px 24px rgba(0,0,0,.45);

  --seat-bg: #1F2937; --seat-border: #4B5563; --seat-text: #D1D5DB;
  --seat-vip: #F59E0B; --seat-sweetbox: #EC4899; --seat-wheelchair: #38BDF8;
  --seat-selected: #E50914; --seat-selected-text: #FFFFFF;
  --seat-held: #4B5563; --seat-booked: #111827; --seat-booked-text: #6B7280;
  --seat-house: #F59E0B; --seat-maint: #374151;
  --heat-1: #374151; --heat-2: #065F46; --heat-3: #047857; --heat-4: #10B981; --heat-5: #34D399;
}
```

Quy tắc dùng màu:
- [U2-03 P2] Nút primary của quản trị và POS màu xanh dương; màu đỏ thương hiệu chỉ dùng cho logo và nút chính của web khách. Nhờ vậy nút chính không bị nhầm với nút xóa (danger).
- [U2-04 P2] Chữ trên nhãn tuổi luôn dùng --age-fg (riêng nhãn C dùng chữ trắng). Không đặt chữ trắng lên nền vàng hoặc xanh lá sáng.
- [U2-05 P6] Màn hình kết quả soát vé dùng nền đặc: hợp lệ #15803D, cảnh báo #B45309, từ chối #B91C1C, chữ trắng.
- [U2-06 P3] Thang heatmap (--heat-1 đến --heat-5) tương ứng các mức điểm 0–39, 40–59, 60–74, 75–89, 90–100. Hai mức sáng nhất dùng chữ tối, ba mức còn lại dùng chữ sáng. Ghế đang chọn luôn giữ màu --seat-selected để không lẫn với heatmap.

Chữ:
- [U2-07 P2] Be Vietnam Pro các độ đậm 400, 500, 600, 700, tự host.
- [U2-08 P2] Thân bài 16px ở web khách, 14px trong bảng quản trị. Không có chữ nào nhỏ hơn 12px.
- [U2-09 P2] line-height tối thiểu 1.4 cho mọi chữ tiếng Việt để không bị cắt dấu. Không đặt height cố định cho phần tử chứa chữ.
- [U2-10 P2] Tiền, giờ, số lượng dùng `font-variant-numeric: tabular-nums`.
- [U2-11 P2] Đoạn văn tối đa khoảng 70 ký tự mỗi dòng trên desktop.
- [U2-12 P2] Không viết hoa toàn bộ, trừ mã như IMAX, 2D. Tiêu đề viết hoa chữ đầu câu.

## 3. Bootstrap và thư viện
- [U3-01 P2] Bootstrap 5.3.3. Theme cinema đặt thêm `data-bs-theme="dark"`.
- [U3-02 P2] Bản dist của Bootstrap không tự đổi màu nút theo --bs-primary. Trong app.css phải ghi đè --bs-btn-* cho từng biến thể nút, cùng --bs-link-color, --bs-focus-ring-color, --bs-border-radius, --bs-body-font-family, --bs-body-bg, --bs-body-color, --bs-border-color để trỏ về token.
- [U3-03 P2] Icon chỉ dùng Bootstrap Icons, cỡ 16, 20 hoặc 24px, luôn đi kèm chữ, hoặc tooltip cộng aria-label.
- [U3-04 P4] Chart.js: màu lấy từ token, lưới mờ, tooltip định dạng tiền Việt, không dùng hiệu ứng 3D. Mỗi biểu đồ có tiêu đề nói rõ đang đo gì và trong khoảng thời gian nào.

## 4. Bố cục (Views/Shared)
- _LayoutCustomer (cinema):
  - [U4-01 P2] Header dính, cao 64px, nền kính mờ (backdrop-filter blur 12px, --glass).
  - [U4-02 P2] Logo FPoly Cinema gồm chữ và một biểu tượng SVG tự vẽ.
  - [U4-03 P2] Menu: Phim, Lịch chiếu, Bắp nước, Ưu đãi; ô chọn rạp; tài khoản. Trên điện thoại, menu nằm trong offcanvas.
  - [U4-04 P2] Nội dung đặt trong container-xl; các khối cách nhau 48–64px.
  - [U4-05 P2] Footer gồm thông tin rạp, liên hệ, điều khoản, chính sách hủy vé.
- _LayoutAdmin (ops):
  - [U4-06 P2] Sidebar rộng 248px, thu gọn còn 72px (chỉ icon kèm tooltip); trên điện thoại là offcanvas.
  - [U4-07 P2] Nhóm mục của sidebar: Tổng quan; Bán hàng; Phim và lịch chiếu; Rạp và phòng; Kho bắp nước; Nhân sự; Báo cáo; Cấu hình.
  - [U4-08 P2] Topbar cao 56px: breadcrumb, chọn rạp, chuông phê duyệt (kèm số yêu cầu đang chờ), menu người dùng.
  - [U4-09 P2] Vùng nội dung padding 24px; form rộng tối đa 960px.
- _LayoutPos (ops; trang bếp và máy soát vé đổi sang cinema):
  - [U4-10 P2] Không có sidebar. Topbar cao 48px: mã quầy, thông tin ca, người dùng, đồng hồ, chỉ báo kết nối, nút đóng ca.
  - [U4-11 P2] Cao đúng 100vh; trang không cuộn, từng cột tự cuộn bên trong.
- [U4-12 P2] _LayoutDisplay (cinema): toàn màn hình, không có thanh điều hướng; ẩn con trỏ sau 3 giây không dùng; hiện lớp phủ "Đang kết nối lại…" khi mất kết nối thời gian thực.
- Thanh demo (chỉ khi Demo:EnableTools):
  - [U4-13 P2] Viên thuốc nổi ở góc trái dưới, thu gọn được.
  - [U4-14 P2] Hiện vai trò hiện tại và 5 nút đổi vai trò.
  - [U4-15 P8] Nút mở trung tâm demo.
  - [U4-16 P2] Không được che nút chính của trang.

## 5. Thành phần dùng chung

- [U5-01 P2] Tất cả thành phần dưới đây đều phải có mặt trên /dev/style-guide, hiển thị ở cả hai theme.
- Nút:
  - [U5-02 P2] Cao 32, 40, 48 hoặc 56px; nút thanh toán của POS cao 64px.
  - [U5-03 P2] Các kiểu: primary, secondary (viền), ghost, danger, success.
  - [U5-04 P2] Khi đang xử lý: spinner thay icon, nút bị khóa, giữ nguyên chiều rộng.
  - [U5-05 P2] Mỗi vùng có tối đa một nút primary.
- Ô nhập:
  - [U5-06 P2] Cao 40px (POS 48px). Nhãn đặt ở trên (14px, đậm 500); placeholder không thay cho nhãn.
  - [U5-07 P2] Chữ trợ giúp 12px màu muted. Lỗi 13px màu danger có icon, hiện ngay dưới ô. Trường bắt buộc có dấu *.
  - [U5-08 P2] Ô tiền có hậu tố ₫ và tự chấm phân cách hàng nghìn. Ngày giờ dùng input date, time gốc nhưng hiển thị dd/MM/yyyy.
- [U5-09 P2] Công tắc Active: bật tắt ngay tại chỗ, đổi trạng thái lạc quan; nếu lỗi thì trả về như cũ và báo.
- Nhãn:
  - [U5-10 P2] Trạng thái: dạng viên thuốc 12px, nền nhạt, chữ đậm hơn nền.
  - [U5-11 P2] Nhãn tuổi (_AgeBadge): bo góc 6px, chữ đậm 700, rộng tối thiểu 36px, chữ màu --age-fg, có tooltip giải thích.
  - [U5-12 P2] Định dạng phòng và ngôn ngữ: dạng viền.
- Thẻ phim:
  - [U5-13 P2] Poster tỉ lệ 2:3 (aspect-ratio), bo góc 10px, nhãn tuổi ở góc trên trái.
  - [U5-14 P2] Tên phim tối đa 2 dòng; dòng phụ dạng "Hành động · 2 giờ 05 phút"; nút "Đặt vé".
  - [U5-15 P2] Khi hover thẻ nâng lên 2px.
- [U5-16 P2] Thẻ KPI: icon trong vòng tròn màu nhạt; nhãn 13px muted; giá trị 28px đậm 600 dạng tabular; chip tăng hoặc giảm so với kỳ trước.
- [U5-17 P2] Thẻ món bắp nước: ảnh tỉ lệ 1:1, tên, giá, bộ tăng giảm (− số +) với nút 40px (POS 48px); khi hết hàng có lớp phủ "Tạm hết".
- Bảng dữ liệu:
  - [U5-18 P2] Tiêu đề cột 13px đậm 600 màu muted; mỗi dòng cao 48px, đổi nền khi hover.
  - [U5-19 P2] Số canh phải; trạng thái hiển thị bằng nhãn.
  - [U5-20 P2] Cột thao tác nằm bên phải, dùng nút icon có tooltip. Tiêu đề dính khi cuộn.
  - [U5-21 P2] Phân trang dạng "1–20 trên 135". Trên điện thoại, bảng chuyển thành danh sách thẻ.
- Thanh lọc:
  - [U5-22 P2] Ô tìm kiếm có icon và nút xóa, tìm sau 300ms ngừng gõ.
  - [U5-23 P2] Chọn trạng thái; chọn khoảng ngày với các mốc nhanh (Hôm nay, 7 ngày, 30 ngày, Tháng này); nút "Xóa bộ lọc".
- [U5-24 P2] Đầu trang (_PageHeader): breadcrumb, tiêu đề 24px đậm 600, mô tả 14px muted, các nút đặt bên phải.
- [U5-25 P2] Stepper 4 bước: số trong vòng tròn, bước hiện tại màu primary, bước đã xong có dấu ✓. Trên điện thoại hiện gọn "Bước 2/4 · Chọn ghế".
- [U5-26 P2] Toast: góc phải trên (điện thoại: giữa phía trên); 4 loại; tự ẩn sau 4 giây (lỗi 6 giây); hiện tối đa 3 cái.
- [U5-27 P2] Hộp xác nhận: tiêu đề, nội dung, danh sách hậu quả nếu có, hai nút Hủy và Xác nhận. Thao tác hàng loạt nguy hiểm yêu cầu gõ lại mã để xác nhận.
- [U5-28 P2] Bàn phím PIN: 6 chấm, phím số bố trí 3 × 4 cỡ 64px. Nhập sai thì rung và báo số lần còn lại, hoặc thời gian bị khóa.
- [U5-29 P2] Trạng thái rỗng: icon 48px trong vòng tròn màu nhạt, tiêu đề, một câu giải thích, một nút hành động.
- [U5-30 P2] Skeleton: khối nhấp nháy đúng theo bố cục thật; chỉ hiện khi tải lâu hơn 250ms.
- Đồng hồ giữ ghế:
  - [U5-31 P2] Dạng viên thuốc mm:ss.
  - [U5-32 P2] Còn từ 2 phút trở xuống thì chuyển màu warning; còn từ 1 phút trở xuống thì màu danger và nhấp nhẹ.
  - [U5-33 P2] Về 0 thì mở hộp "Hết thời gian giữ ghế" với nút "Chọn lại".
- [U5-34 P2] Chỉ báo kết nối: chấm xanh, vàng hoặc đỏ, kèm chữ "Đã kết nối", "Đang kết nối lại", "Mất kết nối".
- [U5-35 P2] Khối tổng tiền: liệt kê từng dòng, tạm tính, giảm giá; tổng 24px đậm 700 (POS 32px).
- [U5-36 P2] Khối QR: nền trắng bo góc 16px, QR 240px (màn hình phụ 360px), mã ghi bên dưới, có đồng hồ đếm ngược.
- [U5-37 P2] Ảnh: luôn có width và height hoặc aspect-ratio cố định, object-fit cover, loading lazy. Khi lỗi, onerror chuyển sang /img/placeholder-poster.svg hoặc /img/placeholder-food.svg.

Partial và helper:
- [U5-38 P2] Partial: _PageHeader, _AgeBadge, _StatusBadge, _EmptyState, _Pagination, _FilterBar, _MoneySummary, _Stepper, _SeatMap.
- [U5-39 P2] C#: Fmt.Money, Fmt.Date, Fmt.Time, Fmt.DateTime, Fmt.Duration, dùng CultureInfo vi-VN.
- ui.js:
  - [U5-40 P2] ui.toast.success, ui.toast.error, ui.toast.warning, ui.toast.info.
  - [U5-41 P2] ui.confirm({...}) trả Promise<boolean>; ui.pin({...}) trả Promise<string|null>.
  - [U5-42 P2] ui.api(url, { method, body }): gửi kèm Anti-Forgery, bật trạng thái đang tải, chuyển lỗi dạng CONTRACTS mục 8 thành toast rồi ném lỗi.
  - [U5-43 P2] ui.money(n), ui.dateTime(d), ui.shake(el).

## 6. Trạng thái và cách hiển thị
- [U6-01 P2] Mọi vùng tải dữ liệu đều đủ 4 trạng thái: đang tải (skeleton), rỗng (_EmptyState), lỗi (thông báo kèm nút Thử lại), có dữ liệu.
- [U6-02 P2] Nút bị khóa luôn có lý do, ghi trong tooltip hoặc dòng chữ nhỏ cạnh nút.
- [U6-03 P2] Không bao giờ hiện thẳng giá trị enum. Dùng StatusDisplay (C#), xuất ra JSON trong layout để JS dùng chung:

| Nhóm | Giá trị → nhãn (kiểu màu) |
|---|---|
| Hóa đơn | waiting → Chờ thanh toán (warning); finish → Đã thanh toán (success); cancel → Đã hủy (danger) |
| Lý do hủy | EXPIRED → Hết hạn giữ ghế; CANCELLED_BEFORE_PAYMENT → Hủy trước khi thanh toán; CANCELLED_BY_USER → Khách tự hủy; VOIDED_BY_POS → Hủy tại quầy (đã duyệt); OVERBOOKED_REFUNDED → Hoàn tiền do mất ghế; INCIDENT_COMPENSATED → Hủy do sự cố, đã bồi thường |
| Vé | NOT_CHECKED_IN → Chưa vào (neutral); CHECKED_IN → Đã vào (success); SWAPPED → Đã đổi ghế (info); VOIDED → Đã hủy (danger) |
| Hóa đơn – vào cửa | NOT_CHECKED_IN → Chưa vào; PARTIALLY_CHECKED_IN → Đã vào một phần (warning); FULLY_CHECKED_IN → Đã vào đủ (success) |
| Bắp nước | PENDING_PICKUP → Chờ nhận (info); PREPARING → Đang chuẩn bị (warning); READY_FOR_PICKUP → Mời nhận đồ (success); FULFILLED → Đã giao (neutral); VOIDED → Đã hủy (danger) |
| Suất chiếu | SCHEDULED → Sắp chiếu (neutral); BOARDING → Mời vào rạp (success); ADS_PLAYING → Đang chiếu quảng cáo (info); FEATURE_PLAYING → Đang chiếu (info); CREDITS_ROLLING → Sắp kết thúc (info); CLEANING_IN_PROGRESS → Đang dọn phòng (warning); COMPLETED → Đã xong (neutral); CANCELLED → Đã hủy (danger) |
| Phê duyệt | PENDING → Chờ duyệt (warning); APPROVED → Đã duyệt (success); REJECTED → Từ chối (danger); EXPIRED → Hết hạn (neutral) |
| Ca | OPEN → Đang mở (success); CLOSED → Đã chốt (neutral) |
| Active | true → Đang hoạt động (success); false → Ngưng (neutral) |
