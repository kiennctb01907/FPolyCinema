# Đặc tả FPolyCinema – mục 10: danh mục use case

Mục lục và cách đọc mã yêu cầu như [S3.6-04 P4]: docs/README.md. Tên chuẩn: docs/CONTRACTS.md. Giao diện: docs/ui.

## 10. Danh mục use case (dùng chung cho code và tài liệu lab)
<!-- spec-check: không gắn mã -->

Đây là danh mục chuẩn. Sơ đồ use case, bảng đặc tả của Lab 1, sơ đồ luồng giao diện của Lab 2 và test case của Lab 7 dùng đúng mã và tên ở đây. Controller và view phải làm đủ các use case này.

Tác nhân và kế thừa:
- Khách vãng lai: chỉ xem.
- Khách hàng (Customer): kế thừa Khách vãng lai.
- Thu ngân (POSCashier), Nhân viên soát vé (TicketInspector).
- Quản lý ca (ShiftManager): kế thừa Thu ngân và Nhân viên soát vé.
- Quản lý rạp (Manager): kế thừa Quản lý ca.
- Tác nhân ngoài: Cổng thanh toán (mô phỏng), Dịch vụ email.

Quan hệ chung:
- Mọi use case của nhân viên, cùng UC09–UC13 của khách hàng, đều include UC01 Đăng nhập.
- UC02 extend UC01.

| Mã | Use case | Tác nhân chính | Quan hệ | SPEC | Màn hình | Phase |
|---|---|---|---|---|---|---|
| UC01 | Đăng nhập, đăng xuất | Khách hàng, mọi nhân viên | | 2.5 | C6 | 2 |
| UC02 | Quên mật khẩu (OTP, cấp mật khẩu mới qua email) | Khách hàng, mọi nhân viên; Dịch vụ email | extend UC01 | 2.5 | C6 | 2 |
| UC03 | Đổi mật khẩu | Khách hàng, mọi nhân viên | include UC01 | 2.5 | C5, A9 | 2, 5 |
| UC04 | Cập nhật thông tin cá nhân | Khách hàng, mọi nhân viên | include UC01 | 2.5 | C5, A9 | 2, 5 |
| UC05 | Đăng ký tài khoản | Khách vãng lai | | 2.5 | C6 | 2 |
| UC06 | Xem phim, lịch chiếu, thực đơn bắp nước | Khách vãng lai | | 3.1, 3.3 | C1, C2 | 5 |
| UC07 | Đặt vé online | Khách hàng | include UC08, UC09 | 3.5, 3.7 | C3 | 5 |
| UC08 | Chọn ghế với gợi ý AI | Khách hàng, Thu ngân | | 3.5, 4.1 | C3, P1 | 4, 5 |
| UC09 | Thanh toán online | Khách hàng; Cổng thanh toán | | 3.7 | C3 | 5 |
| UC10 | Xem vé điện tử, báo "Tôi đã tới rạp" | Khách hàng | | 3.8, 3.11 | C4 | 5, 6 |
| UC11 | Tự hủy vé | Khách hàng | | 3.9 | C4, C5 | 5 |
| UC12 | Quản lý ví, điểm, voucher, thông báo | Khách hàng | | 3.13 | C5 | 5 |
| UC13 | Đặt vé bằng trợ lý AI | Khách hàng | extend UC07 | 4.2 | C7 | 7 |
| UC14 | Mở ca, đóng ca mù | Thu ngân | | 3.6 | P7 | 4 |
| UC15 | Bán vé và bắp nước tại quầy | Thu ngân | include UC08 | 3.6, 3.1 | P1, P2 | 4 |
| UC16 | In vé, nhận đơn online tại quầy | Thu ngân | | 3.6, 3.11 | P1 | 4, 6 |
| UC17 | Đổi, nâng hạng ghế | Thu ngân | | 3.10 | P1 | 6 |
| UC18 | Yêu cầu hủy hóa đơn đã thanh toán | Thu ngân | include UC24 | 3.6 | P1 | 4, 6 |
| UC19 | Xử lý đơn bắp nước tại bếp, gọi số | Thu ngân | | 3.11 | P4, P5 | 6 |
| UC20 | Lập phiếu hủy hàng hỏng | Thu ngân | | 3.11 | P4 | 6 |
| UC21 | Xem hóa đơn của tôi | Thu ngân | | 3.6 | P1 | 4 |
| UC22 | Soát vé, check-in từng phần | Nhân viên soát vé | | 3.8 | P3 | 6 |
| UC23 | Xác nhận dọn phòng xong | Nhân viên soát vé | | 3.3, 3.8 | P3, P6 | 6 |
| UC24 | Duyệt hủy hóa đơn (PIN hoặc từ xa) | Quản lý ca | | 3.6 | P1, A7 | 4, 6 |
| UC25 | Giải phóng ghế nội bộ | Quản lý ca | | 3.2 | A7 | 6 |
| UC26 | Duyệt phiếu hủy hàng | Quản lý ca | | 3.11 | A7 | 6 |
| UC27 | Hủy suất do sự cố | Quản lý ca | | 3.10 | A5 | 6 |
| UC28 | Quản lý loại đồ uống | Quản lý rạp | | 3.14 | A2, A3 | 2 |
| UC29 | Quản lý đồ uống và định mức | Quản lý rạp | | 3.11, 3.14 | A2, A3 | 2 |
| UC30 | Quản lý nguyên liệu, nhập kho | Quản lý rạp | | 3.11 | A2, A3 | 6 |
| UC31 | Quản lý phim, nhà phát hành, KDM, chia doanh thu | Quản lý rạp | | 3.1, 3.3 | A2, A3 | 3 |
| UC32 | Quản lý phòng chiếu và sơ đồ ghế | Quản lý rạp | | 3.2 | A4 | 3 |
| UC33 | Quản lý bảng giá, đối tượng ưu đãi, ngày lễ | Quản lý rạp | | 3.4, 3.1 | A2, A3 | 3 |
| UC34 | Quản lý suất chiếu | Quản lý rạp | | 3.3 | A5 | 3 |
| UC35 | Xếp lịch tự động bằng AI | Quản lý rạp | extend UC34 | 4.3 | A5 | 7 |
| UC36 | Quản lý nhân viên | Quản lý rạp | | 3.14 | A2, A3 | 2 |
| UC37 | Quản lý hóa đơn | Quản lý rạp | | 3.6 | A8 | 4 |
| UC38 | Thống kê, báo cáo | Quản lý rạp | | 3.12, 3.1 | A1, A6 | 4 |
| UC39 | Đối soát nhà phát hành | Quản lý rạp | | 3.12 | A6 | 4 |
| UC40 | Bán vé doanh nghiệp | Quản lý rạp | | 3.10 | A2, A3 | 6 |
