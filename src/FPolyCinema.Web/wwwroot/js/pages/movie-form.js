/* Form phim (UI A3): hai bảng dòng thêm bớt được – chia doanh thu theo tuần (RentalRules) và KDM theo rạp (Kdms).
   Trước khi gửi, đánh số lại tên ô {Tiền tố}[i].{Tên}; dòng để trống hoàn toàn thì không gửi.
   Poster do ui.js (data-dropzone, tỉ lệ 2:3) tải lên và xóa bằng fetch tới FileController. */
(() => {
  'use strict';
  const form = document.getElementById('movie-form');
  if (!form) return;

  const markDirty = () => form.dispatchEvent(new Event('change', { bubbles: true }));
  const sections = () => Array.from(form.querySelectorAll('[data-rows]'));

  function syncEmpty(section) {
    const empty = section.querySelector('[data-row-empty]');
    if (empty) empty.hidden = section.querySelectorAll('[data-row-list] [data-row]').length > 0;
  }

  function isBlank(row) {
    return Array.from(row.querySelectorAll('[data-name]')).every((x) => x.value === '' || x.value === '0');
  }

  function reindex() {
    sections().forEach((section) => {
      const prefix = section.dataset.rows;
      let i = 0;
      section.querySelectorAll('[data-row-list] [data-row]').forEach((row) => {
        const fields = row.querySelectorAll('[data-name]');
        if (isBlank(row)) { fields.forEach((x) => x.removeAttribute('name')); return; }
        fields.forEach((x) => { x.name = `${prefix}[${i}].${x.dataset.name}`; });
        i++;
      });
    });
  }

  form.addEventListener('click', (e) => {
    const add = e.target.closest('[data-row-add]');
    if (add) {
      const section = add.closest('[data-rows]');
      const tpl = section.querySelector('template[data-row-template]');
      const row = tpl.content.firstElementChild.cloneNode(true);
      const list = section.querySelector('[data-row-list]');
      // Tuần mới gợi ý số kế tiếp
      const week = row.querySelector('[data-name="WeekNumber"]');
      if (week) {
        const max = Math.max(0, ...Array.from(list.querySelectorAll('[data-name="WeekNumber"]')).map((x) => Number(x.value) || 0));
        week.value = String(max + 1);
      }
      list.appendChild(row);
      ui.enhance(row); // ô ngày giờ hiển thị dd/MM/yyyy HH:mm
      syncEmpty(section);
      row.querySelector('input, select')?.focus();
      markDirty();
      return;
    }
    const rm = e.target.closest('[data-row-remove]');
    if (rm) {
      const section = rm.closest('[data-rows]');
      bootstrap.Tooltip.getInstance(rm)?.dispose();
      rm.closest('[data-row]').remove();
      syncEmpty(section);
      markDirty();
    }
  });

  form.addEventListener('submit', reindex);
})();
