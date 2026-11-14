/* incident.js – hủy suất do sự cố (SPEC 3.10-05): chọn suất thì hiện mã suất cần gõ lại; nút "Hủy suất" chỉ gửi khi mã gõ lại khớp
   (server kiểm lại bằng IncidentRules). */
(function () {
  'use strict';
  const form = document.querySelector('[data-incident-form]');
  if (!form) return;
  const show = form.querySelector('[data-incident-show]');
  const hint = form.querySelector('[data-incident-hint]');
  const code = form.querySelector('[name="confirmCode"]');
  const current = () => show.selectedOptions[0]?.dataset.code || '';
  show.addEventListener('change', () => {
    const c = current();
    hint.textContent = c ? `Mã của suất đã chọn: ${c}` : 'Mã suất hiện sau khi chọn suất.';
    code.placeholder = c || 'Ví dụ: S1024';
    code.value = '';
  });
  form.addEventListener('submit', (e) => {
    const c = current();
    const errors = {};
    if (!c) errors.showtimeId = 'Bạn chọn suất cần hủy.';
    if ((form.querySelector('[name="reason"]').value || '').trim().length < 5) errors.reason = 'Bạn ghi lý do hủy suất (ít nhất 5 ký tự).';
    if (c && code.value.trim().toUpperCase() !== c.toUpperCase()) errors.confirmCode = `Bạn gõ lại đúng mã suất ${c} để xác nhận.`;
    if (Object.keys(errors).length) { e.preventDefault(); ui.clearFieldErrors(form); ui.showFieldErrors(form, errors); }
  });
})();
