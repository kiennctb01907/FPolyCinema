/* ticket.js – vé điện tử C4 /tickets/{mã}.
   - Nút "Hủy vé" mở hộp xác nhận nêu hậu quả, gọi POST /tickets/{mã}/cancel (SPEC 3.9), xong tải lại trang.
   - Kênh 3 (JoinBill): CHECKIN_UPDATED cập nhật trạng thái vào cửa từng ghế; ghế đã vào có dấu xác nhận động: đồng hồ HH:mm:ss chạy theo giây và
     dải màu chuyển động để phân biệt với ảnh chụp màn hình (SPEC 3.8-10, UI C4-03).
   - "Tôi đã tới rạp – làm bắp nước ngay" (SPEC 3.11-04, UI C4-05): POST /tickets/{mã}/im-here; FNB_STATUS_CHANGED cập nhật trạng thái bắp nước,
     số gọi hiện lớn khi món sẵn sàng (kèm âm báo nhẹ). */
(function () {
  'use strict';
  const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const TICKET_TONE = { NOT_CHECKED_IN: 'neutral', CHECKED_IN: 'success', SWAPPED: 'info', VOIDED: 'danger' };
  const FNB_TONE = { PENDING_PICKUP: 'info', PREPARING: 'warning', READY_FOR_PICKUP: 'success', FULFILLED: 'neutral', VOIDED: 'danger', NONE: 'neutral' };
  const badge = (tone, label) => `<span class="status-badge tone-${tone}">${esc(label)}</span>`;

  // ---------------------------------------------------------------- hủy vé
  const box = document.querySelector('[data-self-cancel]');
  if (box) {
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
  }

  const live = document.querySelector('[data-ticket-live]');
  if (!live) return;
  const code = live.dataset.code;
  const billId = Number(live.dataset.billId);
  const pad = (n) => String(n).padStart(2, '0');

  // ---------------------------------------------------------------- dấu xác nhận động: đồng hồ chạy theo giây
  function tick() {
    const d = new Date(ui.now());
    const text = `${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
    live.querySelectorAll('[data-live-clock]').forEach((el) => { el.textContent = text; });
  }
  setInterval(tick, 1000);
  tick();

  function onCheckin(p) {
    (p.tickets || []).forEach((t) => {
      const stub = live.querySelector(`[data-ticket-id="${t.ticketId}"]`);
      if (!stub) return;
      stub.querySelector('[data-ticket-status]').innerHTML = badge(TICKET_TONE[t.checkInStatus] || 'neutral', t.label);
      stub.classList.toggle('is-used', t.checkInStatus !== 'NOT_CHECKED_IN');
      const mark = stub.querySelector('[data-live-mark]');
      if (t.checkInStatus === 'CHECKED_IN' && !mark) {
        stub.querySelector('[data-ticket-status]').insertAdjacentHTML('afterend',
          '<p class="ticket-live" data-live-mark><i class="bi bi-patch-check-fill" aria-hidden="true"></i>Đã vào cửa <span class="num" data-live-clock></span></p>');
        tick();
      } else if (t.checkInStatus !== 'CHECKED_IN' && mark) mark.remove();
      // Vé đã đổi ghế: tải lại để hiện vé mới với QR mới
      if (t.checkInStatus === 'SWAPPED') setTimeout(() => location.reload(), 1500);
    });
  }

  function chime() {
    try {
      const a = new (window.AudioContext || window.webkitAudioContext)();
      [0, 0.18].forEach((at, i) => {
        const o = a.createOscillator(), g = a.createGain();
        o.frequency.value = i ? 990 : 660;
        g.gain.setValueAtTime(0.0001, a.currentTime + at);
        g.gain.exponentialRampToValueAtTime(0.18, a.currentTime + at + 0.02);
        g.gain.exponentialRampToValueAtTime(0.0001, a.currentTime + at + 0.25);
        o.connect(g).connect(a.destination); o.start(a.currentTime + at); o.stop(a.currentTime + at + 0.27);
      });
    } catch { /* trình duyệt chặn âm thanh */ }
  }

  function onFnb(p) {
    const status = live.querySelector('[data-fnb-status]');
    if (status) status.innerHTML = badge(FNB_TONE[p.fnbStatus] || 'neutral', p.label);
    const line = live.querySelector('[data-pickup-line]');
    if (line && p.pickupNumber) { line.hidden = false; line.querySelector('[data-pickup-number]').textContent = p.pickupNumber; }
    const big = live.querySelector('[data-pickup-big]');
    if (big) {
      const ready = p.fnbStatus === 'READY_FOR_PICKUP';
      if (ready && big.hidden) chime();
      big.hidden = !ready;
      if (p.pickupNumber) big.querySelector('[data-pickup-big-number]').textContent = p.pickupNumber;
    }
    if (p.fnbStatus !== 'PENDING_PICKUP') live.querySelector('[data-im-here-box]')?.remove();
  }

  live.addEventListener('click', async (e) => {
    const b = e.target.closest('[data-im-here]');
    if (!b) return;
    try {
      const r = await ui.api(`/tickets/${encodeURIComponent(code)}/im-here`, { method: 'POST', body: {}, button: b });
      ui.toast.success(r.message);
      onFnb({ fnbStatus: 'PREPARING', label: 'Đang chuẩn bị', pickupNumber: r.pickupNumber });
    } catch { /* ui.api đã báo lỗi */ }
  });

  if (window.realtime) {
    realtime.join('JoinBill', billId);
    realtime.on('CHECKIN_UPDATED', (p) => { if (Number(p.billId) === billId) onCheckin(p); });
    realtime.on('FNB_STATUS_CHANGED', (p) => { if (Number(p.billId) === billId) onFnb(p); });
  }
})();
