# Kiểm tra độc lập – Phase 8

Agent spec-auditor, 15/11/2026, một lô 15 mã (cộng C7-09). Lượt đầu: 14 Đạt, 2 Thiếu; đã sửa và kiểm lại.

| Mã | Kết luận | Lý do | Đã sửa |
|---|---|---|---|
| S5-06 | Đạt | _DemoBar có liên kết /demo/multi-screen (icon, nhãn đọc màn hình, tooltip), chỉ khi Demo:EnableTools; test kiểm href | – |
| S5-08 | Đạt | Route multi-screen trong DemoController; DemoToolsMiddleware trả 404 khi tắt; test 404/200 | – |
| S5-09 | Đạt | WindowOrigin dựng 5 tên miền phụ *.localhost giữ cổng; tự đăng nhập bằng SwitchRolePath; window.open theo Slots chia đôi màn hình; cookie không đặt Domain nên phiên riêng; có unit test | – |
| S5-10 | Đạt | Stage.cshtml nhúng iframe màn hình phụ, bảng gọi số, bảng sảnh (AllowAnonymous, cùng origin); test HTTP kiểm iframe | – |
| S5-11 | Đạt | Kịch bản 1 mở khach-a (khach) và khach-b (khach2) cùng /booking/{id} chọn bằng PickContest; test quy tắc và switch-role khach2 | – |
| S5-12 | Đạt | Kịch bản 2: POS trên cashier.localhost và iframe customer-display theo mã máy của ca đang mở | – |
| S5-13 | Đạt | Kịch bản 3: vé điện tử, bếp, iframe bảng gọi số; PrepareTicket theo đúng luồng online; test trên CSDL mẫu | – |
| S5-14 | Đạt | Kịch bản 4: máy soát vé, vé điện tử, iframe bảng sảnh; PickEntry ưu tiên suất đang mở cửa; có test | – |
| S5-15 | Đạt | Kịch bản 5: /pos/my-bills trên cashier.localhost và /ops trên manager.localhost bằng tài khoản quản lý ca | – |
| S8.8-01 | Đạt | Đủ các ý mục 5 (S5-08 → S5-15); verify 8 --brief ĐẠT | – |
| S8.8-02 | Thiếu → Đạt | Lượt đầu: ảnh D4 1440 cho thấy thanh demo đang mở che nút "Mở các màn hình" của kịch bản 1, 2 | BUG-071: ui.js luôn thu gọn thanh demo ở trang trung tâm demo; nút "Mở các màn hình" đặt bên phải chân thẻ; chụp lại D4 |
| S8.8-03 | Thiếu → Đạt | Lượt đầu: ô kết quả TC-P8-07 còn chữ giữ chỗ, các dòng test chỉ ghi "Xem TC-P8-07" | Ghi kết quả thật: build, test, resetDb ĐẠT; dotnet test 444/444; cập nhật các dòng TC-P1-06, TC-P2-09… |
| S8.8-04 | Đạt | README đủ cài đặt, chuỗi kết nối, cách chạy, tài khoản mẫu, publish, systemd, Nginx (WebSocket), biến môi trường | – |
| U4-15 | Đạt | Như S5-06, có aria-current khi đang ở trung tâm demo | – |
| U7.D4-01 | Đạt | 5 thẻ kịch bản, mỗi thẻ có mô tả, các bước và nút "Mở các màn hình"; test đếm 5 nút; có ảnh D4 | – |
| C7-09 | Đạt | Demo:ClockOffsetMinutes mặc định 0, chỉ áp khi bật công cụ demo; IClock dùng chung cho seed và web; có test | – |

Kết quả: Đạt
