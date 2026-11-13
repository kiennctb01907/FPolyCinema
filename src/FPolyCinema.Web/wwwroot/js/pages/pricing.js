/* Trang bảng giá và ngày lễ.
   - Ô tiền có dấu (data-signed-money): tự chấm phân cách hàng nghìn, cho phép dấu trừ (ví dụ -20.000 cho thứ Ba vui vẻ).
   - Xóa một ngày lễ sau khi hỏi lại (ui.confirm), gọi POST bằng ui.api rồi tải lại trang. */
(() => {
  'use strict';
  const nf = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 });
  document.querySelectorAll('[data-signed-money]').forEach((inp) => {
    inp.addEventListener('input', () => {
      const neg = inp.value.trim().startsWith('-');
      const digits = inp.value.replace(/\D/g, '').replace(/^0+(?=\d)/, '');
      inp.value = (neg ? '-' : '') + (digits ? nf.format(Number(digits)) : '');
    });
  });

  document.addEventListener('click', async (e) => {
    const btn = e.target.closest('[data-holiday-delete]');
    if (!btn) return;
    const ok = await ui.confirm({
      title: `Xóa ngày lễ “${btn.dataset.name}”?`,
      message: 'Ngày này sẽ tính giá lại theo thứ trong tuần (ngày thường, cuối tuần hoặc thứ Ba vui vẻ).',
      consequences: ['Vé đã bán giữ nguyên giá đã bán.', 'Vé bán từ bây giờ cho ngày này tính theo giá mới.'],
      confirmText: 'Xóa', tone: 'danger',
    });
    if (!ok) return;
    try {
      await ui.api(btn.dataset.holidayDelete, { method: 'POST', button: btn });
      ui.toast.success('Đã xóa ngày lễ');
      setTimeout(() => location.reload(), 500);
    } catch { /* đã báo bằng toast */ }
  });
})();
