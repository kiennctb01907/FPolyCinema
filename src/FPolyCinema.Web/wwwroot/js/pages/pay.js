/* pay.js – màn hình QR chờ thanh toán (UI C3-13, SPEC 3.7-08): đếm ngược theo HoldExpiresAt; tham gia nhóm bill_{id} bằng JoinBill và nghe
   PAYMENT_STATUS_CHANGED; "Đang chờ thanh toán…" → "Thanh toán thành công" kèm dấu ✓ rồi tự chuyển sang vé điện tử. Mất kết nối thì hỏi trạng thái
   bằng HTTP mỗi 5 giây. */
(function () {
  'use strict';
  const root = document.querySelector('[data-pay-screen]');
  if (!root) return;
  const billId = Number(root.dataset.billId);
  const code = root.dataset.code;
  const box = root.querySelector('[data-pay-status]');
  let done = false;

  function show(kind, text) {
    const icon = { waiting: 'bi-hourglass-split', success: 'bi-check-lg', failed: 'bi-x-lg', refunded: 'bi-arrow-counterclockwise' }[kind];
    box.innerHTML = `<span class="pay-status-icon is-${kind}" aria-hidden="true"><i class="bi ${icon}"></i></span><span data-pay-status-text></span>`;
    box.querySelector('[data-pay-status-text]').textContent = text;
  }

  function handle(p) {
    if (done || !p) return;
    if (p.status === 'finish') {
      done = true;
      show('success', 'Thanh toán thành công');
      setTimeout(() => { location.href = p.ticketUrl || root.dataset.ticketUrl; }, 1500);
    } else if (p.status === 'cancel') {
      done = true;
      show('refunded', p.message || 'Đơn đã được hủy và hoàn tiền.');
    } else if (p.status === 'FAILED') {
      show('failed', p.message || 'Thanh toán chưa thành công. Bạn thử lại trước khi hết thời gian giữ ghế.');
      setTimeout(() => { if (!done) show('waiting', 'Đang chờ thanh toán…'); }, 6000);
    }
  }

  async function poll() {
    if (done) return;
    try {
      const r = await ui.api(`/booking/pay/${encodeURIComponent(code)}/status`, { silent: true });
      if (r.status === 'finish') handle({ status: 'finish', ticketUrl: r.ticketUrl });
      else if (r.status === 'cancel' && r.cancelReason !== 'EXPIRED') handle({ status: 'cancel', message: r.cancelReason === 'OVERBOOKED_REFUNDED' ? 'Ghế đã được bán trong lúc chờ thanh toán. Tiền đã được hoàn và bạn được tặng voucher.' : 'Đơn đã được hủy.' });
    } catch { /* thử lại ở lượt sau */ }
  }

  if (window.realtime) {
    realtime.on('PAYMENT_STATUS_CHANGED', (p) => { if (Number(p.billId) === billId) handle(p); });
    realtime.join('JoinBill', billId);
    realtime.onReload(poll);
  }
  setInterval(() => { if (!window.realtime || !realtime.isConnected) poll(); }, 5000);
  if (root.dataset.status === 'finish') handle({ status: 'finish' });
})();
