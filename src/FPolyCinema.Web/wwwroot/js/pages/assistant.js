/* assistant.js – C7. Trợ lý đặt vé (UI C7-01 → C7-04, C1-06; SPEC 4.2, UC13).
   - Nút tròn 56px góc phải dưới mở panel 380 × 600 (điện thoại: sheet toàn màn hình); Esc hoặc nút đóng để đóng, trả focus về nút.
   - Gửi câu: POST /assistant/message; khi chờ hiện bong bóng skeleton "Đang tìm suất phù hợp…".
   - Câu trả lời: bong bóng; danh sách suất (chọn một suất: POST /assistant/message với showtimeId); thẻ đặt vé; chip câu hỏi gợi ý.
   - "Giữ ghế & thanh toán ngay": POST /assistant/hold (giữ ghế qua SeatHoldService như luồng đặt vé thường) rồi chuyển sang bước thanh toán.
     Chưa đăng nhập thì ui.api chuyển tới trang đăng nhập và quay lại đúng trang. "Đổi lựa chọn": mở trang đặt vé của suất để tự chọn ghế.
   - Mở lại widget sau khi chuyển trang: GET /assistant/state đọc lại hội thoại trong bộ nhớ của phiên. */
(function () {
  'use strict';
  const root = document.querySelector('[data-assistant]');
  if (!root || !window.ui) return;
  const $ = (s, r = root) => r.querySelector(s);
  const esc = (v) => String(v ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const money = (n) => ui.money(n);
  const fab = $('[data-assistant-open]');
  const panel = $('#assistant-panel');
  const log = $('[data-assistant-log]');
  const chips = $('[data-assistant-chips]');
  const form = $('[data-assistant-form]');
  const input = $('#assistant-text');
  let busy = false;
  let loaded = false;

  function scrollEnd() { log.scrollTop = log.scrollHeight; }

  function bubble(text, who) {
    const b = document.createElement('div');
    b.className = 'bubble ' + (who === 'user' ? 'bubble-user' : 'bubble-bot');
    b.textContent = text;
    log.appendChild(b);
    scrollEnd();
    return b;
  }

  function thinking() {
    const b = document.createElement('div');
    b.className = 'bubble bubble-bot bubble-thinking';
    b.setAttribute('role', 'status');
    b.innerHTML = '<span>Đang tìm suất phù hợp…</span><span class="skeleton skeleton-line" aria-hidden="true"></span><span class="skeleton skeleton-line short" aria-hidden="true"></span>';
    log.appendChild(b);
    scrollEnd();
    return b;
  }

  function setChips(list) {
    chips.innerHTML = (list || []).map((c) => `<button type="button" class="chip-btn" data-chip>${esc(c)}</button>`).join('');
  }

  function ageBadge(card) {
    return `<span class="age-badge age-${esc(card.ageRating)}" role="img" aria-label="${esc(card.ageExplanation)}" title="${esc(card.ageExplanation)}">${esc(card.ageRating)}</span>`;
  }

  function miniMap(card) {
    const rows = (card.map || []).map((r) => {
      const seats = r.seats.map((s) => `<span class="mini-seat${s.state === 'selected' ? ' is-selected' : s.state === 'taken' ? ' is-taken' : ''}" style="--c: ${Number(s.col)}" title="${esc(s.label)}"></span>`).join('');
      return `<div class="mini-row"><span class="mini-row-name">${esc(r.name)}</span><span class="mini-seats" style="--cols: ${Number(card.cols)}">${seats}</span></div>`;
    }).join('');
    return `<div class="mini-map" role="img" aria-label="Sơ đồ quanh cụm ghế ${esc(card.range)}, ghế chọn tô đỏ"><span class="mini-screen" aria-hidden="true"></span><span class="mini-screen-label" aria-hidden="true">Màn hình</span>${rows}</div>`;
  }

  function cardHtml(card) {
    const seats = card.seats.map((s) => `<li><strong>${esc(s.label)}</strong> · ${s.score}/100 · ${money(s.price)}</li>`).join('');
    const items = (card.items || []).map((i) => `<div class="line"><span>${i.quantity} × ${esc(i.name)}</span><span class="money">${money(i.price * i.quantity)}</span></div>`).join('');
    const notes = (card.notes || []).length ? `<ul class="agent-notes">${card.notes.map((n) => `<li>${esc(n)}</li>`).join('')}</ul>` : '';
    return `<article class="agent-card" aria-label="Thẻ đặt vé ${esc(card.title)}">` +
      `<div class="agent-card-head"><img src="${esc(card.poster)}" alt="" width="48" height="72" onerror="this.onerror=null;this.src='/img/placeholder-poster.svg'" />` +
      `<div class="min-w-0"><h3>${esc(card.title)} ${ageBadge(card)}</h3>` +
      `<p>${esc(card.cinema)} · ${esc(card.room)}</p><p><strong>${esc(card.time)}</strong> · ${esc(card.date)} · ${esc(card.format)} · ${esc(card.language)}</p></div></div>` +
      miniMap(card) +
      `<ul class="agent-seats" aria-label="Ghế đề xuất">${seats}</ul>` +
      `<div class="agent-lines"><div class="line"><span>${card.quantity} vé · ${esc(card.range)} · ${card.score}/100</span><span class="money">${money(card.ticketTotal)}</span></div>${items}` +
      `<div class="line total"><span>Tổng tiền</span><span class="money">${money(card.total)}</span></div></div>` + notes +
      `<div class="agent-actions"><button type="button" class="btn btn-primary" data-agent-hold>Giữ ghế &amp; thanh toán ngay</button>` +
      `<a class="btn btn-ghost" href="${esc(card.bookingUrl)}">Đổi lựa chọn</a></div></article>`;
  }

  function showCard(card) {
    const wrap = document.createElement('div');
    wrap.className = 'agent-card-wrap';
    wrap.innerHTML = cardHtml(card);
    log.appendChild(wrap.firstElementChild);
    scrollEnd();
  }

  function showList(list) {
    if (!list || !list.length) return;
    const div = document.createElement('div');
    div.className = 'chip-list';
    div.setAttribute('aria-label', 'Các suất tìm được');
    div.innerHTML = list.map((s) => `<button type="button" class="chip-btn" data-pick="${Number(s.showtimeId)}">${esc(s.label)} · ${esc(s.cinemaName)} · còn ${Number(s.freeSeats)} ghế</button>`).join('');
    log.appendChild(div);
    scrollEnd();
  }

  function render(reply) {
    const first = bubble(reply.text, 'bot');
    if (reply.card) showCard(reply.card);
    showList(reply.showtimes);
    setChips(reply.chips);
    // Câu trả lời dài (có thẻ đặt vé): cuộn tới đầu câu trả lời để thấy cả thẻ từ trên xuống
    if (reply.card || (reply.showtimes || []).length > 2) log.scrollTop = Math.max(0, first.offsetTop - log.offsetTop - 8);
  }

  async function send(text, showtimeId) {
    if (busy) return;
    text = (text || '').trim();
    if (!text && !showtimeId) { input.focus(); return; }
    busy = true;
    if (text) bubble(text, 'user');
    const wait = thinking();
    try {
      const body = { text };
      if (showtimeId) body.showtimeId = showtimeId;
      const reply = await ui.api('/assistant/message', { method: 'POST', body, silent: true });
      wait.remove();
      render(reply);
    } catch (e) {
      wait.remove();
      bubble(e.message || 'Trợ lý đang bận. Bạn thử lại sau ít phút nhé.', 'bot');
    } finally {
      busy = false;
    }
  }

  async function restore() {
    if (loaded) return;
    loaded = true;
    try {
      const s = await ui.api('/assistant/state', { silent: true });
      if (!s.history || !s.history.length) return;
      s.history.forEach((t) => { if (t.text) bubble(t.text, t.role === 'user' ? 'user' : 'bot'); });
      if (s.card) showCard(s.card);
      setChips(s.chips);
    } catch { /* chưa có hội thoại thì giữ lời chào */ }
  }

  function open() {
    panel.hidden = false;
    fab.setAttribute('aria-expanded', 'true');
    restore();
    setTimeout(() => input.focus(), 50);
  }

  function close() {
    panel.hidden = true;
    fab.setAttribute('aria-expanded', 'false');
    fab.focus();
  }

  fab.addEventListener('click', () => (panel.hidden ? open() : close()));
  $('[data-assistant-close]').addEventListener('click', close);
  panel.addEventListener('keydown', (e) => { if (e.key === 'Escape') close(); });

  $('[data-assistant-reset]').addEventListener('click', async () => {
    try {
      const r = await ui.api('/assistant/reset', { method: 'POST' });
      log.innerHTML = '';
      bubble('Mình đã bắt đầu lại. Bạn muốn xem phim gì, vào lúc nào?', 'bot');
      setChips(r.chips);
    } catch { /* ui.api đã báo lỗi */ }
  });

  // Liên kết mở thẳng trợ lý: #tro-ly, hoặc #tro-ly?q=câu hỏi (ví dụ từ banner gợi ý)
  if (location.hash.startsWith('#tro-ly')) {
    const q = new URLSearchParams(location.hash.split('?')[1] || '').get('q');
    open();
    if (q) send(q);
  }

  form.addEventListener('submit', (e) => {
    e.preventDefault();
    const text = input.value;
    input.value = '';
    send(text);
  });

  root.addEventListener('click', async (e) => {
    const chip = e.target.closest('[data-chip]');
    if (chip) { send(chip.textContent); return; }
    const pick = e.target.closest('[data-pick]');
    if (pick) { bubble(pick.textContent, 'user'); send('', Number(pick.dataset.pick)); return; }
    const hold = e.target.closest('[data-agent-hold]');
    if (hold) {
      if (root.dataset.customer !== '1') {
        // Giữ ghế cần tài khoản khách hàng; thẻ đặt vé vẫn nằm trong phiên trợ lý sau khi đăng nhập
        bubble(root.dataset.signedIn === '1'
          ? 'Giữ ghế và thanh toán online dành cho tài khoản khách hàng. Bạn đăng nhập bằng tài khoản khách để tiếp tục nhé.'
          : 'Bạn đăng nhập tài khoản khách để giữ ghế. Thẻ đặt vé vẫn được giữ trong trợ lý; đăng nhập xong bạn mở lại trợ lý và bấm giữ ghế.', 'bot');
        if (root.dataset.signedIn !== '1') setTimeout(() => { location.href = '/Login/Login?ReturnUrl=' + encodeURIComponent(location.pathname + location.search); }, 1500);
        return;
      }
      try {
        const r = await ui.api('/assistant/hold', { method: 'POST', button: hold });
        location.href = r.redirect;
      } catch { /* ui.api đã báo lỗi (ghế vừa có người giữ, chưa đăng nhập…) */ }
    }
  });
})();
