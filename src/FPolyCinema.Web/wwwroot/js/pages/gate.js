/* gate.js – máy soát vé P3 (SPEC 3.8, UI U7.P3).
   - Ô nhập mã luôn autofocus và tự lấy lại focus sau mỗi lần quét (máy quét mã vạch gõ mã rồi Enter); nút bật camera dùng html5-qrcode.
   - Thẻ kết quả nền đặc: xanh hợp lệ, vàng cần xác nhận (phải tích "Đã kiểm tra" mới cho vào), đỏ từ chối. Danh sách ghế có checkbox 32px,
     nút "Cho vào tất cả" 56px. Thẻ đỏ và thẻ "Đã cho vào" tự quay về chờ sau 6 giây (thanh đếm; chạm vào thẻ thì dừng đếm).
   - Rung và âm báo ngắn nếu thiết bị hỗ trợ.
   - Khung "Phòng đang dọn" có nút "Đã dọn xong"; danh sách cập nhật khi nhận SHOWTIME_OPS_CHANGED (kênh 6, JoinOps). */
(function () {
  'use strict';
  const root = document.querySelector('[data-gate]');
  if (!root) return;
  const $ = (s, r = root) => r.querySelector(s);
  const esc = (s) => String(s ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  const cinemaId = Number(root.dataset.cinemaId);
  const resetMs = Number(root.dataset.resetSeconds || 6) * 1000;
  const input = $('[data-gate-code]');
  const result = $('[data-gate-result]');
  const room = $('[data-gate-room]');
  const idleHtml = result.innerHTML;
  let timer = null;
  let current = null;
  let busy = false;

  // ---------------------------------------------------------------- focus luôn ở ô nhập mã
  const refocus = () => { if (!document.querySelector('.modal.show') && document.activeElement !== input) input.focus({ preventScroll: true }); };
  input.addEventListener('blur', () => setTimeout(() => {
    const a = document.activeElement;
    if (!a || a === document.body || !root.contains(a) || !(a.matches('input, select, button, textarea'))) refocus();
  }, 150));

  // ---------------------------------------------------------------- âm báo và rung
  let audio = null;
  function beep(kind) {
    try {
      audio = audio || new (window.AudioContext || window.webkitAudioContext)();
      const tones = kind === 'reject' ? [[220, 0], [180, 0.18]] : kind === 'warn' ? [[660, 0], [520, 0.15]] : [[880, 0]];
      tones.forEach(([f, at]) => {
        const o = audio.createOscillator(), g = audio.createGain();
        o.frequency.value = f; o.type = 'sine';
        g.gain.setValueAtTime(0.0001, audio.currentTime + at);
        g.gain.exponentialRampToValueAtTime(0.25, audio.currentTime + at + 0.02);
        g.gain.exponentialRampToValueAtTime(0.0001, audio.currentTime + at + 0.14);
        o.connect(g).connect(audio.destination);
        o.start(audio.currentTime + at); o.stop(audio.currentTime + at + 0.16);
      });
    } catch { /* thiết bị không hỗ trợ âm thanh */ }
    if (navigator.vibrate) navigator.vibrate(kind === 'reject' ? [120, 80, 120] : kind === 'warn' ? [80, 60, 80] : 60);
  }

  // ---------------------------------------------------------------- quay về chờ sau 6 giây
  function stopTimer() { clearTimeout(timer); timer = null; result.querySelector('.gate-countdown')?.classList.add('is-paused'); }
  function startTimer() {
    clearTimeout(timer);
    const bar = result.querySelector('.gate-countdown');
    if (bar) { bar.style.setProperty('--gate-reset', resetMs + 'ms'); bar.classList.remove('is-paused'); void bar.offsetWidth; bar.classList.add('is-running'); }
    timer = setTimeout(reset, resetMs);
  }
  function reset() {
    clearTimeout(timer);
    current = null;
    result.className = 'gate-result is-idle';
    result.innerHTML = idleHtml;
    input.value = '';
    refocus();
  }
  result.addEventListener('pointerdown', () => { if (timer) stopTimer(); });

  // ---------------------------------------------------------------- vẽ thẻ kết quả
  const ICON = { ok: 'bi-check-circle-fill', warn: 'bi-exclamation-triangle-fill', reject: 'bi-x-octagon-fill' };
  function render(r) {
    current = r;
    const v = r.verdict;
    const roomName = r.auditoriumName ? ` – ${esc(r.auditoriumName)}` : '';
    const main = v.kind === 'reject' ? esc(v.message) : (v.kind === 'warn' ? 'Cần xác nhận' : 'Hợp lệ') + roomName;
    const sub = r.movieTitle
      ? `${esc(r.movieTitle)} <span class="age-badge age-${esc(r.ageRating)}" title="${esc(r.ageExplanation)}">${esc(r.ageRating)}</span> · <span class="num">${ui.time(r.startTime)}–${ui.time(r.endTime)}</span>${r.billCode ? ` · ${esc(r.billCode)}` : ''}`
      : (r.billCode ? esc(r.billCode) : '');
    let html = `<div class="gate-card">
      <div class="gate-card-head"><i class="bi ${ICON[v.kind]} gate-icon" aria-hidden="true"></i>
        <div class="min-w-0"><p class="gate-main">${main}</p><p class="gate-sub">${sub}</p>${r.ageExplanation && v.kind !== 'reject' ? `<p class="gate-age">${esc(r.ageExplanation)}</p>` : ''}</div></div>`;
    if (r.otherRoomNote) html += `<p class="gate-note"><i class="bi bi-signpost-split" aria-hidden="true"></i>${esc(r.otherRoomNote)}</p>`;
    if (v.warnings && v.warnings.length) {
      html += `<div class="gate-warnings" role="alert"><p class="fw-500 mb-1"><i class="bi bi-person-vcard" aria-hidden="true"></i>Kiểm tra trước khi cho vào</p><ul>${v.warnings.map((w) => `<li>${esc(w)}</li>`).join('')}</ul>
        <label class="gate-check"><input type="checkbox" data-warn-confirm /> <span>Tôi đã kiểm tra giấy tờ và điều kiện ở trên</span></label></div>`;
    }
    if (r.tickets && r.tickets.length && v.kind !== 'reject') {
      html += `<ul class="gate-seats" aria-label="Ghế của hóa đơn">${r.tickets.map((t) => `<li>
          <label class="gate-check ${t.admittable ? '' : 'is-off'}"><input type="checkbox" value="${t.ticketId}" data-ticket ${t.admittable ? '' : 'disabled'} ${t.preselected ? 'checked' : ''} />
          <span><strong>${esc(t.seat)}</strong> · ${esc(t.audienceLabel)}</span><span class="gate-seat-state">${esc(t.statusLabel)}</span></label></li>`).join('')}</ul>
        <div class="gate-actions">
          <button type="button" class="btn btn-secondary btn-xl" data-admit-all><i class="bi bi-door-open" aria-hidden="true"></i>Cho vào tất cả</button>
          <button type="button" class="btn btn-ghost" data-admit-selected><i class="bi bi-check2-square" aria-hidden="true"></i>Cho vào ghế đã chọn</button>
        </div>`;
    }
    // Mọi thẻ tự quay về chờ sau 6 giây (UI P3-07); chạm vào thẻ (tích ghế, xác nhận cảnh báo) thì dừng đếm
    html += '<div class="gate-countdown" aria-hidden="true"><span></span></div>';
    html += '</div>';
    result.className = `gate-result is-${v.kind}`;
    result.innerHTML = html;
    beep(v.kind);
    startTimer();
  }

  function renderAdmitted(r) {
    result.className = 'gate-result is-ok';
    result.innerHTML = `<div class="gate-card"><div class="gate-card-head"><i class="bi bi-check-circle-fill gate-icon" aria-hidden="true"></i>
      <div class="min-w-0"><p class="gate-main">Đã cho vào</p><p class="gate-sub">${esc(r.message)}</p></div></div>
      <div class="gate-countdown" aria-hidden="true"><span></span></div></div>`;
    beep('ok');
    startTimer();
  }

  // ---------------------------------------------------------------- quét và cho vào
  async function scan(code) {
    code = (code || '').trim();
    if (!code || busy) return;
    busy = true;
    try {
      const r = await ui.api('/pos/gate-inspector/scan', { method: 'POST', body: { code, auditoriumId: room.value || undefined }, silent: true });
      render(r);
    } catch (err) {
      render({ verdict: { kind: 'reject', message: err.message, warnings: [] }, tickets: [] });
    } finally {
      busy = false;
      input.value = '';
      refocus();
    }
  }
  $('[data-gate-scan]').addEventListener('submit', (e) => { e.preventDefault(); scan(input.value); });

  async function admit(all, button) {
    if (!current || !current.billId) return;
    const confirmBox = result.querySelector('[data-warn-confirm]');
    if (confirmBox && !confirmBox.checked) {
      ui.toast.warning('Bạn tích "Tôi đã kiểm tra…" trước khi cho khách vào.');
      ui.shake(confirmBox.closest('.gate-warnings'));
      return;
    }
    const fd = new FormData();
    fd.append('billId', current.billId);
    fd.append('warningsConfirmed', confirmBox ? 'true' : 'false');
    const boxes = [...result.querySelectorAll('[data-ticket]:not(:disabled)')];
    const chosen = all ? boxes : boxes.filter((b) => b.checked);
    if (!chosen.length) { ui.toast.warning('Bạn chọn ít nhất một ghế để cho vào.'); return; }
    chosen.forEach((b) => fd.append('ticketIds', b.value));
    try {
      const r = await ui.api('/pos/gate-inspector/admit', { method: 'POST', body: fd, button });
      renderAdmitted(r);
    } catch { refocus(); }
  }
  result.addEventListener('click', (e) => {
    const all = e.target.closest('[data-admit-all]');
    const some = e.target.closest('[data-admit-selected]');
    if (all) admit(true, all);
    else if (some) admit(false, some);
  });

  // ---------------------------------------------------------------- camera
  let cam = null, lastCode = '', lastAt = 0;
  const camBtn = $('[data-camera-toggle]');
  async function toggleCamera() {
    const box = $('#gate-camera');
    if (cam) {
      try { await cam.stop(); } catch { /* đã dừng */ }
      cam = null; box.hidden = true; camBtn.setAttribute('aria-pressed', 'false'); camBtn.querySelector('span').textContent = 'Bật camera';
      refocus();
      return;
    }
    if (!window.Html5Qrcode) { ui.toast.error('Thiết bị không hỗ trợ quét bằng camera. Bạn dùng máy quét hoặc nhập mã.'); return; }
    box.hidden = false;
    cam = new Html5Qrcode('gate-camera');
    try {
      await cam.start({ facingMode: 'environment' }, { fps: 10, qrbox: 240 }, (text) => {
        const now = Date.now();
        if (text === lastCode && now - lastAt < 3000) return;
        lastCode = text; lastAt = now;
        scan(text);
      });
      camBtn.setAttribute('aria-pressed', 'true'); camBtn.querySelector('span').textContent = 'Tắt camera';
    } catch {
      cam = null; box.hidden = true;
      ui.toast.error('Không mở được camera. Bạn cho phép trình duyệt dùng camera hoặc nhập mã bằng tay.');
    }
  }
  camBtn.addEventListener('click', toggleCamera);

  // ---------------------------------------------------------------- phòng đang dọn
  const cleanBox = root.querySelector('[data-cleaning-box]');
  function drawCleaning(list) {
    const ul = cleanBox.querySelector('[data-cleaning-list]');
    ul.innerHTML = list.map((c) => `<li><span><strong>${esc(c.auditoriumName)}</strong> · ${esc(c.movieTitle)} · kết thúc <span class="num">${esc(c.end)}</span></span>
      <button type="button" class="btn btn-success" data-cleaned="${c.showtimeId}"><i class="bi bi-check2-circle" aria-hidden="true"></i>Đã dọn xong</button></li>`).join('');
    cleanBox.hidden = list.length === 0;
  }
  async function reloadCleaning() {
    try { drawCleaning(await ui.api('/pos/gate-inspector/cleaning', { silent: true })); } catch { /* thử lại ở lần sau */ }
  }
  cleanBox.addEventListener('click', async (e) => {
    const b = e.target.closest('[data-cleaned]');
    if (!b) return;
    try {
      const r = await ui.api('/pos/gate-inspector/cleaned', { method: 'POST', body: { showtimeId: b.dataset.cleaned }, button: b });
      ui.toast.success(r.message);
      drawCleaning(r.cleaning);
    } catch { reloadCleaning(); }
    refocus();
  });

  if (window.realtime) {
    realtime.join('JoinOps', cinemaId);
    realtime.on('SHOWTIME_OPS_CHANGED', () => reloadCleaning());
    realtime.onReload(reloadCleaning);
  }
  refocus();
})();
