/* quick-buy.js – thanh mua vé nhanh trên trang chủ (UI C1-03): chọn Rạp, Phim, Ngày, Suất rồi bấm "Mua vé" để tới trang đặt vé của suất. */
(function () {
  'use strict';
  const form = document.querySelector('[data-quick-buy]');
  if (!form) return;
  const el = (k) => form.querySelector(`[data-qb="${k}"]`);
  const cinema = el('cinema'), movie = el('movie'), date = el('date'), time = el('time'), go = el('go'), error = el('error');
  let lastLevel = 0;
  const esc = (v) => String(v ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

  function fill(sel, placeholder, items, value, label) {
    sel.innerHTML = `<option value="">${esc(placeholder)}</option>` + items.map((i) => `<option value="${esc(value(i))}" ${i.soldOut ? 'disabled' : ''}>${esc(label(i))}</option>`).join('');
    sel.disabled = items.length === 0;
  }

  // Đang tải: ô kế tiếp ghi "Đang tải…" và bị khóa; lỗi: hiện dòng báo lỗi kèm nút thử lại
  function loading(level) {
    const target = [movie, date, time][Math.min(level, 2)];
    target.innerHTML = '<option value="">Đang tải…</option>';
    target.disabled = true;
    form.setAttribute('aria-busy', 'true');
    error.hidden = true;
  }

  async function load(level) {
    lastLevel = level;
    loading(level);
    const p = new URLSearchParams();
    if (cinema.value) p.set('cinemaId', cinema.value);
    if (level >= 1 && movie.value) p.set('movieId', movie.value);
    if (level >= 2 && date.value) p.set('date', date.value);
    try {
      const r = await ui.api('/quick-buy?' + p.toString(), { silent: true });
      if (level === 0) {
        fill(movie, r.movies.length ? 'Chọn phim' : 'Không có phim', r.movies, (m) => m.id, (m) => m.title);
        fill(date, 'Chọn ngày', [], () => '', () => '');
        fill(time, 'Chọn suất', [], () => '', () => '');
      } else if (level === 1) {
        fill(date, 'Chọn ngày', r.days, (d) => d.value, (d) => d.label);
        fill(time, 'Chọn suất', [], () => '', () => '');
      } else {
        fill(time, r.times.length ? 'Chọn suất' : 'Không còn suất', r.times, (t) => t.id, (t) => t.label);
      }
    } catch {
      const target = [movie, date, time][Math.min(level, 2)];
      target.innerHTML = '<option value="">Chưa tải được</option>';
      error.hidden = false;
    } finally {
      form.removeAttribute('aria-busy');
    }
    go.disabled = !time.value;
  }

  cinema.addEventListener('change', () => load(0));
  el('retry').addEventListener('click', () => load(lastLevel));
  movie.addEventListener('change', () => (movie.value ? load(1) : load(0)));
  date.addEventListener('change', () => (date.value ? load(2) : load(1)));
  time.addEventListener('change', () => { go.disabled = !time.value; });
  form.addEventListener('submit', (e) => {
    e.preventDefault();
    if (time.value) location.href = '/booking/' + encodeURIComponent(time.value);
  });
  load(0);
})();
