/* Các nút thử thành phần trên /dev/style-guide (UI D1). */
(() => {
  'use strict';
  const sample = {
    success: 'Đã lưu thay đổi',
    error: 'Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.',
    warning: 'Ghế G7 vừa có người chọn trước.',
    info: 'Suất 19:30 hôm nay còn 42 ghế.',
    held: 'Đã giữ 3 ghế trong 10 phút',
  };
  let pinFails = 0;
  document.addEventListener('click', async (e) => {
    const t = e.target;
    const toast = t.closest('[data-sg-toast]');
    if (toast) {
      const k = toast.dataset.sgToast;
      if (k === 'many') ['success', 'info', 'warning', 'error', 'success'].forEach((x, i) => setTimeout(() => ui.toast[x](`${i + 1}. ${sample[x]}`), i * 150));
      else if (k === 'held') ui.toast.success(sample.held);
      else ui.toast[k](sample[k]);
      return;
    }
    const busy = t.closest('[data-sg-busy]');
    if (busy) { ui.busy(busy, true); setTimeout(() => ui.busy(busy, false), 1500); return; }
    const conf = t.closest('[data-sg-confirm]');
    if (conf) {
      const k = conf.dataset.sgConfirm;
      let ok;
      if (k === 'normal') ok = await ui.confirm({ title: 'Mở ca làm việc?', message: 'Ca sẽ mở tại máy POS-01 với tiền đầu ca 2.000.000 ₫.' });
      else if (k === 'danger') ok = await ui.confirm({ title: 'Xóa “Combo Solo”?', message: 'Bạn kiểm tra lại trước khi xóa.', consequences: ['Món bị xóa vĩnh viễn, không khôi phục được.', 'Xóa kèm 3 dòng định mức nguyên liệu của món.'], confirmText: 'Xóa', tone: 'danger' });
      else ok = await ui.confirm({ title: 'Hủy 12 suất chiếu của phòng 3?', message: 'Khách đã mua vé sẽ được hoàn tiền và nhận voucher bồi thường.', consequences: ['12 suất chuyển sang Đã hủy.', '86 hóa đơn được hoàn tiền.'], confirmText: 'Hủy các suất', tone: 'danger', requireText: 'PHONG3' });
      ui.toast.info(ok ? 'Bạn đã bấm Xác nhận.' : 'Bạn đã bấm Hủy.');
      return;
    }
    if (t.closest('[data-sg-pin]')) {
      pinFails = 0;
      const pin = await ui.pin({
        title: 'Nhập PIN quản lý',
        message: 'Ví dụ duyệt hủy hóa đơn HD20261107193000ABCD.',
        verify: async (v) => {
          if (v === '135790') return true;
          pinFails++;
          if (pinFails >= 3) return { message: 'Sai PIN quá 3 lần. PIN bị khóa 5 phút, bạn nhờ quản lý khác duyệt.', locked: true };
          return { message: `Mã PIN chưa đúng. Bạn còn ${3 - pinFails} lần thử.` };
        },
      });
      if (pin) ui.toast.success('PIN đúng. Ví dụ: hóa đơn đã được duyệt hủy.');
      return;
    }
    const reg = t.closest('[data-sg-region]');
    if (reg) { const r = document.getElementById(reg.dataset.target); ui.loadRegion(r, reg.dataset.sgRegion, { push: 'none' }); return; }
    const exp = t.closest('[data-sg-expire]');
    if (exp) {
      const slot = exp.parentElement.querySelector('[data-sg-expire-slot]');
      slot.innerHTML = '<span class="hold-timer"><i class="bi bi-hourglass-split" aria-hidden="true"></i><span data-time>00:03</span></span>';
      ui.holdTimer(slot.firstElementChild, new Date(Date.now() + 3000), {
        onExpire: async () => {
          const again = await ui.confirm({ title: 'Hết thời gian giữ ghế', message: 'Đã hết 10 phút giữ ghế. Bạn chọn lại ghế để tiếp tục nhé.', confirmText: 'Chọn lại', cancelText: 'Đóng' });
          if (again) slot.innerHTML = '';
        },
      });
    }
  });
})();
// Mẫu "đang xử lý" tĩnh trong ma trận trạng thái: nút của thẻ phim hiện spinner
document.addEventListener('DOMContentLoaded', () => {
  document.querySelectorAll('.sg-busy .movie-card .btn').forEach((b) => ui.busy(b, true));
});
