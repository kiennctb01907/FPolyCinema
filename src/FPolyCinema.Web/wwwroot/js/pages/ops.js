/* ops.js – dashboard vận hành /ops và khối vận hành ở /admin (UI A7, A1-05; SPEC 3.2-07, 3.3-21, 3.8-12).
   - Kênh 6 (JoinOps): SHOWTIME_OPS_CHANGED tải lại vùng nội dung (suất trễ, phòng đang dọn, sĩ số).
   - Kênh 4 (JoinAuditorium mỗi phòng): OCCUPANCY_UPDATED cập nhật "đã vào / đã bán" của dòng suất ngay.
   - Yêu cầu phê duyệt đổi (approval:changed từ approvals.js) thì tải lại vùng.
   - Nút "Giải phóng" ghế nội bộ: hộp xác nhận rồi POST /ops/house-seats/{id}/release. */
(function () {
  'use strict';
  const root = document.querySelector('[data-ops]');
  if (!root) return;
  const ids = (s) => (s || '').split(',').filter(Boolean).map(Number);
  const regionUrl = root.dataset.url || location.pathname;
  let pending = null;
  const reload = () => {
    clearTimeout(pending);
    pending = setTimeout(() => ui.loadRegion(root, regionUrl, { push: 'none' }), 300);
  };

  root.addEventListener('click', async (e) => {
    const b = e.target.closest('[data-release-house]');
    if (!b) return;
    const ok = await ui.confirm({
      title: 'Giải phóng ghế nội bộ?',
      message: `${b.dataset.title}: ${b.dataset.count} ghế nội bộ sẽ mở bán cho khách ngay.`,
      consequences: ['Ghế hiện trống trên POS và web khách.', 'Thao tác được ghi vào nhật ký.'],
      confirmText: 'Giải phóng', cancelText: 'Để sau', tone: 'primary',
    });
    if (!ok) return;
    try {
      const r = await ui.api(`/ops/house-seats/${b.dataset.releaseHouse}/release`, { method: 'POST', body: {}, button: b });
      ui.toast.success(r.message);
      reload();
    } catch { reload(); }
  });

  function updateOccupancy(p) {
    root.querySelectorAll(`[data-occ-showtime="${p.showtimeId}"]`).forEach((tr) => {
      tr.querySelector('[data-occ-in]').textContent = p.checkedIn;
      tr.querySelector('[data-occ-sold]').textContent = p.sold;
      const pct = p.sold ? Math.round((p.checkedIn * 100) / p.sold) : 0;
      const bar = tr.querySelector('[data-occ-bar]');
      if (bar) { bar.firstElementChild.style.setProperty('--pct', pct); bar.setAttribute('aria-label', `Đã vào ${pct}% số vé đã bán`); }
    });
  }

  document.addEventListener('approval:changed', reload);
  if (window.realtime) {
    ids(root.dataset.cinemaIds).forEach((id) => realtime.join('JoinOps', id));
    ids(root.dataset.auditoriumIds).forEach((id) => realtime.join('JoinAuditorium', id));
    realtime.on('SHOWTIME_OPS_CHANGED', reload);
    realtime.on('OCCUPANCY_UPDATED', updateOccupancy);
    realtime.onReload(reload);
  }
  // Suất trễ phụ thuộc giờ hiện tại: tải lại mỗi phút kể cả khi không có sự kiện
  setInterval(reload, 60000);
})();
