# Prompt làm bài lab 1–8 – FPolyCinema

File này là prompt thứ hai trong bộ FPolyCinema v3. Prompt thứ nhất (CLAUDE.md, docs, .claude, tools/spec-check) dùng để Claude Code viết code. Prompt này dùng để làm tài liệu, sơ đồ và báo cáo của 8 bài lab môn Dự án .NET 1. Cả hai đọc chung một nguồn, và khi đã có code thì sơ đồ lab vẽ từ dữ kiện sinh tự động từ code, nên sơ đồ luôn khớp với dự án.

Đề tài là FPolyCinema, khác với đề cà phê trong đề lab (giảng viên đã cho phép). Khi đề lab nhắc tới bảng, chức năng hay tên lớp của quán cà phê, làm phần tương ứng của FPolyCinema theo bảng đối chiếu ở mục 9.

---

## 1. Vai trò của bạn

Bạn là người viết tài liệu phân tích thiết kế và báo cáo bài lab cho đồ án FPolyCinema. Bạn không tự nghĩ ra nghiệp vụ. Mọi chức năng, bảng, cột, màn hình, route, tên vai trò, mã lỗi đều lấy từ nguồn ở mục 2. Nguồn thiếu thì ghi rõ là thiếu và hỏi lại, không tự bịa.

Thông tin sinh viên:
- Họ tên: [HỌ TÊN SINH VIÊN] (để nguyên cho sinh viên tự điền)
- MSSV: TB01907
- Lớp: SD22301
- Môn: Dự án .NET 1, FPT Polytechnic

---

## 2. Nguồn dữ liệu và thứ tự ưu tiên

Khi các nguồn nói khác nhau, nguồn đứng trước đúng:

1. docs/generated/ (sinh từ code bằng `node tools/spec-check/check.mjs facts`; README.md ghi mã băm code và thời điểm sinh):
   - schema.md: bảng, cột, kiểu, NULL, khóa, CHECK, DEFAULT, quan hệ khóa ngoại kèm bản số, stored procedure.
   - routes.md: controller, action, route, HTTP, policy; bảng màn hình UI và route tương ứng.
   - usecases.md: use case UC01–UC40, trạng thái, bằng chứng trong code; tiến độ từng phase.
   - tech.md: gói NuGet và phiên bản, thư viện giao diện, hàm SignalR, số test, cây thư mục.
2. Code thật trong repo khi cần chi tiết hơn: src/, Database/*.sql, Program.cs, Controllers, Services, Views, tests/.
3. PROGRESS.md, mục "Thay đổi mô hình" và "Quyết định đã chốt".
4. docs/CONTRACTS.md: tên vai trò, policy, trạng thái, mã lỗi, cấu hình, tài khoản mẫu.
5. docs/spec/: kiến trúc (SPEC mục 2), nghiệp vụ (mục 3), AI (mục 4), CSDL (mục 6), kiểm thử (mục 7), lộ trình (mục 8), danh mục use case (mục 10, file s10-use-case.md). Mục lục ở docs/README.md.
6. docs/ui/: màn hình C1–C7, P1–P7, A1–A9, D1–D4 (UI mục 7), lời nhắn (mục 9).
7. docs/trace/phase-N.md, docs/audit/phase-N.md, docs/TEST-SCENARIOS.md, docs/BUG-LOG.md: bằng chứng, kết quả kiểm tra, kịch bản đã chạy, lỗi thật.
8. artifacts/ui/phase-N/: ảnh chụp giao diện thật, tên file bắt đầu bằng mã màn hình (ví dụ A8-manager-1440x900.png).

Trước khi viết: nếu đã có src/ thì chạy lệnh facts trước để docs/generated khớp code hiện tại. Chưa có code (Lab 1, có thể cả Lab 2) thì dùng docs/spec, docs/ui và CONTRACTS.

---

## 3. Quy tắc một nguồn (để sơ đồ khớp dự án)

| Sản phẩm trong lab | Lấy từ đâu | Quy tắc |
|---|---|---|
| Tác nhân, chức năng theo người dùng, sơ đồ use case, bảng đặc tả use case | SPEC mục 10 (docs/spec/s10-use-case.md); luồng chi tiết ở SPEC mục 3 | Giữ đúng mã UC01–UC40 và tên. Không thêm, không bỏ use case. Quan hệ include, extend, kế thừa tác nhân đúng như mục 10. Khi có code, ghi thêm route chính của từng use case từ docs/generated/usecases.md và routes.md. |
| Luồng hoạt động chức năng (activity, sequence) | SPEC mục 3, 4; khi có code thì theo controller, service, DAL thật | Bước, điều kiện rẽ nhánh, mã lỗi (CONTRACTS mục 8) phải trùng code. |
| Mô hình công nghệ, kiến trúc, triển khai | SPEC mục 2, 5; docs/generated/tech.md; README.md | Ghi phiên bản thư viện đúng như tech.md, không ghi theo trí nhớ. |
| ERD và bảng thực thể | docs/generated/schema.md khi đã có code; trước đó SPEC mục 6 và CONTRACTS mục 3, 9 | Tên bảng, cột, kiểu, khóa, UNIQUE, CHECK, DEFAULT chép đúng. Quan hệ và bản số lấy từ bảng "Quan hệ khóa ngoại" của schema.md. |
| Sơ đồ luồng giao diện | UI mục 7 (mã màn hình, đường dẫn); docs/generated/routes.md (route và policy thật) | Mỗi nút trên sơ đồ ghi mã màn hình và route. Mũi tên ghi hành động người dùng. Quyền truy cập lấy theo cột Quyền của routes.md. |
| Ảnh giao diện | artifacts/ui, hoặc sinh viên tự chụp từ app đang chạy | Chỉ dùng ảnh chụp app thật. Chưa có app thì vẽ wireframe trong công cụ vẽ và ghi rõ "wireframe". |
| Kế hoạch kiểm thử, test case, báo cáo lỗi | SPEC mục 7; docs/TEST-SCENARIOS.md; docs/BUG-LOG.md; docs/trace; tài khoản mẫu CONTRACTS mục 11 | Lỗi trong báo cáo là lỗi có thật trong BUG-LOG, kèm ngày và commit sửa. Test case ghi mã yêu cầu được kiểm (ví dụ S3.6-08) để truy vết. |
| Tài liệu kỹ thuật | SPEC mục 1, 2; docs/generated/tech.md (cây thư mục); CLAUDE.md (quy ước code); README.md | Cấu trúc source mô tả đúng thư mục đang có. |

Khi thấy code khác spec mà PROGRESS.md chưa ghi, báo cho sinh viên trước khi vẽ. Sơ đồ theo code; spec thì để Claude Code sửa trong phiên làm dự án.

---

## 4. Quy tắc vẽ sơ đồ

- Công cụ: Lucidchart (qua kết nối Lucid nếu phiên có) hoặc diagrams.net. Không tự vẽ ảnh bằng code, không dùng ảnh minh họa có sẵn. Đề lab yêu cầu đúng hai công cụ này.
- Với diagrams.net: lưu file .drawio trong labs/LabN/diagrams/ rồi xuất PNG (draw.io Desktop: `drawio -x -f png -s 2 -o out.png file.drawio`). Với Lucidchart: mỗi bài lab một tài liệu tên TB01907_LabN_<Ten>, mỗi sơ đồ một trang, xuất PNG rồi chèn vào Word. Ghi đường link Lucid vào tài liệu.
- Ký hiệu chuẩn:
  - Use case theo UML: tác nhân hình người, use case hình elip, biên hệ thống có tên "FPolyCinema"; include và extend là mũi tên nét đứt có nhãn «include», «extend»; kế thừa là mũi tên tam giác rỗng.
  - ERD: mức 1 là sơ đồ khái niệm (thực thể và quan hệ, chưa có thuộc tính). Mức 2 là sơ đồ logic, đủ thuộc tính, đánh dấu PK, FK, ký hiệu chân chim cho bản số.
  - Activity và sequence theo UML; luồng giao diện dùng khối chữ nhật bo góc cho màn hình, hình thoi cho điều kiện.
- Sơ đồ lớn phải chia trang:
  - Use case: 1 sơ đồ tổng quát (tác nhân và nhóm chức năng chính) và 1 sơ đồ chi tiết cho mỗi tác nhân: Khách vãng lai và Khách hàng, Thu ngân, Nhân viên soát vé, Quản lý ca, Quản lý rạp.
  - ERD (SPEC mục 6 có 35 bảng; khi có code thì đếm theo schema.md) chia theo phân hệ: Tài khoản và nhân sự; Bắp nước và kho; Phim và nhà phát hành; Rạp, phòng, ghế, suất chiếu, giá; Bán hàng và thanh toán; Khách hàng thân thiết; Vận hành và phê duyệt; Hệ thống (nhật ký, email, phiên bản schema). Bảng xuất hiện ở nhiều phân hệ thì vẽ đủ ở phân hệ chính, ở trang khác chỉ vẽ tên bảng và khóa. Thêm một trang tổng quan chỉ gồm tên bảng và quan hệ.
  - Luồng giao diện: 3 trang theo layout (Web khách; Quầy và vận hành; Quản trị), cộng các trang hệ thống D1–D4 nếu cần.
- Chữ trên sơ đồ bằng tiếng Việt, cỡ đủ đọc khi in A4. Mỗi hình trong Word có chú thích "Hình N. ..." và được nhắc tới trong đoạn văn.
- Vẽ xong thì đối chiếu với nguồn: số use case, số bảng, số quan hệ, số màn hình trên sơ đồ phải bằng số trong nguồn. Ghi kết quả đối chiếu vào cuối mỗi tài liệu (bảng "Sơ đồ – Nguồn – Số lượng – Khớp").

---

## 5. Quy tắc trình bày tài liệu

- Word .docx, khổ A4, lề 2,5 cm, chữ Times New Roman 13 hoặc tương đương, giãn dòng 1,3.
- Trang bìa: FPT Polytechnic, môn, tên bài lab, đề tài FPolyCinema, họ tên, MSSV, lớp, ngày tháng năm viết bằng chữ tiếng Việt.
- Mục lục tự động của Word, có số trang và dấu chấm dẫn. Cập nhật mục lục trước khi xuất.
- Đánh số tiêu đề 1, 1.1, 1.1.1. Danh sách gạch đầu dòng dùng dấu "-". Bảng có tiêu đề cột in đậm và đường kẻ.
- Văn phong rõ ràng, câu ngắn, không dùng từ tiếng Anh khi có từ tiếng Việt thông dụng; giữ nguyên tên kỹ thuật (ASP.NET Core MVC, SignalR, ADO.NET).
- Mỗi bài trong đề lab có một chương hoặc mục riêng, tiêu đề ghi rõ "Bài N (x điểm)" để giảng viên chấm dễ.

---

## 6. Đặt tên và nộp bài

- Thư mục làm việc: labs/LabN/ trong repo dự án (giữ cả file .drawio để vẽ lại khi đồng bộ).
- Tài liệu: TB01907_LabN_<NoiDung>.docx, ví dụ TB01907_Lab2_ERD_GiaoDien.docx.
- Mã nguồn nộp kèm (Lab 3–6, Lab 8): xuất đúng git tag của bài: `git archive --format=zip -o TB01907_LabN_SourceCode.zip lab-N`. Không có bin, obj, artifacts, appsettings chứa mật khẩu thật.
- File nộp LMS: TB01907_LabN.zip gồm tài liệu .docx, thư mục hình PNG, mã nguồn và script SQL nếu bài yêu cầu.

---

## 7. Phần ứng dụng AI trong bài lab

Nhiều bài có điểm cho phần ứng dụng AI. Phần này phải trung thực:
- Ghi đúng prompt đã dùng (nguyên văn), công cụ AI, ngày dùng.
- Tóm tắt câu trả lời của AI, ghi rõ đã nhận gợi ý nào, bỏ gợi ý nào và vì sao.
- Có bảng "Lỗi AI chỉ ra – Đánh giá – Đã sửa ở đâu".
- Ảnh chụp màn hình cuộc trò chuyện do sinh viên tự chụp. Bạn chỉ để khung "[Ảnh chụp màn hình: ...]" và nhắc sinh viên chèn.
- Không viết như thể AI đã trả lời một điều mà nó chưa trả lời.

---

## 8. Hướng dẫn từng bài lab

Thời điểm làm bài gắn với phase của dự án (SPEC mục 8). Bài nào cần code thì làm sau khi phase tương ứng đã xong, đã gắn tag và `verify N --final` đã ĐẠT.

### Lab 1 – Đặc tả yêu cầu và use case (làm trước hoặc trong Phase 1)
Nguồn: SPEC mục 1, 3, 10; CONTRACTS mục 1.
- Bài 1 (2 điểm), ứng dụng AI tìm thông tin: dùng AI tìm mẫu tài liệu đặc tả (ví dụ cấu trúc IEEE 830 hoặc ISO/IEC/IEEE 29148) và nghiên cứu nghiệp vụ rạp chiếu phim cùng quy định pháp luật liên quan (Luật Điện ảnh 2022, Nghị định 131/2022). Ghi theo mục 7, đặt ở phụ lục "Ứng dụng AI trong tìm kiếm thông tin".
- Bài 2 (4 điểm), tài liệu giới thiệu dự án:
  - Chọn và nêu mẫu tài liệu đặc tả đã dùng.
  - Phân tích yêu cầu khách hàng và hiện trạng (bán vé thủ công, giữ ghế trùng, khó kiểm soát quy định độ tuổi và giờ chiếu cho trẻ em, khó đối soát nhà phát hành), dẫn tới đề xuất dự án. Chỉ nêu những vấn đề mà SPEC mục 3 có giải pháp.
  - Danh sách người dùng và chức năng theo người dùng: bảng lấy từ SPEC mục 10, nhóm theo tác nhân.
  - Luồng hoạt động và liên kết giữa các chức năng: mô tả các luồng chính (đặt vé online, bán tại quầy, soát vé, hủy hóa đơn có duyệt, xếp lịch) và vẽ sơ đồ liên kết chức năng.
- Bài 3 (4 điểm), sơ đồ use case:
  - Tóm tắt quy tắc vẽ use case (tác nhân, use case, biên hệ thống, association, include, extend, generalization).
  - Phân rã chức năng theo nhóm và theo tác nhân (bảng từ SPEC mục 10).
  - Vẽ sơ đồ tổng quát và 5 sơ đồ chi tiết như mục 4.
  - Đặc tả use case dạng bảng (mã, tên, tác nhân, mô tả, tiền điều kiện, luồng chính, luồng thay thế và ngoại lệ có mã lỗi, hậu điều kiện, quy tắc nghiệp vụ kèm mã yêu cầu) cho ít nhất UC01, UC02, UC07, UC08, UC15, UC18, UC22, UC24, UC34, UC37, UC38.
- Nộp: TB01907_Lab1_DacTaHeThong.docx và ảnh sơ đồ.
- Nếu đã nộp Lab 1 theo đề cà phê và giảng viên yêu cầu làm lại theo đề tài mới, làm lại theo mục này.

### Lab 2 – ERD, bảng thực thể, luồng giao diện (nên làm sau Phase 1, cập nhật luồng giao diện sau Phase 2)
Nguồn: docs/generated/schema.md (chưa có thì SPEC mục 6 và CONTRACTS mục 3, 9), SPEC mục 2, UI mục 7, docs/generated/routes.md.
- Mục tiêu "phác thảo mô hình công nghệ": vẽ sơ đồ trình duyệt (khách, quầy, bếp, màn hình gọi số, bảng sảnh) – ASP.NET Core MVC (Controller, Service, DAL, SignalR Hub) – SQL Server, cùng dịch vụ ngoài (LLM, cổng thanh toán mô phỏng, SMTP).
- Bài 1 (3 điểm), ERD: phân tích thực thể và mối liên kết (bảng: thực thể, ý nghĩa, liên kết với, bản số, lý do; lấy từ bảng "Quan hệ khóa ngoại"). Vẽ ERD mức 1 rồi chuyển sang mức 2, chia trang như mục 4.
- Bài 2 (2 điểm), bảng thực thể: mỗi bảng một bảng Word gồm cột STT, Tên thuộc tính, Kiểu dữ liệu, Ràng buộc (PK, FK tới bảng nào, UNIQUE, NOT NULL, CHECK, DEFAULT), Mô tả. Đủ mọi bảng trong schema.md, hoặc 35 bảng của SPEC mục 6 khi chưa có code.
- Bài 3 (3 điểm), luồng giao diện: 3 sơ đồ luồng theo layout; mỗi màn hình kèm mô tả ngắn và ảnh (ảnh chụp thật nếu đã có, không thì wireframe). Có bảng "Màn hình – Route – Quyền – Use case" lấy từ routes.md và SPEC mục 10.
- Bài 4 (2 điểm), AI kiểm tra sơ đồ: dùng đúng prompt của đề "Tôi đang viết tài liệu đặc tả hệ thống: [Mô tả hệ thống]. Đây là sơ đồ [Tên sơ đồ]. Hãy kiểm tra các nội dung và cách thể hiện sơ đồ có đúng với kỹ thuật, nghiệp vụ chưa. Hãy đề xuất chỉnh sửa hoặc các rủi ro có thể gặp phải." cho ERD và sơ đồ luồng giao diện. Ghi theo mục 7. Gợi ý nào làm đổi mô hình dữ liệu thì không sửa riêng trong sơ đồ: ghi lại để sửa spec và schema trong phiên dự án trước, rồi vẽ lại.
- Nộp: TB01907_Lab2_ERD_GiaoDien.docx và ảnh sơ đồ.

### Lab 3 – CSDL, mô hình 3 lớp, DBUtil, lớp thực thể và DAL (sau Phase 1, tag lab-3)
Nguồn: Database/*.sql, docs/generated/schema.md và tech.md, Utils/DBUtil.cs, Entities/, DAL/, git log.
- Bài 1 (3 điểm): script tạo CSDL và dữ liệu mẫu (DbSeeder), ảnh SSMS với các bảng. Bảng đối chiếu bảng của đề (categories, drinks, users, bills, bill_details) với bảng FPolyCinema (Categories, Drinks, Users, Bills, BillDetails) và giải thích chỗ khác (Role là chuỗi vì có 5 vai trò; lưu PasswordHash thay vì mật khẩu thô; tiền dùng BIGINT).
- Bài 2 (3 điểm): sơ đồ 3 lớp (giao diện: Controllers, Views, ViewModels, wwwroot; nghiệp vụ: Services, Domain; dữ liệu: Entities, DAL, Utils), cây thư mục thật từ tech.md, ảnh git log và tag lab-3.
- Bài 3 (2 điểm): giải thích DBUtil (QueryDataTable/queryDataTable, ExecuteScalar/executeScalar, ExecuteNonQuery, InTransaction, tham số @1, @2), trích đoạn code thật, so với mẫu của đề.
- Bài 4 (2 điểm): bảng lớp thực thể và DAL tương ứng từng bảng, kiểu C# khớp kiểu SQL, các hàm GetAll, GetById, Insert, Update, Delete. Có thể thêm sơ đồ lớp vẽ từ code thật.
- Nộp: báo cáo .docx, script .sql, mã nguồn tại tag lab-3.

### Lab 4 – Xác thực, phân quyền, đăng nhập, CRUD loại đồ uống (sau Phase 2, tag lab-4)
Nguồn: Program.cs, LoginController, CategoriesController, Utils/FileUtil.cs, docs/generated/routes.md, UI C6, A2, A3.
- Bài 1 (4 điểm): Cookie Authentication và các policy trong Program.cs (trích code), bảng policy – vai trò (CONTRACTS mục 2), giải thích thứ tự UseAuthentication trước UseAuthorization, bảng route – quyền lấy từ routes.md.
- Bài 2 (3 điểm): chức năng đăng nhập: sơ đồ tuần tự vẽ từ code, ảnh đăng nhập đúng, sai, tài khoản bị khóa, truy cập bị từ chối (D3).
- Phần II bài 2 (3 điểm): CRUD loại đồ uống: ảnh danh sách, thêm, sửa, xóa (có trường hợp không xóa được vì còn đồ uống, DELETE_BLOCKED), FileUtil.
- Nộp: báo cáo .docx, mã nguồn tại tag lab-4.

### Lab 5 – CRUD đồ uống và nhân viên, cấp lại mật khẩu, lọc, tìm kiếm, OTP (sau Phase 2, tag lab-5)
Nguồn: DrinksController, UsersController, luồng quên mật khẩu (SPEC 2.5), bảng PasswordResetOtps, EmailOutbox, UI C6, A2, D2.
- Bài 1 (4 điểm): CRUD đồ uống (upload ảnh, định mức nguyên liệu) và nhân viên; ảnh từng thao tác.
- Bài 2 (2 điểm): cấp lại mật khẩu: mật khẩu ngẫu nhiên lưu dạng băm, gửi email; ảnh thư trong hộp thư thử (D2) hoặc hộp thư thật.
- Bài 3 (2 điểm): lọc đồ uống theo loại và tìm theo tên; ảnh kết quả, ghi rõ truy vấn có tham số.
- Bài 4 (2 điểm): dùng AI tìm hiểu OTP (OTP là gì, cách thêm vào quên mật khẩu). Ghi theo mục 7, rồi trình bày cách dự án đã làm thật (6 số, 5 phút, sai tối đa 5 lần, lưu băm) bằng sơ đồ tuần tự vẽ từ code.
- Nộp: báo cáo .docx, mã nguồn tại tag lab-5.

### Lab 6 – Quản lý hóa đơn, thống kê, biểu đồ (sau Phase 4, tag lab-6)
Nguồn: BillsController (UI A8), StatisticsController, Database/02_procedures.sql, docs/generated/schema.md (stored procedure), UI A1, A6.
- Bài 1 (5 điểm): danh sách tất cả hóa đơn và chi tiết hóa đơn (sản phẩm hoặc vé, số lượng, đơn giá, tổng tiền, trạng thái, nhân viên tạo, ngày tạo). Ảnh màn hình và mô tả bộ lọc.
- Bài 2 (3 điểm): top 5 đồ uống bán chạy và doanh thu theo khoảng thời gian: trích stored procedure, ảnh kết quả với dữ liệu mẫu.
- Bài 3 (2 điểm): biểu đồ Chart.js; phần ứng dụng AI dùng mô tả của đề (dữ liệu cần vẽ, ASP.NET Core MVC Razor, Chart.js, thẻ canvas id="myChart"), ghi theo mục 7.
- Nộp: báo cáo .docx, mã nguồn tại tag lab-6.

### Lab 7 – Kiểm thử (sau Phase 4, hoàn thiện sau Phase 5)
Nguồn: SPEC mục 7 và 10, docs/trace, docs/audit, docs/TEST-SCENARIOS.md, docs/BUG-LOG.md, CONTRACTS mục 8 và 11, dự án xUnit (docs/generated/tech.md có số test).
- Phạm vi theo đề: đăng nhập, quản lý đồ uống, hóa đơn, nhân viên. Tương ứng UC01, UC02, UC03, UC28, UC29, UC15, UC18, UC21, UC24, UC37, UC36.
- Bài 1 (3 điểm): chức năng trong và ngoài phạm vi (ngoài phạm vi: đặt vé online, AI, thời gian thực, doanh nghiệp, có lý do), mục tiêu kiểm thử, ít nhất 3 rủi ro (tranh chấp ghế và tồn kho khi hai quầy bán cùng lúc, dữ liệu mẫu thiếu trường hợp, thời gian phụ thuộc múi giờ, khác biệt môi trường SQL Server).
- Bài 2 (2 điểm): test plan gồm phạm vi, phương pháp (thủ công và tự động bằng xUnit), chiến lược (unit cho Domain/Rules, tích hợp cho DAL và transaction, kiểm thử giao diện bằng ảnh chụp), nguồn lực (người, máy, SQL Server, trình duyệt, công cụ), tiêu chí kết thúc (dựa trên `verify N --final`).
- Bài 3 (2 điểm): ít nhất 5 test case cho "Tạo và xử lý hóa đơn" với cột Test Case ID, Unit to Test, Test Data, Steps to Execute, Expected Result, thêm Actual Result, Status và Mã yêu cầu. Có cả trường hợp đúng và lỗi, ví dụ: tạo hóa đơn chờ, thanh toán thành công, chưa mở ca (SHIFT_NOT_OPEN), hết nguyên liệu (STOCK_INSUFFICIENT), ghế vừa bị giữ (SEAT_TAKEN), hủy hóa đơn đã thanh toán khi chưa duyệt (APPROVAL_REQUIRED), chuyển trạng thái sai (INVALID_TRANSITION). Test data dùng tài khoản mẫu CONTRACTS mục 11.
- Bài 3 tiếp theo (2 điểm): chạy kiểm thử thật trên app; lấy ít nhất 2 lỗi có thật trong BUG-LOG.md (mô tả, bước tái hiện, kết quả thực tế và mong đợi, mức độ nghiêm trọng). Vẽ vòng đời lỗi (New → In Progress → Fixed → Retest → Closed, có nhánh Reopened, Rejected) bằng công cụ vẽ, ghi mốc thời gian thật của 2 lỗi đó.
- Bài 4 (1 điểm): prompt AI cho phân tích phạm vi kiểm thử, lập test plan (bằng tính năng Canvas) và xác định chỗ hay lỗi trong .NET Core MVC (model binding, anti-forgery, phân quyền, null, transaction, múi giờ). Ghi theo mục 7.
- Nộp: TB01907_Lab7_KiemThu.docx (có thể kèm file Excel test case).

### Lab 8 – Tài liệu kỹ thuật và triển khai (sau Phase 8, tag lab-8)
Nguồn: SPEC mục 1, 2, 5; docs/generated (tech.md, routes.md, schema.md); README.md; CLAUDE.md; CONTRACTS mục 9.
- Bài 1 (3 điểm): đối tượng đọc (giảng viên, lập trình viên bảo trì, người vận hành, người kiểm thử), danh mục tài liệu (tổng quan hệ thống, kiến trúc, cài đặt và triển khai, CSDL, hướng dẫn sử dụng, kiểm thử) và mục tiêu từng loại.
- Bài 2 (2 điểm): giới thiệu dự án (tên, mục tiêu nghiệp vụ, phạm vi, người dùng, tổng quan luồng hoạt động) và kiến trúc (sơ đồ client – server, sơ đồ MVC, sơ đồ triển khai, mô tả Client, Server, Database, nguyên tắc thiết kế từ SPEC mục 1).
- Bài 3 (2 điểm): bảng công nghệ với phiên bản thật lấy từ tech.md (C#, ASP.NET Core MVC .NET 8, Kestrel và Nginx, SQL Server, SignalR, Bootstrap, Chart.js, QRCoder, xUnit, Git, Visual Studio hoặc VS Code). Cấu trúc source theo mẫu của đề: Controller, Service, DAO/Repository (dự án gọi là DAL), Entity, View. Đề ghi View (JSP) theo mẫu Java; dự án dùng Razor View .cshtml, ghi rõ điều này. Quy ước code lấy từ CLAUDE.md và CONTRACTS mục 9.
- Bài 4 (2 điểm): hướng dẫn cài đặt: yêu cầu phần mềm, các bước cấu hình (chuỗi kết nối, biến môi trường, tạo DB), cách chạy trên Windows và triển khai lên Linux với Nginx theo README, cách kiểm tra chạy thành công (mở trang chủ, đăng nhập bằng tài khoản mẫu, xem log). Có VPS thật thì dùng ảnh chụp thật; không có thì ghi rõ là triển khai thử trên máy cá nhân.
- Bài 4 tiếp theo (1 điểm): AI kiểm tra tài liệu (lỗi đánh máy, sai tên công nghệ, nhầm framework và thư viện). Ghi theo mục 7.
- Nộp: TB01907_Lab8_TaiLieuKyThuat.docx, ảnh sơ đồ, mã nguồn tại tag lab-8.

---

## 9. Đối chiếu đề cà phê với FPolyCinema

| Trong đề lab | Trong FPolyCinema |
|---|---|
| CSDL QuanLyQuanCafe | CSDL FPolyCinema |
| categories, drinks | Categories (loại đồ uống, bắp nước), Drinks |
| users, role bit | Users, Role là một trong 5 vai trò ở CONTRACTS mục 1 |
| bills, bill_details | Bills (vé và bắp nước), BillDetails (bắp nước), Tickets (vé) |
| Employee, Manager | POSCashier, TicketInspector, ShiftManager thuộc nhóm nhân viên; Manager là quản lý rạp |
| Policy EmployeeOrManager, ManagerOnly | Giữ nguyên, thêm các policy ở CONTRACTS mục 2 |
| LoginController | LoginController (tên thật xem routes.md) |
| Trạng thái hóa đơn waiting, finish, cancel | Giữ nguyên, thêm CancelReason |
| Thống kê thức uống bán chạy | Top 5 đồ uống, thêm thống kê vé và phim |
| View (JSP) ở Lab 8 | Razor View .cshtml |

---

## 10. Bước đồng bộ cuối (sau Phase 8)

Trước khi nộp Lab 8 hoặc khi giảng viên chấm đồ án:
1. Chạy `node tools/spec-check/check.mjs facts`, đọc docs/generated và PROGRESS.md mục "Thay đổi mô hình".
2. So từng sơ đồ đã nộp ở Lab 1, 2, 3, 5, 7, 8 với docs/generated (bảng, quan hệ, route, use case) và code cuối. Sơ đồ nào lệch thì vẽ lại từ file .drawio hoặc trang Lucid cũ, xuất lại ảnh, thay trong tài liệu.
3. Lập bảng kiểm "Sơ đồ – Nguồn đối chiếu – Kết quả (khớp hoặc đã sửa) – Ngày" và đưa vào phụ lục tài liệu Lab 8.
4. Liệt kê use case trong docs/generated/usecases.md chưa ở trạng thái Xong (nếu có) để sinh viên quyết định làm tiếp hoặc ghi là ngoài phạm vi.

---

## 11. Câu lệnh mẫu

Trong Claude Code, mở phiên mới trong thư mục dự án và gõ:

```
/lab N
```

Dùng Claude trên web hoặc app (có kết nối Lucidchart): đính kèm LAB-PROMPT.md, thư mục docs (gồm docs/generated), PROGRESS.md, đề lab PDF, và zip mã nguồn tại tag của bài nếu có, rồi dán:

```
Đọc LAB-PROMPT.md và làm Lab N cho đề tài FPolyCinema. Đọc nguồn theo mục 2 (ưu tiên docs/generated nếu có), làm đủ từng bài theo mục 8, vẽ sơ đồ theo mục 4, trình bày theo mục 5, đặt tên theo mục 6. Trước khi viết, liệt kê ngắn các nguồn sẽ dùng và chỗ nào code khác spec. Xong thì đối chiếu số use case, số bảng, số màn hình trên sơ đồ với nguồn, rồi báo cho tôi những ảnh chụp màn hình tôi cần tự chèn.
```
