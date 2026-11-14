/* void-bill.js – hủy hóa đơn đã thanh toán tại quầy (SPEC 3.6, UI U7.P1-11, CONTRACTS C5-03).
   Thu ngân nhập lý do và chọn quản lý duyệt; quản lý nhập PIN 6 số trên bàn phím PIN (ui.pin). PIN sai thì rung và báo số lần còn lại,
   bị khóa thì báo thời gian khóa. Dùng ở /pos/index (nhập mã hóa đơn) và /pos/my-bills (nút Hủy trên từng hóa đơn). */
(function () {
  'use strict';
  const $ = (s, r = document) => r.querySelector(s);
  let onDone = () => location.reload();

  function modal() { return $('#void-bill'); }

  const VoidBill = {
    init(opts = {}) { if (opts.onDone) onDone = opts.onDone; },
    /** Mở hộp hủy; billId và code khi hủy từ danh sách hóa đơn. */
    open(billId, code) {
      const m = modal();
      if (!m) return;
      const form = $('[data-void-form]', m);
      form.reset();
      ui.clearFieldErrors(form);
      $('[data-void-bill-id]', m).value = billId || '';
      const codeEl = $('[data-void-code]', m);
      if (codeEl) codeEl.textContent = code || '';
      bootstrap.Modal.getOrCreateInstance(m).show();
    },
  };

  document.addEventListener('submit', async (e) => {
    const form = e.target.closest('[data-void-form]');
    if (!form) return;
    e.preventDefault();
    ui.clearFieldErrors(form);
    const data = Object.fromEntries(new FormData(form).entries());
    const errors = {};
    if (!data.billId && !(data.billCode || '').trim()) errors.billCode = 'Bạn nhập mã hóa đơn hoặc mã vé cần hủy.';
    if ((data.reason || '').trim().length < 5) errors.reason = 'Bạn ghi lý do hủy (ít nhất 5 ký tự).';
    if (!data.approverId) errors.approverId = 'Bạn chọn quản lý duyệt.';
    if (Object.keys(errors).length) { ui.showFieldErrors(form, errors); return; }
    const approver = form.querySelector('[name="approverId"]').selectedOptions[0]?.textContent || '';
    // Đóng hẳn hộp lý do rồi mới mở bàn phím PIN (tránh hai lớp nền chồng nhau)
    await new Promise((resolve) => { modal().addEventListener('hidden.bs.modal', resolve, { once: true }); bootstrap.Modal.getOrCreateInstance(modal()).hide(); });

    let outcome = null;
    const pin = await ui.pin({
      title: 'Quản lý nhập PIN để duyệt hủy',
      message: approver,
      verify: async (value) => {
        try {
          outcome = await ui.api('/pos/request-void', { method: 'POST', body: { ...data, billId: data.billId || undefined, pin: value }, silent: true });
          return true;
        } catch (err) {
          if (err.code === 'PIN_INVALID') return { message: err.message };
          if (err.code === 'PIN_LOCKED') return { message: err.message, locked: true };
          outcome = { error: err };
          return true;
        }
      },
    });
    if (!pin || !outcome) return;
    if (outcome.error) {
      ui.toast.error(outcome.error.message);
      if (outcome.error.code === 'VALIDATION_FAILED') { VoidBill.reopen(form, data, outcome.error.details); }
      return;
    }
    ui.toast.success(outcome.message);
    onDone();
  });

  // ---------------------------------------------------------------- duyệt từ xa (UI P1-12, SPEC 3.6-21, 3.6-22)
  // Thu ngân gửi yêu cầu thay cho nhập PIN; quản lý duyệt trên popup hoặc dashboard; APPROVAL_DECIDED về máy này (kênh 2, pos_terminal_{khóa máy}).
  const waiting = new Map(); // approvalId → mã hóa đơn
  document.addEventListener('click', async (e) => {
    const b = e.target.closest('[data-void-remote]');
    if (!b) return;
    const form = b.closest('[data-void-form]');
    ui.clearFieldErrors(form);
    const data = Object.fromEntries(new FormData(form).entries());
    const errors = {};
    if (!data.billId && !(data.billCode || '').trim()) errors.billCode = 'Bạn nhập mã hóa đơn hoặc mã vé cần hủy.';
    if ((data.reason || '').trim().length < 5) errors.reason = 'Bạn ghi lý do hủy (ít nhất 5 ký tự).';
    if (Object.keys(errors).length) { ui.showFieldErrors(form, errors); return; }
    try {
      const r = await ui.api('/pos/request-void-remote', { method: 'POST', body: { billId: data.billId || undefined, billCode: data.billCode, reason: data.reason }, form, button: b });
      waiting.set(Number(r.approvalId), r.billCode);
      bootstrap.Modal.getOrCreateInstance(modal()).hide();
      ui.toast.info(`${r.message} Hạn duyệt tới ${ui.time(r.expiresAt)}.`);
    } catch { /* lỗi theo ô đã hiện trong hộp */ }
  });

  const terminalKey = modal()?.dataset.terminalKey;
  if (window.realtime && terminalKey) {
    realtime.join('JoinTerminal', terminalKey, false);
    realtime.on('APPROVAL_DECIDED', (p) => {
      waiting.delete(Number(p.id));
      if (p.status === 'APPROVED') { ui.toast.success(p.message); onDone(); }
      else if (p.status === 'REJECTED') ui.toast.error(p.message);
      else ui.toast.warning(p.message);
    });
  }

  VoidBill.reopen = (form, data, details) => {
    bootstrap.Modal.getOrCreateInstance(modal()).show();
    Object.entries(data).forEach(([k, v]) => { const el = form.querySelector(`[name="${k}"]`); if (el && el.type !== 'hidden') el.value = v; });
    if (details) ui.showFieldErrors(form, details);
  };

  window.VoidBill = VoidBill;
})();
