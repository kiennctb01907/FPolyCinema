# Truy vết yêu cầu – Phase 7 – AI xếp lịch và trợ lý đặt vé

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
| S3.1-21 | Bộ xếp lịch nhắm tới VietnameseFilmTargetShare. | Chưa |  |  |
| S4.2-01 | LocalAgent (mặc định, không cần mạng): NlpParser dựa trên luật và từ điển tiếng Việt, so khớp không dấu, trích các trườ… | Chưa |  |  |
| S4.2-02 | LlmAgent (khi có Llm:ApiKey): một client duy nhất theo chuẩn OpenAI Chat Completions có function calling. Client này dù… | Chưa |  |  |
| S4.2-03 | search_showtimes(date, timeFrom, timeTo, genre, languageFormat, format, movieQuery, cinemaId, quantity) | Chưa |  |  |
| S4.2-04 | get_best_seats(showtimeId, quantity, persona) | Chưa |  |  |
| S4.2-05 | find_combo(query, quantity) | Chưa |  |  |
| S4.2-06 | check_rules(showtimeId, quantity): kiểm tra độ tuổi và giờ chiếu cho trẻ em | Chưa |  |  |
| S4.2-07 | build_preview(...) | Chưa |  |  |
| S4.2-08 | Agent không tự giữ ghế, không tự thanh toán. | Chưa |  |  |
| S4.2-09 | Ngày: hôm nay, mai, mốt, tối nay, "thứ 2…chủ nhật này", cuối tuần, dd/MM. | Chưa |  |  |
| S4.2-10 | Khung giờ: sáng trước 12:00, chiều 12:00–18:00, tối 18:00–23:59, "sau 19h"; "8 giờ tối" hiểu là 20:00 ± 60 phút. | Chưa |  |  |
| S4.2-11 | Số lượng: "3 người", "2 vé", "cặp đôi" là 2. | Chưa |  |  |
| S4.2-12 | Thể loại; ngôn ngữ (phụ đề, lồng tiếng, bản gốc); định dạng (2D, 3D, IMAX, 4DX). | Chưa |  |  |
| S4.2-13 | Sở thích ghế: "giữa phòng", "đẹp nhất" → BEST_VIEW_AUDIO; "gần màn hình", "bị cận" → NEAR_SCREEN; "riêng tư", "ghế đôi"… | Chưa |  |  |
| S4.2-14 | Tên phim (so khớp gần đúng, không dấu); món bắp nước. | Chưa |  |  |
| S4.2-15 | Thiếu thông tin bắt buộc thì hỏi lại đúng một câu ngắn. Hội thoại giữ trong bộ nhớ theo phiên, không lưu DB. | Chưa |  |  |
| S4.2-16 | "Tối thứ 7 này mình đi 3 người, muốn xem phim hành động phụ đề sau 19h, lấy 3 ghế đẹp nhất giữa phòng kèm 1 combo bắp p… | Chưa |  |  |
| S4.2-17 | "2 vé hoạt hình lồng tiếng sáng chủ nhật cho bé" → Chủ nhật gần nhất, 08:00–12:00, 2 vé, ANIMATION, DUBBED. | Chưa |  |  |
| S4.2-18 | "Cặp đôi muốn ghế đôi riêng tư tối nay" → hôm nay, 18:00–23:59, 2 vé, PRIVATE_COUPLE. | Chưa |  |  |
| S4.2-19 | "Mai 8 giờ tối có phim kinh dị nào không" → ngày mai, 19:00–21:00, HORROR, chỉ tìm suất. | Chưa |  |  |
| S4.2-20 | "Mình bị cận, muốn ngồi gần màn hình, xem IMAX" → NEAR_SCREEN, IMAX. | Chưa |  |  |
| S4.2-21 | "Cuối tuần này có suất bản gốc không" → thứ Bảy và Chủ nhật gần nhất, ORIGINAL. | Chưa |  |  |
| S4.2-22 | "Đặt 4 vé suất gần nhất, ghế gần lối đi" → suất sớm nhất còn bán, 4 vé, NEAR_AISLE. | Chưa |  |  |
| S4.2-23 | "Thêm 2 nước suối" (câu nói tiếp) → thêm vào thẻ đặt vé hiện tại. | Chưa |  |  |
| S4.2-24 | "Đổi sang suất sau đó" → suất kế tiếp của cùng phim. | Chưa |  |  |
| S4.2-25 | "Thôi, làm lại" → xóa thẻ đặt vé hiện tại. | Chưa |  |  |
| S4.2-26 | Giao diện theo UI mục 7, phần C7. Nút "Giữ ghế & thanh toán ngay" gọi đúng SeatHoldService như luồng đặt vé thường, rồi… | Chưa |  |  |
| S4.3-01 | AiDemandScore (thang 1–10) tính bằng công thức trọng số, ghi rõ trong code và có unit test. Các yếu tố: độ mới (số ngày… | Chưa |  |  |
| S4.3-02 | Đầu vào: rạp, khoảng 1–7 ngày, danh sách phim (tự lọc những phim có giấy phép, khác C, KDM còn hiệu lực), danh sách phò… | Chưa |  |  |
| S4.3-03 | Số suất mục tiêu của mỗi phim tỷ lệ với AiDemandScore, mỗi phim ít nhất 1 suất mỗi ngày. Tổng suất phim Việt ≥ Vietname… | Chưa |  |  |
| S4.3-04 | Xếp phòng theo sức chứa giảm dần. Khung 18:00–22:00 ở phòng lớn dành cho phim điểm cao, đồng thời ưu tiên phim Việt tro… | Chưa |  |  |
| S4.3-05 | Điền từng phòng bắt đầu từ OperatingDayStart: suất sau bắt đầu từ CleaningEndTime của suất trước, có làm tròn; giờ bắt … | Chưa |  |  |
| S4.3-06 | Ngôn ngữ: phim P, K có bản DUBBED thì xếp DUBBED cho suất trước 18:00 và SUBTITLED cho suất sau 18:00 (nếu phim có bản … | Chưa |  |  |
| S4.3-07 | Mọi suất đều phải qua ScheduleConstraintChecker: không có lỗi chặn; tránh các cảnh báo (phim K kết thúc từ 22:00, T13 t… | Chưa |  |  |
| S4.3-08 | Không động vào các suất đã có. Kết quả chỉ là bản nháp: Manager xem trên Gantt (khối viền nét đứt), bấm "Áp dụng" mới l… | Chưa |  |  |
| S4.3-09 | Kéo thả được các suất chưa khóa, hít theo bước 5 phút. | Chưa |  |  |
| S4.3-10 | Mỗi lần thả gọi POST /scheduler/validate (dùng ScheduleConstraintChecker) để tô viền đỏ nếu có lỗi chặn, viền vàng nếu … | Chưa |  |  |
| S4.3-11 | Suất LOCKED_HAS_TICKETS hiện biểu tượng khóa và không kéo được. | Chưa |  |  |
| S7-05 | NlpParser (ít nhất 20 câu). | Chưa |  |  |
| S8.7-01 | AiDemandScore, thuật toán xếp lịch, Gantt có kéo thả và kiểm tra tức thì. | Chưa |  |  |
| S8.7-02 | BookingAgentService (LocalAgent, LlmAgent), NlpParser với bộ test 20 câu, widget trợ lý trên web khách. | Chưa |  |  |
| U7.C1-06 | Nút trợ lý AI nổi ở góc phải dưới: hình tròn 56px. | Chưa |  |  |
| U7.C7-01 | C7. Trợ lý đặt vé (widget) | Chưa |  |  |
| U7.C7-02 | Khung: panel 380 × 600px trên desktop, dạng sheet toàn màn hình trên điện thoại. Đầu panel có tiêu đề "Trợ lý đặt vé" v… | Chưa |  |  |
| U7.C7-03 | Hội thoại: tin nhắn dạng bong bóng; chip câu hỏi gợi ý; ô nhập kèm nút gửi; skeleton "Đang tìm suất phù hợp…" khi đang … | Chưa |  |  |
| U7.C7-04 | Thẻ đặt vé trong khung chat: poster nhỏ; tên phim và nhãn tuổi; rạp, phòng, giờ, định dạng; sơ đồ thu nhỏ quanh cụm ghế… | Chưa |  |  |
| U7.A5-03 | Thanh trên thêm nút chuyển giữa chế độ Bảng và Gantt, và nút "AI tự động xếp lịch". | Chưa |  |  |
| U7.A5-05 | Trục thời gian 08:00–02:00, vạch mờ mỗi 30 phút, nhãn mỗi giờ. Mỗi hàng là một phòng; cột trái rộng 160px ghi tên phòng… | Chưa |  |  |
| U7.A5-06 | Khối suất: nền màu nhãn tuổi nhạt 20%, viền trái 4px màu nhãn tuổi. Bên trong ghi tên phim (1 dòng) và "19:15–21:40 · P… | Chưa |  |  |
| U7.A5-07 | Lỗi chặn tô viền đỏ 2px kèm icon, cảnh báo tô viền vàng; tooltip ghi lý do. Bản nháp AI vẽ viền nét đứt, kèm nút "Áp dụ… | Chưa |  |  |
| U7.A5-08 | Chân trang hiện tỷ lệ phim Việt của ngày và của tuần. | Chưa |  |  |
| UC13 | Đặt vé bằng trợ lý AI (màn hình C7) | Chưa |  |  |
| UC35 | Xếp lịch tự động bằng AI (màn hình A5) | Chưa |  |  |
