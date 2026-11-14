# Kiểm tra độc lập – Phase 7

Agent spec-auditor kiểm 3 lô mã (không quá 20 mã mỗi lô, theo nhóm của lệnh `list 7`): đọc spec, truy vết, code thật, chạy test liên quan (NlpParserTests, BookingAgentTests, Phase7RulesTests) và xem ảnh trong artifacts/ui/phase-7. Ngày 14/11/2026. Lượt đầu: 51 Đạt, 2 Thiếu (S4.3-02, UC35). Sau khi sửa, kiểm lại hai mã này: Đạt. C7-07 do script kiểm tự động.
Cột "Đã sửa" ghi việc đã làm cho các dòng Thiếu hoặc Sai và các ghi chú nhỏ đã xử lý.

| Mã | Kết luận | Lý do | Đã sửa |
|---|---|---|---|
| S3.1-21 | Đạt | TargetCounts nâng suất phim Việt tới ⌈VietnameseFilmTargetShare × tổng⌉ (0,22 từ CinemaRules); Generate ưu tiên +100 khi ngày còn thiếu phim Việt; có unit test | – |
| S4.2-01 | Đạt | LocalAgent dùng NlpParser (luật, từ điển, Normalize bỏ dấu), luồng cố định search_showtimes → check_rules → get_best_seats → find_combo → build_preview; không có khóa thì dùng LocalAgent | – |
| S4.2-02 | Đạt | Một client POST {BaseUrl}/chat/completions với tools, tool_calls, tối đa 6 lượt; lỗi mạng, 429, 5xx, hết giờ, JSON hỏng chuyển sang LocalAgent; có test. Ghi chú: content không phải chuỗi sẽ ném lỗi ngoài khối bắt | LlmAgent đọc content bằng TryGetValue; không phải chuỗi thì coi như mô hình không trả lời và chuyển sang LocalAgent |
| S4.2-03 | Đạt | SearchShowtimes lọc đủ 9 tham số, chỉ suất còn bán online; schema LLM đúng tên | – |
| S4.2-04 | Đạt | GetBestSeats dùng SeatSuggestService, trả cụm tốt nhất kèm điểm và giá | – |
| S4.2-05 | Đạt | FindCombo chỉ xét món đang bán còn nguyên liệu, so khớp không dấu, gần đúng; "combo" chung lấy combo rẻ nhất | – |
| S4.2-06 | Đạt | CheckRules chặn khách chưa đủ tuổi theo ngày sinh; "cho bé" chặn phim từ T13; câu xác nhận và lời nhắc giờ chiếu trẻ em vào Notes | – |
| S4.2-07 | Đạt | BuildPreview dựng đủ thẻ, không giữ ghế. Ghi chú: không kiểm ghế mô hình truyền vào còn trống | BuildPreview chỉ dựng khi mọi ghế còn AVAILABLE; thêm kiểm tra vào test Agent_HoldCard_HoldsSeatsViaSeatHoldService_AddsItems_GoesToPayment |
| S4.2-08 | Đạt | IBookingTools không có hàm giữ ghế hay thanh toán (có test); chỉ POST /assistant/hold (CustomerOnly) mới giữ ghế | – |
| S4.2-09 | Đạt | ParseDates đủ hôm nay, tối nay, mai, mốt, thứ, chủ nhật, tuần sau, cuối tuần, dd/MM; có test | – |
| S4.2-10 | Đạt | Sáng, chiều, tối, "sau 19h", "8 giờ tối" là 19:00–21:00; có test | – |
| S4.2-11 | Đạt | N người, N vé, N ghế (cả số viết bằng chữ), "cặp đôi" là 2; có test | – |
| S4.2-12 | Đạt | 11 thể loại, 3 bản ngôn ngữ, 4 định dạng; có test | – |
| S4.2-13 | Đạt | Ánh xạ 4 nhóm cụm từ sang 4 persona; có test | – |
| S4.2-14 | Đạt | MatchMovie (đúng hoặc Levenshtein từ 0,8, không dấu), MatchItems kèm số lượng; có test | – |
| S4.2-15 | Đạt | Hỏi đúng một câu khi thiếu; phiên trong IMemoryCache theo cookie fpc_agent, không ghi DB | – |
| S4.2-16 | Đạt | Test kiểm đủ các trường, thêm trường hợp hôm nay là thứ Bảy | – |
| S4.2-17 | Đạt | Chủ nhật gần nhất, 08:00–12:00, 2 vé, ANIMATION, DUBBED | – |
| S4.2-18 | Đạt | Hôm nay, 18:00–23:59, 2 vé, PRIVATE_COUPLE; có test service | – |
| S4.2-19 | Đạt | Ngày mai, 19:00–21:00, HORROR, chỉ tìm suất | – |
| S4.2-20 | Đạt | NEAR_SCREEN, IMAX | – |
| S4.2-21 | Đạt | Thứ Bảy và Chủ nhật gần nhất, ORIGINAL | – |
| S4.2-22 | Đạt | Suất sớm nhất còn bán, 4 vé, NEAR_AISLE | – |
| S4.2-23 | Đạt | AddItems thêm món vào thẻ hiện tại, tính lại tổng. Ghi chú: món thêm theo câu nói tiếp chưa kiểm tồn kho của rạp | AddItems gọi FindCombo với rạp đang chọn |
| S4.2-24 | Đạt | NextShowtime lấy suất kế tiếp cùng phim, giữ số vé, persona, món; có test | – |
| S4.2-25 | Đạt | Reset xóa thẻ, tiêu chí, câu hỏi đang chờ; có test | – |
| S4.2-26 | Đạt | POST /assistant/hold gọi BookingAgentService.HoldCard, OnlineBookingService.Hold (SeatHoldService) rồi /booking/{id}?step=4; có test với DB | – |
| S7-05 | Đạt | NlpParserTests 25 test, hơn 30 câu, gồm 10 câu bắt buộc | – |
| S8.7-01 | Đạt | AiDemandScore, thuật toán xếp lịch, Gantt kéo thả và kiểm tra tức thì | – |
| S8.7-02 | Đạt | BookingAgentService, LocalAgent, LlmAgent, NlpParser hơn 20 câu, widget trong _LayoutCustomer | – |
| S4.3-01 | Đạt | Công thức trọng số ghi trong code, lưu vào Movies mỗi lần chạy; có test | – |
| S4.3-02 | Thiếu → Đạt | Lượt đầu: danh sách phim trong hộp chỉ lọc cho 1 ngày lúc tải trang, đổi ngày hoặc số ngày không lọc lại | Thêm GET /scheduler/ai/choices (SchedulerController.AiChoices); gantt.js lọc lại khi đổi "Từ ngày" hoặc "Số ngày", giữ lựa chọn cũ, cập nhật số phim xếp được. Kiểm lại: Đạt |
| S4.3-03 | Đạt | TargetCounts tỷ lệ với điểm, mỗi phim ít nhất 1, phim Việt từ 22%; có test | – |
| S4.3-04 | Đạt | Phòng theo sức chứa giảm dần; khung 18:00–22:00 ở phòng lớn ưu tiên phim điểm cao và phim Việt; có test | – |
| S4.3-05 | Đạt | Bắt đầu từ OperatingDayStart, nối CleaningEndTime, lệch giờ từ 10 phút; có test | – |
| S4.3-06 | Đạt | LanguageFor: P, K có DUBBED thì trước 18:00 DUBBED, từ 18:00 SUBTITLED; có test | – |
| S4.3-07 | Đạt | Mọi suất qua ScheduleConstraintChecker, bỏ suất có cảnh báo kết thúc muộn hoặc cùng phút; T18 trước 19:00 bị trừ điểm; có test | – |
| S4.3-08 | Đạt | Không sửa suất đã có; bản nháp viền nét đứt; ApplyAiDrafts trong transaction, IsAiGenerated = 1; có test | – |
| S4.3-09 | Đạt | Kéo bằng pointer, hít 5 phút; suất khóa hoặc đã bắt đầu không kéo được | – |
| S4.3-10 | Đạt | POST /scheduler/validate tính cả suất chưa lưu; viền đỏ, vàng, tooltip; chỉ lưu khi không có lỗi chặn | – |
| S4.3-11 | Đạt | Icon khóa, movable() trả false; server chặn SHOWTIME_LOCKED | – |
| U7.C1-06 | Đạt | Nút tròn 56px góc phải dưới trong layout khách; ảnh C1-guest-tro-ly | – |
| U7.C7-01 | Đạt | Widget và API chạy đủ luồng; ảnh C7-* | – |
| U7.C7-02 | Đạt | Panel 380 × 600px, sheet toàn màn hình dưới 576px, tiêu đề và nút đóng, Esc đóng và trả focus | – |
| U7.C7-03 | Đạt | Bong bóng, chip gợi ý, ô nhập và nút gửi, skeleton "Đang tìm suất phù hợp…" | – |
| U7.C7-04 | Đạt | Thẻ đặt vé đủ thành phần, ghế chọn màu đỏ, hai nút; ảnh C7-khach-the-dat-ve | – |
| U7.A5-03 | Đạt | Nút chuyển Bảng/Gantt và "AI tự động xếp lịch" | – |
| U7.A5-05 | Đạt | Trục 08:00–02:00, vạch 30 phút, nhãn mỗi giờ, cột trái 160px | – |
| U7.A5-06 | Đạt | Nền nhãn tuổi 20%, viền trái 4px, tên 1 dòng và giờ · ngôn ngữ, sọc quảng cáo và dọn phòng, icon khóa | – |
| U7.A5-07 | Đạt | Viền đỏ 2px kèm icon, viền vàng, tooltip lý do, nháp nét đứt với "Áp dụng" và "Bỏ" | – |
| U7.A5-08 | Đạt | Chân trang tỷ lệ phim Việt của ngày và của tuần | – |
| UC13 | Đạt | Hỏi, hỏi lại, chọn suất, dựng thẻ, giữ ghế qua SeatHoldService, sang bước thanh toán | – |
| UC35 | Thiếu → Đạt | Lượt đầu: như S4.3-02 | Như S4.3-02. Kiểm lại: Đạt |

Kết quả: Đạt
