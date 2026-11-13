# Lab 2 – ERD, bảng thực thể, luồng giao diện (FPolyCinema)

- Tài liệu nộp: `TB01907_Lab2_ERD_GiaoDien.docx` (53 trang; mục lục tự động, đã cập nhật bằng Word).
- Ảnh sơ đồ: `diagrams/Hinh01…Hinh14*.png`, xuất từ Lucidchart.
- Nguồn dữ kiện: `docs/generated` mã băm code 4e812462dfc7 (35 bảng, 60 khóa ngoại, 58 route).
- Gói nộp LMS: `TB01907_Lab2.zip` (docx + thư mục diagrams).

## Sơ đồ gốc trên Lucidchart

| Hình | Nội dung | Link sửa |
|---|---|---|
| 1 | Mô hình công nghệ | https://lucid.app/lucidchart/ead0fed6-5323-4a12-a10c-76db0b851c97/edit |
| 2 | ERD mức 1 – tổng quan | https://lucid.app/lucidchart/970fbca5-d53c-44a8-8d13-a6a724511018/edit |
| 3 | ERD mức 2 – P1 Tài khoản và nhân sự | https://lucid.app/lucidchart/04aec04d-f163-4c20-b001-dbcc186e5637/edit |
| 4 | ERD mức 2 – P2 Bắp nước và kho | https://lucid.app/lucidchart/d9429208-f5a0-425b-ba47-895256fde3f6/edit |
| 5 | ERD mức 2 – P3 Phim và nhà phát hành | https://lucid.app/lucidchart/ed3a81d3-1ee4-4d88-b8e7-bbb877cdbf49/edit |
| 6 | ERD mức 2 – P4 Rạp, phòng, ghế, suất, giá | https://lucid.app/lucidchart/ec917fce-c763-4d52-91bb-931dab2f600b/edit |
| 7 | ERD mức 2 – P5 Bán hàng và thanh toán | https://lucid.app/lucidchart/77d54fd9-d6a2-4b72-87dd-fab4d096f199/edit |
| 8 | ERD mức 2 – P6 Khách hàng thân thiết | https://lucid.app/lucidchart/aa8b29ca-d13f-446b-beed-db0fa31362f5/edit |
| 9 | ERD mức 2 – P7 Vận hành và phê duyệt | https://lucid.app/lucidchart/03dd0473-5327-44d3-9e78-157853621375/edit |
| 10 | ERD mức 2 – P8 Hệ thống | https://lucid.app/lucidchart/50974733-0a49-484b-aa91-c9d4165b9d54/edit |
| 11–14 | Luồng giao diện (4 trang: Web khách, Quầy và vận hành, Quản trị, Hệ thống) | https://lucid.app/lucidchart/850e6a7c-f775-4b87-9066-a21d99d8cdac/edit |

Mười ERD mức 2 nằm ở 8 tài liệu Lucid riêng (công cụ tạo ERD của Lucid mỗi lần tạo một tài liệu); có thể kéo gộp thành một tài liệu nhiều trang trong Lucidchart nếu giảng viên yêu cầu.

## Việc sinh viên còn phải tự làm

- Điền họ tên ở trang bìa.
- Chèn ảnh chụp màn hình thật (C1, C6, A2, A3, A9, D1, D2, D3) ở mục 4.2.
- Bài 4: hỏi AI theo hai prompt có sẵn, dán trả lời, chụp màn hình, điền Bảng 6 và bảng thông tin lần hỏi.
- Sửa sơ đồ thì xuất PNG lại và thay trong Word; mở Word cập nhật mục lục.

## Điểm chưa đẹp của sơ đồ (đã biết)

- ERD mức 1: Users, Cinemas, Bills là trung tâm nên nhiều đường đi gần bảng khác.
- ERD mức 2 trang P3: một đường đi sát bảng Distributors.
