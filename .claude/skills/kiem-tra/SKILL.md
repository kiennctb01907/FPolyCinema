---
name: kiem-tra
description: Kiểm tra lại một phase đã làm của FPolyCinema bằng script và agent spec-auditor, không sửa code. Chỉ dùng khi người dùng gõ /kiem-tra N.
argument-hint: "[số phase 1-8]"
disable-model-invocation: true
---

# Kiểm tra lại Phase $ARGUMENTS

1. Chạy `node tools/spec-check/check.mjs verify $ARGUMENTS --final`, tóm tắt lỗi theo từng phần.
2. Chạy `node tools/spec-check/check.mjs list $ARGUMENTS`, chia các mã thành nhóm không quá 20 mã theo nhóm của lệnh list, gọi agent spec-auditor cho từng nhóm (tối đa 4 agent song song).
3. Báo cáo cho người dùng một bảng các mã Thiếu hoặc Sai, lý do và file cần sửa. Không sửa code trong lệnh này; hỏi người dùng có muốn sửa ngay bằng /phase $ARGUMENTS không.
