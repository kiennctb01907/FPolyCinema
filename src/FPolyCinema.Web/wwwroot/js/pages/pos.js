/* pos.js – màn hình bán hàng tại quầy /pos/index (UI P1, SPEC 3.6).
   Mọi thao tác gọi API của PosController (POST có Anti-Forgery qua ui.api) và nhận lại hóa đơn tạm tính { cart } để vẽ cột 3.
   Sơ đồ ghế tải bằng HTML từ /pos/seat-map và nhận SEAT_MAP_UPDATED qua realtime.js; kết nối lại thì tải lại sơ đồ và hóa đơn bằng HTTP.
   Phím tắt: F1 tra cứu thành viên, F4 thanh toán tiền mặt, Esc hủy hóa đơn. */
(function () {
  'use strict';
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));
  const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const money = (n) => Number(n || 0).toLocaleString('vi-VN') + ' ₫';
  const time = (iso) => { const d = new Date(iso); return String(d.getHours()).padStart(2, '0') + ':' + String(d.getMinutes()).padStart(2, '0'); };

  const root = $('[data-pos]');
  if (!root) return;
  const state = JSON.parse($('#pos-state').textContent);
  let cart = state.cart || {};
  let showtimeId = cart.showtime ? cart.showtime.id : null;
  let seatMap = null;
  let stopTimer = null;
  const audiences = $$('[data-aud-all] option').filter((o) => o.value).map((o) => ({ value: o.value, label: o.textContent }));

  // ---------------------------------------------------------------- gọi API
  async function call(url, body, opts = {}) {
    const data = await ui.api(url, { method: 'POST', body, ...opts });
    if (data && data.cart) renderCart(data.cart);
    return data;
  }

  // ---------------------------------------------------------------- vẽ hóa đơn tạm tính
  function renderCart(c) {
    cart = c || {};
    const hasBill = !!cart.billId;
    const tickets = cart.tickets || [], items = cart.items || [];
    const empty = tickets.length === 0 && items.length === 0;
    $('[data-cart-code]').textContent = hasBill ? cart.code : '';
    (cart.notices || []).forEach((n) => ui.toast.info(n));

    // Đồng hồ giữ ghế
    const hold = $('[data-cart-hold]');
    if (stopTimer) { stopTimer(); stopTimer = null; }
    if (cart.holdExpiresAt && tickets.length) {
      hold.hidden = false;
      const timer = $('[data-cart-timer]');
      stopTimer = ui.holdTimer(timer, cart.holdExpiresAt, { onExpire: () => { ui.toast.warning('Đã hết thời gian giữ ghế. Hóa đơn được hủy, bạn chọn lại ghế nhé.'); reloadAll(); } });
      $('[data-extend-hold]').hidden = !cart.canExtend;
    } else hold.hidden = true;

    // Thành viên
    const mc = $('[data-member-card]');
    if (cart.member) {
      const m = cart.member;
      mc.hidden = false;
      mc.innerHTML = `<div><strong>${esc(m.fullName)}</strong> <span class="status-badge tone-info">${esc(m.tierLabel)}</span></div>` +
        `<div class="small text-muted">${esc(m.memberCode || '')} · ${Number(m.points).toLocaleString('vi-VN')} điểm (${money(m.pointValue)})</div>` +
        '<button type="button" class="btn btn-ghost btn-sm" data-member-clear>Bỏ thành viên</button>';
    } else { mc.hidden = true; mc.innerHTML = ''; }

    // Suất
    const sh = $('[data-cart-showtime]');
    if (cart.showtime) {
      const s = cart.showtime;
      sh.hidden = false;
      sh.innerHTML = `<div class="d-flex align-items-center gap-2"><span class="age-badge age-${esc(s.ageRating)}" role="img" aria-label="${esc(s.ageExplanation)}" title="${esc(s.ageExplanation)}">${esc(s.ageRating)}</span>` +
        `<strong class="text-clamp-2">${esc(s.movieTitle)}</strong></div><div class="small text-muted">${time(s.startTime)}–${time(s.endTime)} · ${esc(s.auditoriumName)} · ${esc(s.format)} · ${esc(s.languageLabel)}</div>`;
    } else { sh.hidden = true; sh.innerHTML = ''; }

    // Vé
    $('[data-cart-tickets-wrap]').hidden = tickets.length === 0;
    $('[data-cart-tickets]').innerHTML = tickets.map((t) => {
      const opts = audiences.map((a) => `<option value="${esc(a.value)}" ${a.value === t.audienceType ? 'selected' : ''}>${esc(a.label)}</option>`).join('');
      const warn = t.warning ? `<p class="line-warning" role="alert"><i class="bi bi-exclamation-triangle" aria-hidden="true"></i>${esc(t.warning)}</p>` : '';
      return `<li class="pos-line ${t.warning ? 'has-warning' : ''}" data-seat="${t.seatId}">` +
        `<div class="pos-line-main"><span class="seat-chip">${esc(t.label)}</span><span class="small text-muted">${esc(t.seatTypeLabel)}</span>` +
        `<span class="money ms-auto">${t.finalPrice !== t.listPrice ? `<s class="text-muted">${money(t.listPrice)}</s> ` : ''}${money(t.finalPrice)}</span></div>` +
        `<label class="visually-hidden" for="aud-${t.seatId}">Đối tượng vé ghế ${esc(t.label)}</label>` +
        `<select id="aud-${t.seatId}" class="form-select form-select-sm" data-aud-seat="${t.seatId}">${opts}</select>${warn}</li>`;
    }).join('');
    const g = $('[data-guardian]');
    g.hidden = !cart.guardianRequired;
    $('[data-guardian-check]').checked = !!cart.guardianConfirmed;

    // Bắp nước
    $('[data-cart-items-wrap]').hidden = items.length === 0;
    $('[data-cart-items]').innerHTML = items.map((i) =>
      `<li class="pos-line" data-drink="${i.drinkId}"><div class="pos-line-main"><span class="text-clamp-2">${esc(i.name)}</span><span class="money ms-auto">${money(i.amount)}</span></div>` +
      `<div class="d-flex align-items-center gap-2"><div class="qty-stepper" role="group" aria-label="Số lượng ${esc(i.name)}">` +
      `<button type="button" class="btn btn-secondary" data-qty-act="decr" data-drink-id="${i.drinkId}" aria-label="Bớt một ${esc(i.name)}"><i class="bi bi-dash-lg" aria-hidden="true"></i></button>` +
      `<span class="qty">${i.quantity}</span>` +
      `<button type="button" class="btn btn-secondary" data-qty-act="incr" data-drink-id="${i.drinkId}" aria-label="Thêm một ${esc(i.name)}" ${i.maxQuantity <= 0 ? 'disabled' : ''}><i class="bi bi-plus-lg" aria-hidden="true"></i></button></div>` +
      `<span class="small text-muted">${money(i.price)} / phần</span>` +
      `<button type="button" class="btn btn-ghost btn-sm ms-auto" data-qty-act="delete" data-drink-id="${i.drinkId}" aria-label="Xóa ${esc(i.name)}"><i class="bi bi-trash" aria-hidden="true"></i></button></div></li>`).join('');

    $('[data-cart-empty]').hidden = !empty;
    $('[data-cart-discounts]').hidden = empty;
    $('#pos-voucher').value = cart.voucherCode || '';
    const pf = $('[data-points-form]');
    pf.hidden = !cart.member;
    $('#pos-points').value = cart.pointsUsed || '';
    $('#pos-points').max = cart.maxPoints || 0;
    $('[data-points-hint]').textContent = cart.member ? `Đổi tối đa ${Number(cart.maxPoints || 0).toLocaleString('vi-VN')} điểm cho hóa đơn này.` : '';

    // Tổng tiền
    const lines = [];
    if (cart.ticketSubtotal) lines.push(['Vé', cart.ticketSubtotal]);
    if (cart.fnbSubtotal) lines.push(['Bắp nước', cart.fnbSubtotal]);
    const disc = [];
    if (cart.voucherDiscount) disc.push([`Voucher ${cart.voucherCode || ''}`, cart.voucherDiscount]);
    if (cart.pointsDiscount) disc.push([`Đổi ${Number(cart.pointsUsed).toLocaleString('vi-VN')} điểm`, cart.pointsDiscount]);
    $('[data-cart-summary]').innerHTML =
      lines.map(([l, v]) => `<div class="ms-line"><span class="ms-label">${esc(l)}</span><span class="ms-value money">${money(v)}</span></div>`).join('') +
      disc.map(([l, v]) => `<div class="ms-line ms-discount"><span class="ms-label">${esc(l)}</span><span class="ms-value money">−${money(v)}</span></div>`).join('') +
      `<div class="ms-line"><span class="ms-label">Trong đó VAT</span><span class="ms-value money">${money(cart.vatTotal)}</span></div>` +
      `<div class="ms-total"><span class="ms-label">Tổng cộng</span><span class="ms-value money">${money(cart.total)}</span></div>`;

    const blocked = tickets.some((t) => t.warning);
    $$('[data-pay]').forEach((b) => { b.disabled = empty || blocked; b.title = blocked ? 'Xử lý cảnh báo trên vé trước khi thanh toán' : ''; });
    $('[data-cancel-bill]').disabled = !hasBill;

    // Đồng bộ sơ đồ ghế với lựa chọn của hóa đơn
    if (seatMap) seatMap.setSelection(cart.showtime && cart.showtime.id === seatMap.showtimeId ? tickets.map((t) => t.seatId) : [], cart.billId);
    updateStepperLimits();
  }

  function updateStepperLimits() {
    $$('[data-food-grid] .pos-food').forEach((card) => {
      const inCart = (cart.items || []).find((i) => i.drinkId === Number(card.dataset.drinkId));
      card.classList.toggle('in-cart', !!inCart);
    });
  }

  // ---------------------------------------------------------------- sơ đồ ghế
  async function loadSeatMap(id) {
    const region = $('[data-seat-region]');
    if (!id) {
      if (seatMap) { seatMap.destroy(); seatMap = null; }
      region.innerHTML = '<div class="empty-state"><i class="bi bi-cup-straw" aria-hidden="true"></i><h3 class="h6">Đang bán bắp nước</h3><p class="text-muted mb-0">Chọn một suất ở cột bên trái nếu khách mua thêm vé.</p></div>';
      return;
    }
    region.classList.add('is-loading');
    try {
      const res = await fetch('/pos/seat-map?showtimeId=' + id, { headers: { 'X-Requested-With': 'XMLHttpRequest' }, credentials: 'same-origin' });
      if (!res.ok) throw new Error();
      const html = await res.text();
      if (seatMap) seatMap.destroy();
      region.innerHTML = html;
      seatMap = SeatMap.attach(region);
      ui.enhance(region);
    } catch {
      region.innerHTML = '<div class="empty-state"><i class="bi bi-wifi-off" aria-hidden="true"></i><h3 class="h6">Chưa tải được sơ đồ ghế</h3><p class="text-muted">Kiểm tra mạng rồi thử lại.</p><button type="button" class="btn btn-secondary" data-seat-retry>Thử lại</button></div>';
    } finally { region.classList.remove('is-loading'); }
  }

  async function selectShowtime(id, { confirm = true } = {}) {
    if (confirm && cart.showtime && cart.showtime.id !== id && (cart.tickets || []).length) {
      const ok = await ui.confirm({ title: 'Đổi suất chiếu', message: `Hóa đơn đang giữ ${cart.tickets.length} ghế của suất ${time(cart.showtime.startTime)}. Đổi suất sẽ trả các ghế đó.`, confirmText: 'Đổi suất', tone: 'primary' });
      if (!ok) return;
    }
    showtimeId = id;
    $$('.showtime-chip').forEach((c) => c.classList.toggle('is-active', Number(c.dataset.showtimeId) === id));
    try { await call('/pos/select-showtime', id ? { showtimeId: id } : {}); } catch { return; }
    await loadSeatMap(id);
    switchTab(id ? 'seats' : 'fnb');
    closeDrawer();
  }

  /** Gửi toàn bộ lựa chọn mới lên máy chủ (SeatHoldService.Hold). */
  async function holdSeats(seatIds, changed, extra = {}) {
    if (!seatMap) return;
    seatMap.setPending(changed, true);
    const body = new FormData();
    body.append('showtimeId', seatMap.showtimeId);
    seatIds.forEach((id) => body.append('seatIds', id));
    Object.entries(extra).forEach(([k, v]) => body.append(k, v));
    try {
      await call('/pos/hold-seats', body, { silent: true });
    } catch (e) {
      await onHoldError(e, seatIds, changed);
    } finally {
      seatMap?.setPending(changed, false);
    }
  }

  async function onHoldError(e, seatIds, changed) {
    const d = e.details || {};
    switch (e.code) {
      case 'WHEELCHAIR_CONFIRM_REQUIRED': {
        const ok = await ui.confirm({ title: 'Vị trí dành cho khách dùng xe lăn', message: e.message + ' Chỉ bán chỗ này cho khách dùng xe lăn hoặc người đi cùng.', confirmText: 'Xác nhận khách dùng xe lăn', tone: 'primary' });
        if (ok) await holdSeats(seatIds, changed, { wheelchairConfirmed: 'true' });
        return;
      }
      case 'ORPHAN_SEAT': {
        seatMap.violate([...(d.orphanSeatIds || []), ...changed]);
        ui.toast.error(e.message);
        if (d.canOverride) openOrphan(e.message, seatIds, changed);
        return;
      }
      case 'SWEETBOX_PAIR':
        seatMap.violate(d.seatIds || changed);
        ui.toast.error(e.message);
        return;
      case 'SEAT_TAKEN':
        seatMap.violate(d.seatIds || changed);
        ui.toast.warning(e.message);
        await refreshSeatMap();
        return;
      case 'PAYMENT_EXPIRED':
        ui.toast.warning(e.message);
        await reloadAll();
        return;
      default:
        seatMap.violate(changed);
        ui.toast.error(e.message);
    }
  }

  let orphanRetry = null;
  function openOrphan(message, seatIds, changed) {
    const m = $('#orphan-override');
    $('[data-orphan-message]', m).textContent = message;
    $('#orphan-reason').value = '';
    orphanRetry = { seatIds, changed };
    bootstrap.Modal.getOrCreateInstance(m).show();
  }
  $('[data-orphan-form]').addEventListener('submit', async (e) => {
    e.preventDefault();
    const reason = $('#orphan-reason').value.trim();
    if (reason.length < 5) { ui.showFieldErrors(e.target, { reason: 'Bạn ghi lý do (ít nhất 5 ký tự).' }); return; }
    bootstrap.Modal.getOrCreateInstance($('#orphan-override')).hide();
    if (orphanRetry) await holdSeats(orphanRetry.seatIds, orphanRetry.changed, { overrideOrphan: 'true', overrideReason: reason });
  });

  async function refreshSeatMap() { if (showtimeId) await loadSeatMap(showtimeId); }

  async function reloadCart() {
    try { const d = await ui.api('/pos/cart', { silent: true }); renderCart(d.cart); } catch { /* chỉ báo kết nối đã hiện */ }
  }
  async function reloadStock() {
    try {
      const stock = await ui.api('/pos/drink-stock', { silent: true });
      $$('[data-food-grid] .pos-food').forEach((card) => {
        const n = stock[card.dataset.drinkId] ?? 0;
        const out = n <= 0;
        card.classList.toggle('is-soldout', out);
        $('[data-add-drink]', card).disabled = out;
        $('[data-stock-label]', card).textContent = out ? 'Tạm hết' : '';
      });
    } catch { /* bỏ qua */ }
  }
  async function reloadAll() { await reloadCart(); await refreshSeatMap(); await reloadStock(); }

  // ---------------------------------------------------------------- tab, drawer, tìm suất
  function switchTab(name) {
    $$('.pos-tabs [role="tab"]').forEach((t) => {
      const on = t.dataset.tab === name;
      t.classList.toggle('is-active', on);
      t.setAttribute('aria-selected', String(on));
      t.tabIndex = on ? 0 : -1;
    });
    $('#panel-seats').hidden = name !== 'seats';
    $('#panel-fnb').hidden = name !== 'fnb';
  }
  const drawer = $('#pos-showtimes');
  function openDrawer() { root.classList.add('drawer-open'); $('[data-drawer-open]').setAttribute('aria-expanded', 'true'); $('[data-showtime-search]').focus(); }
  function closeDrawer() { root.classList.remove('drawer-open'); $('[data-drawer-open]').setAttribute('aria-expanded', 'false'); }

  root.addEventListener('click', async (e) => {
    const t = e.target;
    const tab = t.closest('.pos-tabs [role="tab"]'); if (tab) { switchTab(tab.dataset.tab); return; }
    if (t.closest('[data-drawer-open]')) { openDrawer(); return; }
    if (t.closest('[data-drawer-close]')) { closeDrawer(); return; }
    const chip = t.closest('.showtime-chip'); if (chip) { selectShowtime(Number(chip.dataset.showtimeId)); return; }
    if (t.closest('[data-fnb-only]')) { selectShowtime(null); return; }
    if (t.closest('[data-seat-retry]')) { refreshSeatMap(); return; }
    const add = t.closest('[data-add-drink]');
    if (add) {
      try { await call('/pos/update-quantity', { drinkId: add.dataset.addDrink, action: 'incr' }, { button: add }); } catch (err) { if (err.code === 'STOCK_INSUFFICIENT') reloadStock(); }
      return;
    }
    const q = t.closest('[data-qty-act]');
    if (q) { try { await call('/pos/update-quantity', { drinkId: q.dataset.drinkId, action: q.dataset.qtyAct }, { button: q }); } catch (err) { if (err.code === 'STOCK_INSUFFICIENT') reloadStock(); } return; }
    const cat = t.closest('.filter-chip[data-cat]');
    if (cat) {
      $$('.filter-chip[data-cat]').forEach((c) => { const on = c === cat; c.classList.toggle('is-active', on); c.setAttribute('aria-pressed', String(on)); });
      $$('[data-food-grid] .pos-food').forEach((card) => { card.hidden = !!cat.dataset.cat && card.dataset.cat !== cat.dataset.cat; });
      return;
    }
    if (t.closest('[data-member-clear]')) { try { await call('/pos/lookup-member', { query: '' }); } catch { /* đã báo */ } return; }
    const ext = t.closest('[data-extend-hold]'); if (ext) { try { await call('/pos/extend-hold', {}, { button: ext }); } catch { /* đã báo */ } return; }
    const pay = t.closest('[data-pay]'); if (pay && !pay.disabled) { pay.dataset.pay === 'CASH' ? openCash() : openQr(); return; }
    if (t.closest('[data-cancel-bill]')) { cancelBill(); return; }
    if (t.closest('[data-void-open]')) { VoidBill.open(); }
  });

  root.addEventListener('seat-toggle', (e) => holdSeats(e.detail.seatIds, e.detail.changed));

  root.addEventListener('change', async (e) => {
    const sel = e.target.closest('[data-aud-seat]');
    if (sel) {
      try { await call('/pos/set-audience', { seatId: sel.dataset.audSeat, audienceType: sel.value }); }
      catch (err) { renderCart(cart); if (seatMap) seatMap.violate([Number(sel.dataset.audSeat)]); }
      return;
    }
    const all = e.target.closest('[data-aud-all]');
    if (all && all.value) {
      try { await call('/pos/set-audience', { audienceType: all.value }); } catch { renderCart(cart); }
      all.value = '';
      return;
    }
    const g = e.target.closest('[data-guardian-check]');
    if (g) { try { await call('/pos/set-guardian', { confirmed: g.checked }); } catch { renderCart(cart); } }
  });

  $('[data-showtime-search]').addEventListener('input', (e) => {
    const q = e.target.value.trim().toLowerCase();
    $$('.pos-movie').forEach((m) => { m.hidden = !!q && !m.dataset.search.includes(q); });
  });

  $('[data-member-form]').addEventListener('submit', async (e) => {
    e.preventDefault();
    const q = $('#pos-member-q').value.trim();
    if (!q) { ui.showFieldErrors(e.target, { query: 'Bạn nhập mã thành viên, số điện thoại hoặc email.' }); return; }
    try { await call('/pos/lookup-member', { query: q }, { form: e.target, button: e.submitter }); $('#pos-member-q').value = ''; } catch { /* đã báo */ }
  });
  $('[data-voucher-form]').addEventListener('submit', async (e) => {
    e.preventDefault();
    try { await call('/pos/apply-voucher', { code: $('#pos-voucher').value.trim() }, { form: e.target, button: e.submitter }); } catch { /* đã báo */ }
  });
  $('[data-points-form]').addEventListener('submit', async (e) => {
    e.preventDefault();
    try { await call('/pos/apply-points', { points: Number($('#pos-points').value || 0) }, { form: e.target, button: e.submitter }); } catch { /* đã báo */ }
  });
  $('[data-scan-form]').addEventListener('submit', async (e) => {
    e.preventDefault();
    const code = $('#pos-scan-code').value.trim();
    if (!code) return;
    try {
      const d = await ui.api('/pos/scan', { method: 'POST', body: { code }, form: e.target, button: e.submitter });
      window.open(d.printUrl, '_blank', 'noopener');
      ui.toast.success(`Đã mở trang in vé hóa đơn ${d.code}.`);
      $('#pos-scan-code').value = '';
    } catch { /* đã báo */ }
  });

  // ---------------------------------------------------------------- hủy hóa đơn chưa thanh toán
  async function cancelBill() {
    if (!cart.billId) return;
    const ok = await ui.confirm({
      title: 'Hủy hóa đơn', message: `Hủy hóa đơn ${cart.code}?`, tone: 'danger', confirmText: 'Hủy hóa đơn', cancelText: 'Không hủy',
      consequences: ['Các ghế đang giữ được trả lại cho người khác chọn.', 'Bắp nước trong hóa đơn bị bỏ.'],
    });
    if (!ok) return;
    try { await call('/pos/cancel', {}); await refreshSeatMap(); } catch { /* đã báo */ }
  }

  // ---------------------------------------------------------------- thanh toán
  const cashModal = $('#pay-cash'), qrModal = $('#pay-qr');
  const cashInput = $('[data-cash-input]');
  const parseMoney = (s) => Number(String(s || '').replace(/\D/g, '')) || 0;
  function setCash(n) { cashInput.value = n ? n.toLocaleString('vi-VN') : ''; updateChange(); }
  function updateChange() {
    const got = parseMoney(cashInput.value);
    const ch = got - (cart.total || 0);
    $('[data-cash-change-label]').textContent = ch >= 0 ? 'Tiền thối' : 'Còn thiếu';
    $('[data-cash-change]').textContent = money(Math.abs(ch));
    $('[data-cash-change]').classList.toggle('text-danger', ch < 0);
  }
  function openCash() {
    $('[data-cash-total]').textContent = money(cart.total);
    setCash(0);
    bootstrap.Modal.getOrCreateInstance(cashModal).show();
    setTimeout(() => cashInput.focus(), 300);
  }
  cashInput.addEventListener('input', updateChange);
  $('[data-cash-exact]').addEventListener('click', () => setCash(cart.total || 0));
  $$('[data-cash-add]').forEach((b) => b.addEventListener('click', () => setCash(parseMoney(cashInput.value) + Number(b.dataset.cashAdd))));
  $('[data-num-pad]').addEventListener('click', (e) => {
    const k = e.target.closest('[data-k]'); if (!k) return;
    let s = String(parseMoney(cashInput.value) || '');
    s = k.dataset.k === 'back' ? s.slice(0, -1) : (s + k.dataset.k).replace(/^0+/, '');
    setCash(Number(s.slice(0, 10)) || 0);
  });
  $('[data-cash-form]').addEventListener('submit', async (e) => {
    e.preventDefault();
    const got = parseMoney(cashInput.value);
    if (got < (cart.total || 0)) { ui.showFieldErrors(e.target, { cashReceived: `Tiền khách đưa chưa đủ. Cần ít nhất ${money(cart.total)}.` }); return; }
    await checkout('CASH', got, $('[data-cash-confirm]'), cashModal);
  });

  async function openQr() {
    try {
      const d = await ui.api('/pos/payment-qr');
      $('[data-qr-img]').src = d.dataUri;
      $('[data-qr-code]').textContent = d.code;
      $('[data-qr-amount]').textContent = d.amountText;
      bootstrap.Modal.getOrCreateInstance(qrModal).show();
    } catch { /* đã báo */ }
  }
  $('[data-qr-confirm]').addEventListener('click', (e) => checkout('VIETQR_SIM', null, e.currentTarget, qrModal));

  async function checkout(method, cashReceived, button, modal) {
    try {
      const r = await ui.api('/pos/checkout', { method: 'POST', body: { method, cashReceived }, button });
      bootstrap.Modal.getOrCreateInstance(modal).hide();
      ui.toast.success(method === 'CASH' && r.change > 0 ? `Đã thanh toán ${r.code}. Tiền thối ${money(r.change)}.` : `Đã thanh toán ${r.code}.`);
      window.open(r.printUrl + '&auto=1', '_blank', 'noopener');
      renderCart({});
      await reloadAll();
    } catch (e) {
      if (e.code === 'PAYMENT_EXPIRED' || e.code === 'SEAT_TAKEN') { bootstrap.Modal.getOrCreateInstance(modal).hide(); await reloadAll(); }
      if (e.code === 'VALIDATION_FAILED' && e.details && e.details.cashReceived) ui.showFieldErrors($('[data-cash-form]'), e.details);
    }
  }

  // ---------------------------------------------------------------- phím tắt
  document.addEventListener('keydown', (e) => {
    if (document.querySelector('.modal.show')) return;
    if (e.key === 'F1') { e.preventDefault(); $('#pos-member-q').focus(); }
    else if (e.key === 'F4') { e.preventDefault(); const b = $('[data-pay="CASH"]'); if (!b.disabled) openCash(); }
    else if (e.key === 'Escape' && !root.classList.contains('drawer-open')) { if (cart.billId) { e.preventDefault(); cancelBill(); } }
    else if (e.key === 'Escape') closeDrawer();
  });

  // ---------------------------------------------------------------- khởi động
  if (window.realtime) realtime.onReload(() => reloadAll());
  renderCart(cart);
  if (showtimeId) {
    $$('.showtime-chip').forEach((c) => c.classList.toggle('is-active', Number(c.dataset.showtimeId) === showtimeId));
    loadSeatMap(showtimeId);
  } else if ((cart.items || []).length) switchTab('fnb');
  window.VoidBill?.init({ onDone: reloadAll });
})();
