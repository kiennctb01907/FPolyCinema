---
name: tiep-tuc
description: Làm tiếp phase đang dở của FPolyCinema sau khi phiên bị ngắt, bị nén ngữ cảnh hoặc mở phiên mới. Chỉ dùng khi người dùng gõ /tiep-tuc.
disable-model-invocation: true
---

# Làm tiếp phase đang dở

1. Chạy `node tools/spec-check/check.mjs status` để biết phase đang làm (cột Ghi chú có chữ "đang làm") và tiến độ từng phase.
2. Đọc PROGRESS.md, chạy `git status` và `git log -5 --oneline`.
3. Chạy `node tools/spec-check/check.mjs begin N`, với N là phase đang làm. Lệnh giữ nguyên mã phiên nếu vẫn là phase đó.
4. Làm tiếp theo .claude/skills/phase/SKILL.md từ Bước 1, bắt đầu bằng `node tools/spec-check/check.mjs next N`. Nếu next báo hết yêu cầu thì sang Bước 2 (kết thúc phase).
