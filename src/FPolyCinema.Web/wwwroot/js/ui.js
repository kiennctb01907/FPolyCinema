/* FPolyCinema – thư viện giao diện dùng chung (UI mục 5–6, 9–10). ES6 thuần, không phụ thuộc jQuery.
   API: ui.toast.success|error|warning|info, ui.confirm, ui.pin, ui.api, ui.money, ui.date, ui.time, ui.dateTime,
   ui.shake, ui.status, ui.busy, ui.loadRegion, ui.connection, ui.holdTimer. */
(() => {
  'use strict';

  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));
  const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const pad = (n) => String(n).padStart(2, '0');
  const reducedMotion = () => window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  const MSG = {
    offline: 'Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.',
    server: 'Máy chủ đang gặp sự cố nên chưa làm được việc này. Bạn thử lại sau ít phút.',
    saved: 'Đã lưu thay đổi',
    loadError: 'Chưa tải được dữ liệu. Bạn kiểm tra mạng rồi bấm Thử lại.',
  };

  // ---------------------------------------------------------------- định dạng (UI mục 9)
  const nf = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 });
  const toDate = (d) => (d instanceof Date ? d : new Date(d));
  const ui = {
    money: (n) => nf.format(Math.round(Number(n) || 0)) + ' ₫',
    number: (n) => nf.format(Math.round(Number(n) || 0)),
    date: (d) => { const x = toDate(d); return isNaN(x) ? '–' : `${pad(x.getDate())}/${pad(x.getMonth() + 1)}/${x.getFullYear()}`; },
    time: (d) => { const x = toDate(d); return isNaN(x) ? '–' : `${pad(x.getHours())}:${pad(x.getMinutes())}`; },
    dateTime: (d) => { const x = toDate(d); return isNaN(x) ? '–' : `${ui.date(x)} ${ui.time(x)}`; },
    weekdayDate: (d) => { const x = toDate(d); const w = x.getDay() === 0 ? 'CN' : 'T' + (x.getDay() + 1); return `${w}, ${pad(x.getDate())}/${pad(x.getMonth() + 1)}`; },
  };

  // ---------------------------------------------------------------- StatusDisplay xuất từ layout (UI mục 6)
  let statusMap = null;
  ui.status = (group, value) => {
    if (!statusMap) { try { statusMap = JSON.parse($('#status-display')?.textContent || '{}'); } catch { statusMap = {}; } }
    const g = statusMap[group] || {};
    return g[String(value)] || { label: 'Không xác định', tone: 'neutral' };
  };
  ui.statusBadge = (group, value) => { const s = ui.status(group, value); return `<span class="status-badge tone-${esc(s.tone)}">${esc(s.label)}</span>`; };

  // ---------------------------------------------------------------- rung báo lỗi
  ui.shake = (el) => {
    if (!el || reducedMotion()) return;
    el.classList.remove('shake');
    void el.offsetWidth;
    el.classList.add('shake');
    el.addEventListener('animationend', () => el.classList.remove('shake'), { once: true });
  };

  // ---------------------------------------------------------------- toast: góc phải trên, tối đa 3, 4 giây (lỗi 6 giây)
  const TOAST_ICON = { success: 'bi-check-circle-fill', error: 'bi-x-circle-fill', warning: 'bi-exclamation-triangle-fill', info: 'bi-info-circle-fill' };
  function stack() {
    let s = $('#toast-stack');
    if (!s) { s = document.createElement('div'); s.id = 'toast-stack'; s.className = 'toast-stack'; document.body.appendChild(s); }
    return s;
  }
  function removeToast(t) { if (!t.isConnected) return; t.classList.add('is-leaving'); setTimeout(() => t.remove(), 220); }
  function showToast(type, message, opts = {}) {
    const s = stack();
    while (s.children.length >= 3) s.firstElementChild.remove();
    const t = document.createElement('div');
    t.className = `ui-toast ${type}`;
    t.setAttribute('role', type === 'error' ? 'alert' : 'status');
    t.setAttribute('aria-live', type === 'error' ? 'assertive' : 'polite');
    t.innerHTML = `<i class="bi ${TOAST_ICON[type]}" aria-hidden="true"></i><div class="toast-msg">${esc(message)}${opts.code ? `<span class="toast-code">Mã hỗ trợ: ${esc(opts.code)}</span>` : ''}</div>` +
      '<button type="button" class="btn-close" aria-label="Đóng thông báo"></button>';
    t.querySelector('.btn-close').addEventListener('click', () => removeToast(t));
    s.appendChild(t);
    setTimeout(() => removeToast(t), opts.duration || (type === 'error' ? 6000 : 4000));
    return t;
  }
  ui.toast = {
    success: (m, o) => showToast('success', m, o),
    error: (m, o) => showToast('error', m, o),
    warning: (m, o) => showToast('warning', m, o),
    info: (m, o) => showToast('info', m, o),
  };

  // ---------------------------------------------------------------- hộp thoại (Bootstrap modal, giữ focus bên trong)
  function makeModal(inner, { danger = false } = {}) {
    const host = document.createElement('div');
    host.className = 'modal fade';
    host.tabIndex = -1;
    host.setAttribute('aria-modal', 'true');
    host.setAttribute('role', 'dialog');
    host.innerHTML = `<div class="modal-dialog modal-dialog-centered"><div class="modal-content">${inner}</div></div>`;
    // Theo theme của trang
    const theme = document.body.closest('[data-theme]')?.getAttribute('data-theme') || document.documentElement.getAttribute('data-theme');
    if (theme) host.setAttribute('data-theme', theme);
    if (theme === 'cinema') host.setAttribute('data-bs-theme', 'dark');
    document.body.appendChild(host);
    // Hộp thao tác nguy hiểm và hộp PIN không đóng bằng Esc hay bấm ra ngoài (UI mục 10)
    const modal = new bootstrap.Modal(host, danger ? { backdrop: 'static', keyboard: false } : { backdrop: true, keyboard: true });
    host.addEventListener('hidden.bs.modal', () => { modal.dispose(); host.remove(); });
    return { host, modal };
  }

  /**
   * ui.confirm({ title, message, consequences: [], confirmText, cancelText, tone: 'danger'|'primary', requireText })
   * trả Promise<boolean>. requireText: thao tác hàng loạt nguy hiểm, phải gõ lại đúng mã mới bấm được Xác nhận.
   */
  ui.confirm = (o = {}) => new Promise((resolve) => {
    const tone = o.tone || 'primary';
    // Hộp xác nhận thao tác nguy hiểm không đóng bằng Esc hay bấm ra ngoài (UI U10-08)
    const danger = tone === 'danger';
    const icon = tone === 'danger' ? 'bi-exclamation-triangle' : 'bi-question-circle';
    const iconTone = tone === 'danger' ? 'tone-danger' : 'tone-info';
    const cons = (o.consequences || []).length ? `<ul class="consequences">${o.consequences.map((c) => `<li>${esc(c)}</li>`).join('')}</ul>` : '';
    const req = o.requireText ? `<div class="mt-3"><label class="form-label" for="ui-confirm-code">Gõ <strong>${esc(o.requireText)}</strong> để xác nhận</label><input id="ui-confirm-code" class="form-control" autocomplete="off"></div>` : '';
    const { host, modal } = makeModal(
      `<div class="modal-header"><h2 class="modal-title" id="ui-confirm-title">${esc(o.title || 'Xác nhận')}</h2>${danger ? '' : '<button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Đóng"></button>'}</div>` +
      `<div class="modal-body"><div class="confirm-icon ${iconTone}"><i class="bi ${icon}" aria-hidden="true"></i></div><p class="mb-0">${esc(o.message || '')}</p>${cons}${req}</div>` +
      `<div class="modal-footer"><button type="button" class="btn btn-ghost" data-act="cancel">${esc(o.cancelText || 'Hủy')}</button>` +
      `<button type="button" class="btn ${tone === 'danger' ? 'btn-danger' : 'btn-primary'}" data-act="ok">${esc(o.confirmText || 'Xác nhận')}</button></div>`,
      { danger });
    host.setAttribute('aria-labelledby', 'ui-confirm-title');
    let result = false;
    const ok = host.querySelector('[data-act="ok"]');
    const code = host.querySelector('#ui-confirm-code');
    if (code) {
      ok.disabled = true;
      ok.title = 'Gõ đúng mã để bật nút này';
      code.addEventListener('input', () => { ok.disabled = code.value.trim() !== o.requireText; });
    }
    ok.addEventListener('click', () => { result = true; modal.hide(); });
    host.querySelector('[data-act="cancel"]').addEventListener('click', () => modal.hide());
    host.addEventListener('hidden.bs.modal', () => resolve(result));
    host.addEventListener('shown.bs.modal', () => (code || ok).focus());
    modal.show();
  });

  /**
   * ui.pin({ title, message, length: 6, verify: async (pin) => true | { message, locked } }) trả Promise<string|null>.
   * Bàn phím 3 × 4, phím 64px; nhập sai thì rung và báo số lần còn lại hoặc thời gian bị khóa. Không đóng bằng Esc.
   */
  ui.pin = (o = {}) => new Promise((resolve) => {
    const len = o.length || 6;
    const keys = ['1', '2', '3', '4', '5', '6', '7', '8', '9', 'clear', '0', 'back'];
    const keyHtml = keys.map((k) => k === 'clear' ? '<button type="button" data-k="clear" aria-label="Xóa hết">C</button>'
      : k === 'back' ? '<button type="button" data-k="back" aria-label="Xóa một số"><i class="bi bi-backspace" aria-hidden="true"></i></button>'
        : `<button type="button" data-k="${k}">${k}</button>`).join('');
    const { host, modal } = makeModal(
      `<div class="modal-header"><h2 class="modal-title" id="ui-pin-title">${esc(o.title || 'Nhập PIN quản lý')}</h2></div>` +
      `<div class="modal-body"><div class="pin-pad">${o.message ? `<p class="text-muted mb-0 text-center">${esc(o.message)}</p>` : ''}` +
      `<div class="pin-dots" aria-live="polite">${'<span></span>'.repeat(len)}</div><div class="pin-msg" role="alert"></div><div class="pin-keys">${keyHtml}</div></div></div>` +
      '<div class="modal-footer"><button type="button" class="btn btn-ghost" data-act="cancel">Hủy</button></div>',
      { danger: true });
    host.setAttribute('aria-labelledby', 'ui-pin-title');
    let value = '', result = null, busy = false;
    const dots = $$('.pin-dots span', host), msg = $('.pin-msg', host), pad = $('.pin-pad', host);
    const render = () => dots.forEach((d, i) => d.classList.toggle('filled', i < value.length));
    const lock = (on) => $$('.pin-keys button', host).forEach((b) => { b.disabled = on; });
    async function submit() {
      if (!o.verify) { result = value; modal.hide(); return; }
      busy = true; lock(true);
      try {
        const r = await o.verify(value);
        if (r === true) { result = value; modal.hide(); return; }
        msg.textContent = (r && r.message) || 'Mã PIN chưa đúng.';
        ui.shake(pad);
        value = ''; render();
        if (r && r.locked) { lock(true); return; }
      } catch { msg.textContent = MSG.offline; value = ''; render(); }
      busy = false; lock(false);
    }
    function press(k) {
      if (busy) return;
      if (k === 'clear') value = '';
      else if (k === 'back') value = value.slice(0, -1);
      else if (/^\d$/.test(k) && value.length < len) value += k;
      msg.textContent = '';
      render();
      if (value.length === len) submit();
    }
    host.addEventListener('click', (e) => { const b = e.target.closest('[data-k]'); if (b) press(b.dataset.k); });
    host.addEventListener('keydown', (e) => {
      if (/^\d$/.test(e.key)) press(e.key);
      else if (e.key === 'Backspace') press('back');
    });
    host.querySelector('[data-act="cancel"]').addEventListener('click', () => modal.hide());
    host.addEventListener('hidden.bs.modal', () => resolve(result));
    host.addEventListener('shown.bs.modal', () => host.querySelector('[data-k="1"]').focus());
    modal.show();
  });

  // ---------------------------------------------------------------- nút đang xử lý: spinner thay icon, khóa nút, giữ nguyên rộng
  ui.busy = (btn, on) => {
    if (!btn) return;
    if (on) {
      if (btn.classList.contains('is-busy')) return;
      btn.style.setProperty('--busy-w', btn.getBoundingClientRect().width + 'px');
      btn.classList.add('is-busy');
      btn.disabled = true;
      btn.setAttribute('aria-busy', 'true');
      const sp = document.createElement('span');
      sp.className = 'spinner-border';
      sp.setAttribute('aria-hidden', 'true');
      btn.prepend(sp);
    } else {
      btn.classList.remove('is-busy');
      btn.disabled = false;
      btn.removeAttribute('aria-busy');
      btn.style.removeProperty('--busy-w');
      btn.querySelector(':scope > .spinner-border')?.remove();
    }
  };

  // ---------------------------------------------------------------- lỗi theo ô
  ui.clearFieldErrors = (form) => {
    if (!form) return;
    $$('.is-invalid', form).forEach((el) => el.classList.remove('is-invalid'));
    $$('[data-js-error]', form).forEach((el) => el.remove());
  };
  ui.showFieldErrors = (form, details) => {
    if (!form || !details || typeof details !== 'object') return;
    let first = null;
    Object.entries(details).forEach(([name, message]) => {
      const input = form.querySelector(`[name="${CSS.escape(name)}"]`) || form.querySelector(`[data-field="${CSS.escape(name)}"]`);
      const slot = form.querySelector(`[data-valmsg-for="${CSS.escape(name)}"]`);
      if (input) { input.classList.add('is-invalid'); input.setAttribute('aria-invalid', 'true'); first = first || input; }
      if (slot) { slot.textContent = message; slot.classList.remove('field-validation-valid'); slot.classList.add('field-validation-error'); }
      else if (input) {
        const e = document.createElement('div');
        e.className = 'field-error'; e.dataset.jsError = '1'; e.textContent = message;
        (input.closest('.input-password, .input-money, .date-field, .search-box') || input).after(e);
      }
    });
    first?.focus();
  };

  // ---------------------------------------------------------------- ui.api: Anti-Forgery, trạng thái đang tải, lỗi CONTRACTS mục 8 thành toast rồi ném lỗi
  class ApiError extends Error {
    constructor(code, message, details, status) { super(message); this.code = code; this.details = details; this.status = status; }
  }
  ui.ApiError = ApiError;
  const csrf = () => $('meta[name="csrf-token"]')?.content || '';
  ui.api = async (url, opts = {}) => {
    const method = (opts.method || 'GET').toUpperCase();
    const headers = { 'X-Requested-With': 'XMLHttpRequest', Accept: 'application/json' };
    let body = opts.body;
    if (method !== 'GET') headers.RequestVerificationToken = csrf();
    if (body && !(body instanceof FormData) && typeof body === 'object') {
      const fd = new URLSearchParams();
      Object.entries(body).forEach(([k, v]) => { if (v !== undefined && v !== null) fd.append(k, String(v)); });
      body = fd;
    }
    ui.busy(opts.button, true);
    if (opts.form) ui.clearFieldErrors(opts.form);
    let res, json = null;
    try {
      res = await fetch(url, { method, headers, body: method === 'GET' ? undefined : body, credentials: 'same-origin' });
    } catch {
      ui.busy(opts.button, false);
      if (!opts.silent) ui.toast.error(MSG.offline);
      throw new ApiError('NETWORK', MSG.offline, null, 0);
    }
    try { json = await res.json(); } catch { json = null; }
    ui.busy(opts.button, false);
    if (res.ok && json && json.ok !== false) return json.data;
    const err = (json && json.error) || { code: 'SERVER_ERROR', message: MSG.server };
    const ex = new ApiError(err.code, err.message || MSG.server, err.details, res.status);
    if (opts.form && err.code === 'VALIDATION_FAILED' && err.details) ui.showFieldErrors(opts.form, err.details);
    if (res.status === 401 && !opts.silent) {
      ui.toast.warning(err.message);
      setTimeout(() => { location.href = '/Login/Login?ReturnUrl=' + encodeURIComponent(location.pathname + location.search); }, 1200);
    } else if (!opts.silent) {
      ui.toast.error(err.message, { code: err.details && err.details.supportCode });
    }
    throw ex;
  };

  // ---------------------------------------------------------------- vùng tải dữ liệu: đang tải (skeleton sau 250ms), rỗng, lỗi + Thử lại, có dữ liệu
  ui.loadRegion = async (region, url, { push = 'replace' } = {}) => {
    if (!region) return;
    const content = $('[data-region-content]', region);
    region.dataset.url = url;
    const timer = setTimeout(() => region.classList.add('is-loading'), 250);
    region.setAttribute('aria-busy', 'true');
    try {
      const res = await fetch(url + (url.includes('?') ? '&' : '?') + 'partial=1', { headers: { 'X-Requested-With': 'XMLHttpRequest', Accept: 'text/html' }, credentials: 'same-origin' });
      if (!res.ok) throw new Error(String(res.status));
      content.innerHTML = await res.text();
      if (push === 'replace') history.replaceState(null, '', url);
      else if (push === 'push') history.pushState(null, '', url);
      enhance(content);
    } catch {
      content.innerHTML = `<div class="empty-state tone-error" role="alert"><div class="empty-icon"><i class="bi bi-wifi-off" aria-hidden="true"></i></div>` +
        `<h3>Chưa tải được dữ liệu</h3><p>${esc(MSG.loadError)}</p><button type="button" class="btn btn-secondary" data-region-retry><i class="bi bi-arrow-clockwise" aria-hidden="true"></i>Thử lại</button></div>`;
    } finally {
      clearTimeout(timer);
      region.classList.remove('is-loading');
      region.removeAttribute('aria-busy');
    }
  };
  const reloadRegion = (el) => { const r = el.closest('[data-region]') || $('[data-region]'); if (r) ui.loadRegion(r, r.dataset.url || location.pathname + location.search); else location.reload(); };

  // ---------------------------------------------------------------- thanh lọc: tìm sau 300ms ngừng gõ, giữ trên query string
  function filterUrl(form) {
    const p = new URLSearchParams();
    new FormData(form).forEach((v, k) => { if (String(v).trim() !== '') p.set(k, String(v).trim()); });
    const qs = p.toString();
    return (form.getAttribute('action') || location.pathname) + (qs ? '?' + qs : '');
  }
  function initFilter(form) {
    const region = $(form.dataset.filterFor || '[data-region]');
    const go = () => { if (region) ui.loadRegion(region, filterUrl(form)); else location.href = filterUrl(form); };
    let t;
    $$('input[type="search"], input[data-search]', form).forEach((inp) => {
      const clear = inp.parentElement.querySelector('.btn-clear');
      const sync = () => { if (clear) clear.hidden = !inp.value; };
      sync();
      inp.addEventListener('input', () => { sync(); clearTimeout(t); t = setTimeout(go, 300); });
      inp.addEventListener('keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); clearTimeout(t); go(); } });
      clear?.addEventListener('click', () => { inp.value = ''; sync(); inp.focus(); go(); });
    });
    $$('select, input[type="date"]', form).forEach((s) => s.addEventListener('change', go));
    $$('[data-range]', form).forEach((b) => b.addEventListener('click', () => {
      const from = form.querySelector('[data-range-from]'), to = form.querySelector('[data-range-to]');
      const d = new Date(), iso = (x) => `${x.getFullYear()}-${pad(x.getMonth() + 1)}-${pad(x.getDate())}`;
      let start = new Date(d);
      if (b.dataset.range === '7') start.setDate(d.getDate() - 6);
      else if (b.dataset.range === '30') start.setDate(d.getDate() - 29);
      else if (b.dataset.range === 'month') start = new Date(d.getFullYear(), d.getMonth(), 1);
      if (from) { from.value = iso(start); from.dispatchEvent(new Event('input')); }
      if (to) { to.value = iso(d); to.dispatchEvent(new Event('input')); }
      $$('[data-range]', form).forEach((x) => x.classList.toggle('active', x === b));
      go();
    }));
    form.addEventListener('submit', (e) => { e.preventDefault(); go(); });
    $$('[data-filter-clear]', form).forEach((b) => b.addEventListener('click', (e) => {
      e.preventDefault();
      $$('input, select', form).forEach((x) => { if (x.type !== 'hidden') x.value = ''; x.dispatchEvent(new Event('input')); });
      $$('[data-range]', form).forEach((x) => x.classList.remove('active'));
      go();
    }));
  }

  // ---------------------------------------------------------------- công tắc Active: đổi lạc quan, lỗi thì trả về như cũ và báo
  async function onActiveToggle(input) {
    const wrap = input.closest('.active-switch');
    const want = input.checked;
    const label = wrap?.querySelector('[data-active-label]');
    const setLabel = (on) => { if (label) { const s = ui.status('active', on); label.textContent = s.label; } };
    setLabel(want);
    wrap?.classList.add('is-busy');
    try {
      await ui.api(input.dataset.activeUrl, { method: 'POST', body: { active: want } });
      ui.toast.success(want ? (input.dataset.onDone || 'Đã bật hoạt động') : (input.dataset.offDone || 'Đã chuyển sang ngưng'));
    } catch {
      input.checked = !want;
      setLabel(!want);
    } finally {
      wrap?.classList.remove('is-busy');
    }
  }

  // ---------------------------------------------------------------- xóa: hỏi trước, nêu hậu quả; không được xóa thì giải thích và gợi ý ngưng hoạt động
  async function onDelete(btn) {
    const name = btn.dataset.name || 'mục này';
    let check;
    try { check = await ui.api(btn.dataset.checkUrl, { button: btn }); } catch { return; }
    if (!check.allowed) {
      const canDeactivate = !!check.canDeactivate && !!btn.dataset.deactivateUrl;
      const go = await ui.confirm({
        title: `Không xóa được “${name}”`,
        message: check.reason,
        confirmText: canDeactivate ? (btn.dataset.deactivateText || 'Ngưng hoạt động') : 'Đã hiểu',
        cancelText: canDeactivate ? 'Để sau' : 'Đóng',
        tone: 'primary',
      });
      if (go && canDeactivate) {
        try {
          await ui.api(btn.dataset.deactivateUrl, { method: 'POST', body: { active: false } });
          ui.toast.success(btn.dataset.deactivateDone || 'Đã chuyển sang ngưng hoạt động');
          reloadRegion(btn);
        } catch { /* đã báo bằng toast */ }
      }
      return;
    }
    const ok = await ui.confirm({ title: `Xóa “${name}”?`, message: 'Bạn kiểm tra lại trước khi xóa.', consequences: check.consequences || [], confirmText: 'Xóa', tone: 'danger' });
    if (!ok) return;
    try {
      await ui.api(btn.dataset.deleteUrl, { method: 'POST', button: btn });
      ui.toast.success(`Đã xóa “${name}”`);
      if (btn.dataset.afterDelete) setTimeout(() => { location.href = btn.dataset.afterDelete; }, 600);
      else reloadRegion(btn);
    } catch { /* đã báo bằng toast */ }
  }

  // ---------------------------------------------------------------- ô tiền: hậu tố ₫, tự chấm phân cách hàng nghìn
  function initMoney(inp) {
    const hidden = document.getElementById(inp.dataset.money);
    const fmt = () => {
      const digits = inp.value.replace(/\D/g, '').replace(/^0+(?=\d)/, '');
      inp.value = digits ? nf.format(Number(digits)) : '';
      if (hidden) { hidden.value = digits || '0'; hidden.dispatchEvent(new Event('change', { bubbles: true })); }
    };
    if (hidden && Number(hidden.value) > 0 && !inp.value) inp.value = nf.format(Number(hidden.value));
    inp.addEventListener('input', fmt);
  }

  // ---------------------------------------------------------------- ô ngày: input gốc, hiển thị dd/MM/yyyy (ô ngày giờ: dd/MM/yyyy HH:mm, 24 giờ)
  function initDateField(wrap) {
    const inp = $('input', wrap), out = $('.date-display', wrap);
    const withTime = inp.type === 'datetime-local';
    const sync = () => {
      if (!inp.value) { out.textContent = withTime ? 'dd/mm/yyyy --:--' : 'dd/mm/yyyy'; out.classList.add('is-empty'); return; }
      const [date, time] = inp.value.split('T');
      const [y, m, d] = date.split('-');
      out.textContent = `${d}/${m}/${y}` + (withTime && time ? ' ' + time.slice(0, 5) : ''); out.classList.remove('is-empty');
    };
    inp.addEventListener('input', sync); inp.addEventListener('change', sync); sync();
  }

  // ---------------------------------------------------------------- mật khẩu: hiện/ẩn, thước đo độ mạnh
  function togglePw(btn) {
    const inp = btn.parentElement.querySelector('input');
    const show = inp.type === 'password';
    inp.type = show ? 'text' : 'password';
    btn.setAttribute('aria-pressed', String(show));
    btn.setAttribute('aria-label', show ? 'Ẩn mật khẩu' : 'Hiện mật khẩu');
    btn.querySelector('.bi').className = 'bi ' + (show ? 'bi-eye-slash' : 'bi-eye');
  }
  ui.passwordScore = (pw) => {
    if (!pw) return 0;
    let kinds = 0;
    if (/[a-zà-ỹ]/.test(pw)) kinds++;
    if (/[A-ZÀ-Ỹ]/.test(pw)) kinds++;
    if (/\d/.test(pw)) kinds++;
    if (/[^\p{L}\p{N}]/u.test(pw)) kinds++;
    if (pw.length < 8) return kinds >= 2 ? 1 : 0;
    let s = 1; if (kinds >= 2) s++; if (kinds >= 3) s++; if (pw.length >= 12 && kinds >= 3) s++;
    return Math.min(s, 4);
  };
  const PW_LABEL = ['Quá ngắn', 'Yếu', 'Trung bình', 'Khá', 'Mạnh'];
  function initPwMeter(inp) {
    const meter = document.getElementById(inp.dataset.pwMeter);
    const label = meter && document.getElementById(meter.id + '-label');
    const sync = () => { const s = ui.passwordScore(inp.value); meter.dataset.score = String(s); if (label) label.textContent = inp.value ? 'Độ mạnh: ' + PW_LABEL[s] : 'Ít nhất 8 ký tự, có cả chữ và số.'; };
    inp.addEventListener('input', sync); sync();
  }

  // ---------------------------------------------------------------- 6 ô OTP: tự nhảy ô, dán được cả chuỗi
  function initOtp(box) {
    const cells = $$('input[data-otp-cell]', box);
    const hidden = document.getElementById(box.dataset.otp);
    const sync = () => { if (hidden) hidden.value = cells.map((c) => c.value).join(''); box.dispatchEvent(new CustomEvent('otp-change', { detail: hidden?.value })); };
    cells.forEach((c, i) => {
      c.addEventListener('input', () => {
        const digits = c.value.replace(/\D/g, '');
        if (digits.length > 1) { digits.split('').forEach((d, k) => { if (cells[i + k]) cells[i + k].value = d; }); cells[Math.min(i + digits.length, cells.length) - 1].focus(); }
        else { c.value = digits; if (digits && cells[i + 1]) cells[i + 1].focus(); }
        sync();
      });
      c.addEventListener('keydown', (e) => {
        if (e.key === 'Backspace' && !c.value && cells[i - 1]) { cells[i - 1].focus(); cells[i - 1].value = ''; sync(); e.preventDefault(); }
        if (e.key === 'ArrowLeft' && cells[i - 1]) cells[i - 1].focus();
        if (e.key === 'ArrowRight' && cells[i + 1]) cells[i + 1].focus();
      });
      c.addEventListener('paste', (e) => {
        const t = (e.clipboardData || window.clipboardData).getData('text').replace(/\D/g, '');
        if (!t) return;
        e.preventDefault();
        t.slice(0, cells.length).split('').forEach((d, k) => { cells[k].value = d; });
        cells[Math.min(t.length, cells.length) - 1].focus();
        sync();
      });
    });
    ui.otpReset = (b = box) => { $$('input[data-otp-cell]', b).forEach((c) => { c.value = ''; }); sync(); cells[0].focus(); };
  }

  // ---------------------------------------------------------------- cảnh báo khi rời trang mà chưa lưu
  function initDirtyGuard(form) {
    let dirty = false, submitting = false;
    const initial = new URLSearchParams(new FormData(form)).toString();
    const check = () => { dirty = new URLSearchParams(new FormData(form)).toString() !== initial || form.dataset.forceDirty === '1'; };
    form.addEventListener('input', check);
    form.addEventListener('change', check);
    form.addEventListener('submit', () => { submitting = true; });
    window.addEventListener('beforeunload', (e) => { if (dirty && !submitting) { e.preventDefault(); e.returnValue = ''; } });
    document.addEventListener('click', async (e) => {
      const a = e.target.closest('a[href]');
      if (!a || !dirty || submitting || a.target === '_blank' || a.getAttribute('href').startsWith('#') || e.defaultPrevented) return;
      e.preventDefault();
      const leave = await ui.confirm({ title: 'Rời trang khi chưa lưu?', message: 'Các thay đổi trên form sẽ mất nếu bạn rời trang bây giờ.', confirmText: 'Rời trang', cancelText: 'Ở lại', tone: 'danger' });
      if (leave) { dirty = false; location.href = a.href; }
    });
    ui.markClean = () => { dirty = false; };
  }

  // ---------------------------------------------------------------- vùng kéo thả ảnh: tải lên ngay bằng fetch, xem trước, nút xóa ảnh
  function initDropzone(zone) {
    const input = $('input[type="file"]', zone.parentElement) || $('input[type="file"]', zone);
    const hidden = document.getElementById(zone.dataset.target);
    const img = $('img', zone);
    const removeBtn = document.querySelector(`[data-dropzone-remove="${zone.id}"]`);
    const pickBtn = document.querySelector(`[data-dropzone-pick="${zone.id}"]`);
    const saved = hidden.value;           // ảnh đang lưu trong CSDL: chỉ xóa khi bấm Lưu (server tự xóa ảnh cũ)
    let session = null;                   // ảnh vừa tải lên trong lần sửa này (chưa lưu)
    const subject = zone.dataset.subject || 'món';  // "món" hoặc "phim"
    const noun = zone.dataset.noun || 'ảnh';         // "ảnh" hoặc "poster"
    const show = (src) => {
      zone.classList.toggle('has-image', !!src);
      if (src) { img.src = src; img.hidden = false; } else { img.removeAttribute('src'); img.hidden = true; }
      if (removeBtn) removeBtn.disabled = !src;
      const why = document.querySelector(`[data-dropzone-why="${zone.id}"]`);
      if (why) why.textContent = src ? '' : `Nút Xóa ${noun} mở khi ${subject} đã có ${noun}.`;
      if (removeBtn) removeBtn.title = src ? `Gỡ ${noun} khỏi ${subject}` : `Chưa có ${noun} nên chưa xóa được`;
      hidden.value = src || '';
      hidden.dispatchEvent(new Event('change', { bubbles: true }));
    };
    const dropSession = async () => {
      if (!session) return;
      const s = session; session = null;
      try { await ui.api(zone.dataset.deleteUrl, { method: 'POST', body: { fileName: s }, silent: true }); } catch { /* ảnh mồ côi không ảnh hưởng dữ liệu */ }
    };
    async function upload(file) {
      if (!file) return;
      if (!/^image\/(jpeg|png|gif|webp)$/.test(file.type)) { ui.toast.error('Chỉ nhận ảnh .jpg, .jpeg, .png, .gif hoặc .webp. Bạn chọn ảnh khác.'); ui.shake(zone); return; }
      if (file.size > 5 * 1024 * 1024) { ui.toast.error('Ảnh vượt quá 5MB. Bạn chọn ảnh nhỏ hơn.'); ui.shake(zone); return; }
      const fd = new FormData(); fd.append('file', file);
      zone.classList.add('is-busy'); zone.setAttribute('aria-busy', 'true');
      try {
        const data = await ui.api(zone.dataset.uploadUrl, { method: 'POST', body: fd });
        await dropSession();
        session = data.path;
        show(data.path);
        ui.toast.success(`Đã tải ${noun} lên. Bấm Lưu để áp dụng.`);
      } catch { ui.shake(zone); }
      finally { zone.classList.remove('is-busy'); zone.removeAttribute('aria-busy'); input.value = ''; }
    }
    zone.addEventListener('click', () => input.click());
    zone.addEventListener('keydown', (e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); input.click(); } });
    pickBtn?.addEventListener('click', () => input.click());
    input.addEventListener('change', () => upload(input.files[0]));
    ['dragenter', 'dragover'].forEach((ev) => zone.addEventListener(ev, (e) => { e.preventDefault(); zone.classList.add('is-over'); }));
    ['dragleave', 'drop'].forEach((ev) => zone.addEventListener(ev, (e) => { e.preventDefault(); zone.classList.remove('is-over'); }));
    zone.addEventListener('drop', (e) => upload(e.dataTransfer.files[0]));
    removeBtn?.addEventListener('click', async () => {
      if (session) await dropSession();
      show('');
      if (saved) ui.toast.info(`${noun[0].toUpperCase() + noun.slice(1)} sẽ được gỡ khỏi ${subject} khi bạn bấm Lưu.`);
    });
    show(hidden.value);
  }

  // ---------------------------------------------------------------- đồng hồ giữ ghế mm:ss; ≤ 2 phút warning, ≤ 1 phút danger, về 0 mở hộp "Hết thời gian giữ ghế"
  ui.holdTimer = (el, expiresAt, { onExpire } = {}) => {
    const end = toDate(expiresAt).getTime();
    const out = $('[data-time]', el) || el;
    let fired = false;
    const tick = () => {
      const left = Math.max(0, Math.round((end - Date.now()) / 1000));
      out.textContent = `${pad(Math.floor(left / 60))}:${pad(left % 60)}`;
      el.classList.toggle('is-warning', left <= 120 && left > 60);
      el.classList.toggle('is-danger', left <= 60);
      el.setAttribute('aria-label', `Còn ${Math.floor(left / 60)} phút ${left % 60} giây giữ ghế`);
      if (left === 0 && !fired) {
        fired = true; clearInterval(h);
        if (onExpire) onExpire();
        else {
          const restart = el.dataset.restartUrl;
          const { host, modal } = makeModal(
            '<div class="modal-header"><h2 class="modal-title" id="ui-expired-title">Hết thời gian giữ ghế</h2></div>' +
            '<div class="modal-body"><div class="confirm-icon tone-warning"><i class="bi bi-hourglass-bottom" aria-hidden="true"></i></div><p class="mb-0">Đã hết thời gian giữ ghế. Bạn chọn lại ghế để tiếp tục nhé.</p></div>' +
            '<div class="modal-footer"><button type="button" class="btn btn-primary" data-act="restart">Chọn lại</button></div>');
          host.setAttribute('aria-labelledby', 'ui-expired-title');
          host.querySelector('[data-act="restart"]').addEventListener('click', () => { modal.hide(); if (restart) location.href = restart; });
          modal.show();
        }
      }
    };
    const h = setInterval(tick, 1000); tick();
    return () => clearInterval(h);
  };
  function initCountdown(el) {
    const end = toDate(el.dataset.expiresAt).getTime();
    const tick = () => { const left = Math.max(0, Math.round((end - Date.now()) / 1000)); el.textContent = `${pad(Math.floor(left / 60))}:${pad(left % 60)}`; if (!left) clearInterval(h); };
    const h = setInterval(tick, 1000); tick();
  }

  // ---------------------------------------------------------------- chỉ báo kết nối thời gian thực; layout hiển thị hiện lớp phủ khi mất kết nối
  const CONN_TEXT = { connected: 'Đã kết nối', reconnecting: 'Đang kết nối lại', lost: 'Mất kết nối' };
  ui.connection = {
    state: 'connected',
    set(state) {
      this.state = CONN_TEXT[state] ? state : 'lost';
      $$('[data-conn-indicator]:not([data-conn-static])').forEach((el) => { el.dataset.state = this.state; const t = $('.conn-text', el); if (t) t.textContent = CONN_TEXT[this.state]; });
      $('#reconnect-overlay')?.classList.toggle('is-visible', this.state !== 'connected');
      document.dispatchEvent(new CustomEvent('connection-change', { detail: this.state }));
    },
  };

  // ---------------------------------------------------------------- đồng hồ theo giờ máy chủ (giờ Việt Nam)
  function initClock(el) {
    const serverNow = toDate(el.dataset.serverNow).getTime();
    const offset = isNaN(serverNow) ? 0 : serverNow - Date.now();
    const sec = el.dataset.clock === 'seconds';
    const tick = () => { const d = new Date(Date.now() + offset); el.textContent = `${pad(d.getHours())}:${pad(d.getMinutes())}${sec ? ':' + pad(d.getSeconds()) : ''}`; };
    tick(); setInterval(tick, 1000);
  }

  // ---------------------------------------------------------------- lưu trạng thái thu gọn (sidebar, thanh demo)
  const store = {
    get: (k) => { try { return localStorage.getItem(k); } catch { return null; } },
    set: (k, v) => { try { localStorage.setItem(k, v); } catch { /* chế độ riêng tư */ } },
  };

  // ---------------------------------------------------------------- gắn hành vi cho một vùng DOM (gọi lại sau khi tải vùng)
  function enhance(root = document) {
    if (window.bootstrap) $$('[data-bs-toggle="tooltip"]', root).forEach((el) => bootstrap.Tooltip.getOrCreateInstance(el));
    $$('[data-money]', root).forEach((el) => { if (!el.dataset.moneyInit) { el.dataset.moneyInit = '1'; initMoney(el); } });
    $$('.date-field', root).forEach((el) => { if (!el.dataset.init) { el.dataset.init = '1'; initDateField(el); } });
    $$('[data-pw-meter]', root).forEach((el) => { if (!el.dataset.init) { el.dataset.init = '1'; initPwMeter(el); } });
    $$('[data-otp]', root).forEach((el) => { if (!el.dataset.init) { el.dataset.init = '1'; initOtp(el); } });
    $$('[data-countdown]', root).forEach((el) => { if (!el.dataset.init) { el.dataset.init = '1'; initCountdown(el); } });
    $$('[data-hold-timer]', root).forEach((el) => { if (!el.dataset.init && el.dataset.expiresAt) { el.dataset.init = '1'; ui.holdTimer(el, el.dataset.expiresAt); } });
  }
  ui.enhance = enhance;

  document.addEventListener('click', (e) => {
    const t = e.target;
    const pw = t.closest('.btn-toggle-pw'); if (pw) { togglePw(pw); return; }
    // Bộ tăng giảm số lượng của thẻ món (− số +)
    const qb = t.closest('.qty-stepper [data-qty]');
    if (qb) {
      const box = qb.closest('.qty-stepper'), out = box.querySelector('.qty');
      const n = Math.max(0, (parseInt(out.textContent, 10) || 0) + Number(qb.dataset.qty));
      out.textContent = String(n);
      const minus = box.querySelector('[data-qty="-1"]'); if (minus) minus.disabled = n === 0;
      box.dispatchEvent(new CustomEvent('qty-change', { bubbles: true, detail: n }));
      return;
    }
    const del = t.closest('[data-delete-url]'); if (del) { e.preventDefault(); onDelete(del); return; }
    const retry = t.closest('[data-region-retry]'); if (retry) { reloadRegion(retry); return; }
    const link = t.closest('[data-region] a[data-region-link]');
    if (link && !e.ctrlKey && !e.metaKey) { e.preventDefault(); ui.loadRegion(link.closest('[data-region]'), link.getAttribute('href'), { push: 'push' }); return; }
    const sb = t.closest('[data-sidebar-toggle]');
    if (sb) { const shell = $('.admin-shell'); shell.classList.toggle('is-collapsed'); const c = shell.classList.contains('is-collapsed'); store.set('fpc.sidebar', c ? '1' : '0'); sb.setAttribute('aria-expanded', String(!c)); return; }
    const demo = t.closest('[data-demo-toggle]');
    if (demo) { const bar = demo.closest('.demo-bar'); bar.classList.toggle('is-collapsed'); const c = bar.classList.contains('is-collapsed'); store.set('fpc.demo', c ? '1' : '0'); demo.setAttribute('aria-expanded', String(!c)); }
  });
  document.addEventListener('change', (e) => {
    const sw = e.target.closest('input[data-active-url]'); if (sw) { onActiveToggle(sw); return; }
    const cp = e.target.closest('select[data-cinema-picker]'); if (cp) cp.form.submit();
  });
  // Form POST thường (không qua fetch): khi thực sự gửi đi thì nút gửi chuyển sang đang xử lý, chống bấm hai lần (UI U5-04)
  document.addEventListener('submit', (e) => {
    const f = e.target;
    if (!(f instanceof HTMLFormElement) || (f.method || '').toLowerCase() !== 'post') return;
    setTimeout(() => {
      if (e.defaultPrevented) return;
      const btn = e.submitter || f.querySelector('button[type="submit"]');
      if (btn && !btn.classList.contains('dropdown-item')) ui.busy(btn, true);
    }, 0);
  });
  // Quay lại trang bằng nút Back (bfcache) thì bỏ trạng thái đang xử lý
  window.addEventListener('pageshow', (e) => { if (e.persisted) $$('.btn.is-busy').forEach((b) => ui.busy(b, false)); });
  // Form cần hỏi lại trước khi gửi (ví dụ "Xóa hết" ở hộp thư thử): data-confirm-title, data-confirm-message
  document.addEventListener('submit', async (e) => {
    const f = e.target.closest('form[data-confirm-title]');
    if (!f || f.dataset.confirmed === '1') return;
    e.preventDefault();
    const ok = await ui.confirm({ title: f.dataset.confirmTitle, message: f.dataset.confirmMessage || '', confirmText: f.dataset.confirmText || 'Xác nhận', tone: f.dataset.confirmTone || 'danger' });
    if (ok) { f.dataset.confirmed = '1'; f.submit(); }
  });
  window.addEventListener('popstate', () => { const r = $('[data-region]'); if (r && r.dataset.url) ui.loadRegion(r, location.pathname + location.search, { push: 'none' }); });

  document.addEventListener('DOMContentLoaded', () => {
    enhance(document);
    $$('form[data-filter]').forEach(initFilter);
    $$('form[data-dirty-guard]').forEach(initDirtyGuard);
    $$('[data-dropzone]').forEach(initDropzone);
    $$('[data-clock]').forEach(initClock);
    // Trạng thái thu gọn đã lưu
    const shell = $('.admin-shell');
    if (shell && store.get('fpc.sidebar') === '1') shell.classList.add('is-collapsed');
    const bar = $('.demo-bar');
    if (bar && (store.get('fpc.demo') === '1' || (store.get('fpc.demo') === null && (window.innerWidth < 992 || !!document.querySelector('.pos-shell'))))) { bar.classList.add('is-collapsed'); bar.querySelector('[data-demo-toggle]')?.setAttribute('aria-expanded', 'false'); }
    // Toast do server gửi sau khi chuyển trang (TempData)
    try { JSON.parse($('#flash-toasts')?.textContent || '[]').forEach((f) => ui.toast[f.type]?.(f.message)); } catch { /* bỏ qua */ }
    // Layout hiển thị: ẩn con trỏ sau 3 giây không dùng
    const display = $('.display-shell');
    if (display) {
      let h;
      const wake = () => { display.classList.remove('cursor-hidden'); clearTimeout(h); h = setTimeout(() => display.classList.add('cursor-hidden'), 3000); };
      ['mousemove', 'mousedown', 'keydown', 'touchstart'].forEach((ev) => document.addEventListener(ev, wake, { passive: true }));
      wake();
    }
    const initialConn = document.body.dataset.connState;
    if (initialConn) ui.connection.set(initialConn);
    // Chưa có kênh thời gian thực (realtime.js từ Phase 4) thì chỉ báo phản ánh kết nối mạng của trình duyệt
    else if ($('[data-conn-indicator]:not([data-conn-static])') && !window.realtime) {
      const sync = () => ui.connection.set(navigator.onLine ? 'connected' : 'lost');
      window.addEventListener('online', sync);
      window.addEventListener('offline', sync);
      if (!navigator.onLine) sync();
    }
  });

  window.ui = ui;
})();
