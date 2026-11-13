/* Form đồ uống (UI A3): bảng định mức theo rạp – thêm, bỏ dòng, hiện đơn vị, chép định mức từ rạp khác theo tên nguyên liệu,
   đánh số lại tên ô Recipes[i].* trước khi gửi. Ảnh món do ui.js (data-dropzone) xử lý bằng fetch tới FileController. */
(() => {
  'use strict';
  const form = document.getElementById('drink-form');
  if (!form) return;

  const blocks = () => Array.from(form.querySelectorAll('[data-recipe-cinema]'));
  const markDirty = () => form.dispatchEvent(new Event('change', { bubbles: true }));

  function syncEmpty(block) {
    const empty = block.querySelector('[data-recipe-empty]');
    if (empty) empty.hidden = block.querySelectorAll('[data-recipe-row]').length > 0;
  }

  function addRow(block, materialName, qty) {
    const tpl = block.querySelector('template[data-recipe-template]');
    const row = tpl.content.firstElementChild.cloneNode(true);
    block.querySelector('[data-recipe-rows]').appendChild(row);
    if (materialName) {
      const opt = Array.from(row.querySelectorAll('select option')).find((o) => o.textContent.trim() === materialName);
      if (opt) { opt.selected = true; row.querySelector('[data-unit]').textContent = opt.dataset.unit || '–'; }
    }
    if (qty) row.querySelector('[data-name="Quantity"]').value = qty;
    syncEmpty(block);
    return row;
  }

  function reindex() {
    let i = 0;
    blocks().forEach((b) => b.querySelectorAll('[data-recipe-row]').forEach((row) => {
      const sel = row.querySelector('[data-name="RawMaterialId"]');
      const qty = row.querySelector('[data-name="Quantity"]');
      // Dòng chưa chọn nguyên liệu và chưa nhập định mức thì bỏ, không gửi lên
      if (sel.value === '0' && !qty.value) { row.querySelectorAll('[data-name]').forEach((x) => x.removeAttribute('name')); return; }
      row.querySelectorAll('[data-name]').forEach((x) => { x.name = `Recipes[${i}].${x.dataset.name}`; });
      i++;
    }));
  }

  form.addEventListener('click', (e) => {
    const add = e.target.closest('[data-recipe-add]');
    if (add) { const block = add.closest('[data-recipe-cinema]'); addRow(block).querySelector('select').focus(); markDirty(); return; }
    const rm = e.target.closest('[data-recipe-remove]');
    if (rm) {
      const block = rm.closest('[data-recipe-cinema]');
      bootstrap.Tooltip.getInstance(rm)?.dispose();
      rm.closest('[data-recipe-row]').remove();
      syncEmpty(block); markDirty(); return;
    }
    const copy = e.target.closest('[data-recipe-copy]');
    if (copy) {
      const target = copy.closest('[data-recipe-cinema]');
      const source = form.querySelector(`[data-recipe-cinema="${copy.dataset.recipeCopy}"]`);
      const items = Array.from(source.querySelectorAll('[data-recipe-row]')).map((r) => {
        const sel = r.querySelector('select');
        return { name: sel.selectedOptions[0]?.value !== '0' ? sel.selectedOptions[0].textContent.trim() : null, qty: r.querySelector('[data-name="Quantity"]').value };
      }).filter((x) => x.name);
      if (!items.length) { ui.toast.warning(`${source.dataset.cinemaName} chưa có định mức để chép.`); return; }
      target.querySelectorAll('[data-recipe-row]').forEach((r) => r.remove());
      let missing = 0;
      items.forEach((it) => { const row = addRow(target, it.name, it.qty); if (row.querySelector('select').value === '0') missing++; });
      syncEmpty(target); markDirty();
      if (missing) ui.toast.warning(`Đã chép định mức. Có ${missing} nguyên liệu chưa có ở ${target.dataset.cinemaName}, bạn chọn lại cho các dòng đó.`);
      else ui.toast.success(`Đã chép định mức sang ${target.dataset.cinemaName}. Bấm Lưu để áp dụng.`);
    }
  });

  form.addEventListener('change', (e) => {
    const sel = e.target.closest('[data-recipe-row] select');
    if (sel) sel.closest('[data-recipe-row]').querySelector('[data-unit]').textContent = sel.selectedOptions[0]?.dataset.unit || '–';
  });

  form.addEventListener('submit', reindex);
})();
