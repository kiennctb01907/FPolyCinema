# Đặc tả FPolyCinema – mục 4: ba tính năng AI

Mục lục và cách đọc mã yêu cầu như [S3.6-04 P4]: docs/README.md. Tên chuẩn: docs/CONTRACTS.md. Giao diện: docs/ui.

## 4. Ba tính năng AI

### 4.1 AI Seat Scoring – chấm điểm vị trí ghế
- [S4.1-01 P1] Ký hiệu: R = TotalRows, C = TotalCols, r = RowIndex (1 là hàng gần màn hình), c = ColIndex (tính cả ô trống).
- [S4.1-02 P1] Tọa độ lý tưởng: r_ideal = IdealRowRatio × R (mặc định 0,65; persona NEAR_SCREEN dùng 0,40); c_ideal = (C + 1) / 2.
- [S4.1-03 P1] S_raw = 100 − (0,6 × |r − r_ideal| / R × 100 + 0,4 × |c − c_ideal| / (C / 2) × 100).
- Điều chỉnh:
  - [S4.1-04 P1] r ≤ 2 trừ 25 điểm; riêng NEAR_SCREEN chỉ trừ 10 điểm ở r = 1.
  - [S4.1-05 P1] PRIVATE_COUPLE cộng 20 điểm nếu r ≥ 0,85R hoặc ghế là SWEETBOX.
  - [S4.1-06 P1] NEAR_AISLE cộng 15 điểm nếu ghế ở đầu đoạn (kề lối đi hoặc tường).
- [S4.1-07 P1] S_final = round(S_raw + điều chỉnh), rồi giới hạn trong khoảng [0, 100].
- [S4.1-08 P1] SeatScoring là logic thuần, có unit test: ghế ở tọa độ lý tưởng đạt 100; hàng đầu luôn thấp điểm hơn hàng giữa; kết quả luôn nằm trong 0–100.
- Tìm cụm K ghế:
  - [S4.1-09 P5] Duyệt từng đoạn bằng cửa sổ trượt K ghế trống liền nhau; với SWEETBOX chỉ nhận nguyên cặp.
  - [S4.1-10 P5] Bỏ các cụm vi phạm luật ghế mồ côi.
  - [S4.1-11 P5] Xếp theo điểm trung bình; nếu bằng điểm thì ưu tiên cụm gần r_ideal hơn. Trả tối đa 3 phương án.
- [S4.1-12 P5] Gợi ý chuyển suất: nếu phương án tốt nhất dưới GoodClusterScore, tìm suất cùng phim, cùng rạp, cùng LanguageFormat, StartTime lệch trong ±CrossShowtimeWindowMinutes, còn cụm đạt ngưỡng. Khi tìm được, hiện gợi ý như "Suất 21:15 còn cụm ghế VIP trung tâm 96/100 điểm. Chuyển sang suất này?" kèm nút chuyển.

### 4.2 AI Booking Agent – trợ lý đặt vé bằng tiếng Việt
- BookingAgentService có hai bộ máy, dùng chung một bộ công cụ:
  - [S4.2-01 P7] LocalAgent (mặc định, không cần mạng): NlpParser dựa trên luật và từ điển tiếng Việt, so khớp không dấu, trích các trường rồi gọi công cụ theo luồng cố định.
  - [S4.2-02 P7] LlmAgent (khi có Llm:ApiKey): một client duy nhất theo chuẩn OpenAI Chat Completions có function calling. Client này dùng được với OpenAI, với Gemini qua endpoint tương thích OpenAI, hoặc với máy chủ chạy cục bộ như Ollama (cấu hình Llm:BaseUrl, Llm:Model). Gặp lỗi mạng hoặc hết hạn mức thì tự chuyển sang LocalAgent; khách không thấy lỗi.
- Bộ công cụ:
  - [S4.2-03 P7] search_showtimes(date, timeFrom, timeTo, genre, languageFormat, format, movieQuery, cinemaId, quantity)
  - [S4.2-04 P7] get_best_seats(showtimeId, quantity, persona)
  - [S4.2-05 P7] find_combo(query, quantity)
  - [S4.2-06 P7] check_rules(showtimeId, quantity): kiểm tra độ tuổi và giờ chiếu cho trẻ em
  - [S4.2-07 P7] build_preview(...)
- [S4.2-08 P7] Agent không tự giữ ghế, không tự thanh toán.
- Các trường cần trích:
  - [S4.2-09 P7] Ngày: hôm nay, mai, mốt, tối nay, "thứ 2…chủ nhật này", cuối tuần, dd/MM.
  - [S4.2-10 P7] Khung giờ: sáng trước 12:00, chiều 12:00–18:00, tối 18:00–23:59, "sau 19h"; "8 giờ tối" hiểu là 20:00 ± 60 phút.
  - [S4.2-11 P7] Số lượng: "3 người", "2 vé", "cặp đôi" là 2.
  - [S4.2-12 P7] Thể loại; ngôn ngữ (phụ đề, lồng tiếng, bản gốc); định dạng (2D, 3D, IMAX, 4DX).
  - [S4.2-13 P7] Sở thích ghế: "giữa phòng", "đẹp nhất" → BEST_VIEW_AUDIO; "gần màn hình", "bị cận" → NEAR_SCREEN; "riêng tư", "ghế đôi" → PRIVATE_COUPLE; "gần lối đi" → NEAR_AISLE.
  - [S4.2-14 P7] Tên phim (so khớp gần đúng, không dấu); món bắp nước.
- [S4.2-15 P7] Thiếu thông tin bắt buộc thì hỏi lại đúng một câu ngắn. Hội thoại giữ trong bộ nhớ theo phiên, không lưu DB.
- Unit test ít nhất 20 câu, trong đó có sẵn các câu sau:
  1. [S4.2-16 P7] "Tối thứ 7 này mình đi 3 người, muốn xem phim hành động phụ đề sau 19h, lấy 3 ghế đẹp nhất giữa phòng kèm 1 combo bắp phô mai" → thứ Bảy gần nhất (hôm nay nếu hôm nay là thứ Bảy), từ 19:00, 3 vé, ACTION, SUBTITLED, BEST_VIEW_AUDIO, 1 combo bắp phô mai.
  2. [S4.2-17 P7] "2 vé hoạt hình lồng tiếng sáng chủ nhật cho bé" → Chủ nhật gần nhất, 08:00–12:00, 2 vé, ANIMATION, DUBBED.
  3. [S4.2-18 P7] "Cặp đôi muốn ghế đôi riêng tư tối nay" → hôm nay, 18:00–23:59, 2 vé, PRIVATE_COUPLE.
  4. [S4.2-19 P7] "Mai 8 giờ tối có phim kinh dị nào không" → ngày mai, 19:00–21:00, HORROR, chỉ tìm suất.
  5. [S4.2-20 P7] "Mình bị cận, muốn ngồi gần màn hình, xem IMAX" → NEAR_SCREEN, IMAX.
  6. [S4.2-21 P7] "Cuối tuần này có suất bản gốc không" → thứ Bảy và Chủ nhật gần nhất, ORIGINAL.
  7. [S4.2-22 P7] "Đặt 4 vé suất gần nhất, ghế gần lối đi" → suất sớm nhất còn bán, 4 vé, NEAR_AISLE.
  8. [S4.2-23 P7] "Thêm 2 nước suối" (câu nói tiếp) → thêm vào thẻ đặt vé hiện tại.
  9. [S4.2-24 P7] "Đổi sang suất sau đó" → suất kế tiếp của cùng phim.
  10. [S4.2-25 P7] "Thôi, làm lại" → xóa thẻ đặt vé hiện tại.
- [S4.2-26 P7] Giao diện theo UI mục 7, phần C7. Nút "Giữ ghế & thanh toán ngay" gọi đúng SeatHoldService như luồng đặt vé thường, rồi chuyển sang bước thanh toán.

### 4.3 AI Smart Scheduler – xếp lịch chiếu tự động
- [S4.3-01 P7] AiDemandScore (thang 1–10) tính bằng công thức trọng số, ghi rõ trong code và có unit test. Các yếu tố: độ mới (số ngày tính từ ReleaseDate), tỷ lệ lấp đầy trung bình 7 ngày qua của phim, số vé đã bán trước cho các suất tương lai, phim bom tấn. Kết quả lưu vào Movies mỗi lần chạy.
- [S4.3-02 P7] Đầu vào: rạp, khoảng 1–7 ngày, danh sách phim (tự lọc những phim có giấy phép, khác C, KDM còn hiệu lực), danh sách phòng.
- Thuật toán tham lam có ràng buộc:
  1. [S4.3-03 P7] Số suất mục tiêu của mỗi phim tỷ lệ với AiDemandScore, mỗi phim ít nhất 1 suất mỗi ngày. Tổng suất phim Việt ≥ VietnameseFilmTargetShare.
  2. [S4.3-04 P7] Xếp phòng theo sức chứa giảm dần. Khung 18:00–22:00 ở phòng lớn dành cho phim điểm cao, đồng thời ưu tiên phim Việt trong khung này theo Điều 9 khoản 2.
  3. [S4.3-05 P7] Điền từng phòng bắt đầu từ OperatingDayStart: suất sau bắt đầu từ CleaningEndTime của suất trước, có làm tròn; giờ bắt đầu lệch tối thiểu MinStartStaggerMinutes so với các phòng khác cùng rạp.
  4. [S4.3-06 P7] Ngôn ngữ: phim P, K có bản DUBBED thì xếp DUBBED cho suất trước 18:00 và SUBTITLED cho suất sau 18:00 (nếu phim có bản đó).
  5. [S4.3-07 P7] Mọi suất đều phải qua ScheduleConstraintChecker: không có lỗi chặn; tránh các cảnh báo (phim K kết thúc từ 22:00, T13 từ 23:00); phim T18 ưu tiên xếp sau 19:00.
  6. [S4.3-08 P7] Không động vào các suất đã có. Kết quả chỉ là bản nháp: Manager xem trên Gantt (khối viền nét đứt), bấm "Áp dụng" mới lưu (IsAiGenerated = 1).
- Gantt:
  - [S4.3-09 P7] Kéo thả được các suất chưa khóa, hít theo bước 5 phút.
  - [S4.3-10 P7] Mỗi lần thả gọi POST /scheduler/validate (dùng ScheduleConstraintChecker) để tô viền đỏ nếu có lỗi chặn, viền vàng nếu có cảnh báo, kèm lý do. Chỉ lưu được khi không có lỗi chặn.
  - [S4.3-11 P7] Suất LOCKED_HAS_TICKETS hiện biểu tượng khóa và không kéo được.
