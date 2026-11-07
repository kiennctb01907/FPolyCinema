# FPolyCinema – hướng dẫn cho Claude Code

Đồ án môn Dự án .NET 1 (FPT Polytechnic): website quản lý chuỗi rạp chiếu phim. Công nghệ: ASP.NET Core MVC .NET 8, Razor View, ADO.NET, SQL Server, SignalR.

## Tài liệu

Đọc theo phase, không đọc hết một lúc. Mục lục ở docs/README.md.
- docs/spec/*.md: nghiệp vụ, kiến trúc, CSDL, kiểm thử, lộ trình, use case (SPEC mục 1–10).
- docs/ui/*.md: hệ thống thiết kế và từng màn hình (UI mục 1–13). Mọi giao diện làm theo đây.
- docs/CONTRACTS.md: tên vai trò, policy, trạng thái, sự kiện SignalR, cấu hình, mã lỗi. Không tự đặt tên khác; cần tên mới thì thêm vào file này trước.
- docs/trace/phase-N.md: bằng chứng cho từng yêu cầu. docs/audit/phase-N.md: kết quả kiểm tra độc lập. docs/generated/: dữ kiện sinh từ code cho tài liệu lab.
- PROGRESS.md, docs/TEST-SCENARIOS.md, docs/BUG-LOG.md.
- LAB-PROMPT.md: chỉ dùng trong phiên làm bài lab (/lab N).

## Quy trình chống sót (bắt buộc)

- Mỗi yêu cầu trong spec có một mã như [S3.6-04 P4]: S là spec, U là giao diện, C là CONTRACTS; P4 là phase phải xong. Việc của một phase là danh sách mã do script in ra, không phải trí nhớ.
- Làm phase bằng lệnh /phase N (quy trình ở .claude/skills/phase/SKILL.md). Làm tiếp phase dở: /tiep-tuc. Mỗi phiên làm đúng một phase; không làm trước việc của phase sau.
- Vòng lặp: `next N` → đọc nguyên văn ở file:dòng → code và test → `mark MÃ --ev "…"` → `verify N --brief`.
- Chỉ báo xong phase khi `node tools/spec-check/check.mjs verify N --final` in ĐẠT. Hook Stop chặn dừng sớm; hook SessionStart in lại các mã còn thiếu sau khi nén ngữ cảnh.
- Không xóa hay đổi mã yêu cầu, không xóa dòng truy vết, không sửa tools/spec-check/scope.json (chỉ người dùng được cắt phạm vi), không sửa script kiểm tra để che lỗi.
- Đổi mô hình (bảng, cột, use case, màn hình): sửa nội dung trong docs/spec hoặc docs/ui, chạy `sync`, ghi PROGRESS.md mục "Thay đổi mô hình". Tài liệu lab vẽ lại theo đó. Thêm yêu cầu mới thì gắn mã mới (số kế tiếp trong mục đó, kèm phase) theo docs/README.md; verify báo lỗi mọi gạch đầu dòng chưa có mã.
- Spec thiếu hoặc chưa rõ: chọn cách đơn giản nhất mà vẫn đúng spec, ghi vào PROGRESS.md mục "Quyết định đã chốt", làm tiếp. Chỉ dừng hỏi khi thật sự không thể đi tiếp: ghi PROGRESS.md mục "Đang bị chặn", viết một dòng bắt đầu bằng "CẦN NGƯỜI DÙNG:" rồi dừng.

## Lệnh (PowerShell hoặc Git Bash)

- Spec: `node tools/spec-check/check.mjs begin|next|list|verify N`, `mark MÃ --ev "…"`, `status`, `sync`, `test --reset-db`, `facts`
- Build: dotnet build
- Test: dotnet test
- Chạy: dotnet run --project src/FPolyCinema.Web, mở http://localhost:5080
- Tạo lại DB và dữ liệu mẫu: dotnet run --project src/FPolyCinema.Web -- --reset-db
- Chụp giao diện (app phải đang chạy): node tools/ui-snapshots/snap.mjs --phase N
- Gắn tag nộp lab: git tag -f lab-N

## Quy tắc code

Script kiểm phần lớn các quy tắc này bằng lint L01–L24 trong lệnh verify.
- Chỉ truy cập DB qua Utils.DBUtil và các lớp DAL; tham số đặt theo kiểu @1, @2…; không nối chuỗi dữ liệu người dùng vào SQL; không dùng Entity Framework.
- Không viết SQL hay logic nghiệp vụ trong View; Controller không gọi thẳng DBUtil. Quy tắc thuần đặt ở Domain/Rules và có unit test. Thao tác nhiều bước chạy trong DBUtil.InTransaction.
- Tiền dùng long/BIGINT. Thời gian lấy từ IClock (giờ Việt Nam); không dùng DateTime.Now hay GETDATE() trong nghiệp vụ.
- Mọi POST có Anti-Forgery. Mọi controller có [Authorize] hoặc [AllowAnonymous] rõ ràng. Mọi quy tắc đều kiểm tra ở server. Route của màn hình khai báo bằng attribute routing đúng chuỗi trong UI mục 7.
- Giao diện chỉ dùng token trong wwwroot/css/tokens.css và thành phần có trên /dev/style-guide; không style inline (trừ biến CSS); không dùng alert, confirm, prompt của trình duyệt; mọi chữ hiển thị bằng tiếng Việt.
- Phần đã báo xong thì không còn nút chết, dữ liệu giả cứng trên giao diện, TODO hay hàm rỗng. Việc để dành cho phase sau ghi `TODO(Phase k)`.
- Không ghi bí mật vào mã nguồn; khóa API và mật khẩu SMTP đọc từ biến môi trường.

## Môi trường

- Windows, SQL Server (bản Express được), Node.js LTS, Git for Windows (có Git Bash, Claude Code dùng để chạy hook).
- Chuỗi kết nối nằm ở src/FPolyCinema.Web/appsettings.Development.json, khóa ConnectionStrings:Default.
- Trình duyệt dùng để kiểm tra: Chrome hoặc Edge.
