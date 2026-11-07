# Mục lục tài liệu FPolyCinema

Bộ spec được chia thành nhiều file nhỏ để Claude Code đọc trọn từng file, không bị cắt giữa chừng. Số mục giữ nguyên như bản gốc: "SPEC mục 3.6" nằm trong docs/spec, "UI mục 7" nằm trong docs/ui.

## File và mục

| Mục | File |
|---|---|
| SPEC 1–2: mục tiêu, kiến trúc, công nghệ, DBUtil, khởi tạo DB, xác thực, upload, bảo mật | docs/spec/s01-02-tong-quan-kien-truc.md |
| SPEC 3.1–3.4: pháp lý, phòng chiếu và ghế, suất chiếu, giá vé | docs/spec/s03a-phap-ly-phong-suat-gia.md |
| SPEC 3.5–3.7: giữ ghế, luật ghế mồ côi, hóa đơn và POS, thanh toán online | docs/spec/s03b-giu-ghe-hoa-don-thanh-toan.md |
| SPEC 3.8–3.14: soát vé, tự hủy vé, nghiệp vụ khác, kho, thống kê, điểm và ví, quy tắc xóa | docs/spec/s03c-van-hanh-kho-bao-cao.md |
| SPEC 4: ba tính năng AI | docs/spec/s04-ai.md |
| SPEC 5–7: thời gian thực và demo, cơ sở dữ liệu, kiểm thử và tiêu chuẩn hoàn thành | docs/spec/s05-07-realtime-csdl-kiem-thu.md |
| SPEC 8–9: lộ trình 8 phase, mẫu PROGRESS.md, ngoài phạm vi | docs/spec/s08-09-lo-trinh.md |
| SPEC 10: danh mục use case UC01–UC40 | docs/spec/s10-use-case.md |
| UI 1–6: nguyên tắc, token, thư viện, bố cục, thành phần, trạng thái | docs/ui/u01-06-he-thong-thiet-ke.md |
| UI 7: từng màn hình C1–C7, P1–P7, A1–A9, D1–D4, ảnh mẫu | docs/ui/u07-man-hinh.md |
| UI 8–13: sơ đồ ghế, chữ và lời nhắn, tiếp cận, kích thước, điều cấm, kiểm tra giao diện | docs/ui/u08-13-ghe-chu-kiem-tra.md |
| Tên chuẩn: vai trò, policy, trạng thái, SignalR, cấu hình, mã lỗi, quy ước, stored procedure, tài khoản mẫu | docs/CONTRACTS.md |

## Mã yêu cầu

Mỗi gạch đầu dòng là một yêu cầu có mã ở đầu dòng, ví dụ `[S3.6-04 P4]` hoặc `[C3-10 P1 auto:enum]`.

- Chữ đầu: S là spec, U là giao diện (U7.A8 là màn hình A8), C là CONTRACTS.
- Phần giữa là số mục và số thứ tự trong mục. Mã không đổi khi sửa nội dung, nên dùng được để truy vết trong code, test và tài liệu lab.
- P1 đến P8: phase phải làm xong yêu cầu đó. Một ý có phần làm ở nhiều phase thì đã được tách thành nhiều dòng.
- auto:…: script kiểm tự động, không cần ghi bằng chứng. Ví dụ auto:table kiểm bảng và cột trong Database/*.sql, auto:enum kiểm hằng trong Domain/Constants và CHECK constraint.
- Gạch đầu dòng kết thúc bằng dấu hai chấm và có dòng con là nhãn nhóm, không có mã; các dòng con mới là yêu cầu.
- Mục có chú thích "spec-check: không gắn mã" là nguyên tắc chung hoặc thông tin tham khảo (SPEC 1, 7.1, 9, 10; UI 1).

Use case UC01–UC40 ở SPEC mục 10 cũng là yêu cầu, thuộc phase lớn nhất ghi ở cột Phase.

## Script kiểm tra

`node tools/spec-check/check.mjs` đọc các mã trên và đối chiếu với code:

| Lệnh | Việc |
|---|---|
| begin N | Bắt đầu Phase N: in mã phiên, danh sách yêu cầu theo nhóm, tạo docs/trace/phase-N.md |
| next N | In nguyên văn nhóm yêu cầu kế tiếp còn "Chưa" |
| list N [--full] [--pending] [--group K] | Liệt kê yêu cầu của Phase N |
| verify N [--brief] [--final] | Kiểm truy vết, luật tự động, đối chiếu code, lint; --final kiểm thêm các bước kết thúc phase |
| mark MÃ --ev "…" [--note "…"] [--status Xong/Bỏ/Chưa] | Ghi dòng truy vết và kiểm bằng chứng ngay |
| status | Tiến độ 8 phase |
| sync | Đồng bộ docs/trace với spec sau khi sửa spec |
| test [--reset-db] | dotnet build, dotnet test, tạo lại DB; ghi dấu kết quả theo mã băm code |
| facts | Sinh docs/generated (bảng, quan hệ, route, use case, thư viện) cho tài liệu lab |
| selftest, untagged | Kiểm tính nhất quán của chính bộ spec |

## Thư mục do quy trình tạo ra

- docs/trace/phase-N.md: mỗi yêu cầu một dòng với Trạng thái, Bằng chứng, Ghi chú.
- docs/audit/phase-N.md: kết quả kiểm tra độc lập của agent spec-auditor.
- docs/generated/: dữ kiện sinh từ code. Tài liệu lab vẽ sơ đồ từ đây.
- tools/spec-check/scope.json: chỉ người dùng sửa, để cắt phase hoặc cắt mã khi thiếu thời gian.
