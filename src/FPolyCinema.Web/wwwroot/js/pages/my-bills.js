/* my-bills.js – Hóa đơn của tôi (/pos/my-bills): hủy hóa đơn đã thanh toán (lý do + PIN quản lý) và hủy hóa đơn chưa thanh toán của ca đang mở. */
(function () {
  'use strict';
  document.addEventListener('click', async (e) => {
    const v = e.target.closest('[data-void-bill]');
    if (v) { window.VoidBill?.open(v.dataset.voidBill, v.dataset.voidCode); return; }
    const c = e.target.closest('[data-cancel-waiting]');
    if (!c) return;
    const ok = await ui.confirm({
      title: 'Hủy hóa đơn chưa thanh toán', message: `Hủy hóa đơn ${c.dataset.code}?`, tone: 'danger', confirmText: 'Hủy hóa đơn', cancelText: 'Không hủy',
      consequences: ['Các ghế đang giữ được trả lại.'],
    });
    if (!ok) return;
    try {
      await ui.api('/pos/cancel', { method: 'POST', body: { billId: c.dataset.cancelWaiting }, button: c });
      ui.toast.success(`Đã hủy hóa đơn ${c.dataset.code}.`);
      setTimeout(() => location.reload(), 600);
    } catch { /* ui.api đã báo */ }
  });
  window.VoidBill?.init({ onDone: () => setTimeout(() => location.reload(), 600) });
})();
