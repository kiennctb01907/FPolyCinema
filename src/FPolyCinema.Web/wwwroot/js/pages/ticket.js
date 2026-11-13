/* ticket.js – vé điện tử C4: nút "Hủy vé" mở hộp xác nhận nêu hậu quả, gọi POST /tickets/{mã}/cancel (SPEC 3.9), xong tải lại trang. */
(function () {
  'use strict';
  const box = document.querySelector('[data-self-cancel]');
  if (!box) return;
  box.addEventListener('click', async (e) => {
    const b = e.target.closest('[data-cancel-ticket]');
    if (!b) return;
    const ok = await ui.confirm({
      title: 'Hủy vé này?',
      message: 'Vé sẽ không dùng được nữa và ghế được mở bán lại.',
      consequences: ['Hoàn 100% số tiền đã trả vào ví FPoly.', 'Voucher và điểm đã dùng được trả lại; điểm đã cộng bị thu hồi.', 'Lượt tự hủy trong tháng giảm đi 1.'],
      confirmText: 'Hủy vé', cancelText: 'Giữ vé',
      tone: 'danger',
    });
    if (!ok) return;
    try {
      const r = await ui.api(`/tickets/${encodeURIComponent(box.dataset.code)}/cancel`, { method: 'POST', body: {}, button: b });
      ui.toast.success(r.message);
      setTimeout(() => location.reload(), 1200);
    } catch { /* ui.api đã báo lỗi */ }
  });
})();
