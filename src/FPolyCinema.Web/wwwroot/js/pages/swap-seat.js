/* swap-seat.js – đổi, nâng hạng ghế tại quầy (SPEC 3.10-01..04, UC17).
   1. Quét mã vé hoặc mã hóa đơn → POST /pos/swap-seat/find: suất, các vé và lý do vé nào không đổi được.
   2. Tích vé cần đổi, bấm đúng số ghế mới trên sơ đồ (chọn tạm ở trình duyệt; ghế chỉ được giữ khi xác nhận).
   3. Mỗi lần đổi lựa chọn → POST /pos/swap-seat/quote: chênh lệch = giá mới − giá cũ; dương thì hiện ô thu thêm.
   4. Xác nhận → POST /pos/swap-seat (SeatHoldService giữ ghế mới trong transaction; vé cũ SWAPPED, vé mới QR mới); xong thì mở in vé. */
(function () {
  'use strict';
  const root = document.querySelector('[data-swap]');
  if (!root) return;
  const $ = (s) => root.querySelector(s);
  const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const findForm = $('[data-swap-find]');
  const body = $('[data-swap-body]');
  const region = $('[data-swap-seatmap]');
  const confirmBtn = $('[data-swap-confirm]');
  const pay = $('[data-swap-pay]');
  let bill = null, seatMap = null, quote = null, seq = 0, keepDone = false;

  const chosenTickets = () => [...root.querySelectorAll('[data-ticket]:checked')].map((b) => Number(b.value));
  const chosenSeats = () => (seatMap ? seatMap.selectedIds() : []);

  function resetQuote(hint) {
    quote = null;
    $('[data-swap-lines]').innerHTML = '';
    $('[data-swap-message]').textContent = '';
    $('[data-swap-hint]').textContent = hint;
    pay.hidden = true;
    confirmBtn.disabled = true;
  }

  async function refreshQuote() {
    const t = chosenTickets(), s = chosenSeats();
    if (!t.length) { resetQuote('Chọn vé cần đổi ở bên trái.'); return; }
    if (s.length !== t.length) { resetQuote(`Bấm chọn ${t.length} ghế mới trên sơ đồ (đã chọn ${s.length}).`); return; }
    const fd = new FormData();
    fd.append('billId', bill.billId);
    t.forEach((id) => fd.append('ticketIds', id));
    s.forEach((id) => fd.append('seatIds', id));
    const my = ++seq;
    try {
      const r = await ui.api('/pos/swap-seat/quote', { method: 'POST', body: fd, silent: true });
      if (my !== seq) return;
      quote = r;
      $('[data-swap-hint]').textContent = '';
      $('[data-swap-lines]').innerHTML = r.lines.map((l) => `<li><span>${esc(l.oldSeat)} → <strong>${esc(l.newSeat)}</strong></span><span class="money">${esc(l.oldPrice)} → ${esc(l.newPrice)}</span></li>`).join('');
      $('[data-swap-message]').textContent = r.message;
      pay.hidden = !r.upgrade;
      if (r.upgrade) $('#swap-cash').min = r.charge;
      confirmBtn.disabled = false;
    } catch (err) {
      if (my !== seq) return;
      resetQuote(err.message);
      if (seatMap && err.details) seatMap.violate(s);
    }
  }

  async function loadSeatMap(showtimeId) {
    if (seatMap) { seatMap.destroy(); seatMap = null; }
    try {
      const res = await fetch('/pos/swap-seat/seat-map?showtimeId=' + showtimeId, { headers: { 'X-Requested-With': 'XMLHttpRequest' }, credentials: 'same-origin' });
      if (!res.ok) throw new Error();
      region.innerHTML = await res.text();
      seatMap = SeatMap.attach(region);
      ui.enhance(region);
    } catch {
      region.innerHTML = '<p class="text-muted p-3 mb-0">Chưa tải được sơ đồ ghế. Bạn bấm Tìm vé để thử lại.</p>';
    }
  }

  // Chọn ghế mới: chỉ chọn tạm ở trình duyệt, không giữ ghế cho tới khi xác nhận
  region.addEventListener('seat-toggle', (e) => {
    if (!seatMap) return;
    const need = chosenTickets().length;
    if (e.detail.selecting && e.detail.seatIds.length > need) {
      ui.toast.warning(need ? `Chỉ chọn ${need} ghế mới cho ${need} vé cần đổi.` : 'Bạn tích vé cần đổi trước.');
      seatMap.violate(e.detail.changed);
      return;
    }
    seatMap.setSelection(e.detail.seatIds);
    refreshQuote();
  });
  root.addEventListener('change', (e) => {
    if (e.target.matches('[data-ticket]')) refreshQuote();
    if (e.target.name === 'method') $('[data-swap-cash]').hidden = e.target.value !== 'CASH';
  });

  findForm.addEventListener('submit', async (e) => {
    e.preventDefault();
    ui.clearFieldErrors(findForm);
    const code = findForm.code.value.trim();
    if (!code) { ui.showFieldErrors(findForm, { code: 'Bạn quét hoặc nhập mã vé, mã hóa đơn.' }); return; }
    try {
      bill = await ui.api('/pos/swap-seat/find', { method: 'POST', body: { code }, form: findForm, button: findForm.querySelector('[type="submit"]') });
    } catch { return; }
    body.hidden = false;
    if (!keepDone) $('[data-swap-done]').hidden = true;
    keepDone = false;
    $('[data-swap-code]').textContent = bill.code;
    $('[data-swap-show]').innerHTML = `<strong>${esc(bill.title)}</strong> <span class="age-badge age-${esc(bill.ageRating)}">${esc(bill.ageRating)}</span> · ${esc(bill.time)} · ${esc(bill.room)}`;
    $('[data-swap-tickets]').innerHTML = bill.tickets.map((t) => `<li><label class="${t.blocked ? 'is-off' : ''}">
        <input type="checkbox" value="${t.ticketId}" data-ticket ${t.blocked ? 'disabled' : ''} />
        <span class="flex-grow-1"><strong>Ghế ${esc(t.seat)}</strong> · ${esc(t.seatType)} · ${esc(t.audience)}${t.blocked ? `<span class="d-block small text-muted">${esc(t.blocked)}</span>` : ''}</span>
        <span class="money">${esc(t.price)}</span></label></li>`).join('');
    resetQuote('Chọn vé cần đổi ở bên trái.');
    await loadSeatMap(bill.showtimeId);
  });

  confirmBtn.addEventListener('click', async () => {
    if (!quote || !bill) return;
    const fd = new FormData();
    fd.append('billId', bill.billId);
    chosenTickets().forEach((id) => fd.append('ticketIds', id));
    chosenSeats().forEach((id) => fd.append('seatIds', id));
    if (quote.upgrade) {
      const method = root.querySelector('[name="method"]:checked').value;
      fd.append('method', method);
      if (method === 'CASH') fd.append('cashReceived', $('#swap-cash').value || '0');
    }
    const ok = await ui.confirm({
      title: 'Xác nhận đổi ghế?', message: quote.message,
      consequences: ['Vé cũ hết hiệu lực, QR cũ không vào cửa được.', 'Vé mới có QR mới trong cùng hóa đơn; in lại vé cho khách.'],
      confirmText: 'Đổi ghế', cancelText: 'Quay lại', tone: 'primary',
    });
    if (!ok) return;
    try {
      const r = await ui.api('/pos/swap-seat', { method: 'POST', body: fd, button: confirmBtn });
      const done = $('[data-swap-done]');
      done.hidden = false;
      done.innerHTML = `<i class="bi bi-check-circle" aria-hidden="true"></i><span>${esc(r.message)} <a href="${esc(r.printUrl)}" target="_blank" rel="noopener">In vé mới</a></span>`;
      ui.toast.success(r.message);
      confirmBtn.disabled = true;
      window.open(r.printUrl, '_blank', 'noopener');
      findForm.code.value = bill.code;
      keepDone = true;
      findForm.requestSubmit();
    } catch { refreshQuote(); }
  });
})();
