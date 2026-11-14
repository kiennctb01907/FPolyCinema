/* gantt.js – A5. Lịch chiếu chế độ Gantt và AI tự động xếp lịch (UI A5-03 → A5-08; SPEC 4.3-08 → 4.3-11; UC35).
   - Hộp "AI tự động xếp lịch": POST /scheduler/ai/generate trả bản nháp; bản nháp giữ ở sessionStorage của trình duyệt (chưa lưu DB) và hiện trên Gantt.
   - Khối suất: nền màu nhãn tuổi nhạt 20%, viền trái 4px; tên phim 1 dòng và "19:15–21:40 · Phụ đề"; phần quảng cáo và dọn phòng vẽ sọc;
     suất đã bán vé (LOCKED_HAS_TICKETS) có icon khóa và không kéo được; bản nháp AI viền nét đứt.
   - Kéo thả (chuột, cảm ứng) hoặc phím ← → (5 phút), ↑ ↓ (đổi phòng); hít theo bước 5 phút. Mỗi lần thả gọi POST /scheduler/validate:
     lỗi chặn viền đỏ 2px kèm icon, cảnh báo viền vàng, tooltip ghi lý do.
   - Thanh bản nháp: "Áp dụng" (POST /scheduler/ai/apply, lưu IsAiGenerated = 1) và "Bỏ"; thanh thay đổi: "Lưu thay đổi" (POST /scheduler/save) và "Hoàn tác".
     Chỉ lưu được khi không còn lỗi chặn. */
(function () {
  'use strict';
  if (!window.ui) return;
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));
  const esc = (v) => String(v ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const KEY = (cinemaId) => `fpc-ai-draft-${cinemaId}`;
  const load = (cinemaId) => { try { return JSON.parse(sessionStorage.getItem(KEY(cinemaId)) || 'null'); } catch { return null; } };
  const store = (cinemaId, v) => { try { if (v) sessionStorage.setItem(KEY(cinemaId), JSON.stringify(v)); else sessionStorage.removeItem(KEY(cinemaId)); } catch { /* trình duyệt chặn lưu tạm */ } };

  // ---------------------------------------------------------------- hộp "AI tự động xếp lịch"
  const aiForm = $('[data-ai-form]');
  if (aiForm) {
    // Đổi "Từ ngày" hoặc "Số ngày": lọc lại phim theo giấy phép, phim C, KDM trong khoảng ngày (SPEC 4.3-02)
    const list = $('[data-ai-movies]', aiForm);
    const reloadMovies = async () => {
      const from = $('#ai-from', aiForm).value, days = $('#ai-days', aiForm).value;
      if (!from) return;
      const checked = new Set($$('input[name="movieIds"]:checked', list).map((i) => i.value));
      const prevEnabled = new Set($$('input[name="movieIds"]:not(:disabled)', list).map((i) => i.value));
      try {
        const movies = await ui.api(`/scheduler/ai/choices?cinemaId=${encodeURIComponent($('input[name="cinemaId"]', aiForm).value)}&fromDate=${encodeURIComponent(from)}&days=${encodeURIComponent(days)}`, { silent: true });
        list.innerHTML = movies.map((m) => {
          // Giữ lựa chọn cũ; phim vừa xếp được (trước đó bị khóa) thì chọn sẵn
          const on = m.eligible && (checked.has(String(m.id)) || !prevEnabled.has(String(m.id)));
          return `<div class="form-check"><input class="form-check-input" type="checkbox" name="movieIds" value="${m.id}" id="ai-m-${m.id}"${on ? ' checked' : ''}${m.eligible ? '' : ' disabled'} />` +
            `<label class="form-check-label" for="ai-m-${m.id}"><span class="age-badge age-${esc(m.ageRating)}" role="img" aria-label="${esc(m.ageExplanation)}" title="${esc(m.ageExplanation)}">${esc(m.ageRating)}</span> ${esc(m.title)}` +
            `${m.isVietnamese ? ' <span class="outline-badge">Phim Việt</span>' : ''}<span class="meta d-block">${esc(m.meta)}</span></label></div>`;
        }).join('');
        $('[data-ai-eligible]', aiForm).textContent = String(movies.filter((m) => m.eligible).length);
      } catch { /* giữ danh sách cũ, server vẫn lọc lại khi xếp */ }
    };
    $('#ai-from', aiForm).addEventListener('change', reloadMovies);
    $('#ai-days', aiForm).addEventListener('change', reloadMovies);

    aiForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      const fd = new FormData(aiForm);
      try {
        const r = await ui.api('/scheduler/ai/generate', { method: 'POST', body: fd, form: aiForm, button: $('[data-ai-submit]', aiForm) });
        if (!r.drafts.length) { ui.toast.warning('AI không xếp thêm được suất nào: các phòng đã kín lịch hoặc không còn khung giờ hợp lệ.'); return; }
        store(r.cinemaId, r);
        location.href = `/scheduler?cinemaId=${r.cinemaId}&date=${r.fromDate}&view=gantt`;
      } catch { /* ui.api đã báo lỗi theo ô hoặc toast */ }
    });
  }

  // Liên kết mở thẳng hộp AI: /scheduler#ai-xep-lich
  if (location.hash === '#ai-xep-lich' && window.bootstrap && $('#ai-scheduler')) bootstrap.Modal.getOrCreateInstance($('#ai-scheduler')).show();

  const gantt = $('[data-gantt]');
  if (!gantt) return;
  const state = JSON.parse($('#gantt-state').textContent);
  const parse = (s) => { const [d, t] = s.split('T'); const [y, m, dd] = d.split('-').map(Number); const [h, mi] = t.split(':').map(Number); return new Date(y, m - 1, dd, h, mi); };
  const pad = (n) => String(n).padStart(2, '0');
  const iso = (dt) => `${dt.getFullYear()}-${pad(dt.getMonth() + 1)}-${pad(dt.getDate())}T${pad(dt.getHours())}:${pad(dt.getMinutes())}`;
  const hm = (s) => s.slice(11, 16);
  const addMin = (s, m) => iso(new Date(parse(s).getTime() + m * 60000));
  const diffMin = (a, b) => Math.round((parse(b) - parse(a)) / 60000);
  const axisStart = state.axisStart;
  const total = diffMin(state.axisStart, state.axisEnd);
  const step = state.roundMinutes || 5;
  const pct = (m) => `${(m / total * 100).toFixed(4)}%`;
  const rooms = new Map(state.rooms.map((r) => [r.id, r]));

  const blocks = state.showtimes.map((s) => ({ ...s, kind: 'existing', key: 'st-' + s.id, orig: { auditoriumId: s.auditoriumId, start: s.start, feature: s.feature, end: s.end, cleaningEnd: s.cleaningEnd, issues: s.issues } }));
  let draft = load(state.cinemaId);
  if (draft && draft.cinemaId !== state.cinemaId) draft = null;
  const draftBlocks = () => (draft ? draft.drafts : []);
  const visibleDrafts = () => draftBlocks().filter((d) => d.date === state.date).map((d) => Object.assign(d, { kind: 'draft' }));

  const hasError = (b) => (b.issues || []).some((i) => i.severity === 'error');
  const hasWarning = (b) => (b.issues || []).some((i) => i.severity === 'warning');
  const isDirty = (b) => b.kind === 'existing' && (b.auditoriumId !== b.orig.auditoriumId || b.start !== b.orig.start);
  const movable = (b) => b.kind === 'draft' || (!b.locked && !b.started);

  // ---------------------------------------------------------------- vẽ
  function blockHtml(b) {
    const len = Math.max(diffMin(b.start, b.cleaningEnd), 1);
    const ads = diffMin(b.start, b.feature) / len * 100, clean = diffMin(b.end, b.cleaningEnd) / len * 100;
    const icons = [];
    if (b.locked) icons.push('<i class="bi bi-lock-fill" aria-hidden="true"></i>');
    if (hasError(b)) icons.push('<i class="bi bi-x-octagon-fill" aria-hidden="true"></i>');
    else if (hasWarning(b)) icons.push('<i class="bi bi-exclamation-triangle-fill" aria-hidden="true"></i>');
    if (b.kind === 'draft') icons.push('<i class="bi bi-stars" aria-hidden="true"></i>');
    const reasons = (b.issues || []).map((i) => (i.severity === 'error' ? 'Lỗi: ' : 'Cảnh báo: ') + i.message);
    const status = b.locked ? 'Đã bán vé, không đổi giờ được' : b.started ? 'Đã bắt đầu' : b.kind === 'draft' ? 'Bản nháp AI, chưa lưu' : isDirty(b) ? 'Đã đổi, chưa lưu' : '';
    const tip = [`${b.title} · ${hm(b.start)}–${hm(b.end)} · ${b.languageLabel}`, status, ...reasons].filter(Boolean).join('\n');
    const cls = ['gantt-block', 'age-' + b.ageRating, b.kind === 'draft' ? 'is-draft' : '', b.locked ? 'is-locked' : '', b.started ? 'is-started' : '',
      hasError(b) ? 'has-error' : hasWarning(b) ? 'has-warning' : '', isDirty(b) ? 'is-dirty' : ''].filter(Boolean).join(' ');
    return `<div class="${cls}" role="listitem" tabindex="0" data-key="${esc(b.key)}" style="--x: ${pct(diffMin(axisStart, b.start))}; --w: ${pct(len)}" title="${esc(tip)}" aria-label="${esc(tip.replace(/\n/g, '. '))}"` +
      `${movable(b) ? ' aria-roledescription="khối kéo được"' : ''}>` +
      `<span class="gantt-seg" style="--w: ${ads.toFixed(2)}%" aria-hidden="true"></span>` +
      `<span class="gantt-body"><span class="gantt-title">${icons.join('')}<span class="text-truncate">${esc(b.title)}</span></span>` +
      `<span class="gantt-meta">${hm(b.start)}–${hm(b.end)} · ${esc(b.languageLabel)}</span></span>` +
      `<span class="gantt-seg" style="--w: ${clean.toFixed(2)}%" aria-hidden="true"></span></div>`;
  }

  function allBlocks() { return blocks.concat(visibleDrafts()); }

  function render() {
    $$('[data-track]', gantt).forEach((track) => {
      const id = Number(track.dataset.track);
      let html = allBlocks().filter((b) => b.auditoriumId === id).map(blockHtml).join('');
      const now = diffMin(axisStart, state.now);
      if (now > 0 && now < total) html += `<span class="gantt-now" style="--x: ${pct(now)}" aria-hidden="true"></span>`;
      track.innerHTML = html;
    });
    renderBars();
  }

  function renderBars() {
    const bar = $('[data-draft-bar]');
    const list = draftBlocks();
    bar.hidden = !list.length;
    if (list.length) {
      const errs = list.filter(hasError).length;
      const today = visibleDrafts().length;
      const vn = list.filter((d) => d.isVietnamese).length;
      $('[data-draft-text]').innerHTML = `<strong>Bản nháp AI: ${list.length} suất</strong> trong ${draft.days} ngày từ ${esc(draft.fromDate.split('-').reverse().join('/'))}` +
        ` (${today} suất ngày đang xem) · ${vn} suất phim Việt · tỷ lệ phim Việt các ngày xếp ${esc(draft.share)} (mục tiêu ${esc(draft.target)}).` +
        (errs ? ` <span class="text-danger">Còn ${errs} suất có lỗi chặn – kéo sang giờ khác trước khi áp dụng.</span>` : '') +
        ((draft.notes || []).length ? `<br><span class="text-muted small">${draft.notes.map(esc).join(' ')}</span>` : '');
      $('[data-draft-apply]').disabled = errs > 0;
    }
    const dirty = blocks.filter(isDirty);
    const dbar = $('[data-dirty-bar]');
    dbar.hidden = !dirty.length;
    if (dirty.length) {
      const errs = dirty.filter(hasError).length;
      $('[data-dirty-text]').innerHTML = `<strong>${dirty.length} suất đã đổi giờ hoặc phòng, chưa lưu.</strong>` +
        (errs ? ` <span class="text-danger">${errs} suất có lỗi chặn nên chưa lưu được.</span>` : '');
      $('[data-dirty-save]').disabled = errs > 0;
    }
  }

  const find = (key) => allBlocks().find((b) => b.key === key);
  const persist = () => { if (draft) store(state.cinemaId, draft); };

  // ---------------------------------------------------------------- kiểm tra tức thì
  function pendingFor(b) {
    return allBlocks().filter((x) => x !== b && (x.kind === 'draft' || isDirty(x)))
      .map((x) => ({ id: x.kind === 'draft' ? null : x.id, auditoriumId: x.auditoriumId, movieId: x.movieId, startTime: x.start, languageFormat: x.language }))
      // bản nháp của ngày khác cũng chặn chồng giờ
      .concat(draftBlocks().filter((d) => d.date !== state.date && d !== b).map((d) => ({ id: null, auditoriumId: d.auditoriumId, movieId: d.movieId, startTime: d.start, languageFormat: d.language })));
  }

  async function validate(b) {
    const body = { auditoriumId: b.auditoriumId, movieId: b.movieId, startTime: b.start, languageFormat: b.language, pending: JSON.stringify(pendingFor(b)) };
    if (b.kind === 'existing') body.id = b.id;
    try {
      const r = await ui.api('/scheduler/validate', { method: 'POST', body, silent: true });
      if (r.times) Object.assign(b, { start: r.times.start, feature: r.times.feature, end: r.times.end, cleaningEnd: r.times.cleaningEnd });
      b.issues = r.issues;
    } catch (e) {
      b.issues = [{ severity: 'error', message: e.message }];
    }
    if (b.kind === 'existing' && !isDirty(b)) b.issues = b.orig.issues;
    persist();
    render();
    const el = $(`[data-key="${CSS.escape(b.key)}"]`, gantt);
    if (el) el.focus({ preventScroll: true });
  }

  function moveTo(b, start, auditoriumId) {
    const delta = diffMin(b.start, start);
    Object.assign(b, { start, feature: addMin(b.feature, delta), end: addMin(b.end, delta), cleaningEnd: addMin(b.cleaningEnd, delta), auditoriumId });
  }

  // ---------------------------------------------------------------- kéo thả
  let drag = null;
  gantt.addEventListener('pointerdown', (e) => {
    const el = e.target.closest('.gantt-block');
    if (!el || e.button !== 0) return;
    const b = find(el.dataset.key);
    if (!b || !movable(b)) return;
    const track = el.closest('[data-track]');
    drag = { el, b, x0: e.clientX, minutes: 0, auditoriumId: b.auditoriumId, width: track.getBoundingClientRect().width, left0: diffMin(axisStart, b.start) };
    el.setPointerCapture(e.pointerId);
    el.classList.add('is-dragging');
  });
  gantt.addEventListener('pointermove', (e) => {
    if (!drag) return;
    const raw = (e.clientX - drag.x0) / drag.width * total;
    drag.minutes = Math.round(raw / step) * step;
    drag.el.style.setProperty('--x', pct(drag.left0 + drag.minutes));
    const over = document.elementsFromPoint(e.clientX, e.clientY).map((n) => n.closest && n.closest('[data-track]')).find(Boolean);
    $$('[data-track]', gantt).forEach((t) => t.classList.toggle('is-drop-target', t === over));
    if (over && Number(over.dataset.track) !== drag.auditoriumId) { drag.auditoriumId = Number(over.dataset.track); over.appendChild(drag.el); }
  });
  function endDrag() {
    if (!drag) return;
    const { b, minutes, auditoriumId } = drag;
    drag.el.classList.remove('is-dragging');
    $$('[data-track]', gantt).forEach((t) => t.classList.remove('is-drop-target'));
    drag = null;
    if (minutes === 0 && auditoriumId === b.auditoriumId) { render(); return; }
    moveTo(b, addMin(b.start, minutes), auditoriumId);
    validate(b);
  }
  gantt.addEventListener('pointerup', endDrag);
  gantt.addEventListener('pointercancel', endDrag);

  gantt.addEventListener('keydown', (e) => {
    const el = e.target.closest('.gantt-block');
    if (!el) return;
    const b = find(el.dataset.key);
    if (!b || !movable(b)) return;
    const ids = state.rooms.map((r) => r.id);
    let start = b.start, aud = b.auditoriumId;
    if (e.key === 'ArrowLeft') start = addMin(b.start, -step);
    else if (e.key === 'ArrowRight') start = addMin(b.start, step);
    else if (e.key === 'ArrowUp' && ids.indexOf(aud) > 0) aud = ids[ids.indexOf(aud) - 1];
    else if (e.key === 'ArrowDown' && ids.indexOf(aud) < ids.length - 1) aud = ids[ids.indexOf(aud) + 1];
    else return;
    e.preventDefault();
    moveTo(b, start, aud);
    validate(b);
  });

  // ---------------------------------------------------------------- thanh bản nháp và thay đổi
  $('[data-draft-discard]').addEventListener('click', async () => {
    const ok = await ui.confirm({ title: 'Bỏ bản nháp AI?', message: `Bỏ ${draftBlocks().length} suất AI đã xếp. Các suất đã có không bị ảnh hưởng.`, confirmText: 'Bỏ bản nháp', cancelText: 'Giữ lại', tone: 'warning' });
    if (!ok) return;
    draft = null;
    store(state.cinemaId, null);
    render();
  });

  $('[data-draft-apply]').addEventListener('click', async (e) => {
    const list = draftBlocks();
    if (!list.length || list.some(hasError)) return;
    const payload = list.map((d) => ({ auditoriumId: d.auditoriumId, movieId: d.movieId, startTime: d.start, languageFormat: d.language }));
    try {
      const r = await ui.api('/scheduler/ai/apply', { method: 'POST', body: { cinemaId: state.cinemaId, drafts: JSON.stringify(payload) }, button: e.currentTarget });
      store(state.cinemaId, null);
      location.href = r.redirect;
    } catch (err) {
      // Suất nháp bị lỗi chặn khi lưu (ví dụ phòng vừa có suất mới): đánh dấu để quản lý kéo lại
      const idx = err.details && Number.isInteger(err.details.index) ? err.details.index : -1;
      if (idx >= 0 && list[idx]) { list[idx].issues = err.details.issues || [{ severity: 'error', message: err.message }]; persist(); render(); }
    }
  });

  $('[data-dirty-undo]').addEventListener('click', () => {
    blocks.filter(isDirty).forEach((b) => Object.assign(b, b.orig));
    blocks.forEach((b) => { b.auditoriumId = b.orig.auditoriumId; });
    render();
  });

  $('[data-dirty-save]').addEventListener('click', async (e) => {
    const dirty = blocks.filter(isDirty);
    if (dirty.some(hasError)) return;
    for (const b of dirty) {
      try {
        await ui.api('/scheduler/save', { method: 'POST', body: { id: b.id, auditoriumId: b.auditoriumId, movieId: b.movieId, startTime: b.start, languageFormat: b.language }, button: e.currentTarget });
      } catch (err) {
        b.issues = (err.details && err.details.issues) || [{ severity: 'error', message: err.message }];
        render();
        return;
      }
    }
    location.reload();
  });

  render();
})();
