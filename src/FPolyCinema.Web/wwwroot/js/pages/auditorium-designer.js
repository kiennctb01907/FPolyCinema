/* A4. Công cụ thiết kế phòng chiếu (UI A4, SPEC 3.2).
   - Công cụ tô: STANDARD, VIP, SWEETBOX, WHEELCHAIR, EMPTY (lối đi), HOUSE (bật tắt ghế nội bộ), MAINT (bật tắt bảo trì).
   - Bấm hoặc kéo để tô; phím mũi tên di chuyển, Enter hoặc phím cách để tô.
   - Nhãn, cặp ghế đôi, điểm BaseViewScore, mức heatmap và gợi ý ghế nội bộ do server tính (POST /preview, AuditoriumLayout),
     để công cụ và lúc lưu dùng chung một quy tắc. Lưu: POST /layout.
   - Phòng đang khóa cấu trúc (đã bán vé suất sắp chiếu): chỉ công cụ Bảo trì dùng được. */
(() => {
  'use strict';
  const root = document.getElementById('designer');
  const dataEl = document.getElementById('design-data');
  if (!root || !dataEl) return;
  const data = JSON.parse(dataEl.textContent);
  const grid = document.getElementById('design-grid');
  const map = document.getElementById('design-map');
  const errorsBox = document.getElementById('design-errors');
  const locked = !!data.locked;
  const TYPE_LABEL = { STANDARD: 'ghế thường', VIP: 'ghế VIP', SWEETBOX: 'ghế đôi', WHEELCHAIR: 'chỗ xe lăn' };

  let rows = data.rows, cols = data.cols;
  const cells = new Map(); // "r,c" → { t, h, m, label, score, heat, pair }
  const key = (r, c) => `${r},${c}`;
  data.cells.forEach((x) => cells.set(key(x.row, x.col), { t: x.type, h: x.house, m: x.maint, label: x.label, score: x.score, heat: x.heat, pair: null }));
  let tool = locked ? 'MAINT' : 'STANDARD';
  let dirty = false;

  const rowLetter = (r) => String.fromCharCode(64 + r);

  // ------------------------------------------------------------ vẽ lưới
  function cellClass(cell, r, c) {
    const cls = ['dcell'];
    if (!cell) return cls.join(' ');
    cls.push('has-seat', 't-' + cell.t.toLowerCase());
    if (cell.h) cls.push('is-house');
    if (cell.m) cls.push('is-maint');
    if (cell.heat) cls.push('heat-' + cell.heat);
    if (cell.t === 'SWEETBOX') {
      const left = cells.get(key(r, c - 1)), right = cells.get(key(r, c + 1));
      if (cell.pair && right && right.t === 'SWEETBOX' && right.pair === cell.pair) cls.push('pair-left');
      else if (cell.pair && left && left.t === 'SWEETBOX' && left.pair === cell.pair) cls.push('pair-right');
      else cls.push('pair-bad');
    }
    return cls.join(' ');
  }

  function describe(cell, r, c) {
    if (!cell) return `Ô trống hàng ${rowLetter(r)}, cột ${c}`;
    const bits = [`Ghế ${cell.label || rowLetter(r) + '?'}`, TYPE_LABEL[cell.t] || cell.t];
    if (cell.h) bits.push('ghế nội bộ');
    if (cell.m) bits.push('đang bảo trì');
    if (cell.score != null) bits.push(`${cell.score} điểm`);
    if (cell.t === 'SWEETBOX' && !cell.pair) bits.push('chưa đủ cặp');
    return bits.join(', ');
  }

  function paintCell(btn) {
    const r = Number(btn.dataset.r), c = Number(btn.dataset.c);
    const cell = cells.get(key(r, c));
    btn.className = cellClass(cell, r, c);
    btn.setAttribute('aria-label', describe(cell, r, c));
    btn.title = describe(cell, r, c);
    btn.innerHTML = '';
    if (!cell) return;
    if (cell.m) btn.insertAdjacentHTML('beforeend', '<i class="bi bi-wrench" aria-hidden="true"></i>');
    else if (cell.h) btn.insertAdjacentHTML('beforeend', '<i class="bi bi-lock" aria-hidden="true"></i>');
    else if (cell.t === 'WHEELCHAIR') btn.insertAdjacentHTML('beforeend', '<i class="bi bi-person-wheelchair" aria-hidden="true"></i>');
    else {
      const span = document.createElement('span');
      span.setAttribute('aria-hidden', 'true');
      span.textContent = cell.label ? cell.label.slice(1) : '';
      btn.appendChild(span);
    }
  }

  function render() {
    grid.style.setProperty('--cols', String(cols));
    grid.innerHTML = '';
    for (let r = 1; r <= rows; r++) {
      const lead = document.createElement('span');
      lead.className = 'seat-row-label'; lead.setAttribute('role', 'rowheader'); lead.textContent = rowLetter(r);
      grid.appendChild(lead);
      for (let c = 1; c <= cols; c++) {
        const b = document.createElement('button');
        b.type = 'button'; b.dataset.r = String(r); b.dataset.c = String(c); b.tabIndex = r === 1 && c === 1 ? 0 : -1;
        paintCell(b);
        grid.appendChild(b);
      }
      const tail = document.createElement('span');
      tail.className = 'seat-row-label'; tail.setAttribute('aria-hidden', 'true'); tail.textContent = rowLetter(r);
      grid.appendChild(tail);
    }
  }

  const btnAt = (r, c) => grid.querySelector(`button[data-r="${r}"][data-c="${c}"]`);
  const repaintRow = (r) => { for (let c = 1; c <= cols; c++) { const b = btnAt(r, c); if (b) paintCell(b); } };

  // ------------------------------------------------------------ tô
  let stroke = null; // { flag: true/false cho HOUSE/MAINT }
  function apply(r, c) {
    const k = key(r, c);
    const cell = cells.get(k);
    if (tool === 'EMPTY') { if (!cell) return false; cells.delete(k); return true; }
    if (tool === 'HOUSE' || tool === 'MAINT') {
      if (!cell) return false;
      const prop = tool === 'HOUSE' ? 'h' : 'm';
      if (stroke.flag === null) stroke.flag = !cell[prop];
      if (cell[prop] === stroke.flag) return false;
      cell[prop] = stroke.flag;
      return true;
    }
    if (cell && cell.t === tool) return false;
    if (cell) { cell.t = tool; cell.pair = null; cell.heat = null; }
    else cells.set(k, { t: tool, h: false, m: false, label: '', score: null, heat: null, pair: null });
    if (tool === 'SWEETBOX' && stroke.single && c + 1 <= cols) {
      // Một lần bấm tô luôn cả cặp: ô bên phải trong hàng
      const k2 = key(r, c + 1);
      const right = cells.get(k2);
      if (right) { right.t = 'SWEETBOX'; right.pair = null; } else cells.set(k2, { t: 'SWEETBOX', h: false, m: false, label: '', score: null, heat: null, pair: null });
    }
    return true;
  }

  function touch(btn) {
    const r = Number(btn.dataset.r), c = Number(btn.dataset.c);
    if (apply(r, c)) { dirty = true; repaintRow(r); }
  }

  grid.addEventListener('pointerdown', (e) => {
    const b = e.target.closest('button[data-r]');
    if (!b) return;
    e.preventDefault();
    stroke = { flag: null, single: true, last: b };
    b.focus();
    touch(b);
  });
  grid.addEventListener('pointerover', (e) => {
    if (!stroke) return;
    const b = e.target.closest('button[data-r]');
    if (!b || b === stroke.last) return;
    stroke.single = false;
    stroke.last = b;
    touch(b);
  });
  window.addEventListener('pointerup', () => { if (stroke) { stroke = null; schedulePreview(); } });

  grid.addEventListener('keydown', (e) => {
    const b = e.target.closest('button[data-r]');
    if (!b) return;
    let r = Number(b.dataset.r), c = Number(b.dataset.c);
    if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); stroke = { flag: null, single: true, last: b }; touch(b); stroke = null; schedulePreview(); return; }
    if (e.key === 'ArrowRight') c = Math.min(cols, c + 1);
    else if (e.key === 'ArrowLeft') c = Math.max(1, c - 1);
    else if (e.key === 'ArrowDown') r = Math.min(rows, r + 1);
    else if (e.key === 'ArrowUp') r = Math.max(1, r - 1);
    else return;
    e.preventDefault();
    const next = btnAt(r, c);
    if (next) { b.tabIndex = -1; next.tabIndex = 0; next.focus(); }
  });

  root.querySelectorAll('[data-tool]').forEach((t) => t.addEventListener('click', () => {
    if (t.disabled) return;
    tool = t.dataset.tool;
    root.querySelectorAll('[data-tool]').forEach((x) => { const on = x === t; x.classList.toggle('is-active', on); x.setAttribute('aria-checked', String(on)); });
  }));

  // ------------------------------------------------------------ xem trước trên server
  const layoutJson = () => JSON.stringify({ rows, cols, cells: Array.from(cells, ([k, v]) => { const [r, c] = k.split(',').map(Number); return { r, c, t: v.t, h: v.h, m: v.m }; }) });
  let previewTimer = null;
  function schedulePreview() { clearTimeout(previewTimer); previewTimer = setTimeout(() => preview().catch(() => {}), 300); }

  function showErrors(list) {
    errorsBox.innerHTML = '';
    if (!list || !list.length) return;
    const ul = document.createElement('ul');
    ul.className = 'issue-list mb-3';
    list.forEach((m) => {
      const li = document.createElement('li');
      li.className = 'tone-danger';
      li.innerHTML = '<i class="bi bi-x-octagon" aria-hidden="true"></i>';
      const span = document.createElement('span'); span.textContent = m; li.appendChild(span);
      ul.appendChild(li);
    });
    errorsBox.appendChild(ul);
  }

  async function preview(opts = {}) {
    const res = await ui.api(`/admin/auditoriums/${data.id}/preview`, { method: 'POST', body: { layout: layoutJson() }, button: opts.button, silent: opts.silent });
    res.seats.forEach((s) => { const cell = cells.get(key(s.r, s.c)); if (cell) { cell.label = s.label; cell.score = s.score; cell.heat = s.heat; cell.pair = s.pair; } });
    for (let r = 1; r <= rows; r++) repaintRow(r);
    const st = res.stats;
    const set = (k, v) => { const el = document.querySelector(`[data-stat="${k}"]`); if (el) el.textContent = ui.number(v); };
    ['STANDARD', 'VIP', 'SWEETBOX', 'WHEELCHAIR'].forEach((t) => set(t, (st.byType && st.byType[t]) || 0));
    set('house', st.house); set('maintenance', st.maintenance); set('total', st.total); set('sellable', st.sellable);
    showErrors(res.errors);
    return res;
  }

  document.getElementById('design-renumber')?.addEventListener('click', async (e) => {
    try { await preview({ button: e.currentTarget }); ui.toast.success('Đã đánh số lại theo hàng, từ trái sang phải, bỏ qua ô trống. Bấm Lưu sơ đồ để áp dụng.'); } catch { /* đã báo */ }
  });

  document.getElementById('design-suggest')?.addEventListener('click', async (e) => {
    if (locked) return;
    try {
      const res = await preview({ button: e.currentTarget });
      if (!res.suggestedHouse.length) { ui.toast.warning('Chưa tìm được ghế phù hợp. Bạn cần có ghế thường hoặc VIP không bảo trì.'); return; }
      cells.forEach((v) => { v.h = false; });
      res.suggestedHouse.forEach((p) => { const cell = cells.get(key(p.r, p.c)); if (cell) cell.h = true; });
      dirty = true;
      await preview({ silent: true });
      const labels = res.suggestedHouse.map((p) => cells.get(key(p.r, p.c))?.label).filter(Boolean);
      ui.toast.success(`Đã gợi ý ${labels.length} ghế nội bộ ở trung tâm: ${labels.join(', ')}. Bấm Lưu sơ đồ để áp dụng.`);
    } catch { /* đã báo */ }
  });

  document.getElementById('heat-toggle')?.addEventListener('change', (e) => map.classList.toggle('show-heat', e.target.checked));

  document.getElementById('grid-apply')?.addEventListener('click', () => {
    const nr = Number(document.getElementById('grid-rows').value), nc = Number(document.getElementById('grid-cols').value);
    if (!(nr >= 1 && nr <= 26 && nc >= 1 && nc <= 40)) { ui.toast.error('Số hàng phải từ 1 đến 26, số cột từ 1 đến 40.'); return; }
    let dropped = 0;
    Array.from(cells.keys()).forEach((k) => { const [r, c] = k.split(',').map(Number); if (r > nr || c > nc) { cells.delete(k); dropped++; } });
    // Hàng, cột mới thêm vào được lấp sẵn ghế thường
    for (let r = 1; r <= nr; r++) for (let c = 1; c <= nc; c++) if ((r > rows || c > cols) && !cells.has(key(r, c))) cells.set(key(r, c), { t: 'STANDARD', h: false, m: false, label: '', score: null, heat: null, pair: null });
    rows = nr; cols = nc; dirty = true;
    render();
    preview({ silent: true }).catch(() => {});
    ui.toast.info(dropped ? `Đã đổi lưới thành ${nr} × ${nc}, bỏ ${dropped} ghế nằm ngoài lưới.` : `Đã đổi lưới thành ${nr} × ${nc}.`);
  });

  document.getElementById('design-save')?.addEventListener('click', async (e) => {
    try {
      const res = await ui.api(`/admin/auditoriums/${data.id}/layout`, { method: 'POST', body: { layout: layoutJson() }, button: e.currentTarget });
      dirty = false;
      ui.toast.success(res.message);
      preview({ silent: true }).catch(() => {});
    } catch (err) {
      if (err && err.details && err.details.layout) showErrors([err.message]);
    }
  });

  window.addEventListener('beforeunload', (e) => { if (dirty) { e.preventDefault(); e.returnValue = ''; } });

  render();
  preview({ silent: true }).catch(() => {});
})();
