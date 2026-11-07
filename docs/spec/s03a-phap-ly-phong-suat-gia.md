# Đặc tả FPolyCinema – mục 3.1 đến 3.4: pháp lý, phòng chiếu, suất chiếu, giá vé

Mục lục và cách đọc mã yêu cầu như [S3.6-04 P4]: docs/README.md. Tên chuẩn: docs/CONTRACTS.md. Giao diện: docs/ui.

## 3. Nghiệp vụ

### 3.1 Pháp lý (Luật Điện ảnh 2022, Nghị định 131/2022/NĐ-CP, Thông tư 05/2023/TT-BVHTTDL)
1. [S3.1-01 P3] Chỉ phim có số Giấy phép phân loại phim (Movies.ClassificationLicenseNo) và AgeRating khác C mới được tạo suất chiếu (LICENSE_MISSING, RATING_C). Phim C chỉ lưu hồ sơ.
2. [S3.1-02 P3] Nhãn độ tuổi (Điều 32 Luật Điện ảnh): T13, T16, T18 yêu cầu người xem đủ tuổi.
   - [S3.1-03 P4] POS: không bán vé CHILD_U13 cho phim T13 trở lên; không bán vé CHILD_13_15 cho phim T16, T18 (AGE_RESTRICTED).
   - [S3.1-04 P5] Online: tài khoản có ngày sinh mà chưa đủ tuổi thì chặn. Phim T13 trở lên luôn yêu cầu tích "Tôi xác nhận người xem đủ tuổi theo quy định" trước khi thanh toán (AGE_CONFIRM_REQUIRED).
   - [S3.1-05 P6] Máy soát vé hiện cảnh báo kiểm tra CCCD hoặc VNeID với phim T13 trở lên.
3. [S3.1-06 P3] Phim K: trẻ dưới 13 tuổi phải đi cùng cha mẹ hoặc người giám hộ.
   - [S3.1-07 P4] POS: hóa đơn có vé CHILD_U13 cho phim K phải có ít nhất một vé ADULT hoặc SENIOR, hoặc thu ngân tích "Có người giám hộ đi cùng" (GUARDIAN_REQUIRED).
   - [S3.1-08 P5] Online: hiện lưu ý và yêu cầu tích xác nhận.
4. Giờ chiếu cho trẻ em (Điều 9 khoản 4 NĐ 131/2022) áp theo tuổi người xem, không áp theo nhãn phim:
   - [S3.1-09 P3] Vé CHILD_U13 chỉ bán cho suất có EndTime trước ChildU13LatestEnd (22:00). Vé CHILD_13_15 chỉ bán cho suất có EndTime trước ChildU16LatestEnd (23:00) (CHILD_CURFEW).
   - [S3.1-10 P3] Suất bắt đầu trước 06:00 hoặc kết thúc sang ngày hôm sau luôn được coi là suất khuya.
   - [S3.1-11 P5] Online không bán vé trẻ em. Suất kết thúc từ 22:00 hiện lưu ý "Trẻ em dưới 13 tuổi không vào được suất này", suất kết thúc từ 23:00 thì ghi "dưới 16 tuổi".
   - [S3.1-12 P6] Máy soát vé hiện cảnh báo tương tự với các suất đó.
   - [S3.1-13 P3] Khi xếp lịch, phim K kết thúc từ 22:00 hoặc phim T13 kết thúc từ 23:00 chỉ bị cảnh báo vàng (vì khán giả chính sẽ không vào được), không bị chặn.
5. [S3.1-14 P1] Giảm giá vé theo luật (Điều 19 Luật Điện ảnh, Điều 10 NĐ 131/2022): mức giảm theo CONTRACTS mục 4, lưu ở bảng AudienceDiscounts.
   - [S3.1-15 P4] Các loại vé giảm theo luật chỉ bán tại quầy để nhân viên kiểm tra giấy tờ (luật áp dụng khi khách trực tiếp xem tại rạp).
   - [S3.1-16 P5] Các loại vé này bị ẩn trên web khách.
   - [S3.1-17 P3] Màn hình cấu hình không cho lưu mức thấp hơn mức tối thiểu.
6. [S3.1-18 P3] Hạn ngạch phim Việt (Điều 9 khoản 2, 3 NĐ 131/2022): từ 01/01/2026, phim Việt phải chiếm ít nhất 20% tổng số suất chiếu trong năm và được ưu tiên chiếu trong khung 18:00–22:00.
   - [S3.1-19 P4] Dashboard hiện tỷ lệ lũy kế từ đầu năm theo từng rạp và toàn chuỗi (chỉ tính suất không bị hủy), có vạch mốc 20%.
   - [S3.1-20 P4] Tỷ lệ phim Việt trong khung 18:00–22:00 hiện để tham khảo.
   - [S3.1-21 P7] Bộ xếp lịch nhắm tới VietnameseFilmTargetShare.
7. [S3.1-22 P2] Hiển thị phân loại (Thông tư 05/2023): nhãn độ tuổi kèm câu giải thích dùng chung partial _AgeBadge và phải hiện ở mọi nơi bán vé và hiển thị suất chiếu.
   - [S3.1-23 P4] POS: danh sách suất, hóa đơn tạm tính, vé in.
   - [S3.1-24 P5] Web khách: thẻ phim, trang phim, chọn suất, chọn ghế, thanh toán, vé điện tử. Trang phim hiện thêm cảnh báo nội dung (Movies.ContentWarnings).
   - [S3.1-25 P6] Máy soát vé và bảng sảnh.

### 3.2 Phòng chiếu và ghế
- [S3.2-01 P1] Sơ đồ là lưới TotalRows × TotalCols. RowIndex 1 là hàng gần màn hình nhất và mang chữ A. Ô không có bản ghi Seats là lối đi hoặc khoảng trống. Nhãn ghế gồm chữ hàng và số thứ tự ghế thật trong hàng (không đếm ô trống), ví dụ G7.
- [S3.2-02 P4] Đoạn ghế: dãy ghế liền nhau trong một hàng, ngăn cách bởi ô trống hoặc tường.
- [S3.2-03 P4] SWEETBOX luôn theo cặp cố định (PairGroupId): hai ghế liền nhau cùng hàng. Chọn, giữ, bán, đổi, hủy luôn đi cả cặp (SWEETBOX_PAIR); giá tính theo từng ghế.
- [S3.2-04 P4] WHEELCHAIR: khi chọn phải xác nhận "Vị trí dành cho khách dùng xe lăn" (WHEELCHAIR_CONFIRM_REQUIRED).
- IsHouseSeat (ghế nội bộ):
  - [S3.2-05 P3] Công cụ thiết kế phòng gợi ý HouseSeatsPerAuditorium ghế ở trung tâm.
  - [S3.2-06 P3] Khi tạo suất, các ghế này nhận trạng thái HOUSE_HOLD; khách thấy nhưng không chọn được.
  - [S3.2-07 P6] SupervisorOrManager giải phóng được ghế nội bộ của từng suất vào bất kỳ lúc nào; dashboard quản lý ca nhắc trước giờ chiếu HouseSeatReminderMinutes phút.
- [S3.2-08 P3] IsMaintenance (ghế bảo trì): Manager bật hoặc tắt trong công cụ thiết kế phòng.
  - [S3.2-09 P4] Ghế đang bảo trì không giữ được ở mọi suất.
  - [S3.2-10 P4] Nếu ghế đã được bán ở một suất tương lai, dashboard quản lý (UI A1) đưa ghế đó vào danh sách "Cần đổi ghế".
- [S3.2-11 P3] BaseViewScore được tính lại và lưu mỗi khi lưu sơ đồ (mục 4.1, persona BEST_VIEW_AUDIO).
- [S3.2-12 P3] Phòng đã có suất tương lai bán vé thì không được sửa cấu trúc; chỉ được bật tắt IsMaintenance.

### 3.3 Suất chiếu
- Các mốc thời gian:
  - [S3.3-01 P1] StartTime: mở cửa và bắt đầu quảng cáo, làm tròn theo bội số ScheduleRoundMinutes.
  - [S3.3-02 P1] FeatureStartTime = StartTime + AdsMinutes.
  - [S3.3-03 P1] EndTime = FeatureStartTime + DurationMinutes.
  - [S3.3-04 P1] CleaningEndTime = EndTime + CleaningMinutes, làm tròn lên bội số ScheduleRoundMinutes.
- Lỗi chặn:
  - [S3.3-05 P3] Chồng giờ trong cùng phòng, tức hai khoảng [StartTime, CleaningEndTime) giao nhau (SHOWTIME_OVERLAP); kiểm tra trong transaction khi lưu.
  - [S3.3-06 P3] Phim thiếu giấy phép hoặc là phim C.
  - [S3.3-07 P3] KDM không phủ trọn [FeatureStartTime, EndTime] (KDM_INVALID).
  - [S3.3-08 P3] StartTime ngoài khung OperatingDayStart đến LastShowStartLatest.
  - [S3.3-09 P3] LanguageFormat không nằm trong Movies.LanguageFormats.
- Cảnh báo, không chặn:
  - [S3.3-10 P3] Quá MaxSameMinuteStarts phòng trong cùng rạp bắt đầu cùng một phút.
  - [S3.3-11 P3] Phim K hoặc T13 kết thúc muộn (3.1 mục 4).
  - [S3.3-12 P3] KDM hết hạn trong 24 giờ tới mà vẫn còn suất sau thời điểm đó.
  - [S3.3-13 P3] Tỷ lệ phim Việt dưới ngưỡng.
- [S3.3-14 P3] Mọi kiểm tra nằm trong ScheduleConstraintChecker (logic thuần), dùng chung cho form, Gantt và bộ xếp lịch AI.
- [S3.3-15 P3] Tạo suất sẽ sinh ShowtimeSeats cho mọi ghế, ở trạng thái AVAILABLE hoặc HOUSE_HOLD.
- [S3.3-16 P3] Các suất chiếu do DbSeeder tạo ở Phase 1 đều qua ScheduleConstraintChecker mà không có lỗi chặn; có unit test kiểm tra điều này.
- [S3.3-17 P3] Suất đang LOCKED_HAS_TICKETS không đổi được giờ, phòng hay phim (SHOWTIME_LOCKED). Muốn dừng suất thì dùng hủy suất do sự cố (3.10).
- OperationalStatus do ShowtimeClockService cập nhật mỗi 30 giây:
  - [S3.3-18 P6] SCHEDULED chuyển BOARDING từ StartTime − BoardingLeadMinutes, nếu phòng đã sẵn sàng.
  - [S3.3-19 P6] ADS_PLAYING từ StartTime; FEATURE_PLAYING từ FeatureStartTime; CREDITS_ROLLING từ EndTime − CreditsMinutes; CLEANING_IN_PROGRESS từ EndTime.
  - [S3.3-20 P6] COMPLETED khi nhân viên bấm "Đã dọn xong".
  - [S3.3-21 P6] Phòng sẵn sàng khi suất trước trong phòng đã COMPLETED hoặc không có suất trước. Nếu phòng chưa sẵn sàng thì suất sau đứng ở SCHEDULED, và khi đã quá StartTime thì dashboard quản lý ca báo đỏ. Khi phòng sẵn sàng, suất nhảy thẳng tới trạng thái đúng với giờ hiện tại.
  - [S3.3-22 P6] Mỗi lần đổi trạng thái đều phát SHOWTIME_OPS_CHANGED.

### 3.4 Giá vé
```
Gốc       = BASE + DAY_TYPE + TIME_SLOT + SEAT_TYPE + FORMAT + (phim bom tấn ? BLOCKBUSTER : 0)
ListPrice = làm tròn theo PriceRoundTo( Gốc × (100 − % giảm của AudienceType) / 100 ), không âm
```
- [S3.4-01 P1] PricingRules lưu số tiền theo từng rạp; có thể âm (ví dụ HAPPY_TUESDAY = −20.000).
- [S3.4-02 P1] DAY_TYPE tính theo ngày vận hành: HOLIDAY nếu ngày có trong Holidays; nếu không thì HAPPY_TUESDAY vào thứ Ba, WEEKEND vào thứ Bảy và Chủ nhật, còn lại là WEEKDAY.
- [S3.4-03 P1] TIME_SLOT tính theo FeatureStartTime: trước PriceSlotAfternoonStart là MORNING, trước PriceSlotGoldenStart là AFTERNOON, trước PriceSlotLateStart là GOLDEN, còn lại là LATE. Suất trước 06:00 là LATE.
- [S3.4-04 P1] Làm tròn đến bội số 1.000 gần nhất; 500 thì làm tròn lên.
- [S3.4-05 P4] Voucher và điểm giảm ở cấp hóa đơn, rồi được phân bổ theo tỷ lệ giá trị cho từng vé và từng dòng bắp nước (phần dư dồn vào dòng cuối). Kết quả lưu ở Tickets.FinalPrice và BillDetails.LineFinalAmount. Ví là phương thức thanh toán, không phải giảm giá.
- [S3.4-06 P1] Giá đã gồm VAT. Khi bán, mỗi vé lưu VatRate = TicketVatPercent, mỗi dòng bắp nước lưu VatRate = FnbVatPercent. Tiền trước thuế = làm tròn(FinalPrice × 100 / (100 + VatRate)). Áp dụng cả cho hóa đơn trong dữ liệu mẫu.
- [S3.4-07 P1] PricingCalculator là logic thuần, có unit test cho cả 6 chiều và các đối tượng vé.
