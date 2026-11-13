/* bills.js – quản lý hóa đơn (UI A8): nút "Xuất CSV" đi theo bộ lọc hiện tại; hủy hóa đơn trên trang chi tiết theo đúng quy tắc
   (chưa thanh toán: xác nhận rồi hủy; đã thanh toán: lý do rồi PIN quản lý trên bàn phím PIN). */
(function () {
  'use strict';
  const exp = document.querySelector('[data-export-link]');
  if (exp) {
    // Bộ lọc đổi query string bằng history.replaceState: cập nhật đường dẫn xuất CSV trước khi bấm
    exp.addEventListener('click', () => { exp.href = '/admin/bills/export' + location.search; });
  }

  document.addEventListener('click', async (e) => {
    const btn = e.target.closest('[data-bill-cancel]');
    if (!btn) return;
    const id = btn.dataset.billCancel, code = btn.dataset.code;
    const url = `/admin/bills/${id}/cancel`;
    if (btn.dataset.status === 'waiting') {
      const ok = await ui.confirm({ title: 'Hủy hóa đơn chưa thanh toán', message: `Hủy hóa đơn ${code}?`, tone: 'danger', confirmText: 'Hủy hóa đơn', cancelText: 'Không hủy', consequences: ['Các ghế đang giữ được trả lại.'] });
      if (!ok) return;
      try { const d = await ui.api(url, { method: 'POST', body: {}, button: btn }); ui.toast.success(d.message); setTimeout(() => location.reload(), 600); } catch { /* đã báo */ }
      return;
    }
    const ok = await ui.confirm({
      title: 'Hủy hóa đơn đã thanh toán', message: `Hủy hóa đơn ${code} và hoàn tiền?`, tone: 'danger', confirmText: 'Tiếp tục', cancelText: 'Không hủy',
      consequences: ['Hoàn tiền theo phương thức gốc.', 'Vé bị hủy, ghế trả về trống, điểm đã cộng bị thu hồi.', 'Cần nhập lý do và PIN quản lý của bạn.'],
    });
    if (!ok) return;
    const reason = await askReason();
    if (!reason) return;
    let outcome = null;
    const pin = await ui.pin({
      title: 'Nhập PIN quản lý để duyệt hủy',
      verify: async (value) => {
        try { outcome = await ui.api(url, { method: 'POST', body: { reason, pin: value }, silent: true }); return true; }
        catch (err) {
          if (err.code === 'PIN_INVALID') return { message: err.message };
          if (err.code === 'PIN_LOCKED') return { message: err.message, locked: true };
          outcome = { error: err }; return true;
        }
      },
    });
    if (!pin || !outcome) return;
    if (outcome.error) { ui.toast.error(outcome.error.message); return; }
    ui.toast.success(outcome.message);
    setTimeout(() => location.reload(), 600);
  });

  /** Hộp nhập lý do hủy (không dùng prompt của trình duyệt). */
  function askReason() {
    return new Promise((resolve) => {
      const m = document.getElementById('cancel-reason');
      const form = m.querySelector('form');
      form.reset();
      ui.clearFieldErrors(form);
      let value = null;
      const modal = bootstrap.Modal.getOrCreateInstance(m);
      const onSubmit = (e) => {
        e.preventDefault();
        const r = form.reason.value.trim();
        if (r.length < 5) { ui.showFieldErrors(form, { reason: 'Bạn ghi lý do hủy (ít nhất 5 ký tự).' }); return; }
        value = r; modal.hide();
      };
      form.addEventListener('submit', onSubmit);
      m.addEventListener('hidden.bs.modal', () => { form.removeEventListener('submit', onSubmit); resolve(value); }, { once: true });
      modal.show();
      setTimeout(() => form.reason.focus(), 300);
    });
  }
})();
