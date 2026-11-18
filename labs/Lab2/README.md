# Lab 2 – ERD, bảng thực thể, luồng giao diện (FPolyCinema)

- Tài liệu nộp: `TB01907_Lab2_ERD_GiaoDien.docx` (60 trang; mục lục tự động, đã cập nhật bằng Word).
- Ảnh sơ đồ: `diagrams/Hinh01…Hinh14*.png`, xuất từ Lucidchart.
- Nguồn dữ kiện: `docs/generated` mã băm code 4788f79b97b4 (đối chiếu lại 18/11/2026, chỉ số test đổi 369 → 372) (35 bảng, 60 khóa ngoại, 30 controller, 221 route, 28 màn hình).
- Gói nộp LMS: `TB01907_Lab2.zip` (docx + thư mục diagrams).

## Lịch sử cập nhật

- 08/11/2026: bản đầu, viết khi mới xong Phase 1 (luồng giao diện có khối xám cho màn hình chưa làm).
- 15/11/2026: cập nhật theo code sau Phase 8.
  - Vẽ lại Hình 1 (mô hình công nghệ: SignalR Hub, background service đã có) và Hình 11–14 (luồng giao diện: route và quyền thật, thêm A10, RoleRouting hiện tại).
  - Vẽ lại ERD mức 2 trang P1, P3, P4, P5. Bản cũ có vài đường quan hệ gắn nhầm dòng cột. P4 thêm cột ShowtimeSeats.HoldAudienceType, P5 thêm cột Bills.HoldExtended. P4, P5 nối đường ở mức bảng.
  - Mục 3.2: thêm 2 cột trên vào bảng thuộc tính.
  - Bảng 4: chèn ảnh chụp thật của 28 màn hình (artifacts/ui/phase-8), thêm A10.
  - Bảng 5: route và quyền theo routes.md, thêm A10.
  - Số liệu ở mục 1, 2, 5 đã cập nhật.
- 18/11/2026: vẽ lại Hình 2 (ERD mức 1) cho gọn: gom bảng theo 8 khung phân hệ, đi dây vuông góc, dùng ô bảng tham chiếu nét đứt (Users, Cinemas, Showtimes, CorporateBookings) thay cho đường dài; vẫn đủ 35 bảng, 57 đường, chỉ còn 1 chỗ cắt. Hình 2 giờ sinh bằng code: `diagrams/Hinh02_gen.mjs` -> `Hinh02_ERD_Muc1_TongQuan.svg/.png` (không còn dùng bản Lucid). Sửa đoạn mô tả ở mục 3.1.3, cập nhật mục lục.

## Sơ đồ gốc trên Lucidchart

| Hình | Nội dung | Link sửa |
|---|---|---|
| 1 | Mô hình công nghệ (trang 1) | https://lucid.app/lucidchart/d7ee25d1-ef06-4e46-b319-12feda24e855/edit |
| 2 | ERD mức 1 – tổng quan | https://lucid.app/lucidchart/970fbca5-d53c-44a8-8d13-a6a724511018/edit |
| 3 | ERD mức 2 – P1 Tài khoản và nhân sự | https://lucid.app/lucidchart/bceb7112-d8b3-4488-88c0-5b174138c34e/edit |
| 4 | ERD mức 2 – P2 Bắp nước và kho | https://lucid.app/lucidchart/d9429208-f5a0-425b-ba47-895256fde3f6/edit |
| 5 | ERD mức 2 – P3 Phim và nhà phát hành | https://lucid.app/lucidchart/f953a7a9-e563-44ae-a8b2-82df673b9897/edit |
| 6 | ERD mức 2 – P4 Rạp, phòng, ghế, suất, giá | https://lucid.app/lucidchart/6e32b96d-3992-4453-ae97-6d6e6c35a65a/edit |
| 7 | ERD mức 2 – P5 Bán hàng và thanh toán | https://lucid.app/lucidchart/0723605e-a0de-4f16-b7e5-9a18962b614a/edit |
| 8 | ERD mức 2 – P6 Khách hàng thân thiết | https://lucid.app/lucidchart/aa8b29ca-d13f-446b-beed-db0fa31362f5/edit |
| 9 | ERD mức 2 – P7 Vận hành và phê duyệt | https://lucid.app/lucidchart/03dd0473-5327-44d3-9e78-157853621375/edit |
| 10 | ERD mức 2 – P8 Hệ thống | https://lucid.app/lucidchart/50974733-0a49-484b-aa91-c9d4165b9d54/edit |
| 11–14 | Luồng giao diện (trang 2–5: Web khách, Quầy và vận hành, Quản trị, Hệ thống) | https://lucid.app/lucidchart/d7ee25d1-ef06-4e46-b319-12feda24e855/edit |

Các tài liệu Lucid cũ của Hình 1, 3, 5, 6, 7, 11–14 (bản 08/11) vẫn còn trong tài khoản Lucid, không còn dùng. Trên Lucid còn hai bản nháp thừa (P4 95c62e63…, P5 fc19ac19…), có thể xóa.

## Việc sinh viên còn phải tự làm

- Điền họ tên ở trang bìa.
- Bài 4: hỏi AI theo hai prompt có sẵn, dán trả lời, chụp màn hình, điền Bảng 6 và bảng thông tin lần hỏi.
- Sửa sơ đồ thì xuất PNG lại và thay trong Word; mở Word cập nhật mục lục.

## Điểm chưa đẹp của sơ đồ (đã biết)

- ERD mức 1: Bills vẫn là trung tâm nên có nhiều đường đi vào cạnh phải; còn một chỗ cắt (Bills–ShowtimeSeats qua Showtimes–Tickets).
- ERD mức 2 trang P4, P5 nối ở mức bảng, không chỉ tới từng dòng cột khóa ngoại (cột FK vẫn được đánh dấu trong bảng).
- Luồng Quản trị: đường "Xóa" và "Phòng chiếu" đi chung một đoạn dọc.
