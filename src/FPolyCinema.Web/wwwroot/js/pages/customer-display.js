/* customer-display.js – màn hình phụ của quầy P2 (SPEC 3.6-11, 3.6-17; kênh 2).
   Tham gia pos_terminal_{khóa máy} ẩn danh (JoinTerminal(code, isDisplay = true)) và chuyển giữa 4 trạng thái:
   chờ (slideshow poster) → giỏ hàng (POS_SHOWTIME_SELECTED, POS_CART_UPDATED: sơ đồ ghế lớn, ghế đang chọn sáng lên kèm điểm) →
   QR (POS_PAYMENT_QR: QR 360px, số tiền 56px, đồng hồ đếm ngược) → cảm ơn (POS_PAYMENT_DONE: mã vé và QR vé, sau 20 giây về chờ).
   POS_RESET về chờ. Sơ đồ ghế cập nhật trực tiếp qua SEAT_MAP_UPDATED (JoinShowtime) và tải lại khi giỏ đổi. */
(function () {
  'use strict';
  const root = document.querySelector('[data-cdisplay]');
  if (!root) return;
  const $ = (s) => root.querySelector(s);
  const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const key = root.dataset.terminalKey;
  const terminal = root.dataset.terminal;
  const cinemaId = root.dataset.cinemaId;
  let showtimeId = null, billId = null, showtimeGroup = null, doneTimer = null, countdown = null, mapTimer = null;

  function show(view) {
    root.querySelectorAll('[data-view]').forEach((v) => { const on = v.dataset.view === view; v.hidden = !on; v.classList.toggle('is-active', on); });
    if (view !== 'qr') clearInterval(countdown);
    if (view !== 'done') clearTimeout(doneTimer);
  }

  // ---------------------------------------------------------------- slideshow poster lúc chờ
  const slides = [...root.querySelectorAll('.cd-slide')];
  let si = 0;
  setInterval(() => {
    if (!slides.length || $('[data-view="idle"]').hidden) return;
    slides[si].classList.remove('is-active');
    si = (si + 1) % slides.length;
    slides[si].classList.add('is-active');
  }, 6000);

  // ---------------------------------------------------------------- giỏ hàng
  async function loadMap() {
    if (!showtimeId) { $('[data-cd-seatmap]').innerHTML = ''; return; }
    try {
      const res = await fetch(`/pos/customer-display/seat-map?showtimeId=${showtimeId}${billId ? `&billId=${billId}` : ''}`, { headers: { Accept: 'text/html' } });
      if (res.ok) $('[data-cd-seatmap]').innerHTML = await res.text();
    } catch { /* giữ sơ đồ cũ */ }
  }
  const scheduleMap = () => { clearTimeout(mapTimer); mapTimer = setTimeout(loadMap, 150); };

  async function followShowtime(id) {
    if (id === showtimeId) return;
    if (showtimeGroup && window.realtime) await realtime.leave(showtimeGroup);
    showtimeId = id;
    showtimeGroup = id && window.realtime ? await realtime.join('JoinShowtime', id) : null;
  }

  function drawShow(s) {
    $('[data-cd-show]').innerHTML = s
      ? `<h2 class="cd-movie">${esc(s.title)} <span class="age-badge age-badge-lg age-${esc(s.ageRating)}" title="${esc(s.ageExplanation)}">${esc(s.ageRating)}</span></h2>
         <p class="cd-show-meta">${esc(s.time)} · ${esc(s.room)}</p><p class="cd-show-age">${esc(s.ageExplanation)}</p>`
      : '<h2 class="cd-movie">Bắp nước</h2>';
  }

  function drawCart(c) {
    if (!c) return;
    billId = c.billId;
    if (c.showtime) drawShow(c.showtime);
    const lines = [];
    (c.tickets || []).forEach((t) => lines.push(`<li><span><strong>Ghế ${esc(t.seat)}</strong> · ${esc(t.seatType)} · ${esc(t.audience)}${t.score != null ? ` <span class="cd-score">${t.score} điểm</span>` : ''}</span><span class="money">${esc(t.price)}</span></li>`));
    (c.items || []).forEach((i) => lines.push(`<li><span>${i.quantity} × ${esc(i.name)}</span><span class="money">${esc(i.amount)}</span></li>`));
    $('[data-cd-lines]').innerHTML = lines.join('') || '<li class="cd-empty">Thu ngân đang chọn ghế cho quý khách…</li>';
    const d = $('[data-cd-discount]'); d.hidden = !c.discount; d.textContent = c.discount ? `Giảm giá ${c.discount}` : '';
    const m = $('[data-cd-member]'); m.hidden = !c.member; m.textContent = c.member ? `Thành viên: ${c.member}` : '';
    $('[data-cd-total]').textContent = c.total || '0 ₫';
  }

  function onShowtimeSelected(p) {
    show('cart');
    followShowtime(p.showtimeId || null);
    billId = p.billId;
    drawShow(p.showtime);
    drawCart(p.cart);
    scheduleMap();
  }
  function onCart(c) {
    show('cart');
    if (c.showtime && c.showtime.id !== showtimeId) followShowtime(c.showtime.id);
    drawCart(c);
    scheduleMap();
  }

  // ---------------------------------------------------------------- QR và cảm ơn
  function onQr(p) {
    show('qr');
    $('[data-cd-qr]').src = p.dataUri;
    $('[data-cd-amount]').textContent = p.amount;
    $('[data-cd-qr-code]').textContent = `Nội dung chuyển khoản: ${p.code}`;
    const end = new Date(p.expiresAt).getTime();
    const el = $('[data-cd-countdown]');
    const tick = () => { const s = Math.max(0, Math.round((end - Date.now()) / 1000)); el.textContent = `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`; };
    clearInterval(countdown); tick(); countdown = setInterval(tick, 1000);
  }
  function onDone(p) {
    show('done');
    $('[data-cd-done-qr]').src = p.qr;
    $('[data-cd-done-code]').textContent = p.code;
    $('[data-cd-done-seats]').textContent = p.seats && p.seats.length ? `Ghế ${p.seats.join(', ')}` : '';
    const pk = $('[data-cd-done-pickup]'); pk.hidden = !p.pickupNumber; pk.textContent = p.pickupNumber ? `Số gọi bắp nước: ${p.pickupNumber}` : '';
    $('[data-cd-done-note]').textContent = p.seats && p.seats.length ? 'Đưa mã này ở cửa soát vé. Chúc quý khách xem phim vui vẻ!' : 'Mời quý khách nhận bắp nước khi số gọi hiện trên bảng. Cảm ơn quý khách!';
    doneTimer = setTimeout(reset, 20000);
  }
  function reset() { followShowtime(null); billId = null; show('idle'); }

  async function reload() {
    try {
      const res = await fetch(`/pos/customer-display/state?terminal=${encodeURIComponent(terminal)}&cinemaId=${cinemaId}`, { headers: { Accept: 'application/json' } });
      const json = await res.json();
      if (json.data) onCart(json.data); else if (!$('[data-view="done"]').classList.contains('is-active')) reset();
    } catch { /* thử lại khi kết nối lại */ }
  }

  if (window.realtime) {
    realtime.join('JoinTerminal', key, true);
    realtime.on('POS_SHOWTIME_SELECTED', onShowtimeSelected);
    realtime.on('POS_CART_UPDATED', onCart);
    realtime.on('POS_PAYMENT_QR', onQr);
    realtime.on('POS_PAYMENT_DONE', onDone);
    realtime.on('POS_RESET', reset);
    realtime.on('SEAT_MAP_UPDATED', (p) => { if (p.showtimeId === showtimeId) scheduleMap(); });
    realtime.onReload(reload);
  }
  try {
    const initial = JSON.parse(document.getElementById('cd-state')?.textContent || 'null');
    if (initial) onCart(initial);
  } catch { /* trạng thái chờ */ }
})();
