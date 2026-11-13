/* Form suất chiếu (UI A5): mỗi lần đổi phòng, phim, giờ, ngôn ngữ thì gọi POST /scheduler/check (ScheduleConstraintChecker)
   để hiện các mốc giờ, lỗi chặn (đỏ) và cảnh báo (vàng). Bấm Lưu gọi POST /scheduler/save; có lỗi chặn thì hiện danh sách lỗi. */
(() => {
  'use strict';
  const form = document.getElementById('showtime-form');
  if (!form) return;
  const id = form.dataset.id || '';
  const result = document.getElementById('check-result');
  const times = document.getElementById('check-times');
  const movie = document.getElementById('MovieId');
  const lang = document.getElementById('LanguageFormat');
  const save = document.getElementById('showtime-save');

  const values = () => ({
    id,
    auditoriumId: document.getElementById('AuditoriumId').value,
    movieId: movie.value,
    startTime: document.getElementById('StartTime').value,
    languageFormat: lang.value,
  });

  function renderIssues(issues) {
    result.innerHTML = '';
    if (!issues || !issues.length) {
      result.innerHTML = '<div class="issue-ok"><i class="bi bi-check-circle" aria-hidden="true"></i><span>Suất hợp lệ, không có cảnh báo.</span></div>';
      return;
    }
    const ul = document.createElement('ul');
    ul.className = 'issue-list';
    issues.forEach((i) => {
      const li = document.createElement('li');
      const err = i.severity === 'error';
      li.className = err ? 'tone-danger' : 'tone-warning';
      li.innerHTML = `<i class="bi ${err ? 'bi-x-octagon' : 'bi-exclamation-triangle'}" aria-hidden="true"></i>`;
      const span = document.createElement('span');
      span.textContent = (err ? '' : 'Cảnh báo: ') + i.message;
      li.appendChild(span);
      ul.appendChild(li);
    });
    result.appendChild(ul);
  }

  function renderTimes(t, data) {
    if (!t) { times.hidden = true; return; }
    times.hidden = false;
    Object.entries(t).forEach(([k, v]) => { const el = times.querySelector(`[data-t="${k}"]`); if (el) el.textContent = v; });
    const age = times.querySelector('[data-t="age"]');
    age.textContent = data.ageExplanation || '–';
  }

  // Chỉ mở các bản ngôn ngữ phim có
  function syncLanguages() {
    const opt = movie.selectedOptions[0];
    const allowed = (opt && opt.dataset.languages ? opt.dataset.languages.split(',') : []);
    Array.from(lang.options).forEach((o) => { o.disabled = allowed.length > 0 && !allowed.includes(o.value); });
    if (lang.selectedOptions[0]?.disabled) { const first = Array.from(lang.options).find((o) => !o.disabled); if (first) lang.value = first.value; }
  }

  let timer = null, seq = 0;
  async function check() {
    const mine = ++seq;
    const v = values();
    if (v.auditoriumId === '0' || v.movieId === '0' || !v.startTime) { times.hidden = true; return; }
    try {
      const data = await ui.api('/scheduler/check', { method: 'POST', body: v, silent: true });
      if (mine !== seq) return;
      renderTimes(data.times, data);
      renderIssues(data.issues);
    } catch (e) {
      if (mine === seq) { const div = document.createElement('div'); div.className = 'field-error'; div.textContent = e.message; result.replaceChildren(div); }
    }
  }
  const schedule = () => { clearTimeout(timer); timer = setTimeout(check, 250); };

  form.addEventListener('change', (e) => { if (e.target === movie) syncLanguages(); schedule(); });
  form.addEventListener('input', schedule);

  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    try {
      const data = await ui.api('/scheduler/save', { method: 'POST', body: values(), button: save, silent: true });
      ui.markClean?.();
      location.href = data.redirect;
    } catch (err) {
      if (err.details && err.details.issues) renderIssues(err.details.issues);
      ui.toast.error(err.message);
      ui.shake(result);
    }
  });

  syncLanguages();
  check();
})();
