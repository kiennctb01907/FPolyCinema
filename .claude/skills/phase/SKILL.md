---
name: phase
description: Làm một phase của dự án FPolyCinema theo quy trình chống sót (danh sách yêu cầu do script sinh từ spec, truy vết bằng chứng, kiểm tra độc lập, kiểm tra cuối). Chỉ dùng khi người dùng gõ /phase N.
argument-hint: "[số phase 1-8]"
disable-model-invocation: true
---

# Làm Phase $ARGUMENTS của FPolyCinema

Mục tiêu: làm đủ mọi yêu cầu của Phase $ARGUMENTS, không bỏ dòng nào, không làm trước việc của phase sau. Danh sách việc là danh sách mã do script in ra từ spec, không lập theo trí nhớ.

## Bước 0. Mở phiên

1. Chạy `node tools/spec-check/check.mjs begin $ARGUMENTS`. Lệnh in dòng PHASE-TOKEN, danh sách mọi yêu cầu chia theo nhóm, và tạo hoặc cập nhật docs/trace/phase-$ARGUMENTS.md.
2. Đọc PROGRESS.md (nếu đã có; các mục Quyết định đã chốt, Thay đổi mô hình, Đang bị chặn) và docs/CONTRACTS.md. CLAUDE.md đã được nạp sẵn.
3. Nếu begin báo các phase trước còn lỗi thì sửa trước, trừ khi lỗi đó là việc của phase này.
4. Tạo task list: mỗi nhóm trong danh sách là một task (ghi số nhóm và mục), thêm 5 task cuối: "Kiểm tra giao diện", "Kiểm tra độc lập", "Test và tạo lại DB", "Cập nhật tài liệu", "Commit, tag và kiểm tra cuối".

## Bước 1. Làm từng nhóm theo thứ tự

Lặp cho đến khi lệnh next báo hết:

1. `node tools/spec-check/check.mjs next $ARGUMENTS` in nguyên văn các yêu cầu còn "Chưa" của nhóm kế tiếp, kèm file:dòng và những thứ script sẽ đối chiếu (đường dẫn, lớp, cột).
2. Mở file spec ở đúng các dòng đó, đọc cả đoạn xung quanh (dòng cha, ví dụ, mã lỗi) và các mục được nhắc tới (ví dụ "theo CONTRACTS mục 5").
3. Code đủ từng yêu cầu. Logic thuần đặt ở Domain/Rules và có unit test. Mã lỗi, sự kiện, cấu hình, trạng thái dùng đúng tên trong CONTRACTS.
4. Ghi truy vết cho từng mã vừa làm bằng `node tools/spec-check/check.mjs mark MÃ --ev "file#hàm; test:TênTest"` (nhiều mã cùng bằng chứng thì ghi MÃ1,MÃ2). Bằng chứng trỏ tới đúng file và hàm thật; cách ghi ở đầu docs/trace/phase-$ARGUMENTS.md. Lệnh báo ngay bằng chứng nào sai. Không ghi Xong cho phần chưa chạy được.
5. `node tools/spec-check/check.mjs verify $ARGUMENTS --brief`, sửa các lỗi thuộc nhóm vừa làm (truy vết, tự động, đối chiếu code, quy tắc code).
6. Đánh dấu task của nhóm là xong, quay lại bước 1.

Quy tắc khi làm:
- Không bỏ qua, gộp hay làm qua loa một yêu cầu vì nó nhỏ. Yêu cầu giao diện cũng là yêu cầu.
- Spec thiếu hoặc chưa rõ: chọn cách đơn giản nhất mà vẫn đúng spec, ghi vào PROGRESS.md mục "Quyết định đã chốt", làm tiếp.
- Chỉ dừng hỏi khi thật sự không thể đi tiếp (ví dụ thiếu .NET 8 hoặc SQL Server). Khi đó ghi vào PROGRESS.md mục "Đang bị chặn", viết một dòng bắt đầu bằng "CẦN NGƯỜI DÙNG:" kèm câu hỏi, rồi dừng.
- Muốn đổi mô hình (bảng, cột, use case, màn hình): sửa nội dung trong docs/spec hoặc docs/ui, giữ nguyên mã [..], chạy `node tools/spec-check/check.mjs sync`, ghi vào PROGRESS.md mục "Thay đổi mô hình".
- Không xóa mã yêu cầu, không xóa dòng truy vết, không sửa tools/spec-check/scope.json, không sửa tools/spec-check/check.mjs để lỗi biến mất. Nếu một luật lint báo sai ở một dòng code cụ thể, thêm chú thích `spec-check: allow Lxx <lý do>` ngay dòng đó và ghi lý do vào PROGRESS.md.

## Bước 2. Kết thúc phase (đủ 8 việc, theo thứ tự)

1. `node tools/spec-check/check.mjs verify $ARGUMENTS` không còn lỗi ở các phần Truy vết, Tự động, Đối chiếu code, Quy tắc code.
2. Kiểm tra giao diện theo UI mục 13 cho mọi màn hình có nhóm "màn hình X" trong lệnh `list $ARGUMENTS`: chạy app, `node tools/ui-snapshots/snap.mjs --phase $ARGUMENTS`, mở từng ảnh, sửa, chụp lại đến khi đạt. Phase 1 không có màn hình nên bỏ qua bước này.
3. Kiểm tra độc lập: chia mã của phase thành các nhóm không quá 20 mã, theo nhóm của lệnh list. Với mỗi nhóm, gọi agent spec-auditor: "Kiểm tra Phase $ARGUMENTS, các mã: …". Có thể chạy song song tối đa 4 agent. Gộp kết quả vào docs/audit/phase-$ARGUMENTS.md thành bảng Mã | Kết luận | Lý do | Đã sửa. Sửa mọi dòng Thiếu hoặc Sai, gọi lại agent cho các mã đó đến khi đều Đạt, rồi ghi dòng "Kết quả: Đạt" ở cuối file.
4. `node tools/spec-check/check.mjs test --reset-db` (build, test, tạo lại DB và dữ liệu mẫu). Lỗi thì sửa rồi chạy lại.
5. `node tools/spec-check/check.mjs facts`.
6. Cập nhật PROGRESS.md (đánh dấu [x] Phase $ARGUMENTS, việc đã làm, quyết định, lỗi biết trước, kết quả kiểm tra giao diện), docs/TEST-SCENARIOS.md (mục "## Phase $ARGUMENTS" với ít nhất 3 kịch bản đã chạy thật kèm kết quả), docs/BUG-LOG.md (mọi lỗi thật đã gặp và đã sửa trong phase, đủ vòng đời), docs/CONTRACTS.md mục 12 (mục "### Phase $ARGUMENTS").
7. `git add -A` rồi `git commit -m "Phase $ARGUMENTS: <tóm tắt>"`. Phase có tag lab thì `git tag -f <tag>`: lab-3 sau Phase 1; lab-4 và lab-5 sau Phase 2; lab-6 sau Phase 4; lab-8 sau Phase 8.
8. `node tools/spec-check/check.mjs verify $ARGUMENTS --final` phải in "ĐẠT". Còn lỗi thì sửa và quay lại bước tương ứng; sửa code sau bước 4 thì chạy lại bước 4, 5, 7.

Chỉ khi bước 8 ĐẠT mới báo cáo cho người dùng: đã làm gì, cách thử (tài khoản mẫu ở CONTRACTS mục 11), việc còn lại, câu hỏi nếu có. Hook Stop của dự án sẽ chặn việc dừng sớm khi verify --final chưa ĐẠT.

## Khi ngữ cảnh bị nén hoặc phiên bị ngắt

Hook SessionStart in lại các mã còn "Chưa". Đọc lại file này, chạy `node tools/spec-check/check.mjs status`, `next $ARGUMENTS` và `git status`, rồi làm tiếp. Việc đã làm hay chưa thì tin docs/trace và code, không tin trí nhớ.
