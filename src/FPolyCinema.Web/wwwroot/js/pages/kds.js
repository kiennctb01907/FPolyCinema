/* kds.js – màn hình bếp P4 (SPEC 3.11, UI U7.P4).
   - Tham gia fnb_kds_{cinemaId} (JoinKds); KDS_ORDER_QUEUED thêm thẻ vào cột "Đang chuẩn bị" (trượt vào kèm âm báo nhẹ);
     KDS_ORDER_UPDATED chuyển thẻ sang "Sẵn sàng giao", hoặc gỡ thẻ khi FULFILLED hay VOIDED.
   - Thời gian chờ cập nhật mỗi 15 giây: xanh dưới 5 phút, vàng 5–10 phút, đỏ trên 10 phút (KdsRules.WaitLevel).
   - Ô quét QR đơn online ở quầy bắp nước: POST /pos/fnb-kds/pickup bắt đầu làm món cho đơn online. */
(function () {
  'use strict';
  const root = document.querySelector('[data-kds]');
  if (!root) return;
  const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const cinemaId = Number(root.dataset.cinemaId);
  // Lệch giờ giữa máy chủ và trình duyệt (thời gian chờ tính theo giờ máy chủ)
  const skew = new Date(root.dataset.now).getTime() - Date.now();
  const col = (status) => root.querySelector(`[data-col="${status}"]`);

  function card(o) {
    const pos = o.channel === 'POS';
    const btn = o.fnbStatus === 'PREPARING'
      ? `<button type="button" class="btn btn-primary btn-xl w-100" data-ready="${o.billId}"><i class="bi bi-bell" aria-hidden="true"></i>Sẵn sàng giao</button>`
      : `<button type="button" class="btn btn-success btn-xl w-100" data-served="${o.billId}"><i class="bi bi-bag-check" aria-hidden="true"></i>Đã giao</button>`;
    const el = document.createElement('article');
    el.className = 'kds-card';
    el.dataset.billId = o.billId;
    el.dataset.queuedAt = o.queuedAt;
    el.setAttribute('aria-label', `Đơn số ${o.pickupNumber}`);
    el.innerHTML = `<div class="kds-card-head"><span class="kds-number num">${esc(o.pickupNumber)}</span>
      <span class="status-badge tone-${pos ? 'neutral' : 'info'}">${esc(o.channelLabel)}</span>
      <span class="kds-wait" data-wait><i class="bi bi-stopwatch" aria-hidden="true"></i><span class="num" data-wait-text>0 phút</span></span></div>
      <ul class="kds-items">${(o.items || []).map((i) => `<li><span class="num">${i.quantity} ×</span> ${esc(i.name)}</li>`).join('')}</ul>
      <p class="kds-code">${esc(o.code)}</p>${btn}`;
    return el;
  }

  function counts() {
    ['PREPARING', 'READY_FOR_PICKUP'].forEach((s) => {
      const n = col(s).children.length;
      root.querySelector(`[data-count="${s}"]`).textContent = n;
      root.querySelector(`[data-empty="${s}"]`).hidden = n > 0;
    });
  }

  function tickWait() {
    const now = Date.now() + skew;
    root.querySelectorAll('.kds-card').forEach((c) => {
      // Màu theo KdsRules.WaitLevel: xanh dưới 5 phút, vàng 5–10 phút, đỏ trên 10 phút (tính theo giây)
      const ms = Math.max(0, now - new Date(c.dataset.queuedAt).getTime());
      const min = Math.floor(ms / 60000);
      const w = c.querySelector("[data-wait]");
      w.classList.toggle("is-ok", ms < 300000);
      w.classList.toggle("is-warn", ms >= 300000 && ms <= 600000);
      w.classList.toggle("is-late", ms > 600000);
      c.querySelector('[data-wait-text]').textContent = `${min} phút`;
    });
  }

  let audio = null;
  function chime() {
    try {
      audio = audio || new (window.AudioContext || window.webkitAudioContext)();
      const o = audio.createOscillator(), g = audio.createGain();
      o.frequency.value = 740; g.gain.setValueAtTime(0.0001, audio.currentTime);
      g.gain.exponentialRampToValueAtTime(0.15, audio.currentTime + 0.03);
      g.gain.exponentialRampToValueAtTime(0.0001, audio.currentTime + 0.35);
      o.connect(g).connect(audio.destination); o.start(); o.stop(audio.currentTime + 0.4);
    } catch { /* thiết bị không phát được âm thanh */ }
  }

  function upsert(o, isNew) {
    const old = root.querySelector(`.kds-card[data-bill-id="${o.billId}"]`);
    if (o.fnbStatus === 'FULFILLED' || o.fnbStatus === 'VOIDED') { old?.remove(); counts(); return; }
    const target = col(o.fnbStatus);
    if (!target) return;
    const el = card(o);
    if (old) old.replaceWith(el); else { el.classList.add('is-new'); target.appendChild(el); }
    if (old && old.parentElement !== target) target.appendChild(el);
    if (isNew) chime();
    counts();
    tickWait();
  }

  async function reload() {
    try {
      const r = await ui.api('/pos/fnb-kds/orders', { silent: true });
      ['PREPARING', 'READY_FOR_PICKUP'].forEach((s) => { col(s).innerHTML = ''; });
      r.orders.forEach((o) => col(o.fnbStatus)?.appendChild(card(o)));
      counts();
      tickWait();
    } catch { /* thử lại ở lần kết nối sau */ }
  }

  root.addEventListener('click', async (e) => {
    const ready = e.target.closest('[data-ready]');
    const served = e.target.closest('[data-served]');
    const b = ready || served;
    if (!b) return;
    const id = ready ? ready.dataset.ready : served.dataset.served;
    try {
      const o = await ui.api(`/pos/fnb-kds/${id}/${ready ? 'ready' : 'served'}`, { method: 'POST', body: {}, button: b });
      upsert(o, false);
    } catch { reload(); }
  });

  root.querySelector('[data-pickup-form]').addEventListener('submit', async (e) => {
    e.preventDefault();
    const form = e.currentTarget;
    const code = form.code.value.trim();
    if (!code) { ui.showFieldErrors(form, { code: 'Bạn quét hoặc nhập mã hóa đơn, mã vé của đơn online.' }); return; }
    try {
      const r = await ui.api('/pos/fnb-kds/pickup', { method: 'POST', body: { code }, form, button: form.querySelector('button') });
      ui.toast.success(r.message);
      form.reset();
    } catch { /* ui.api đã báo lỗi */ }
    form.code.focus();
  });

  if (window.realtime) {
    realtime.join('JoinKds', cinemaId, false);
    realtime.on('KDS_ORDER_QUEUED', (o) => upsert(o, true));
    realtime.on('KDS_ORDER_UPDATED', (o) => upsert(o, false));
    realtime.onReload(reload);
  }
  counts();
  tickWait();
  setInterval(tickWait, 15000);
})();
