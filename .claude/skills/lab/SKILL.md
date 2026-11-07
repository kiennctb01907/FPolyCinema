---
name: lab
description: Làm tài liệu, sơ đồ và báo cáo của một bài lab 1–8 môn Dự án .NET 1 cho đề tài FPolyCinema, lấy dữ kiện từ spec và code thật. Chỉ dùng khi người dùng gõ /lab N.
argument-hint: "[số bài lab 1-8]"
disable-model-invocation: true
---

# Làm Lab $ARGUMENTS

1. Nếu đã có thư mục src/, chạy `node tools/spec-check/check.mjs facts` để cập nhật docs/generated theo code hiện tại.
2. Đọc LAB-PROMPT.md, rồi làm đúng phần "Lab $ARGUMENTS" ở mục 8. Nguồn và thứ tự ưu tiên theo LAB-PROMPT.md mục 2 và 3.
3. Phiên lab không sửa code, spec hay docs/trace. Thấy code khác spec mà PROGRESS.md chưa ghi thì liệt kê ra cho người dùng, rồi vẽ theo code.
4. Lưu mọi file vào labs/Lab$ARGUMENTS/. Xong thì đối chiếu số use case, số bảng, số màn hình trên sơ đồ với docs/generated và docs/spec/s10-use-case.md, rồi báo cho người dùng những ảnh chụp màn hình họ cần tự chèn.
