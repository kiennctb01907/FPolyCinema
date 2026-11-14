/* booking.js – trang đặt vé C3 (UI mục 7, SPEC 8.5): 4 bước Chọn suất, Chọn ghế, Bắp nước và ưu đãi, Thanh toán.
   - Bước 2: segmented 4 persona, công tắc "Bản đồ điểm AI", bộ chọn số vé, nút "Chọn giúp tôi"; banner "AI đề xuất G6–G8 · 96/100" kèm "Chọn nhanh";
     gợi ý chuyển suất khi cụm tốt nhất dưới ngưỡng; sơ đồ cập nhật thời gian thực qua kênh 1 (seatmap.js).
   - Chưa đăng nhập: bấm ghế mở hộp đăng nhập hoặc đăng ký, xong quay lại đúng suất.
   - Bước 3: thẻ món 3 cột, voucher, công tắc dùng điểm, công tắc dùng ví. Bước 4: thẻ radio phương thức, ô xác nhận độ tuổi, nút "Thanh toán 245.000 ₫".
   - Khối tóm tắt dính bên phải (điện thoại: thanh dính đáy và bottom sheet) có đồng hồ giữ ghế. */
(function () {
  'use strict';
  const root = document.querySelector('[data-booking]');
  if (!root) return;
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));
  const esc = (v) => String(v ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const money = (n) => Number(n || 0).toLocaleString('vi-VN') + ' ₫';
  const state = JSON.parse($('#booking-state').textContent);
  const base = `/booking/${state.showtimeId}`;
  const loggedIn = root.dataset.loggedIn === '1';
  const isCustomer = root.dataset.customer === '1';
  const STEPS = ['Chọn suất', 'Chọn ghế', 'Bắp nước và ưu đãi', 'Thanh toán'];
  // Nhãn tuổi do partial _AgeBadge dựng ở server (SPEC 3.1-22, 3.1-24), khối tóm tắt chỉ chèn lại
  const ageBadgeHtml = ($('[data-age-badge-tpl]')?.innerHTML || '').trim();

  let cart = state.cart || null;
  let step = 2;
  let persona = state.persona;
  let qty = state.quantity;
  let suggestion = null;
  let chosen = 0;
  let payMethod = 'VIETQR_SIM';
  let stopTimer = null;
  let zoom = 0;
  let seatMap = window.SeatMap ? SeatMap.attach(root) : null;
  const loginModal = () => bootstrap.Modal.getOrCreateInstance($('#booking-login'));

  const tickets = () => (cart && cart.tickets) || [];
  const hasSeats = () => tickets().length > 0;

  // ---------------------------------------------------------------- bước
  function renderStepper() {
    $$('.stepper li', root).forEach((li, i) => {
      const n = i + 1;
      li.classList.toggle('is-done', n < step);
      li.classList.toggle('is-current', n === step);
      if (n === step) li.setAttribute('aria-current', 'step'); else li.removeAttribute('aria-current');
      const no = $('.step-no', li);
      if (no) no.textContent = n < step ? '✓' : String(n);
      const hint = $('.visually-hidden', li);
      if (hint) hint.textContent = n < step ? ' (đã xong)' : n === step ? ' (bước hiện tại)' : '';
    });
    const compact = $('.stepper-compact', root);
    if (compact) compact.textContent = `Bước ${step}/4 · ${STEPS[step - 1]}`;
  }

  function go(n) {
    if (n >= 3 && !hasSeats()) {
      ui.toast.warning('Bạn chọn ít nhất một ghế trước khi sang bước tiếp theo.');
      if (seatMap) seatMap.root.scrollIntoView({ behavior: 'smooth', block: 'center' });
      return;
    }
    step = n;
    $$('[data-step-panel]', root).forEach((p) => { p.hidden = Number(p.dataset.stepPanel) !== n; });
    renderStepper();
    renderSummary();
    root.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  // ---------------------------------------------------------------- khối tóm tắt
  function primaryLabel() {
    if (step === 1) return 'Chọn ghế';
    if (step === 4) return 'Thanh toán ' + money(cart ? cart.total : 0);
    return 'Tiếp tục';
  }

  function seatScore(id) { const b = seatMap && seatMap.byId.get(Number(id)); return b && b.dataset.score ? Number(b.dataset.score) : null; }

  function summaryHtml() {
    const m = state.movie, s = state.show;
    const seats = tickets().map((t) => {
      const sc = seatScore(t.seatId);
      return `<li class="seat-chip"><strong>${esc(t.label)}</strong>${sc ? ` · ${sc}/100` : ''} · <span class="money">${money(t.listPrice)}</span></li>`;
    }).join('');
    const items = ((cart && cart.items) || []).map((i) => `<div class="ms-line"><span class="ms-label">${i.quantity} × ${esc(i.name)}</span><span class="ms-value money">${money(i.amount)}</span></div>`).join('');
    const disc = [];
    if (cart && cart.voucherDiscount > 0) disc.push([`Voucher ${cart.voucherCode || ''}`, cart.voucherDiscount]);
    if (cart && cart.pointsDiscount > 0) disc.push([`Đổi ${Number(cart.pointsUsed).toLocaleString('vi-VN')} điểm`, cart.pointsDiscount]);
    return `<div class="summary-movie"><img src="${esc(m.poster)}" alt="" width="56" height="84" onerror="this.onerror=null;this.src='/img/placeholder-poster.svg'" />` +
      `<div class="min-w-0"><h2 class="h6 mb-1">${esc(m.title)} ${ageBadgeHtml}</h2>` +
      `<p class="small mb-0">${esc(s.cinema)} · ${esc(s.room)}</p><p class="small mb-0">${esc(s.date)} · <strong>${esc(s.time)}</strong></p>` +
      `<p class="small text-muted mb-0">${esc(s.format)} · ${esc(s.language)}</p></div></div>` +
      `<div class="summary-block"><p class="summary-label">Ghế</p>${seats ? `<ul class="seat-chips">${seats}</ul>` : '<p class="small text-muted mb-0">Chưa chọn ghế.</p>'}</div>` +
      (items ? `<div class="summary-block"><p class="summary-label">Bắp nước</p><div class="money-summary">${items}</div></div>` : '') +
      `<div class="money-summary summary-total">` +
      disc.map(([l, v]) => `<div class="ms-line ms-discount"><span class="ms-label">${esc(l)}</span><span class="ms-value money">−${money(v)}</span></div>`).join('') +
      `<div class="ms-total"><span class="ms-label">Tổng tiền</span><span class="ms-value money">${money(cart ? cart.total : 0)}</span></div></div>` +
      (cart && cart.holdExpiresAt && hasSeats() ? `<div class="summary-timer"><span class="small text-muted">Giữ ghế còn</span><span class="hold-timer" data-summary-timer role="timer"><i class="bi bi-hourglass-split" aria-hidden="true"></i><span data-time>--:--</span></span></div>` : '') +
      `<button type="button" class="btn btn-primary btn-lg w-100" data-summary-primary>${esc(primaryLabel())}</button>`;
  }

  function renderSummary() {
    const html = summaryHtml();
    const host = $('[data-summary]', root), sheet = $('[data-summary-sheet]', root);
    if (host) host.innerHTML = html;
    if (sheet) sheet.innerHTML = html;
    if (stopTimer) { stopTimer(); stopTimer = null; }
    const timers = $$('[data-summary-timer]', root);
    if (timers.length && cart && cart.holdExpiresAt) {
      const stops = timers.map((t, i) => ui.holdTimer(t, cart.holdExpiresAt, i === 0 ? { onExpire: expired } : { onExpire: () => {} }));
      stopTimer = () => stops.forEach((f) => f());
    }
    $('[data-bar-total]', root).textContent = money(cart ? cart.total : 0);
    $('[data-bar-primary]', root).textContent = primaryLabel();
    const pay = $('[data-pay-label]', root);
    if (pay) pay.textContent = 'Thanh toán ' + money(cart ? cart.total : 0);
    $$('[data-need-seat]', root).forEach((b) => { b.disabled = !hasSeats(); });
  }

  async function expired() {
    await ui.confirm({ title: 'Hết thời gian giữ ghế', message: `Đã hết ${state.holdMinutes} phút giữ ghế. Bạn chọn lại ghế để tiếp tục nhé.`, confirmText: 'Chọn lại', cancelText: 'Đóng', tone: 'warning' });
    location.href = base;
  }

  function primary() {
    if (step === 1) go(2);
    else if (step < 4) go(step + 1);
    else pay($('[data-pay-btn]', root));
  }

  // ---------------------------------------------------------------- giỏ và kho
  function applyCart(r) {
    cart = r.cart && r.cart.billId ? r.cart : null;
    if (seatMap) seatMap.setSelection(tickets().map((t) => t.seatId), cart ? cart.billId : 0);
    renderDrinks(r.stock || {});
    renderOffers();
    renderSummary();
    (r.cart && r.cart.notices || []).forEach((n) => ui.toast.info(n));
  }

  function renderDrinks(stock) {
    $$('[data-drink-id]', root).forEach((w) => {
      const id = Number(w.dataset.drinkId);
      const item = ((cart && cart.items) || []).find((i) => i.drinkId === id);
      const q = item ? item.quantity : 0;
      const soldOut = stock[id] !== undefined && stock[id] <= 0 && q === 0;
      const card = $('.food-card', w);
      card.classList.toggle('is-soldout', soldOut);
      let overlay = $('.soldout', w);
      if (soldOut && !overlay) { overlay = document.createElement('div'); overlay.className = 'soldout'; overlay.setAttribute('role', 'status'); overlay.textContent = 'Tạm hết'; $('.food-img', w).appendChild(overlay); }
      if (!soldOut && overlay) overlay.remove();
      $('.qty', w).textContent = q;
      $('[data-qty="-1"]', w).disabled = q === 0;
      $('[data-qty="1"]', w).disabled = soldOut || (stock[id] !== undefined && stock[id] <= 0);
    });
  }

  function renderOffers() {
    const applied = $('[data-voucher-applied]', root);
    if (applied) {
      applied.hidden = !(cart && cart.voucherCode);
      applied.innerHTML = cart && cart.voucherCode ? `<i class="bi bi-check-circle" aria-hidden="true"></i> Đã áp mã <strong>${esc(cart.voucherCode)}</strong>, giảm <span class="money">${money(cart.voucherDiscount)}</span>. <button type="button" class="btn btn-link btn-sm p-0" data-voucher-remove>Gỡ mã</button>` : '';
    }
    const pts = $('[data-use-points]', root);
    if (pts) pts.checked = !!(cart && cart.pointsUsed > 0);
    const hint = $('[data-points-hint]', root);
    if (hint) hint.textContent = cart && cart.pointsUsed > 0
      ? `Đang dùng ${Number(cart.pointsUsed).toLocaleString('vi-VN')} điểm, giảm ${money(cart.pointsDiscount)}.`
      : cart ? `Đơn này đổi được tối đa ${Number(cart.maxPoints || 0).toLocaleString('vi-VN')} điểm.` : '';
  }

  // ---------------------------------------------------------------- giữ ghế
  async function hold(ids, opts = {}) {
    const fd = new FormData();
    ids.forEach((id) => fd.append('seatIds', id));
    if (opts.wheelchair) fd.append('wheelchairConfirmed', 'true');
    const r = await ui.api(base + '/hold', { method: 'POST', body: fd, silent: opts.silent });
    applyCart(r);
    return r;
  }

  async function holdWithRules(ids, changed) {
    if (seatMap) seatMap.setPending(changed || ids, true);
    try {
      await hold(ids, { silent: true });
    } catch (e) {
      await seatError(e, ids, changed);
    } finally {
      if (seatMap) seatMap.setPending(changed || ids, false);
    }
  }

  async function seatError(e, ids, changed) {
    const d = e.details || {};
    if (e.code === 'WHEELCHAIR_CONFIRM_REQUIRED') {
      const ok = await ui.confirm({ title: 'Vị trí dành cho khách dùng xe lăn', message: e.message, confirmText: 'Tôi xác nhận', tone: 'info' });
      if (ok) { try { await hold(ids, { wheelchair: true }); } catch (e2) { await seatError(e2, ids, changed); } }
      return;
    }
    ui.toast.error(e.message);
    if (e.code === 'ORPHAN_SEAT' && seatMap) seatMap.violate(d.orphanSeatIds || []);
    else if (e.code === 'SWEETBOX_PAIR' && seatMap) seatMap.violate(d.seatIds || changed || []);
    else if (e.code === 'SEAT_TAKEN') await reloadSeatMap();
    else if (e.code === 'PAYMENT_EXPIRED') setTimeout(() => { location.href = base; }, 1500);
    else if (e.code === 'AUTH_REQUIRED') loginModal().show();
  }

  root.addEventListener('seat-toggle', (e) => {
    if (!loggedIn) { loginModal().show(); return; }
    if (!isCustomer) { ui.toast.info('Đặt vé online dành cho tài khoản khách hàng.'); return; }
    if (!state.salesOpen) { ui.toast.warning('Suất này đã hết giờ bán online.'); return; }
    holdWithRules(e.detail.seatIds, e.detail.changed);
  });

  // ---------------------------------------------------------------- gợi ý AI
  function clusterLabel(c) { return `AI đề xuất ${c.range} · ${c.score}/100`; }

  function renderSuggestion() {
    const banner = $('[data-ai-banner]', root), sw = $('[data-switch-banner]', root);
    if (!suggestion) { banner.hidden = true; sw.hidden = true; return; }
    if (seatMap) seatMap.setScores(suggestion.scores, state.goodScore);
    const cs = suggestion.clusters;
    banner.hidden = false;
    if (cs.length) {
      chosen = Math.min(chosen, cs.length - 1);
      const c = cs[chosen];
      $('[data-ai-text]', banner).textContent = clusterLabel(c);
      $('[data-ai-alts]', banner).innerHTML = cs.length > 1
        ? cs.map((x, i) => `<button type="button" class="filter-chip ${i === chosen ? 'is-active' : ''}" data-ai-alt="${i}" aria-pressed="${i === chosen}">${esc(x.range)} · ${x.score}</button>`).join('')
        : '';
      $('[data-ai-pick]', banner).disabled = !state.salesOpen;
      if (seatMap) seatMap.highlight(c.seatIds, `AI đề xuất ${c.score}/100`);
    } else {
      $('[data-ai-text]', banner).textContent = `Không còn ${qty} ghế liền nhau phù hợp ở suất này.`;
      $('[data-ai-alts]', banner).innerHTML = '';
      $('[data-ai-pick]', banner).disabled = true;
      if (seatMap) seatMap.highlight([], null);
    }
    if (suggestion.switchTo) {
      sw.hidden = false;
      $('[data-switch-text]', sw).textContent = suggestion.switchTo.message;
      $('[data-switch-link]', sw).href = suggestion.switchTo.url;
    } else sw.hidden = true;
  }

  async function suggest(pick) {
    try {
      suggestion = await ui.api(`${base}/suggest?persona=${encodeURIComponent(persona)}&qty=${qty}`, { silent: true });
      chosen = 0;
      renderSuggestion();
      if (pick) await pickCluster();
    } catch { /* bỏ qua: sơ đồ vẫn dùng được */ }
  }

  async function pickCluster() {
    if (!loggedIn) { loginModal().show(); return; }
    if (!isCustomer) { ui.toast.info('Đặt vé online dành cho tài khoản khách hàng.'); return; }
    const c = suggestion && suggestion.clusters[chosen];
    if (!c) { ui.toast.warning(`Không còn ${qty} ghế liền nhau phù hợp. Bạn chọn số vé ít hơn hoặc chọn tay trên sơ đồ.`); return; }
    await holdWithRules(c.seatIds, c.seatIds);
  }

  $$('[data-persona]', root).forEach((b) => b.addEventListener('click', () => {
    persona = b.dataset.persona;
    $$('[data-persona]', root).forEach((x) => { const on = x === b; x.classList.toggle('is-active', on); x.setAttribute('aria-checked', on ? 'true' : 'false'); });
    suggest(false);
  }));
  $$('[data-qty-step]', root).forEach((b) => b.addEventListener('click', () => {
    qty = Math.max(1, Math.min(10, qty + Number(b.dataset.qtyStep)));
    $('[data-qty-value]', root).textContent = qty;
    suggest(false);
  }));
  $('[data-heat-toggle]', root)?.addEventListener('change', (e) => { if (seatMap) seatMap.setHeat(e.target.checked); });
  $('[data-pick-for-me]', root)?.addEventListener('click', () => suggest(true));
  $('[data-ai-pick]', root)?.addEventListener('click', () => pickCluster());
  root.addEventListener('click', (e) => {
    const alt = e.target.closest('[data-ai-alt]');
    if (alt) { chosen = Number(alt.dataset.aiAlt); renderSuggestion(); return; }
    const g = e.target.closest('[data-go]');
    if (g && !g.disabled) { go(Number(g.dataset.go)); return; }
    if (e.target.closest('[data-summary-primary]') || e.target.closest('[data-bar-primary]')) { primary(); return; }
    if (e.target.closest('[data-voucher-remove]')) { applyVoucher(''); }
  });
  let resuggest = null;
  root.addEventListener('seatmap-change', () => { clearTimeout(resuggest); resuggest = setTimeout(() => suggest(false), 400); });

  // ---------------------------------------------------------------- phóng to sơ đồ (điện thoại)
  $$('[data-zoom]', root).forEach((b) => b.addEventListener('click', () => {
    if (!seatMap) return;
    zoom = seatMap.setZoom(zoom + Number(b.dataset.zoom));
    $('[data-zoom-label]', root).textContent = `Cỡ ${zoom + 1}/3`;
  }));

  async function reloadSeatMap() {
    const region = $('[data-seat-region]', root);
    try {
      const res = await fetch(base + '/seat-map', { headers: { 'X-Requested-With': 'XMLHttpRequest' }, credentials: 'same-origin' });
      if (!res.ok) return;
      if (seatMap) seatMap.destroy();
      region.innerHTML = await res.text();
      region.querySelector('[data-seatmap]').seatMap = null;
      seatMap = SeatMap.attach(region);
      if (zoom) seatMap.setZoom(zoom);
      seatMap.setHeat($('[data-heat-toggle]', root)?.checked);
      renderSuggestion();
    } catch { /* đã có thông báo kết nối */ }
  }

  // ---------------------------------------------------------------- bước 3: bắp nước, voucher, điểm, ví
  $$('[data-drink-id]', root).forEach((w) => w.addEventListener('click', async (e) => {
    const b = e.target.closest('[data-qty]');
    if (!b || b.disabled) return;
    if (!hasSeats()) { ui.toast.warning('Bạn chọn ghế trước khi thêm bắp nước.'); return; }
    try {
      applyCart(await ui.api(base + '/quantity', { method: 'POST', body: { drinkId: w.dataset.drinkId, action: b.dataset.qty === '1' ? 'incr' : 'decr' }, button: b }));
    } catch (err) { if (err.code === 'STOCK_INSUFFICIENT') refreshCart(); }
  }));

  async function applyVoucher(code) {
    const form = $('[data-voucher-form]', root);
    try {
      applyCart(await ui.api(base + '/voucher', { method: 'POST', body: { code }, form, silent: true }));
      $('[data-field-error="code"]', form).hidden = true;
      if (code) ui.toast.success('Đã áp dụng voucher.');
    } catch (e) {
      const err = $('[data-field-error="code"]', form);
      err.textContent = e.message;
      err.hidden = false;
    }
  }
  $('[data-voucher-form]', root)?.addEventListener('submit', (e) => { e.preventDefault(); applyVoucher(e.target.code.value.trim()); });

  $('[data-use-points]', root)?.addEventListener('change', async (e) => {
    try { applyCart(await ui.api(base + '/points', { method: 'POST', body: { points: e.target.checked ? (cart ? cart.maxPoints : 0) : 0 } })); }
    catch { e.target.checked = !e.target.checked; }
  });

  function setMethod(m) {
    payMethod = m;
    $$('[data-pay-method]', root).forEach((r) => { r.checked = r.value === m; });
    const w = $('[data-use-wallet]', root);
    if (w) w.checked = m === 'WALLET';
  }
  $('[data-use-wallet]', root)?.addEventListener('change', (e) => setMethod(e.target.checked ? 'WALLET' : 'VIETQR_SIM'));
  $$('[data-pay-method]', root).forEach((r) => r.addEventListener('change', () => { if (r.checked) setMethod(r.value); }));

  // ---------------------------------------------------------------- bước 4: xác nhận và thanh toán
  $$('[data-confirm]', root).forEach((c) => c.addEventListener('change', async () => {
    const age = $('[data-confirm="age"]', root), guardian = $('[data-confirm="guardian"]', root);
    try { await ui.api(base + '/confirm', { method: 'POST', body: { age: !!(age && age.checked), guardian: !!(guardian && guardian.checked) } }); }
    catch { c.checked = !c.checked; }
  }));

  async function pay(button) {
    if (!hasSeats()) { go(2); return; }
    try {
      const r = await ui.api(base + '/checkout', { method: 'POST', body: { method: payMethod }, button });
      if (r.done) { ui.toast.success('Thanh toán thành công.'); location.href = r.ticketUrl; }
      else location.href = r.payUrl;
    } catch (e) {
      if (e.code === 'PAYMENT_EXPIRED') setTimeout(() => { location.href = base; }, 1500);
      if (e.code === 'AGE_CONFIRM_REQUIRED' || e.code === 'GUARDIAN_REQUIRED') $('[data-confirm]', root)?.focus();
      if (e.code === 'SEAT_TAKEN') { await reloadSeatMap(); go(2); }
    }
  }
  $('[data-pay-btn]', root)?.addEventListener('click', (e) => pay(e.currentTarget));

  // ---------------------------------------------------------------- tải lại khi kết nối lại
  async function refreshCart() {
    if (!isCustomer) return;
    try { applyCart(await ui.api(base + '/cart', { silent: true })); } catch { /* bỏ qua */ }
  }
  if (window.realtime) realtime.onReload(() => { refreshCart(); reloadSeatMap(); });

  // ---------------------------------------------------------------- khởi tạo
  renderStepper();
  renderOffers();
  renderSummary();
  suggest(false);
  // Từ trợ lý đặt vé (SPEC 4.2-26): ghế đã giữ qua SeatHoldService, vào thẳng bước thanh toán
  if (new URLSearchParams(location.search).get('step') === '4' && hasSeats()) go(4);
})();
