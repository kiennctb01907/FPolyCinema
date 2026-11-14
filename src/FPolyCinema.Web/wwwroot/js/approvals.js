/* approvals.js – phê duyệt thời gian thực cho quản lý ca và quản lý rạp (UI A7, SPEC 3.6-22, kênh 7a manager_alerts_{cinemaId}).
   - APPROVAL_REQUESTED: popup ở góc phải, không chặn thao tác khác; nội dung hóa đơn, lý do, người yêu cầu; hai nút lớn Duyệt và Từ chối.
   - APPROVAL_DECIDED: đóng popup và bỏ dòng tương ứng trong danh sách chờ duyệt (người khác đã duyệt, hoặc yêu cầu hết hạn).
   - STOCK_LOW: thông báo nguyên liệu xuống dưới ngưỡng tối thiểu.
   Nút data-approval-decide (danh sách chờ duyệt ở dashboard) và data-approval-open (chuông) dùng chung cách xử lý ở đây. */
(function () {
  'use strict';
  const stack = document.querySelector('[data-approval-stack]');
  if (!stack) return;
  const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const cinemaIds = (stack.dataset.cinemaIds || '').split(',').filter(Boolean).map(Number);
  const shown = new Map(); // id → phần tử popup

  function chime() {
    try {
      const a = new (window.AudioContext || window.webkitAudioContext)();
      const o = a.createOscillator(), g = a.createGain();
      o.frequency.value = 740; g.gain.setValueAtTime(0.0001, a.currentTime);
      g.gain.exponentialRampToValueAtTime(0.15, a.currentTime + 0.02);
      g.gain.exponentialRampToValueAtTime(0.0001, a.currentTime + 0.3);
      o.connect(g).connect(a.destination); o.start(); o.stop(a.currentTime + 0.32);
    } catch { /* trình duyệt chặn âm thanh khi chưa tương tác */ }
  }

  function popup(p) {
    if (shown.has(p.id)) return;
    const el = document.createElement('section');
    el.className = 'approval-pop';
    el.setAttribute('role', 'dialog');
    el.setAttribute('aria-label', `Yêu cầu hủy hóa đơn ${p.billCode}`);
    el.dataset.popId = p.id;
    el.innerHTML = `<h2><i class="bi bi-shield-exclamation" aria-hidden="true"></i>Yêu cầu hủy hóa đơn</h2>
      <dl><dt>Hóa đơn</dt><dd><strong>${esc(p.billCode)}</strong>${p.totalText ? ` · <span class="money">${esc(p.totalText)}</span>` : ''}</dd>
      <dt>Lý do</dt><dd>${esc(p.reason)}</dd><dt>Người yêu cầu</dt><dd>${esc(p.requester)}${p.createdText ? ` · <span class="num">${esc(p.createdText)}</span>` : ''}</dd></dl>
      <div class="approval-actions">
        <button type="button" class="btn btn-success" data-approval-decide="approve" data-id="${p.id}" data-code="${esc(p.billCode)}"><i class="bi bi-check2" aria-hidden="true"></i>Duyệt</button>
        <button type="button" class="btn btn-danger" data-approval-decide="reject" data-id="${p.id}" data-code="${esc(p.billCode)}"><i class="bi bi-x-lg" aria-hidden="true"></i>Từ chối</button>
      </div>
      ${p.expiresAt ? `<p class="approval-expire">Hết hạn lúc <span class="num">${ui.time(p.expiresAt)}</span>. Để sau thì xem lại ở trang Vận hành ca.</p>` : ''}`;
    const close = document.createElement('button');
    close.type = 'button';
    close.className = 'btn-close position-absolute';
    close.setAttribute('aria-label', 'Ẩn popup (yêu cầu vẫn chờ ở trang Vận hành ca)');
    close.addEventListener('click', () => remove(p.id));
    el.classList.add('position-relative');
    close.classList.add('top-0', 'end-0', 'm-2');
    el.prepend(close);
    stack.prepend(el);
    shown.set(p.id, el);
  }

  function remove(id) {
    const el = shown.get(Number(id));
    if (el) { el.remove(); shown.delete(Number(id)); }
  }

  // Chuông phê duyệt: số và danh sách luôn vẽ lại theo /ops/approvals/pending (không cộng trừ dồn nên không lệch khi nhận sự kiện hai lần hay kết nối lại)
  function drawBell(list) {
    document.querySelectorAll('.bell').forEach((bell) => {
      const btn = bell.querySelector('[data-bs-toggle]');
      let pill = btn.querySelector('.count-pill');
      if (!list.length) { if (pill) pill.remove(); }
      else {
        if (!pill) { pill = document.createElement('span'); pill.className = 'count-pill'; pill.setAttribute('aria-hidden', 'true'); btn.appendChild(pill); }
        pill.textContent = list.length > 99 ? '99+' : String(list.length);
      }
      btn.setAttribute('aria-label', list.length ? `Có ${list.length} yêu cầu phê duyệt đang chờ` : 'Không có yêu cầu phê duyệt nào đang chờ');
      const body = bell.querySelector('[data-bell-body]');
      if (!body) return;
      body.innerHTML = !list.length
        ? '<p class="px-3 pb-3 mb-0 text-muted small">Không có yêu cầu nào đang chờ duyệt.</p>'
        : '<ul class="list-unstyled mb-0">' + list.slice(0, 5).map((a) => `<li data-approval-id="${a.id}"><button type="button" class="dropdown-item d-flex flex-column align-items-start"
            data-approval-open="${a.id}" data-code="${esc(a.billCode)}" data-reason="${esc(a.reason)}" data-requester="${esc(a.requester)}" data-time="${esc(a.createdText)}">
            <span class="fw-500">Hủy hóa đơn ${esc(a.billCode)}</span><span class="small text-muted">${esc(a.requester)} · <span class="num">${esc(a.createdText)}</span></span>
            <span class="small text-truncate-1">${esc(a.reason)}</span></button></li>`).join('') + '</ul><a class="dropdown-item text-primary small" href="/ops">Xem tất cả ở trang Vận hành ca</a>';
    });
  }
  async function refreshBell() {
    try { const list = await ui.api('/ops/approvals/pending', { silent: true }); drawBell(list); return list; } catch { return null; }
  }

  /** Bỏ yêu cầu đã xử lý khỏi màn hình và vẽ lại chuông theo danh sách chờ trên máy chủ. */
  function settled(id) {
    id = Number(id);
    remove(id);
    document.querySelectorAll(`[data-approval-list] [data-approval-id="${id}"]`).forEach((li) => {
      const list = li.parentElement;
      li.remove();
      if (list && !list.children.length) list.outerHTML = '<p class="text-muted mb-0" data-approval-empty>Không có yêu cầu nào đang chờ duyệt.</p>';
    });
    refreshBell();
  }

  document.addEventListener('click', async (e) => {
    const open = e.target.closest('[data-approval-open]');
    if (open) {
      popup({ id: Number(open.dataset.approvalOpen), billCode: open.dataset.code, reason: open.dataset.reason, requester: open.dataset.requester, createdText: open.dataset.time });
      return;
    }
    const b = e.target.closest('[data-approval-decide]');
    if (!b) return;
    const approve = b.dataset.approvalDecide === 'approve';
    if (!approve) {
      const ok = await ui.confirm({
        title: `Từ chối hủy hóa đơn ${b.dataset.code}?`, message: 'Hóa đơn giữ nguyên, máy POS nhận kết quả ngay.',
        confirmText: 'Từ chối', cancelText: 'Quay lại', tone: 'danger',
      });
      if (!ok) return;
    }
    try {
      const r = await ui.api(`/ops/approvals/${b.dataset.id}/${approve ? 'approve' : 'reject'}`, { method: 'POST', body: {}, button: b });
      ui.toast.success(r.message);
      settled(b.dataset.id);
    } catch (err) {
      // Yêu cầu đã được xử lý hoặc đã hết hạn: bỏ khỏi màn hình
      if (err && err.code === 'INVALID_TRANSITION') settled(b.dataset.id);
    }
  });

  if (!window.realtime) return;
  cinemaIds.forEach((id) => realtime.join('JoinManagerAlerts', id));
  realtime.on('APPROVAL_REQUESTED', (p) => {
    popup(p);
    refreshBell();
    chime();
    document.dispatchEvent(new CustomEvent('approval:changed'));
  });
  realtime.on('APPROVAL_DECIDED', (p) => {
    settled(p.id);
    document.dispatchEvent(new CustomEvent('approval:changed'));
  });
  realtime.on('STOCK_LOW', (p) => {
    const list = (p.materials || []).map((m) => m.text).join('; ');
    if (list) ui.toast.warning(`Nguyên liệu dưới ngưỡng tối thiểu: ${list}.`);
  });
  // Kết nối lại: vẽ lại chuông và hiện popup cho các yêu cầu đã tới trong lúc mất kết nối
  realtime.onReload(async () => {
    const list = await refreshBell();
    if (list) list.forEach(popup);
  });
})();
