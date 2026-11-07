# Truy vết yêu cầu – Phase 8 – Hoàn thiện, demo, kiểm tra tổng thể

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
| S5-06 | Có nút mở trung tâm demo. | Chưa |  |  |
| S5-08 | Trang /demo/multi-screen chỉ có khi bật Demo:EnableTools. | Chưa |  |  |
| S5-09 | Mỗi màn hình mở trong một cửa sổ riêng, trên một tên miền phụ khác nhau của localhost: cashier.localhost, manager.local… | Chưa |  |  |
| S5-10 | Các màn hình không cần đăng nhập (màn hình phụ, bảng gọi số, bảng sảnh) được nhúng bằng iframe. | Chưa |  |  |
| S5-11 | Hai khách cùng giành một ghế. | Chưa |  |  |
| S5-12 | POS và màn hình phụ của khách. | Chưa |  |  |
| S5-13 | Khách đặt bắp nước, bếp và bảng gọi số. | Chưa |  |  |
| S5-14 | Máy soát vé, vé điện tử và bảng sảnh. | Chưa |  |  |
| S5-15 | Thu ngân xin hủy hóa đơn, quản lý ca duyệt từ xa. | Chưa |  |  |
| S8.8-01 | Trung tâm demo nhiều màn hình (mục 5). | Chưa |  |  |
| S8.8-02 | Kiểm tra giao diện mọi màn hình (C, P, A, D) của cả 5 vai trò theo UI mục 13 và sửa hết lỗi. | Chưa |  |  |
| S8.8-03 | Chạy lại toàn bộ TEST-SCENARIOS.md trên DB mới, cập nhật kết quả. | Chưa |  |  |
| S8.8-04 | README.md gồm: yêu cầu cài đặt, cấu hình chuỗi kết nối, cách chạy, tài khoản mẫu, hướng dẫn publish và chạy sau Nginx t… | Chưa |  |  |
| U4-15 | Nút mở trung tâm demo. | Chưa |  |  |
| U7.D4-01 | D4. Trung tâm demo /demo/multi-screen (chỉ khi Demo:EnableTools): 5 kịch bản ở SPEC mục 5, mỗi kịch bản một thẻ có mô t… | Chưa |  |  |
