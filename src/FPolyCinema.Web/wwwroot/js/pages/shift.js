/* shift.js – mở ca và đóng ca mù (UI P7). Wizard theo bước, bàn phím số lớn cho ô tiền, đếm tiền theo mệnh giá (hệ thống tự cộng),
   gửi số tờ theo mệnh giá lên /pos/close-shift rồi chuyển sang biên bản; nút in biên bản. */
(function () {
  'use strict';
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));
  const money = (n) => Number(n || 0).toLocaleString('vi-VN') + ' ₫';

  // Bàn phím số: ghi vào ô tiền đích rồi phát sự kiện input để ô tự định dạng và cập nhật ô ẩn
  $$('[data-num-pad]').forEach((pad) => {
    if (pad.closest('#pay-cash')) return; // POS tự xử lý
    const target = $(pad.dataset.target);
    pad.addEventListener('click', (e) => {
      const k = e.target.closest('[data-k]'); if (!k || !target) return;
      let s = target.value.replace(/\D/g, '');
      s = k.dataset.k === 'back' ? s.slice(0, -1) : (s + k.dataset.k).replace(/^0+/, '');
      target.value = s.slice(0, 10);
      target.dispatchEvent(new Event('input', { bubbles: true }));
    });
  });

  // Wizard theo bước (mở ca, đóng ca)
  function stepper(form, onStep) {
    const steps = $$('[data-step]', form);
    let cur = 0;
    const show = (i) => {
      cur = i;
      steps.forEach((s, j) => { s.hidden = j !== i; });
      const items = $$('.stepper li', document);
      items.forEach((li, j) => {
        li.classList.toggle('is-done', j < i); li.classList.toggle('is-current', j === i);
        if (j === i) li.setAttribute('aria-current', 'step'); else li.removeAttribute('aria-current');
        const no = $('.step-no', li); if (no) no.textContent = j < i ? '✓' : String(j + 1);
      });
      const compact = $('.stepper-compact'); if (compact) compact.textContent = `Bước ${i + 1}/${items.length} · ${$('span:not(.step-no)', items[i])?.firstChild?.textContent || ''}`;
      onStep?.(i);
      (steps[i].querySelector('input:not([type=hidden]), button') || steps[i]).focus();
    };
    form.addEventListener('click', (e) => {
      if (e.target.closest('[data-next]')) show(Math.min(cur + 1, steps.length - 1));
      else if (e.target.closest('[data-prev]')) show(Math.max(cur - 1, 0));
    });
    return { show, get current() { return cur; } };
  }

  const open = $('[data-shift-wizard]');
  if (open) {
    const w = stepper(open);
    // Có lỗi ở ô tiền thì mở thẳng bước 2
    if ($('#open-cash.is-invalid')) w.show(1);
  }

  const close = $('[data-close-form]');
  if (close) {
    const total = () => $$('[data-denom]', close).reduce((s, i) => s + Number(i.dataset.denom) * Math.max(0, Math.floor(Number(i.value) || 0)), 0);
    const sync = () => {
      $$('[data-denom]', close).forEach((i) => { $(`[data-denom-sum="${i.dataset.denom}"]`).textContent = money(Number(i.dataset.denom) * Math.max(0, Math.floor(Number(i.value) || 0))); });
      $('[data-count-total]').textContent = money(total());
      $('[data-count-total-confirm]').textContent = money(total());
    };
    close.addEventListener('input', sync);
    close.addEventListener('focusin', (e) => { if (e.target.matches('[data-denom]')) e.target.select(); });
    stepper(close, sync);
    close.addEventListener('submit', async (e) => {
      e.preventDefault();
      const body = new FormData();
      $$('[data-denom]', close).forEach((i) => body.append(`counts[${i.dataset.denom}]`, String(Math.max(0, Math.floor(Number(i.value) || 0)))));
      try {
        const d = await ui.api('/pos/close-shift', { method: 'POST', body, button: e.submitter });
        location.href = d.reportUrl;
      } catch { /* ui.api đã báo lỗi */ }
    });
    sync();
  }

  document.addEventListener('click', (e) => { if (e.target.closest('[data-print]')) window.print(); });
  // Trang in vé mở với auto=1: tự gọi lệnh in sau khi ảnh QR và font tải xong
  if (document.body.classList.contains('is-auto-print')) window.addEventListener('load', () => setTimeout(() => window.print(), 300));
})();
