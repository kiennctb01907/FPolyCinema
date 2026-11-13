# Kiểm tra độc lập – Phase 3

Agent spec-auditor kiểm ba nhóm mã (18 mã mỗi nhóm, theo thứ tự của lệnh `list 3`), đọc spec, truy vết, code thật, chạy test liên quan và xem ảnh chụp giao diện trong artifacts/ui/phase-3. Ngày 13/11/2026.
Cột "Đã sửa" ghi việc đã làm cho các dòng Thiếu hoặc Sai và kết quả của lượt kiểm lại.

| Mã | Kết luận | Lý do | Đã sửa |
|---|---|---|---|
| S2.6-06 | Đạt | Form Phim có vùng kéo thả gắn /File/Upload, /File/Delete, gửi bằng fetch qua ui.api; Xóa poster xóa ảnh vừa tải; lưu xong xóa poster cũ không còn dùng | – |
| S2.7-05 | Đạt | Lưu bảng giá và mức giảm ghi AuditLogs PRICING_UPDATED kèm từng dòng đổi, trong DBUtil.InTransaction; hằng có trong CONTRACTS C3-39 | – |
| S3.1-01 | Đạt | AgeRules.CanSchedule trả RATING_C, LICENSE_MISSING; checker coi là lỗi chặn; Save ném lỗi và kiểm lại trong transaction | – |
| S3.1-02 | Đạt | MinimumAge 13/16/18, CanWatch, CheckTicket trả AGE_RESTRICTED; có unit test | – |
| S3.1-06 | Đạt | CheckGuardian: phim K có CHILD_U13 cần vé ADULT/SENIOR hoặc xác nhận, nếu không GUARDIAN_REQUIRED | – |
| S3.1-09 | Đạt | CheckTicket dùng ChildU13LatestEnd, ChildU16LatestEnd với "kết thúc trước", trả CHILD_CURFEW, áp theo tuổi người xem | – |
| S3.1-10 | Đạt | IsLateShow: trước 06:00 hoặc kết thúc sang hôm sau; EndsBefore luôn coi suất khuya là muộn | – |
| S3.1-13 | Đạt | ScheduleWarning chỉ là cảnh báo cho K từ 22:00, T13 từ 23:00; hiện vàng ở form và bảng | – |
| S3.1-17 | Đạt | SaveDiscounts chặn dưới LegalMinPercent, lời nhắn gắn đúng ô; trang có cột "Tối thiểu theo luật" | – |
| S3.1-18 | Đạt | VietnameseFilmQuota từ 01/01/2026, 20% và khung 18:00–22:00 từ cấu hình; đếm bỏ suất hủy; lịch chiếu hiện tỷ lệ ngày, khung, năm | – |
| S3.2-05 | Đạt | SuggestHouseSeats chọn 4 ghế thường/VIP không bảo trì ở hàng gần r_ideal, đoạn gần giữa | – |
| S3.2-06 | Đạt | CreateForShowtime gán HOUSE_HOLD cho ghế nội bộ; _SeatMap hiện nhưng khóa nút | – |
| S3.2-08 | Đạt | Công cụ Bảo trì trong trình thiết kế (ManagerOnly), SaveLayout lưu IsMaintenance | – |
| S3.2-11 | Đạt | Cả hai nhánh SaveLayout tính lại BaseViewScore bằng SeatScoring BEST_VIEW_AUDIO | – |
| S3.2-12 | Đạt | IsStructuralChange bỏ qua bảo trì; phòng có suất tương lai đã bán bị chặn đổi cấu trúc; giao diện chỉ mở công cụ Bảo trì | – |
| S3.14-03 | Đạt | DeleteRules.Movie chặn khi có suất hoặc vé, gợi ý ngưng chiếu; kiểm lại trong transaction | – |
| S3.14-05 | Đạt | DeleteRules.Auditorium chặn khi có suất; xóa phòng kèm ghế; ghế đã có vé không xóa khỏi sơ đồ | – |
| S7-02 | Đạt | 7 test AgeRules và 11 test ScheduleConstraintChecker trong Phase3RulesTests | – |
| S3.3-05 | Đạt | Overlaps nửa mở cùng phòng, SHOWTIME_OVERLAP; Save khóa UPDLOCK, HOLDLOCK rồi kiểm lại trong transaction | – |
| S3.3-06 | Đạt | Checker gọi AgeRules.CanSchedule (RATING_C, LICENSE_MISSING) | – |
| S3.3-07 | Đạt | KdmCovers đòi KDM phủ [FeatureStartTime, EndTime]; ba lời nhắn; KDM_INVALID | – |
| S3.3-08 | Đạt | InOperatingWindow 08:00–23:30 từ CinemaRules, VALIDATION_FAILED trên ô StartTime; test biên | – |
| S3.3-09 | Đạt | Chặn LanguageFormat ngoài Movies.LanguageFormats; form chỉ mở bản hợp lệ | – |
| S3.3-10 | Đạt | Đếm phòng khác cùng phút, chỉ cảnh báo khi vượt MaxSameMinuteStarts | – |
| S3.3-11 | Đạt | AgeRules.ScheduleWarning, chỉ cảnh báo | – |
| S3.3-12 | Đạt | Hạn KDM trong (Now, Now+24h] và còn suất sau hạn thì cảnh báo; test ba nhánh | – |
| S3.3-13 | Đạt | Tỷ lệ lũy kế năm của rạp, trừ suất gốc khi sửa; cảnh báo dưới VietnameseFilmMinShare | – |
| S3.3-14 | Đạt | ScheduleConstraintChecker thuần; form, lưu, bảng lịch đều gọi Check; Gantt, AI là Phase 7 | – |
| S3.3-15 | Đạt | CreateForShowtime cho mọi ghế trong transaction; test 40 ghế (2 HOUSE_HOLD, 38 AVAILABLE) | – |
| S3.3-16 | Đạt | Test chạy checker cho hơn 1000 suất của DbSeeder, không có lỗi chặn | – |
| S3.3-17 | Đạt | Suất khóa đổi giờ, phòng, phim báo SHOWTIME_LOCKED; form khóa ô; không xóa suất đã bán | – |
| C3-37 | Đạt | Cờ tính khi đọc (BOOKED và HELD còn hạn), ShowtimeLock.IsLocked; test cả giữ còn hạn và hết hạn | – |
| C8-06 | Đạt | Đủ 8 hằng mã lỗi trong ErrorCodes | – |
| U9-11 | Đạt | Lời nhắn KDM_INVALID đúng mẫu, ngày giờ dd/MM/yyyy HH:mm; test so nguyên câu | – |
| U9-12 | Đạt | Lời nhắn SHOWTIME_OVERLAP đúng mẫu; test so nguyên câu | – |
| U9-13 | Đạt | Lời nhắn SHOWTIME_LOCKED trùng nguyên văn | – |
| S8.3-01 | Đạt | MoviesController đủ chức năng, poster qua FileController, chia doanh thu và KDM trong một transaction; DistributorsController ManagerOnly | – |
| S8.3-02 | Đạt | AuditoriumsController CRUD, Design, Preview, SaveLayout; BaseViewScore, ghế nội bộ, bảo trì | – |
| S8.3-03 | Đạt | Bảng giá 6 chiều cho phép âm, AuditLogs; chặn mức dưới tối thiểu; ngày lễ; trang thử tính giá | – |
| S8.3-04 | Đạt | /scheduler chế độ Bảng và form kiểm tra trực tiếp; khóa khi lưu; sinh ShowtimeSeats | – |
| U2-06 | Đạt | HeatLevel đúng mốc; heat-4, heat-5 chữ --ink, còn lại chữ trắng; ghế đang chọn giữ --seat-selected | – |
| U7.A3-04 | Đạt | Dropzone ratio-2x3 dùng chung initDropzone, lời nhắc đổi sang "poster"; ảnh A3-manager-phim | – |
| U7.A4-01 | Đạt | Route /admin/auditoriums/{id}/design, bố cục ba cột; ảnh A4-manager | – |
| U7.A4-02 | Đạt | Đủ 7 công cụ; phòng khóa chỉ dùng Bảo trì | – |
| U7.A4-03 | Đạt | Bấm hoặc kéo để tô, có bàn phím | – |
| U7.A4-04 | Đạt | Thống kê theo loại, sức chứa bán được; công tắc heatmap kèm chú giải | – |
| U7.A4-05 | Đạt | Nút Tự đánh số và Gợi ý ghế nội bộ (4 ghế) | – |
| U7.A5-01 | Đạt | [Route("scheduler")], ManagerOnly; ảnh A5-manager | – |
| U7.A5-02 | Đạt | Chọn rạp, ‹ Hôm nay ›, ô chọn ngày | – |
| U7.A5-04 | Đạt | Mỗi phòng một nhóm; dòng có giờ, phim, nhãn tuổi, ngôn ngữ, đã bán, thao tác | – |
| UC31 | Đạt | A2, A3 phim và nhà phát hành; KDM, chia doanh thu; phân quyền đúng | – |
| UC32 | Đạt | Danh sách, form, công cụ A4; khóa cấu trúc; quy tắc xóa | – |
| UC33 | Đạt | Bảng giá, đối tượng, ngày lễ, thử tính giá; kiểm ở server | – |
| UC34 | Đạt | A5 Bảng, form thêm sửa, kiểm tra trực tiếp, xóa suất chưa bán | – |

Tổng: 54 Đạt, 0 Thiếu, 0 Sai (lượt đầu).

Kết quả: Đạt
