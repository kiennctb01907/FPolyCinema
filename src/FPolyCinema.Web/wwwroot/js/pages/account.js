/* account.js – tài khoản C5: đánh dấu mọi thông báo đã đọc. */
(function () {
  'use strict';
  const btn = document.querySelector('[data-read-all]');
  if (!btn) return;
  btn.addEventListener('click', async () => {
    try {
      await ui.api('/account/notifications/read', { method: 'POST', body: {}, button: btn });
      document.querySelectorAll('[data-notifications] .is-unread').forEach((li) => li.classList.remove('is-unread'));
      document.querySelectorAll('.page-tabs .count-pill').forEach((p) => p.remove());
      btn.remove();
      ui.toast.success('Đã đánh dấu mọi thông báo là đã đọc.');
    } catch { /* ui.api đã báo lỗi */ }
  });
})();
