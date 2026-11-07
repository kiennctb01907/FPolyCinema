# Giao diện FPolyCinema – mục 8 đến 13: sơ đồ ghế, chữ, tiếp cận, kích thước, điều cấm, kiểm tra giao diện

Mục lục và cách đọc mã yêu cầu như [S3.6-04 P4]: docs/README.md. Tên chuẩn: docs/CONTRACTS.md. Giao diện: docs/ui.

## 8. Sơ đồ ghế (_SeatMap)
Dùng chung cho web khách, POS, màn hình phụ và thẻ của trợ lý.
- Kích thước:
  - [U8-01 P4] Mỗi ghế là một <button>, dùng được bằng bàn phím và có aria.
  - [U8-02 P4] Desktop 32px, tablet 28px. Không bao giờ nhỏ hơn 24px.
  - [U8-03 P5] Điện thoại tối thiểu 28px, đặt trong vùng cuộn ngang, có nút phóng to và thu nhỏ 3 mức.
  - [U8-04 P4] Ô trống (lối đi) chừa khoảng rộng bằng một ghế. Nhãn hàng (A, B, …) hiện ở cả hai đầu mỗi hàng.
- [U8-05 P4] Màn hình chiếu: đường cong SVG ở phía trên, có quầng sáng trắng xanh nhạt, chữ "Màn hình" màu muted.
- [U8-06 P4] Hiển thị theo trạng thái (dùng cả màu lẫn ký hiệu, không chỉ dựa vào màu):

| Trạng thái | Cách hiển thị |
|---|---|
| Trống, ghế thường | Nền --seat-bg, viền --seat-border, chữ số ghế |
| Trống, VIP | Viền 2px --seat-vip, chấm vàng ở góc |
| Trống, sweetbox | Hai ô dính liền thành một khối bo tròn, viền --seat-sweetbox |
| Trống, xe lăn | Viền --seat-wheelchair, icon xe lăn |
| Đang chọn (của mình) | Nền --seat-selected, chữ trắng, dấu ✓ |
| Người khác đang giữ | Nền --seat-held có vạch chéo, icon đồng hồ nhỏ, không bấm được |
| Đã bán | Nền --seat-booked, ký hiệu ✕ màu --seat-booked-text |
| Ghế nội bộ | Viền nét đứt --seat-house, icon khóa |
| Bảo trì | Nền --seat-maint có gạch chéo, icon cờ lê |

- [U8-07 P4] Ghế đôi: hai nút dính nhau; hover và chọn luôn áp cho cả cặp.
- [U8-08 P4] Tooltip khi rê chuột hoặc focus, ví dụ: "Ghế VIP G7 · Điểm góc nhìn và âm thanh 96/100 (tọa độ vàng) · 95.000 ₫".
- [U8-09 P5] Trên điện thoại: chạm để chọn, nhấn giữ để xem tooltip.
- [U8-10 P5] Heatmap bật: ghế trống tô theo thang --heat-*; ghế đã bán hoặc đang giữ vẫn hiện đúng trạng thái. Cụm AI đề xuất có viền vàng phát sáng và nhãn "AI đề xuất 96/100".
- [U8-11 P4] Khi vi phạm luật (ORPHAN_SEAT, SWEETBOX_PAIR, GUARDIAN_REQUIRED…): ghế liên quan rung nhẹ 320ms và nháy viền đỏ 2 lần, kèm toast nêu đúng ghế và cách sửa. Không xóa lựa chọn của khách.
- [U8-12 P4] Cập nhật thời gian thực: ghế đổi trạng thái chuyển màu mờ dần trong 200ms. Nếu ghế khách vừa định chọn bị người khác lấy, hiện toast "Ghế G7 vừa có người chọn trước."
- [U8-13 P4] Bàn phím: phím mũi tên để di chuyển giữa các ghế, Enter hoặc Space để chọn.

## 9. Chữ và lời nhắn
- Giọng văn:
  - [U9-01 P2] Với khách: lịch sự, ngắn gọn, xưng "bạn". Với nhân viên: viết thẳng việc cần làm.
  - [U9-02 P2] Không dùng thuật ngữ kỹ thuật. Không hiện mã lỗi; chỉ ghi mã nhỏ ở cuối khi cần liên hệ hỗ trợ.
  - [U9-03 P2] Lời báo lỗi gồm hai ý: chuyện gì xảy ra, và cách sửa.
- [U9-04 P2] Định dạng: tiền "125.000 ₫"; ngày "06/11/2026"; giờ "19:30"; ngày trong tuần "T7, 07/11"; thời lượng "2 giờ 05 phút"; ghế "G7".
- Lời nhắn mẫu theo mã lỗi:
  - [U9-05 P4] ORPHAN_SEAT: "Cách chọn này để trống ghế G8 ở giữa. Bạn chọn thêm G8 hoặc dời sang G6–G7 nhé."
  - [U9-06 P4] SWEETBOX_PAIR: "Ghế đôi M1–M2 được bán theo cặp. Bạn chọn cả hai ghế nhé."
  - [U9-07 P4] SEAT_TAKEN: "Ghế G7 vừa có người chọn trước nên đã được bỏ khỏi lựa chọn của bạn."
  - [U9-08 P4] AGE_RESTRICTED: "Phim T16 dành cho khán giả từ đủ 16 tuổi. Vé trẻ em không áp dụng cho phim này."
  - [U9-09 P4] CHILD_CURFEW: "Suất này kết thúc lúc 22:35. Theo quy định, trẻ em dưới 13 tuổi chỉ xem suất kết thúc trước 22:00."
  - [U9-10 P4] GUARDIAN_REQUIRED: "Phim K: trẻ dưới 13 tuổi cần cha, mẹ hoặc người giám hộ đi cùng. Thêm 1 vé người lớn hoặc xác nhận có người giám hộ."
  - [U9-11 P3] KDM_INVALID: "Bản quyền chiếu (KDM) của phim này tại rạp chỉ có hiệu lực đến 12/11/2026 23:59. Chọn giờ trong khoảng hiệu lực hoặc cập nhật KDM."
  - [U9-12 P3] SHOWTIME_OVERLAP: "Phòng 2 đã có suất 19:00–21:25 (gồm thời gian dọn phòng). Suất mới phải bắt đầu từ 21:25."
  - [U9-13 P3] SHOWTIME_LOCKED: "Suất này đã có khách mua vé nên không đổi giờ được. Nếu cần, dùng chức năng Hủy suất do sự cố."
  - [U9-14 P4] SHIFT_NOT_OPEN: "Bạn cần mở ca trước khi bán hàng."
  - [U9-15 P4] STOCK_INSUFFICIENT: "Combo bắp phô mai lớn tạm hết nguyên liệu."
  - [U9-16 P5] SELF_CANCEL_NOT_ALLOWED: "Vé chỉ tự hủy được trước giờ chiếu 2 tiếng. Suất này bắt đầu lúc 19:30."
  - [U9-17 P5] PAYMENT_EXPIRED: "Đã hết 10 phút giữ ghế. Bạn chọn lại ghế để tiếp tục nhé."
  - [U9-18 P2] OTP_INVALID: "Mã xác nhận chưa đúng. Bạn còn 3 lần thử."
  - [U9-19 P2] OTP_EXPIRED: "Mã xác nhận đã hết hạn. Bấm Gửi lại mã để nhận mã mới."
  - [U9-20 P2] MUST_CHANGE_PASSWORD: "Bạn đang dùng mật khẩu được cấp. Đặt mật khẩu mới để tiếp tục."
  - [U9-21 P2] Mất mạng: "Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại."
  - [U9-22 P2] 403: "Tài khoản của bạn không có quyền dùng chức năng này."
- [U9-23 P2] Trạng thái rỗng mẫu: "Chưa có hóa đơn nào hôm nay"; "Không có suất chiếu phù hợp – thử đổi ngày hoặc bỏ bớt bộ lọc".
- [U9-24 P2] Toast thành công mẫu: "Đã lưu thay đổi"; "Đã giữ 3 ghế trong 10 phút".

## 10. Khả năng tiếp cận
- [U10-01 P2] Độ tương phản: chữ thường ít nhất 4,5:1; chữ lớn và viền thành phần ít nhất 3:1. Các cặp màu trong token đã đạt mức này; không tự tạo cặp màu khác.
- [U10-02 P2] Vòng focus 2px màu --focus, cách 2px, hiện khi dùng bàn phím (:focus-visible).
- [U10-03 P2] Nút chỉ có icon phải có aria-label.
- [U10-04 P4] Mỗi ghế có aria-label đầy đủ, ví dụ "Ghế G7, VIP, còn trống, 96 điểm, 95.000 ₫".
- [U10-05 P2] Không truyền thông tin chỉ bằng màu: trạng thái ghế và kết quả soát vé luôn có icon hoặc chữ đi kèm.
- [U10-06 P2] Vùng chạm tối thiểu 44px (POS 48px, máy soát vé 56px).
- [U10-07 P2] Tôn trọng prefers-reduced-motion: tắt hiệu ứng rung, nhấp nháy và chuyển động trang trí.
- [U10-08 P2] Hộp thoại giữ focus bên trong và đóng được bằng Esc, trừ hộp PIN và hộp xác nhận thao tác nguy hiểm.

## 11. Kích thước màn hình cần kiểm tra

| Khu vực | Kích thước |
|---|---|
| Web khách | 390 × 844, 768 × 1024, 1440 × 900 |
| Quản trị | 1440 × 900, 1280 × 800; vẫn dùng được ở 768 |
| POS | 1366 × 768, 1920 × 1080 |
| Màn hình phụ, bảng sảnh, bảng gọi số | 1920 × 1080 |
| Bếp | 1280 × 800, 1920 × 1080 |
| Máy soát vé | 390 × 844, 768 × 1024 |

- [U11-01 P2] tools/ui-snapshots/routes.json ghi mã màn hình, vai trò, đường dẫn và đúng các kích thước trong bảng trên cho từng khu vực.

## 12. Không được làm
- [U12-01 P2] Dùng bảng hoặc form HTML mặc định thay cho thành phần chung; để tiêu đề scaffold kiểu "Index", "Create", "Edit"; để sót chữ tiếng Anh trên giao diện.
- [U12-02 P2] Dùng thuộc tính style inline (trừ biến CSS động, như vị trí khối trên Gantt); dùng mã màu ngoài tokens.css; dùng cỡ chữ hay khoảng cách ngoài thang token.
- [U12-03 P2] Dùng alert(), confirm(), prompt() của trình duyệt.
- [U12-04 P2] Dùng lorem ipsum, ảnh tải từ internet; để ảnh vỡ hoặc ảnh méo tỉ lệ.
- [U12-05 P2] Hiện mã enum, stack trace hay lỗi 500 thô cho người dùng.
- [U12-06 P2] Trộn nhiều bộ icon hoặc nhiều font.
- [U12-07 P2] Đặt hơn một nút primary trong một vùng; để nút chính của POS hay bước đặt vé phải cuộn mới thấy.
- [U12-08 P2] Để cả trang có thanh cuộn ngang trên điện thoại (chỉ vùng sơ đồ ghế được cuộn ngang).
- [U12-09 P2] Đặt chữ xám nhạt trên nền xám; chữ trắng trên nền vàng hoặc xanh lá sáng.

## 13. Kiểm tra giao diện (bắt buộc cuối mỗi phase có giao diện)
1. [U13-01 P2] Phase 2 dựng /dev/style-guide trước tiên. Mọi màn hình sau chỉ dùng thành phần có ở đó. Cần thành phần mới thì thêm vào style guide trước.
2. Script tools/ui-snapshots/snap.mjs (Node + Playwright):
   - [U13-02 P2] Đọc routes.json: mã màn hình, vai trò, đường dẫn, kích thước theo mục 11.
   - [U13-03 P2] Đăng nhập bằng /demo/switch-role; chờ trang tải xong và font sẵn sàng.
   - [U13-04 P2] Chụp toàn trang vào artifacts/ui/phase-N/{mãMànHình}-{vai-tro}-{rong}x{cao}.png, ví dụ artifacts/ui/phase-4/A8-manager-1440x900.png.
   - [U13-05 P2] Cài một lần: npm i -D playwright, rồi npx playwright install chromium.
3. [U13-06 P2] Claude Code mở từng ảnh, đối chiếu với danh sách bên dưới, sửa rồi chụp lại cho đến khi đạt, và ghi kết quả vào PROGRESS.md. Nếu không cài được Playwright thì ghi vào PROGRESS.md mục "Đang bị chặn" và hỏi người dùng.
4. Danh sách kiểm tra cho mỗi ảnh:
   - [U13-07 P2] Chữ không tràn, không bị cắt dấu, không chồng lên nhau; không có thanh cuộn ngang trên điện thoại.
   - [U13-08 P2] Lề thẳng theo lưới; khoảng cách theo thang token; các thẻ cùng loại cao bằng nhau.
   - [U13-09 P2] Mỗi vùng có đúng một nút chính và nhìn thấy ngay.
   - [U13-10 P2] Tương phản đủ đọc, nhất là chữ muted trên nền tối.
   - [U13-11 P2] Có trạng thái rỗng, lỗi, đang tải; không có ảnh vỡ; không sót chữ tiếng Anh.
   - [U13-12 P2] Tiền, ngày, giờ đúng định dạng Việt Nam; trạng thái là nhãn tiếng Việt.
   - [U13-13 P2] Nhãn độ tuổi xuất hiện ở đủ những nơi liệt kê trong SPEC 3.1 mục 7.
   - [U13-14 P2] Màn hình hiển thị đọc rõ từ xa: chữ chính tối thiểu 28px.
   - [U13-15 P2] Dữ liệu dài hoặc rỗng không làm vỡ bố cục: thử với tên phim khoảng 40 ký tự, giỏ 20 món, danh sách 0 kết quả.

Lệnh `verify N --final` đòi có ít nhất một ảnh cho mỗi mã màn hình có yêu cầu thuộc Phase N.
