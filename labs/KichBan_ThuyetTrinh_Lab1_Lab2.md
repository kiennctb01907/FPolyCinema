# Kịch bản thuyết trình – Lý thuyết Lab 1, Lab 2 và cách thiết kế ERD

Thời lượng dự kiến: 9–10 phút (khoảng 1 phút mỗi slide, slide 6 và 7 dài hơn vì là trọng tâm).
Slide trên Canva: https://canva.link/8tfr3pg3les1869 (kịch bản này cũng nằm trong phần ghi chú người thuyết trình của từng slide).

---

## Slide 1 – Bìa (khoảng 30 giây)

Em xin chào thầy cô và các bạn. Hôm nay em trình bày phần lý thuyết của Lab 1 và Lab 2 trong đồ án FPolyCinema – website quản lý chuỗi rạp chiếu phim.

Lab 1 là đặc tả yêu cầu và use case: hệ thống cần làm gì và ai dùng. Lab 2 là thiết kế ERD và luồng giao diện: dữ liệu được tổ chức thế nào và người dùng đi qua các màn hình ra sao.

Dự án dùng ASP.NET Core MVC trên .NET 8, truy cập dữ liệu bằng ADO.NET, cơ sở dữ liệu SQL Server và SignalR để cập nhật thời gian thực. Trọng tâm phần trình bày hôm nay là cách em thiết kế ERD.

## Slide 2 – Lab 1: Bài toán và giải pháp (khoảng 1 phút)

Khách hàng của đề tài là một chuỗi rạp vừa và nhỏ, hiện bán vé thủ công hoặc bằng nhiều phần mềm rời rạc. Em tóm lại năm vấn đề chính và cách hệ thống giải quyết:

- Thứ nhất, quầy và kênh online có thể bán trùng một ghế. Hệ thống giữ ghế 10 phút theo hóa đơn và khóa theo suất chiếu trong transaction, nên hai người không thể giữ cùng một ghế.
- Thứ hai, khách chọn ghế để lại một ghế lẻ giữa hàng rất khó bán. Hệ thống có luật ghế mồ côi và gợi ý vị trí hợp lệ.
- Thứ ba là yêu cầu pháp lý: bán vé phải đúng nhãn độ tuổi và giờ chiếu cho trẻ em theo Luật Điện ảnh 2022.
- Thứ tư, để chống gian lận, thu ngân không tự hủy được hóa đơn đã thu tiền; phải có quản lý duyệt bằng PIN.
- Thứ năm, kho bắp nước được kiểm soát theo định mức nguyên liệu, gọi là BOM.

Mọi quy tắc này đều kiểm tra ở server. Phạm vi hệ thống gồm 40 use case, từ UC01 đến UC40.

## Slide 3 – Lab 1: Tác nhân và sơ đồ use case (khoảng 1 phút 15 giây)

Bên trái là các tác nhân. Hệ thống có năm vai trò đăng nhập cộng với khách vãng lai, và hai tác nhân ngoài là cổng thanh toán mô phỏng và dịch vụ email.

Các tác nhân có quan hệ kế thừa: Khách hàng kế thừa Khách vãng lai; Quản lý ca kế thừa cả Thu ngân và Nhân viên soát vé; Quản lý rạp kế thừa Quản lý ca. Kế thừa nghĩa là tác nhân con có toàn bộ use case của tác nhân cha, nhờ vậy sơ đồ đỡ rối vì không phải vẽ lại.

Bên phải là hai quan hệ dễ nhầm nhất:
- Include là use case gốc luôn thực hiện use case được gộp. Ví dụ UC07 Đặt vé online lúc nào cũng gồm UC08 Chọn ghế và UC09 Thanh toán. Mũi tên đi từ use case gốc sang use case được gộp.
- Extend là use case mở rộng chỉ xảy ra trong một số điều kiện. Ví dụ UC02 Quên mật khẩu chỉ xảy ra khi người dùng không đăng nhập được, nên nó mở rộng UC01 Đăng nhập. Mũi tên đi từ use case mở rộng về use case gốc.

40 use case được chia thành 10 nhóm chức năng, và phần đặc tả yêu cầu viết theo mẫu IEEE 830.

## Slide 4 – Lab 1: Quy trình bán hàng hai kênh (khoảng 1 phút 15 giây)

Hệ thống có hai kênh bán.

Tại quầy: thu ngân phải mở ca trước, nhập tiền đầu ca. Sau đó chọn suất chiếu, chọn ghế, chọn đối tượng vé và bắp nước, thu tiền rồi in vé giấy.

Online: khách chọn suất, chọn ghế thì ghế được giữ 10 phút, thêm bắp nước và voucher nếu có, thanh toán bằng QR. Cổng thanh toán gọi lại webhook có chữ ký để xác nhận, rồi khách nhận vé điện tử.

Điểm quan trọng ở giữa là hai kênh dùng chung một cơ chế giữ ghế và một máy trạng thái hóa đơn. Hóa đơn có ba trạng thái: waiting khi đang giữ ghế, finish khi đã thanh toán, cancel khi bị hủy. Vì vậy một ghế chỉ bán được một lần.

Khi vi phạm quy tắc, hệ thống trả về mã lỗi rõ ràng, ví dụ SEAT_TAKEN khi ghế đã có người giữ, ORPHAN_SEAT khi để lại ghế lẻ, AGE_RESTRICTED khi sai độ tuổi, SHIFT_NOT_OPEN khi chưa mở ca.

## Slide 5 – Lab 2: Mô hình công nghệ ba lớp (khoảng 1 phút)

Sang Lab 2, đầu tiên là mô hình công nghệ. Hệ thống theo mô hình ba lớp, đi từ trên xuống:

- Trình duyệt gửi yêu cầu HTTP tới Controller của ASP.NET Core MVC.
- Controller không truy cập dữ liệu trực tiếp mà gọi Service và các quy tắc nghiệp vụ ở Domain Rules.
- Service gọi lớp DAL; DAL dùng Utils.DBUtil, tức ADO.NET, để làm việc với SQL Server.

Bên trái là SignalR Hub CinemaHub, dùng để cập nhật thời gian thực, ví dụ sơ đồ ghế đổi ngay khi người khác vừa chọn.

Dự án không dùng Entity Framework; mọi câu SQL đều dùng tham số @1, @2 để chống SQL Injection. Cơ sở dữ liệu có 35 bảng, 60 khóa ngoại và 4 stored procedure. Các dịch vụ ngoài gồm AI gợi ý ghế và xếp lịch, VietQR mô phỏng và SMTP để gửi email.

## Slide 6 – Cách thiết kế ERD: 5 bước (khoảng 1 phút 45 giây – trọng tâm)

Đây là phần trọng tâm: em thiết kế ERD qua năm bước.

Bước 1, tìm thực thể. Em đọc lại use case và mô tả nghiệp vụ ở Lab 1, gạch chân các danh từ quan trọng như phim, suất chiếu, phòng, ghế, hóa đơn, vé. Danh từ nào cần lưu nhiều bản ghi và có thuộc tính riêng thì thành một thực thể.

Bước 2, xác định khóa chính. Phần lớn bảng dùng cột Id tự tăng. Có trường hợp đặc biệt là ShowtimeSeats – trạng thái của từng ghế trong từng suất – dùng khóa ghép gồm ShowtimeId và SeatId, vì một ghế trong một suất chỉ có đúng một dòng.

Bước 3, xác định mối liên kết và bản số. Với mỗi cặp thực thể, em hỏi: một bản ghi bên này liên quan tới bao nhiêu bản ghi bên kia? Ví dụ một loại đồ uống có nhiều đồ uống, nhưng mỗi đồ uống chỉ thuộc một loại, nên bản số là 1 – 0..n. Nếu khóa ngoại được phép NULL thì phía cha ghi 0..1.

Bước 4, tách quan hệ nhiều – nhiều bằng bảng trung gian. Ví dụ một món bắp nước dùng nhiều nguyên liệu, và một nguyên liệu dùng cho nhiều món. Em tạo bảng ProductRecipes nối Drinks và RawMaterials, đồng thời lưu thêm định lượng.

Bước 5, chia phân hệ rồi vẽ ERD mức 1 trước, mức 2 sau. Cuối cùng em đối chiếu sơ đồ với schema sinh từ script SQL để chắc số bảng và số khóa ngoại khớp với code thật.

## Slide 7 – ERD mức 1 và mức 2, 8 phân hệ (khoảng 1 phút 30 giây)

Kết quả là hai mức sơ đồ.

ERD mức 1 là sơ đồ khái niệm: chỉ có tên thực thể và đường quan hệ, chưa có thuộc tính. Sơ đồ có 57 đường cho 60 khóa ngoại, vì ba cặp bảng có hai khóa ngoại cùng trỏ vào nhau nên được vẽ gộp một đường.

ERD mức 2 là sơ đồ logic: đủ thuộc tính, đánh dấu khóa chính, khóa ngoại, kiểu dữ liệu, NOT NULL, UNIQUE, CHECK, và dùng ký hiệu chân chim cho bản số.

Ở giữa là ba bảng trung tâm: Users, Cinemas và Bills. Rất nhiều bảng khác nối vào ba bảng này. Vì vậy nếu vẽ chung một trang thì sơ đồ rất rối; em chia 35 bảng thành 8 phân hệ, mỗi phân hệ một trang mức 2: tài khoản và nhân sự 2 bảng; bắp nước và kho 7 bảng; phim và nhà phát hành 4 bảng; rạp, phòng, ghế, suất chiếu, giá 8 bảng; bán hàng và thanh toán 5 bảng; khách hàng thân thiết 2 bảng; vận hành và phê duyệt 4 bảng; hệ thống 3 bảng. Cộng lại đúng 35 bảng, mỗi bảng chỉ vẽ đầy đủ một lần.

## Slide 8 – Điểm đáng chú ý và kết luận (khoảng 1 phút 15 giây)

Cuối cùng là vài điểm đáng chú ý khi thiết kế.

Về ERD:
- Khóa ngoại cho phép NULL thì vẽ 0..1. Ví dụ Users.CinemaId: nhân viên thuộc một rạp, còn khách hàng không thuộc rạp nào nên để NULL.
- Có bảng có hai khóa ngoại cùng trỏ một bảng: Bills.UserId là người tạo hóa đơn, Bills.CustomerId là khách được tích điểm.
- Có quan hệ tự tham chiếu: Tickets.SwappedFromTicketId cho biết vé mới được đổi từ vé cũ nào.

Về luồng giao diện, em chia thành bốn nhóm: Web khách, Quầy và vận hành, Quản trị, và Trang hệ thống.

Sau khi vẽ, em dùng AI để rà soát sơ đồ và tự đối chiếu lại đủ 35 bảng và 60 khóa ngoại với schema.

Phần trình bày của em đến đây là hết. Em cảm ơn thầy cô và các bạn đã lắng nghe, em xin mời mọi người đặt câu hỏi.

---

## Câu hỏi có thể gặp và gợi ý trả lời

- **Vì sao không dùng Entity Framework?** Yêu cầu môn học là ADO.NET với DBUtil và DAL; tự viết SQL có tham số giúp kiểm soát câu lệnh và hiểu rõ cách truy cập dữ liệu.
- **Include khác extend thế nào?** Include luôn xảy ra (bắt buộc), extend chỉ xảy ra khi có điều kiện (tùy chọn). Chiều mũi tên ngược nhau.
- **Làm sao chống bán trùng ghế khi hai người bấm cùng lúc?** Giữ ghế chạy trong transaction, khóa theo suất chiếu (UPDLOCK, HOLDLOCK); người thứ hai nhận lỗi SEAT_TAKEN.
- **Vì sao ShowtimeSeats dùng khóa ghép?** Mỗi ghế trong mỗi suất chỉ có một trạng thái, nên cặp (ShowtimeId, SeatId) đã là duy nhất, không cần thêm Id.
- **Vì sao 57 đường cho 60 khóa ngoại?** Ba cặp bảng (Bills–Users, WastageLogs–Users, SupervisorApprovals–Users) có hai khóa ngoại cùng trỏ nhau, được vẽ gộp ở mức 1.
