/* stock-form.js – phiếu kho: lập phiếu hủy hàng hỏng (/pos/wastage, PosStaff) và phiếu nhập kho (/admin/materials/import, Manager) – SPEC 3.11-14, 3.11-15.
   Thêm, bớt dòng nguyên liệu; gửi form bằng ui.api; lỗi theo dòng (lines[i].Quantity, lines[i].Reason, lines[i].RawMaterialId) hiện ngay dưới ô.
   Trang chi tiết phiếu hủy hàng: nút Duyệt, Từ chối (data-wastage-decide). */
(function () {
  'use strict';
  const form = document.querySelector('[data-stock-form]');
  if (form) {
    const box = form.querySelector('[data-stock-lines]') || form;
    const list = box.querySelector('[data-line-list]');
    const tpl = box.querySelector('[data-line-template]');
    const errBox = box.querySelector('[data-lines-error]');
    let seq = 0;

    function addLine() {
      const node = tpl.content.firstElementChild.cloneNode(true);
      seq++;
      node.querySelectorAll('.form-label').forEach((label) => {
        const field = label.parentElement.querySelector('input, select');
        if (field) { field.id = `${field.name}-${seq}`; label.htmlFor = field.id; }
      });
      list.appendChild(node);
      return node;
    }
    const lines = () => [...list.querySelectorAll('[data-line]')];

    box.addEventListener('click', (e) => {
      if (e.target.closest('[data-line-add]')) { addLine().querySelector('select').focus(); return; }
      const rm = e.target.closest('[data-line-remove]');
      if (rm) {
        if (lines().length > 1) rm.closest('[data-line]').remove();
        else { const l = rm.closest('[data-line]'); l.querySelectorAll('input, select').forEach((f) => { f.value = ''; }); }
      }
    });

    function clearErrors() {
      form.querySelectorAll('.field-error[data-generated]').forEach((x) => x.remove());
      form.querySelectorAll('.is-invalid').forEach((x) => x.classList.remove('is-invalid'));
      errBox.hidden = true;
    }
    function fieldError(el, msg) {
      el.classList.add('is-invalid');
      const p = document.createElement('span');
      p.className = 'field-error';
      p.dataset.generated = '1';
      p.textContent = msg;
      el.insertAdjacentElement('afterend', p);
    }
    const NAME = { RawMaterialId: 'materialIds', Quantity: 'quantities', Reason: 'reasons' };
    function showErrors(details) {
      Object.entries(details || {}).forEach(([k, msg]) => {
        const m = /^lines\[(\d+)\]\.(\w+)$/.exec(k);
        const line = m ? lines()[Number(m[1])] : null;
        const el = line ? line.querySelector(`[name="${NAME[m[2]]}"]`) : form.querySelector(`[name="${k}"]`);
        if (el) fieldError(el, msg);
        else { errBox.textContent = msg; errBox.hidden = false; }
      });
    }

    form.addEventListener('submit', async (e) => {
      e.preventDefault();
      clearErrors();
      const fd = new FormData(form);
      try {
        const r = await ui.api(form.getAttribute('action'), { method: 'POST', body: fd, button: form.querySelector('[type="submit"]'), silent: true });
        ui.toast.success(r.message);
        form.reset();
        list.innerHTML = '';
        if (tpl) addLine();
        setTimeout(() => location.reload(), 1200);
      } catch (err) {
        if (err && err.code === 'VALIDATION_FAILED') { showErrors(err.details); ui.toast.error(err.message); }
        else ui.toast.error(err ? err.message : 'Chưa gửi được phiếu. Bạn thử lại.');
      }
    });
    if (tpl) addLine();
  }

  const decide = document.querySelector('[data-wastage-decide]');
  if (decide) {
    decide.addEventListener('click', async (e) => {
      const b = e.target.closest('[data-decide]');
      if (!b) return;
      const approve = b.dataset.decide === 'approve';
      const ok = await ui.confirm(approve
        ? { title: 'Duyệt phiếu hủy hàng?', message: 'Tồn kho thực tế giảm theo số lượng trên phiếu.', consequences: ['Ghi giao dịch kho WASTAGE và nhật ký.', 'Món không còn đủ một phần sẽ tạm hết trên POS và web.'], confirmText: 'Duyệt', cancelText: 'Quay lại', tone: 'primary' }
        : { title: 'Từ chối phiếu hủy hàng?', message: 'Tồn kho giữ nguyên.', confirmText: 'Từ chối', cancelText: 'Quay lại', tone: 'danger' });
      if (!ok) return;
      try {
        const r = await ui.api(`/ops/wastage/${decide.dataset.id}/${approve ? 'approve' : 'reject'}`, { method: 'POST', body: {}, button: b });
        ui.toast.success(r.message);
        setTimeout(() => location.reload(), 1000);
      } catch { /* ui.api đã báo lỗi */ }
    });
  }
})();
