# Đặc tả FPolyCinema – mục 8 và 9: lộ trình 8 phase, ngoài phạm vi

Mục lục và cách đọc mã yêu cầu như [S3.6-04 P4]: docs/README.md. Tên chuẩn: docs/CONTRACTS.md. Giao diện: docs/ui.

## 8. Lộ trình

Mỗi phase làm trong một phiên, đúng thứ tự; xong phase thì dừng và báo cáo. Git tag để nộp lab: lab-3 sau Phase 1; lab-4 và lab-5 sau Phase 2; lab-6 sau Phase 4; lab-8 sau Phase 8. Mức ưu tiên: Phase 1–5 và Phase 8 là bắt buộc; Phase 6 nên có; Phase 7 làm khi còn thời gian. Nếu thiếu thời gian, cắt Phase 7 trước, sau đó đến phần doanh nghiệp ở 3.10.

Danh sách yêu cầu chính xác của từng phase do lệnh `node tools/spec-check/check.mjs begin N` in ra từ các mã gắn trong docs/spec, docs/ui và docs/CONTRACTS.md, kèm các use case ở mục 10 có cột Phase trùng với phase đó. Bảng dưới chỉ để tham khảo nhanh.

| Phase | Nội dung | Mục cần đọc | Bài lab |
|---|---|---|---|
| 1 | Nền tảng dữ liệu | SPEC 2, 3.14, 6, 7; CONTRACTS; UI 2, 7 (phần Ảnh mẫu) | Lab 3 |
| 2 | Giao diện nền, đăng nhập, CRUD cơ bản | SPEC 2.5, 2.6, 3.14, 5; UI 1–6, 7 (C6, A2, A3, A9), 9–13 | Lab 4, 5 |
| 3 | Phim, phòng chiếu, giá, suất chiếu | SPEC 3.1–3.4, 4.1; UI 7 (A2–A5), 8 | Lab 5 mở rộng |
| 4 | POS, hóa đơn, thống kê | SPEC 3.5, 3.6, 3.11, 3.12, 3.13; UI 7 (P1, P7, A1, A6, A8), 8 | Lab 6, Lab 7 |
| 5 | Web khách và đặt vé online | SPEC 3.1, 3.5, 3.7, 3.9, 3.13, 4.1, 5; UI 7 (C1–C6), 8 | Mở rộng |
| 6 | Vận hành thời gian thực | SPEC 3.8, 3.10, 3.11, 5; UI 7 (P2–P6, A7) | Mở rộng |
| 7 | AI xếp lịch và trợ lý đặt vé | SPEC 4.2, 4.3; UI 7 (A5, C7) | Mở rộng |
| 8 | Hoàn thiện, demo, kiểm tra tổng thể | SPEC 5, 7, 10; UI 13 | Lab 7, Lab 8 |

### 8.1 Phase 1 – Nền tảng dữ liệu
1. Kiểm tra môi trường:
   - [S8.1-01 P1] `dotnet --list-sdks` có .NET 8.
   - [S8.1-02 P1] SQL Server đang chạy (Get-Service MSSQL* hoặc sqlcmd -L); chọn đúng instance và ghi vào appsettings.Development.json.
   - [S8.1-03 P1] Có Git, có Node.js (dùng cho bước kiểm tra giao diện).
   - [S8.1-04 P1] Thiếu .NET 8 hoặc SQL Server thì dừng và báo người dùng.
2. Khởi tạo dự án:
   - [S8.1-05 P1] git init và .gitignore (bin, obj, artifacts, wwwroot/uploads trừ thư mục seed, appsettings.*.local.json, .claude/state, .claude/settings.local.json).
   - [S8.1-06 P1] Tạo solution, hai project, cấu trúc thư mục 2.2; launchSettings chạy ở http://localhost:5080.
   - [S8.1-07 P1] Tạo PROGRESS.md theo mẫu cuối mục này, docs/TEST-SCENARIOS.md và docs/BUG-LOG.md (cấu trúc ở mục 7).
3. [S8.1-08 P1] Domain/Constants theo CONTRACTS mục 3–4. Database/01_schema.sql gồm mọi bảng ở mục 6 với khóa ngoại, UNIQUE, CHECK, index. Database/02_procedures.sql gồm 4 stored procedure. Viết DbInitializer và IClock.
4. [S8.1-09 P1] Utils.DBUtil (2.3), Utils.FileUtil (2.6), toàn bộ Entity và DAL.
5. [S8.1-10 P1] Domain/Rules: BillStateMachine và PricingCalculator, kèm unit test.
6. DbSeeder, nạp dữ liệu mẫu đủ dùng cho mọi phase sau:
   - [S8.1-11 P1] 2 rạp. Mỗi rạp 3 phòng: phòng lớn IMAX 12 × 18 có hàng SWEETBOX cuối, phòng 3D 10 × 14, phòng 2D 8 × 12. Có lối đi, VIP ở các hàng giữa, 2 ghế xe lăn, 4 ghế nội bộ, 1 ghế bảo trì; BaseViewScore tính sẵn.
   - 3 nhà phát hành và 10 phim hư cấu (không trùng tên phim thật):
     - [S8.1-12 P1] 3 phim Việt; đủ các nhãn P, K, T13, T16, T18, và 1 phim C chỉ lưu hồ sơ.
     - [S8.1-13 P1] 2 phim hoạt hình có bản lồng tiếng; 1 phim có tên dài khoảng 40 ký tự để thử bố cục.
     - [S8.1-14 P1] Mỗi phim có số giấy phép, cảnh báo nội dung, quy tắc chia doanh thu 3 tuần, giá sàn và KDM. Có 1 phim với KDM hết hạn trong 24 giờ để thử cảnh báo.
   - [S8.1-15 P1] Poster và ảnh món là ảnh thật (Unsplash License) lưu sẵn trong repo ở wwwroot/uploads/seed/photos/ (nguồn ghi ở CREDITS.md); SvgArtGenerator vẫn sinh bản SVG dự phòng ở wwwroot/uploads/seed/.
   - [S8.1-16 P1] Bắp nước: 4 danh mục, khoảng 14 món và 4 combo; khoảng 12 nguyên liệu và bao bì với định mức đầy đủ, trong đó 1 nguyên liệu gần ngưỡng tối thiểu.
   - [S8.1-17 P1] Bảng giá đủ 6 chiều cho mỗi rạp, AudienceDiscounts theo CONTRACTS mục 4, các ngày lễ năm 2026.
   - [S8.1-18 P1] Suất chiếu từ hôm nay đến 6 ngày sau, hợp lệ mọi ràng buộc; một số suất đã có vé (bị khóa).
   - [S8.1-19 P1] Hóa đơn đã thanh toán của 30 ngày qua, cả tại quầy lẫn online, có vé và bắp nước, cuối tuần và buổi tối đông hơn, để biểu đồ, báo cáo, đối soát có số liệu. Thêm vài hóa đơn hủy với các lý do khác nhau.
   - [S8.1-20 P1] Tài khoản theo CONTRACTS mục 11; một ca đang mở của thu ngân tại POS-01; 2 voucher công khai, 1 voucher bồi thường của khách mẫu; khách mẫu có vé sắp chiếu.
7. [S8.1-21 P1] Kiểm tra: build, test, chạy với --reset-db, gọi thử cả 4 stored procedure, ghi kết quả vào PROGRESS.md.

### 8.2 Phase 2 – Giao diện nền, đăng nhập, CRUD cơ bản
1. [S8.2-01 P2] Hệ thống thiết kế theo UI mục 2–6: tokens.css, app.css, 4 layout, các partial dùng chung, ui.js, StatusDisplay và trang /dev/style-guide. Chụp và kiểm tra trang style guide trước khi làm màn hình khác.
2. [S8.2-02 P2] Program.cs: xác thực, policy, Anti-Forgery toàn cục, xử lý lỗi, UseForwardedHeaders. LoginController đủ chức năng ở 2.5; trang AccessDenied; trang /dev/mailbox.
3. [S8.2-03 P2] FileController; CategoriesController; DrinksController (upload ảnh bằng fetch, nhập định mức nguyên liệu, lọc theo loại và tìm theo tên như Lab 5 bài 3); UsersController (SetActive, quy tắc xóa). Quên mật khẩu bằng OTP và IEmailSender (2.5). Trang cá nhân nhân viên /me (UI A9): cập nhật thông tin, đổi mật khẩu, đổi mật khẩu bắt buộc khi MustChangePassword = 1. Tất cả theo mẫu danh sách và form ở UI mục 7 (A2, A3). Nhân viên xem được danh sách và chi tiết; chỉ Manager được thêm, sửa, xóa.
4. [S8.2-04 P2] Thanh đổi vai trò và /demo/switch-role (mục 5).
5. [S8.2-05 P2] Script tools/ui-snapshots (UI mục 13).

### 8.3 Phase 3 – Phim, phòng chiếu, giá, suất chiếu
1. [S8.3-01 P3] MoviesController (giấy phép, nhãn tuổi, cảnh báo nội dung, ngôn ngữ, poster qua FileController), DistributorsController, quy tắc chia doanh thu, KDM.
2. [S8.3-02 P3] AuditoriumsController với công cụ thiết kế sơ đồ, BaseViewScore, ghế nội bộ, ghế bảo trì.
3. [S8.3-03 P3] Bảng giá 6 chiều, AudienceDiscounts (chặn mức dưới tối thiểu), Holidays, kèm trang thử tính giá.
4. [S8.3-04 P3] Quản lý suất chiếu: chế độ bảng theo ngày và form (UI A5, chế độ bảng), dùng ScheduleConstraintChecker và AgeRules; tạo ShowtimeSeats.

### 8.4 Phase 4 – POS, hóa đơn, thống kê (yêu cầu cốt lõi của bài 6)
1. Màn hình POS:
   - [S8.4-01 P4] Mở ca; chọn suất.
   - [S8.4-02 P4] Sơ đồ ghế dùng SeatHoldService, OrphanSeatValidator, luật cặp SWEETBOX; CinemaHub và kênh 1.
   - [S8.4-03 P4] Chọn đối tượng vé cho từng ghế, áp các quy tắc ở 3.1.
   - [S8.4-04 P4] Bắp nước, trừ kho khi bán và chặn khi không đủ (InventoryService phần cơ bản).
   - [S8.4-05 P4] Voucher, điểm, tra cứu thành viên.
   - [S8.4-06 P4] Thanh toán CASH và VIETQR_SIM; tạo vé và QR; in vé.
   - [S8.4-07 P4] Hủy hóa đơn waiting; trang hóa đơn của tôi.
2. [S8.4-08 P4] Hủy hóa đơn đã thanh toán bằng PIN (cách duyệt từ xa làm ở Phase 6). Đóng ca mù.
3. [S8.4-09 P4] SeatHoldSweeper.
4. [S8.4-10 P4] BillsController quản lý hóa đơn (3.6, UI A8). StatisticsController với 4 stored procedure, có biểu đồ Chart.js cho top 5 đồ uống và doanh thu theo khoảng thời gian (Lab 6); dashboard quản lý (UI A1); báo cáo tỷ lệ phim Việt; đối soát nhà phát hành; xuất CSV.

### 8.5 Phase 5 – Web khách và đặt vé online
1. [S8.5-01 P5] Trang chủ, danh sách và chi tiết phim, lịch chiếu lọc theo rạp, ngày, định dạng, ngôn ngữ; thực đơn bắp nước.
2. Luồng đặt vé 4 bước:
   - [S8.5-02 P5] Chọn suất.
   - [S8.5-03 P5] Chọn ghế: SeatScoring, cụm ghế gợi ý, heatmap, gợi ý chuyển suất, cập nhật thời gian thực qua kênh 1.
   - [S8.5-04 P5] Bắp nước và ưu đãi; giữ kho khi thanh toán (RESERVE).
   - [S8.5-05 P5] Thanh toán qua trang mô phỏng và webhook (kênh 3), xử lý webhook đến muộn.
3. [S8.5-06 P5] Vé điện tử; tài khoản (vé của tôi, ví và điểm, voucher, thông báo, thông tin cá nhân, mã thành viên); tự hủy vé.

### 8.6 Phase 6 – Vận hành thời gian thực
1. [S8.6-01 P6] ShowtimeClockService, máy soát vé (kênh 4), bảng sảnh (kênh 6), nút "Đã dọn xong".
2. [S8.6-02 P6] Bếp, bảng gọi số, nút "Tôi đã tới rạp", kho hai tầng, báo hết món, nhập kho, hủy hàng hỏng (kênh 5, 7).
3. [S8.6-03 P6] Màn hình phụ của quầy (kênh 2); duyệt hủy từ xa (kênh 7); giải phóng ghế nội bộ; đổi và nâng hạng ghế; hủy suất do sự cố; doanh nghiệp.

### 8.7 Phase 7 – AI xếp lịch và trợ lý đặt vé
1. [S8.7-01 P7] AiDemandScore, thuật toán xếp lịch, Gantt có kéo thả và kiểm tra tức thì.
2. [S8.7-02 P7] BookingAgentService (LocalAgent, LlmAgent), NlpParser với bộ test 20 câu, widget trợ lý trên web khách.

### 8.8 Phase 8 – Hoàn thiện
1. [S8.8-01 P8] Trung tâm demo nhiều màn hình (mục 5).
2. [S8.8-02 P8] Kiểm tra giao diện mọi màn hình (C, P, A, D) của cả 5 vai trò theo UI mục 13 và sửa hết lỗi.
3. [S8.8-03 P8] Chạy lại toàn bộ TEST-SCENARIOS.md trên DB mới, cập nhật kết quả.
4. [S8.8-04 P8] README.md gồm: yêu cầu cài đặt, cấu hình chuỗi kết nối, cách chạy, tài khoản mẫu, hướng dẫn publish và chạy sau Nginx trên Linux (dotnet publish, systemd, cấu hình Nginx, chuỗi kết nối qua biến môi trường).

Mẫu PROGRESS.md:
```
# Tiến độ
## Trạng thái
- [ ] Phase 1  - [ ] Phase 2  - [ ] Phase 3  - [ ] Phase 4
- [ ] Phase 5  - [ ] Phase 6  - [ ] Phase 7  - [ ] Phase 8
## Phase đang làm
## Quyết định đã chốt (ngày – nội dung – lý do)
## Thay đổi mô hình (ngày – bảng/cột/use case/màn hình – trước – sau)
## Lỗi biết trước và việc còn lại
## Đang bị chặn (câu hỏi cần người dùng trả lời)
## Kết quả kiểm tra giao diện (phase – màn hình – kích thước – đạt/chưa)
```

## 9. Ngoài phạm vi
<!-- spec-check: không gắn mã -->
- Cổng thanh toán thật.
- Kết nối hóa đơn điện tử với cơ quan thuế: chỉ in hóa đơn có mã và đủ thông tin.
- Gửi SMS. Email chỉ gửi thật khi đã cấu hình SMTP.
- Giải mã KDM và điều khiển máy chiếu thật.
- Ứng dụng di động.
- Đa ngôn ngữ: giao diện chỉ có tiếng Việt.
