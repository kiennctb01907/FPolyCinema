# Đặc tả FPolyCinema – mục 1 và 2: mục tiêu, kiến trúc, công nghệ

Mục lục và cách đọc mã yêu cầu như [S3.6-04 P4]: docs/README.md. Tên chuẩn: docs/CONTRACTS.md. Giao diện: docs/ui.

## 1. Mục tiêu và nguyên tắc
<!-- spec-check: không gắn mã -->

FPolyCinema là website quản lý chuỗi rạp. Khách xem phim và đặt vé online; thu ngân bán vé, bắp nước tại quầy; nhân viên soát vé và điều phối phòng; quản lý ca xử lý ngoại lệ; quản lý rạp cấu hình, xếp lịch, xem báo cáo. Đây là đồ án môn Dự án .NET 1, nên phải giữ đúng yêu cầu bắt buộc của môn: ADO.NET với DBUtil và DAL, Cookie Authentication với hai policy EmployeeOrManager và ManagerOnly, CRUD có upload ảnh, POS với hóa đơn 3 trạng thái, thống kê bằng stored procedure. Phần rạp phim là mở rộng trên nền đó.

Nguyên tắc:
1. Đúng yêu cầu môn trước, mở rộng sau. Lộ trình ở mục 8 sắp theo thứ tự này.
2. Mọi quy tắc nghiệp vụ được kiểm tra ở server. Giao diện có kiểm tra trước để phản hồi nhanh, nhưng không thay cho server.
3. Tên trạng thái, sự kiện, cấu hình lấy từ CONTRACTS.md, không tự đặt.
4. Chạy được ngay trên máy có SQL Server: tự tạo DB, bảng, stored procedure, dữ liệu mẫu. Không cần dịch vụ ngoài; thanh toán, email, AI đều có bản mô phỏng hoặc chạy cục bộ.
5. Phần nào báo xong thì phần đó chạy thật: không nút chết, không dữ liệu giả cứng trên giao diện, không hàm rỗng.

## 2. Kiến trúc và công nghệ

### 2.1 Công nghệ (đã chốt)
- [S2.1-01 P1] .NET 8, ASP.NET Core MVC, Razor View. Không dùng Entity Framework.
- [S2.1-02 P1] SQL Server 2019 trở lên (bản Express được), Microsoft.Data.SqlClient 5.2.x.
- [S2.1-03 P4] SignalR có sẵn trong ASP.NET Core; client @microsoft/signalr 8.0.x.
- [S2.1-04 P1] Microsoft.Extensions.Identity.Core 8.0.x, chỉ dùng PasswordHasher để băm mật khẩu và PIN.
- [S2.1-05 P4] QRCoder 1.6.x sinh mã QR dạng SVG ở server.
- [S2.1-06 P2] Giao diện: Bootstrap 5.3.3, Bootstrap Icons 1.11.3, Chart.js 4.4.x, html5-qrcode 2.3.8, font Be Vietnam Pro tự host (woff2 từ Google Fonts hoặc gói @fontsource/be-vietnam-pro). Không dùng Tailwind, không dùng FontAwesome. jQuery và jquery-validation-unobtrusive có sẵn trong template chỉ dùng để validate form; JS còn lại viết ES6 thuần. Sơ đồ ghế, Gantt, bàn phím PIN tự viết bằng CSS Grid và Pointer Events.
- [S2.1-07 P2] Thư viện front-end đặt trong wwwroot/lib (tải một lần bằng libman hoặc tải tay), khi chạy không phụ thuộc CDN.
- [S2.1-08 P1] Kiểm thử: xUnit.

### 2.2 Cấu trúc thư mục
```
FPolyCinema.sln
src/FPolyCinema.Web/
  Controllers/  Views/  ViewModels/
  Entities/  DAL/  Utils/              DBUtil, FileUtil như slide
  Services/                            nghiệp vụ nhiều bước, chạy trong transaction
  Domain/Constants/  Domain/Rules/     logic thuần, không truy cập DB, có unit test
  Hubs/  Background/  Infrastructure/  IClock, DbInitializer, DbSeeder, SvgArtGenerator
  Database/01_schema.sql  Database/02_procedures.sql
  wwwroot/css/tokens.css, app.css   wwwroot/js/ui.js, realtime.js và file JS theo trang
tests/FPolyCinema.Tests/
tools/ui-snapshots/                    script chụp màn hình kiểm tra giao diện
tools/spec-check/                      script kiểm tra spec (có sẵn trong bộ prompt)
docs/README.md  spec/  ui/  CONTRACTS.md  trace/  audit/  generated/  TEST-SCENARIOS.md  BUG-LOG.md
.claude/settings.json  .claude/skills/  .claude/agents/   (có sẵn trong bộ prompt)
CLAUDE.md  LAB-PROMPT.md  PROGRESS.md  README.md
```

Đây là mô hình 3 lớp mà Lab 3 yêu cầu:
- [S2.2-01 P1] Lớp dữ liệu: Entities, DAL, Utils/DBUtil.
- [S2.2-02 P1] Lớp nghiệp vụ: Services, Domain.
- [S2.2-03 P1] Lớp giao diện: Controllers, Views, ViewModels, wwwroot.
- [S2.2-04 P1] Lớp trên chỉ gọi lớp ngay dưới nó; Controller không gọi thẳng DBUtil.
- [S2.2-05 P1] Tên solution, tên project và cấu trúc thư mục đúng như khối trên.

### 2.3 DBUtil và DAL
- Utils.DBUtil là lớp tĩnh, nhận chuỗi kết nối lúc khởi động. Có đúng các hàm của slide, thêm hỗ trợ transaction:
  - [S2.3-01 P1] `DataTable QueryDataTable(string sql, List<object>? parameters = null)`, kèm bí danh `queryDataTable`.
  - [S2.3-02 P1] `object? ExecuteScalar(string sql, List<object>? parameters = null)`, kèm bí danh `executeScalar`. Dùng cho SELECT một giá trị, INSERT kèm `SELECT SCOPE_IDENTITY()`, UPDATE hoặc DELETE kèm `SELECT @@ROWCOUNT`.
  - [S2.3-03 P1] `int ExecuteNonQuery(string sql, List<object>? parameters = null)`.
  - [S2.3-04 P1] Mỗi hàm có bản nạp chồng nhận thêm `(SqlConnection conn, SqlTransaction tx)`.
  - [S2.3-05 P1] `T InTransaction<T>(Func<SqlConnection, SqlTransaction, T> work)`: mở kết nối, bắt đầu transaction, commit; nếu có lỗi thì rollback và ném lại.
  - [S2.3-06 P1] Tham số @1, @2, … theo thứ tự trong danh sách. null chuyển thành DBNull, DateTime dùng kiểu DateTime2, long dùng BigInt. Có hàm `DBUtil.InParams(IEnumerable<object> values, List<object> parameters)` thêm các giá trị vào danh sách tham số và trả chuỗi "@k, @k+1, …" để ghép vào mệnh đề IN (...); đây là chỗ duy nhất được ghép chuỗi vào SQL.
- [S2.3-07 P1] Mỗi Entity có một DAL với FromDataRow, GetAll, GetById, Insert, Update, Delete. Các hàm ghi nhận conn và tx tùy chọn để service gom nhiều bước vào một transaction.
- [S2.3-08 P1] Các lớp cốt lõi giữ đúng tên slide: Category/CategoryDAL, Drink/DrinkDAL, User/UserDAL (có Authenticate(email, password) và SetActive(id, active)), Bill/BillDAL, BillDetail/BillDetailDAL, StatisticDAL.
- [S2.3-09 P4] Thao tác nhiều bước (giữ ghế, thanh toán, đổi ghế, hủy, kho) nằm trong Services và chạy trong DBUtil.InTransaction.

### 2.4 Khởi tạo cơ sở dữ liệu
- [S2.4-01 P1] DbInitializer chạy khi khởi động: kết nối master, tạo DB nếu chưa có, chạy các file Database/*.sql theo thứ tự tên. Tách batch theo dòng chỉ chứa chữ GO, vì SqlClient không hiểu GO. Ghi phiên bản vào bảng SchemaVersions. Stored procedure viết bằng CREATE OR ALTER.
- [S2.4-02 P1] DbSeeder nạp dữ liệu mẫu khi bảng Users còn rỗng (mục 8, Phase 1).
- [S2.4-03 P1] Tham số dòng lệnh `--reset-db` (chỉ trong Development): xóa DB, tạo lại và seed lại.
- [S2.4-04 P1] Tham số `--init-only`: chạy khởi tạo DB (và seed nếu DB mới tạo) rồi thoát với mã 0 khi thành công, mã khác 0 khi lỗi. Lệnh `node tools/spec-check/check.mjs test --reset-db` dùng `--reset-db --init-only` để kiểm tra tự động.

### 2.5 Xác thực và phân quyền (theo slide bài 4)
- [S2.5-01 P2] Cookie Authentication: ExpireTimeSpan 24 giờ, LoginPath /Login/Login, AccessDeniedPath /Home/AccessDenied. UseAuthentication đặt trước UseAuthorization. Policy theo CONTRACTS mục 2.
- [S2.5-02 P2] Claims: NameIdentifier, Name, Email, Role, CinemaId (với nhân viên).
- [S2.5-03 P2] UserDAL.Authenticate(email, password) lấy người dùng theo email và Active = 1, rồi kiểm tra mật khẩu bằng PasswordHasher. Slide so sánh mật khẩu trực tiếp trong SQL; ở đây băm để an toàn nhưng giữ nguyên tên hàm.
- [S2.5-04 P2] LoginController có: Login, Logout, Register (cho khách), ForgotPassword, ChangePassword, Profile. Đăng nhập xong chuyển về trang chính của vai trò.
- Quên mật khẩu (Lab 5 bài 2, mở rộng thêm OTP theo Lab 5 bài 4):
  1. [S2.5-05 P2] Người dùng nhập email.
  2. [S2.5-06 P2] Hệ thống gửi mã OTP 6 số qua email; mã có hạn 5 phút và cho nhập sai tối đa 5 lần (OTP_INVALID).
  3. [S2.5-07 P2] OTP đúng thì hệ thống đặt lại mật khẩu bằng một chuỗi ngẫu nhiên, lưu dạng băm vào CSDL, rồi gửi mật khẩu mới qua email.
  4. [S2.5-08 P2] Users.MustChangePassword = 1, nên lần đăng nhập kế tiếp bắt buộc phải đổi mật khẩu.
- Gửi email qua IEmailSender:
  - [S2.5-09 P2] Có cấu hình Email:SmtpHost thì gửi bằng SMTP thật.
  - [S2.5-10 P2] Không có cấu hình (mặc định khi chạy Development) thì ghi thư vào trang /dev/mailbox để xem.
- [S2.5-11 P2] Bộ lọc toàn cục AutoValidateAntiforgeryToken. AJAX gửi token qua header RequestVerificationToken.
- [S2.5-12 P4] PIN quản lý (ShiftManager, Manager): 6 chữ số, lưu dạng băm. Sai ManagerPinMaxAttempts lần thì khóa ManagerPinLockMinutes phút.

### 2.6 Upload ảnh (theo slide bài 4–5)
- [S2.6-01 P1] Utils.FileUtil là lớp tĩnh.
  - [S2.6-02 P1] Upload(IFormFile file, string webRootPath) chỉ nhận .jpg, .jpeg, .png, .gif, .webp (kiểm tra cả phần mở rộng lẫn chữ ký đầu file), tối đa 5MB, lưu thành wwwroot/uploads/{timestamp}_{shortGuid}{ext}.
  - [S2.6-03 P1] Delete(string fileName, string webRootPath) chỉ xóa file nằm trong thư mục uploads.
- [S2.6-04 P2] FileController có [HttpPost] Upload và Delete, gắn [ValidateAntiForgeryToken] và [Authorize(Policy = "ManagerOnly")].
- [S2.6-05 P2] Form Đồ uống gọi Upload và Delete của FileController bằng fetch.
- [S2.6-06 P3] Form Phim gọi Upload và Delete của FileController bằng fetch.

### 2.7 Thời gian, bảo mật, vận hành
- [S2.7-01 P1] IClock.Now trả giờ Việt Nam theo TimeZoneId. Mọi chỗ cần "bây giờ" đều dùng IClock; unit test dùng đồng hồ giả.
- [S2.7-02 P2] Mọi controller có [Authorize] hoặc [AllowAnonymous] rõ ràng. Không đưa bí mật vào mã nguồn.
- [S2.7-03 P2] Xử lý lỗi tập trung: request thường trả trang lỗi thân thiện, AJAX trả JSON theo CONTRACTS mục 8. Lỗi nghiệp vụ ném BusinessRuleException(code, message, details).
- AuditLogs ghi lại các việc sau, mỗi việc làm cùng phase với chức năng của nó:
  - [S2.7-04 P2] Khóa hoặc mở tài khoản.
  - [S2.7-05 P3] Sửa bảng giá.
  - [S2.7-06 P4] Hủy hóa đơn đã thanh toán; bỏ qua luật ghế mồ côi.
  - [S2.7-07 P6] Đổi ghế; giải phóng ghế nội bộ; hủy suất; duyệt hủy hàng.
- Sẵn sàng deploy lên Linux sau Nginx (bài 8):
  - [S2.7-08 P2] UseForwardedHeaders đặt đầu pipeline.
  - [S2.7-09 P1] Không dùng API chỉ có trên Windows: ảnh mẫu và QR sinh dạng SVG, không dùng System.Drawing.
  - [S2.7-10 P1] Đường dẫn ghép bằng Path.Combine, tên file đúng chữ hoa chữ thường.
  - [S2.7-11 P1] Mọi cấu hình đọc được từ biến môi trường.
