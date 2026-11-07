# Truy vết yêu cầu – Phase 3 – Phim, phòng chiếu, giá, suất chiếu

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
| S2.6-06 | Form Phim gọi Upload và Delete của FileController bằng fetch. | Chưa |  |  |
| S2.7-05 | Sửa bảng giá. | Chưa |  |  |
| S3.1-01 | Chỉ phim có số Giấy phép phân loại phim (Movies.ClassificationLicenseNo) và AgeRating khác C mới được tạo suất chiếu (L… | Chưa |  |  |
| S3.1-02 | Nhãn độ tuổi (Điều 32 Luật Điện ảnh): T13, T16, T18 yêu cầu người xem đủ tuổi. | Chưa |  |  |
| S3.1-06 | Phim K: trẻ dưới 13 tuổi phải đi cùng cha mẹ hoặc người giám hộ. | Chưa |  |  |
| S3.1-09 | Vé CHILD_U13 chỉ bán cho suất có EndTime trước ChildU13LatestEnd (22:00). Vé CHILD_13_15 chỉ bán cho suất có EndTime tr… | Chưa |  |  |
| S3.1-10 | Suất bắt đầu trước 06:00 hoặc kết thúc sang ngày hôm sau luôn được coi là suất khuya. | Chưa |  |  |
| S3.1-13 | Khi xếp lịch, phim K kết thúc từ 22:00 hoặc phim T13 kết thúc từ 23:00 chỉ bị cảnh báo vàng (vì khán giả chính sẽ không… | Chưa |  |  |
| S3.1-17 | Màn hình cấu hình không cho lưu mức thấp hơn mức tối thiểu. | Chưa |  |  |
| S3.1-18 | Hạn ngạch phim Việt (Điều 9 khoản 2, 3 NĐ 131/2022): từ 01/01/2026, phim Việt phải chiếm ít nhất 20% tổng số suất chiếu… | Chưa |  |  |
| S3.2-05 | Công cụ thiết kế phòng gợi ý HouseSeatsPerAuditorium ghế ở trung tâm. | Chưa |  |  |
| S3.2-06 | Khi tạo suất, các ghế này nhận trạng thái HOUSE_HOLD; khách thấy nhưng không chọn được. | Chưa |  |  |
| S3.2-08 | IsMaintenance (ghế bảo trì): Manager bật hoặc tắt trong công cụ thiết kế phòng. | Chưa |  |  |
| S3.2-11 | BaseViewScore được tính lại và lưu mỗi khi lưu sơ đồ (mục 4.1, persona BEST_VIEW_AUDIO). | Chưa |  |  |
| S3.2-12 | Phòng đã có suất tương lai bán vé thì không được sửa cấu trúc; chỉ được bật tắt IsMaintenance. | Chưa |  |  |
| S3.3-05 | Chồng giờ trong cùng phòng, tức hai khoảng [StartTime, CleaningEndTime) giao nhau (SHOWTIME_OVERLAP); kiểm tra trong tr… | Chưa |  |  |
| S3.3-06 | Phim thiếu giấy phép hoặc là phim C. | Chưa |  |  |
| S3.3-07 | KDM không phủ trọn [FeatureStartTime, EndTime] (KDM_INVALID). | Chưa |  |  |
| S3.3-08 | StartTime ngoài khung OperatingDayStart đến LastShowStartLatest. | Chưa |  |  |
| S3.3-09 | LanguageFormat không nằm trong Movies.LanguageFormats. | Chưa |  |  |
| S3.3-10 | Quá MaxSameMinuteStarts phòng trong cùng rạp bắt đầu cùng một phút. | Chưa |  |  |
| S3.3-11 | Phim K hoặc T13 kết thúc muộn (3.1 mục 4). | Chưa |  |  |
| S3.3-12 | KDM hết hạn trong 24 giờ tới mà vẫn còn suất sau thời điểm đó. | Chưa |  |  |
| S3.3-13 | Tỷ lệ phim Việt dưới ngưỡng. | Chưa |  |  |
| S3.3-14 | Mọi kiểm tra nằm trong ScheduleConstraintChecker (logic thuần), dùng chung cho form, Gantt và bộ xếp lịch AI. | Chưa |  |  |
| S3.3-15 | Tạo suất sẽ sinh ShowtimeSeats cho mọi ghế, ở trạng thái AVAILABLE hoặc HOUSE_HOLD. | Chưa |  |  |
| S3.3-16 | Các suất chiếu do DbSeeder tạo ở Phase 1 đều qua ScheduleConstraintChecker mà không có lỗi chặn; có unit test kiểm tra … | Chưa |  |  |
| S3.3-17 | Suất đang LOCKED_HAS_TICKETS không đổi được giờ, phòng hay phim (SHOWTIME_LOCKED). Muốn dừng suất thì dùng hủy suất do … | Chưa |  |  |
| S3.14-03 | Phim: chỉ xóa cứng khi chưa có suất chiếu hay vé. Nếu đã có thì chỉ được ngưng (Active = 0). | Chưa |  |  |
| S3.14-05 | Phòng chiếu: chỉ xóa khi chưa có suất chiếu nào. Ghế: theo 3.2. | Chưa |  |  |
| S7-02 | AgeRules (độ tuổi, giờ chiếu cho trẻ em, người giám hộ), ScheduleConstraintChecker. | Chưa |  |  |
| S8.3-01 | MoviesController (giấy phép, nhãn tuổi, cảnh báo nội dung, ngôn ngữ, poster qua FileController), DistributorsController… | Chưa |  |  |
| S8.3-02 | AuditoriumsController với công cụ thiết kế sơ đồ, BaseViewScore, ghế nội bộ, ghế bảo trì. | Chưa |  |  |
| S8.3-03 | Bảng giá 6 chiều, AudienceDiscounts (chặn mức dưới tối thiểu), Holidays, kèm trang thử tính giá. | Chưa |  |  |
| S8.3-04 | Quản lý suất chiếu: chế độ bảng theo ngày và form (UI A5, chế độ bảng), dùng ScheduleConstraintChecker và AgeRules; tạo… | Chưa |  |  |
| U2-06 | Thang heatmap (--heat-1 đến --heat-5) tương ứng các mức điểm 0–39, 40–59, 60–74, 75–89, 90–100. Hai mức sáng nhất dùng … | Chưa |  |  |
| U7.A3-04 | Vùng kéo thả poster phim tỉ lệ 2:3, cùng cách hoạt động. | Chưa |  |  |
| U7.A4-01 | A4. Thiết kế phòng chiếu | Chưa |  |  |
| U7.A4-02 | Trái là công cụ tô: ghế thường, VIP, cặp sweetbox, xe lăn, ô trống hoặc lối đi, ghế nội bộ, bảo trì. | Chưa |  |  |
| U7.A4-03 | Giữa là lưới chỉnh sửa: bấm hoặc kéo để tô. | Chưa |  |  |
| U7.A4-04 | Phải là thống kê (số ghế theo loại, sức chứa) và công tắc xem heatmap BaseViewScore. | Chưa |  |  |
| U7.A4-05 | Có nút "Tự đánh số" và "Gợi ý ghế nội bộ". | Chưa |  |  |
| U7.A5-01 | A5. Lịch chiếu /scheduler | Chưa |  |  |
| U7.A5-02 | Thanh trên: chọn rạp, chọn ngày (‹ Hôm nay ›). | Chưa |  |  |
| U7.A5-04 | Chế độ Bảng: mỗi phòng một nhóm, các suất xếp theo giờ, mỗi dòng có giờ, phim, nhãn tuổi, ngôn ngữ, số ghế đã bán, thao… | Chưa |  |  |
| U9-11 | KDM_INVALID: "Bản quyền chiếu (KDM) của phim này tại rạp chỉ có hiệu lực đến 12/11/2026 23:59. Chọn giờ trong khoảng hi… | Chưa |  |  |
| U9-12 | SHOWTIME_OVERLAP: "Phòng 2 đã có suất 19:00–21:25 (gồm thời gian dọn phòng). Suất mới phải bắt đầu từ 21:25." | Chưa |  |  |
| U9-13 | SHOWTIME_LOCKED: "Suất này đã có khách mua vé nên không đổi giờ được. Nếu cần, dùng chức năng Hủy suất do sự cố." | Chưa |  |  |
| C3-37 | LOCKED_HAS_TICKETS: suất có ít nhất một ghế BOOKED hoặc HELD còn hạn. | Chưa |  |  |
| UC31 | Quản lý phim, nhà phát hành, KDM, chia doanh thu (màn hình A2, A3) | Chưa |  |  |
| UC32 | Quản lý phòng chiếu và sơ đồ ghế (màn hình A4) | Chưa |  |  |
| UC33 | Quản lý bảng giá, đối tượng ưu đãi, ngày lễ (màn hình A2, A3) | Chưa |  |  |
| UC34 | Quản lý suất chiếu (màn hình A5) | Chưa |  |  |
