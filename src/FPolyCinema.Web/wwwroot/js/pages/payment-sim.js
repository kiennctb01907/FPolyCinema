/* payment-sim.js – trang mô phỏng cổng thanh toán (SPEC 3.7-01): bấm "Thanh toán thành công" hoặc "Thất bại" thì gọi POST /payments/webhook
   giống cổng thật, gửi nội dung JSON đã ký sẵn (ProviderTxnId mới mỗi lần mở trang) và chữ ký HMAC-SHA256 ở header X-Signature. */
(function () {
  'use strict';
  const root = document.querySelector('[data-payment-sim]');
  if (!root) return;
  const s = JSON.parse(document.getElementById('sim-state').textContent);
  const out = root.querySelector('[data-sim-result]');

  root.addEventListener('click', async (e) => {
    const b = e.target.closest('[data-sim-send]');
    if (!b) return;
    const ok = b.dataset.simSend === 'success';
    root.querySelectorAll('[data-sim-send]').forEach((x) => { x.disabled = true; });
    ui.busy(b, true);
    try {
      const res = await fetch('/payments/webhook', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-Signature': ok ? s.successSig : s.failSig },
        body: ok ? s.success : s.fail,
        credentials: 'omit',
      });
      const json = await res.json().catch(() => null);
      out.textContent = res.ok
        ? (ok ? 'Cổng thanh toán đã gửi kết quả thành công. Bạn quay lại màn hình chờ, vé sẽ tự hiện.' : 'Cổng thanh toán đã báo thất bại. Bạn có thể thử lại trước khi hết giờ giữ ghế.')
        : (json && json.error && json.error.message) || 'Cổng thanh toán không gửi được kết quả.';
      out.className = 'small mt-3 mb-0 ' + (res.ok ? 'text-success-fg' : 'text-danger-fg');
      // Báo thất bại (hoặc cổng không gửi được) thì khách được thử lại ngay trên trang này (SPEC 3.7-07); mã giao dịch thành công vẫn chưa dùng
      if (!ok || !res.ok) root.querySelectorAll('[data-sim-send]').forEach((x) => { x.disabled = false; });
    } catch {
      out.textContent = 'Mất kết nối tới máy chủ. Bạn thử lại.';
      out.className = 'small mt-3 mb-0 text-danger-fg';
      root.querySelectorAll('[data-sim-send]').forEach((x) => { x.disabled = false; });
    } finally {
      ui.busy(b, false);
    }
  });
})();
