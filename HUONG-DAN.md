# Cách dùng bộ prompt FPolyCinema v3

Bộ này có 2 prompt dùng chung một nguồn:
- Prompt dự án: CLAUDE.md, thư mục docs, .claude và tools/spec-check. Claude Code đọc để viết code theo 8 phase.
- Prompt bài lab: LAB-PROMPT.md, dùng với lệnh /lab N.

## v3 chống bỏ sót như thế nào

1. Spec được tách thành 11 file nhỏ (mỗi file dưới 20 KB), nên Claude Code đọc trọn từng file, không bị cắt giữa chừng như khi đọc một file 67 KB.
2. Mỗi yêu cầu có một mã và một phase, ví dụ [S3.6-04 P4]. Có 775 yêu cầu và 40 use case. Ý nào gồm việc của nhiều phase đã được tách thành nhiều dòng, để không phase nào bỏ sót phần của mình.
3. Script tools/spec-check/check.mjs in đúng danh sách việc của từng phase và đòi mỗi yêu cầu có bằng chứng (file, hàm, test) trong docs/trace/phase-N.md. Script còn tự đối chiếu với code:
   - Đủ 35 bảng và mọi cột; kiểu tiền, phần trăm, thời gian đúng quy ước.
   - CHECK constraint đúng giá trị trạng thái.
   - Đủ khóa cấu hình, mã lỗi, sự kiện SignalR, stored procedure, tài khoản mẫu, token màu.
   - Có route của mọi màn hình và có các lớp được nhắc tên trong spec.
   - 24 quy tắc code, ví dụ không dùng DateTime.Now, không nối chuỗi SQL, không alert, không style inline, không sót chữ tiếng Anh trên giao diện.
4. Hai hook:
   - Hook Stop không cho Claude Code dừng khi `verify N --final` chưa ĐẠT, trừ khi nó thật sự cần bạn trả lời hoặc đã kẹt không tiến triển thêm (lúc đó hook cho dừng để bạn xem).
   - Hook SessionStart tự in lại các yêu cầu còn thiếu sau khi ngữ cảnh bị nén.
5. Agent spec-auditor đọc code và kiểm độc lập từng nhóm yêu cầu, không tin vào lời Claude Code tự báo.
6. Các bước cuối phase cũng do script kiểm:
   - Test đạt trên đúng code hiện tại và tạo lại DB được.
   - Có ảnh chụp cho mọi màn hình của phase.
   - Đã cập nhật PROGRESS, TEST-SCENARIOS, BUG-LOG và CONTRACTS.
   - Đã commit và gắn tag lab.

Sơ đồ lab khớp dự án nhờ lệnh `facts`. Lệnh này sinh docs/generated (bảng, cột, quan hệ khóa ngoại, route, quyền, use case, thư viện và phiên bản) từ chính code, và LAB-PROMPT.md bắt buộc vẽ từ đó.

## Chuẩn bị

1. Cài các phần mềm sau:
   - .NET 8 SDK.
   - SQL Server Express (hoặc Developer) kèm SSMS.
   - Git for Windows, có kèm Git Bash. Claude Code dùng Git Bash để chạy hook.
   - Node.js LTS. Bắt buộc, vì script kiểm tra chạy bằng Node.
   - Chrome hoặc Edge.
2. Tạo thư mục dự án trống, ví dụ D:\Projects\FPolyCinema.
3. Giải nén gói, rồi chép toàn bộ nội dung trong thư mục FPolyCinema-v3 vào thư mục dự án:
   - Thư mục .claude (tên bắt đầu bằng dấu chấm), docs, tools.
   - CLAUDE.md và LAB-PROMPT.md.
   - File HUONG-DAN.md thì không cần chép.
4. Mở Claude Code trong thư mục dự án. Nếu Claude Code hỏi có tin cậy thư mục này không thì chọn đồng ý. Gõ /hooks để xem hai hook SessionStart và Stop đã được nạp.

## Làm dự án

- Mỗi phase một phiên. Gõ `/phase 1`; xong thì gõ /clear rồi `/phase 2`, cứ thế đến Phase 8.
- Phiên bị ngắt hoặc mở lại hôm sau: gõ `/tiep-tuc`.
- Muốn tự kiểm lại một phase đã xong: gõ `/kiem-tra N`.
- Xem tiến độ: chạy `node tools/spec-check/check.mjs status` trong terminal.
- Claude Code chỉ dừng giữa phase khi thật sự cần bạn quyết định, và sẽ viết một dòng bắt đầu bằng "CẦN NGƯỜI DÙNG:". Trả lời câu hỏi đó rồi gõ /tiep-tuc. Muốn tự dừng thì bấm Esc.
- Sau mỗi phase, tự chạy thử bằng các tài khoản mẫu ở docs/CONTRACTS.md mục 11 (mật khẩu 123456). Thấy lỗi thì mô tả cụ thể ngay trong phiên đó; Claude Code sẽ sửa và ghi vào docs/BUG-LOG.md (Lab 7 dùng chính các lỗi này).
- Một phase có từ 15 đến khoảng 185 yêu cầu (nhiều nhất là Phase 2 vì có cả hệ thống giao diện), nên một phiên có thể chạy lâu. Đó là bình thường.

## Cắt bớt phạm vi khi thiếu thời gian

Chỉ bạn sửa file tools/spec-check/scope.json. Ví dụ:
- Bỏ Phase 7: `"skipPhases": [7]`.
- Bỏ phần bán vé doanh nghiệp: `"skipIds": ["S3.10-08", "S3.10-09", "UC40"]`.

Script sẽ không đòi các mã đó nữa. Thứ tự nên cắt: Phase 7 trước, sau đó đến phần doanh nghiệp.

## Làm bài lab

| Bài lab | Làm khi | Mã nguồn nộp |
|---|---|---|
| Lab 1 | Trước hoặc trong Phase 1 | Không |
| Lab 2 | Sau Phase 1 (ERD lấy từ schema thật); cập nhật luồng giao diện sau Phase 2 | Không |
| Lab 3 | Sau Phase 1 | tag lab-3 |
| Lab 4, Lab 5 | Sau Phase 2 | tag lab-4, lab-5 |
| Lab 6 | Sau Phase 4 | tag lab-6 |
| Lab 7 | Sau Phase 4, bổ sung sau Phase 5 | Không bắt buộc |
| Lab 8 | Sau Phase 8 | tag lab-8 |

Có 2 cách:
- Cách 1, khuyên dùng: mở một phiên Claude Code mới trong thư mục dự án (không dùng chung với phiên làm phase), gõ `/lab N`. Sơ đồ vẽ bằng diagrams.net (file .drawio trong labs/LabN/diagrams), hoặc Lucidchart nếu Claude Code đã kết nối Lucid.
- Cách 2: dùng Claude trên web hoặc app có kết nối Lucidchart. Đính kèm LAB-PROMPT.md, thư mục docs (có docs/generated), PROGRESS.md, đề lab PDF và zip mã nguồn tại tag của bài, rồi dán câu lệnh mẫu ở LAB-PROMPT.md mục 11.

Những việc bạn tự làm:
- Điền họ tên vào trang bìa (chỗ [HỌ TÊN SINH VIÊN]).
- Chụp ảnh màn hình các cuộc trò chuyện với AI và chèn vào phần ứng dụng AI.
- Chụp ảnh app đang chạy ở những chỗ tài liệu ghi "[Ảnh chụp màn hình: ...]".

## Khi có vấn đề

- Thấy thông báo lỗi hook (hook error): kiểm tra đã cài Node.js và Git for Windows, rồi mở lại Claude Code.
- Claude Code cho rằng script kiểm sai: bảo nó ghi lý do vào PROGRESS.md. Chỉ quy tắc code (lint) mới được bỏ qua ở từng dòng bằng chú thích `spec-check: allow Lxx <lý do>`; mọi chỗ khác phải sửa code hoặc sửa spec.
- Muốn đổi một quy tắc (ví dụ thời gian giữ ghế): sửa docs/CONTRACTS.md mục 7 hoặc file spec tương ứng trước, rồi bảo Claude Code cập nhật code. Đổi mô hình dữ liệu hay use case sau khi đã nộp lab thì nhớ vẽ lại sơ đồ ở bước đồng bộ cuối (LAB-PROMPT.md mục 10).
