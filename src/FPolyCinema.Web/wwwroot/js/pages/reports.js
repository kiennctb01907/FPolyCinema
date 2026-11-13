/* reports.js – mốc nhanh của trang báo cáo (UI A6): Hôm nay, 7 ngày, 30 ngày, Tháng này, Tháng trước.
   Server tính khoảng ngày theo IClock (giờ Việt Nam) từ tham số preset; sửa tay ô ngày thì bỏ mốc nhanh. */
(function () {
  'use strict';
  const form = document.querySelector('.report-filter');
  if (!form) return;
  const preset = form.querySelector('[data-range-preset]');
  form.addEventListener('click', (e) => {
    const b = e.target.closest('[data-report-range]');
    if (!b) return;
    preset.value = b.dataset.reportRange;
    form.requestSubmit();
  });
  form.addEventListener('change', (e) => {
    if (e.target.matches('[data-range-from], [data-range-to]')) preset.value = '';
  });
})();
