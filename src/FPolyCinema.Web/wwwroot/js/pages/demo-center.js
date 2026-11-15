/* demo-center.js – Trung tâm demo nhiều màn hình /demo/multi-screen (UI D4, SPEC 5).
   - "Mở các màn hình": mở mọi cửa sổ của kịch bản bằng window.open, đặt kích thước và vị trí để chia đôi màn hình
     (vị trí do server tính – DemoScreenRules.Slots: cửa sổ đầu nửa trái, các cửa sổ còn lại chia nửa phải theo chiều dọc).
     Mỗi cửa sổ mở trên một tên miền phụ của localhost và tự đăng nhập bằng /demo/switch-role (phiên riêng từng cửa sổ).
   - Trình duyệt chặn cửa sổ bật lên thì hiện hướng dẫn cho phép, kèm số cửa sổ bị chặn; "Mở riêng" mở từng cửa sổ một (mỗi lần bấm một cửa sổ). */
(function () {
  'use strict';

  const blockedBox = document.querySelector('[data-popup-blocked]');

  // Vùng màn hình dùng được (trừ thanh tác vụ); availLeft/availTop có trên Chrome và Edge khi dùng nhiều màn hình.
  function area() {
    const s = window.screen;
    return { left: s.availLeft || 0, top: s.availTop || 0, width: s.availWidth || 1280, height: s.availHeight || 800 };
  }

  // Chuỗi tính năng của window.open theo tỷ lệ x, y, w, h (0–1) của vùng màn hình. width/height là kích thước vùng nội dung
  // nên trừ phần khung cửa sổ (thanh tiêu đề, thanh địa chỉ của cửa sổ bật lên).
  function features(el) {
    const a = area();
    const x = parseFloat(el.dataset.x || '0'), y = parseFloat(el.dataset.y || '0');
    const w = parseFloat(el.dataset.w || '1'), h = parseFloat(el.dataset.h || '1');
    const frameW = Math.min(Math.max(0, window.outerWidth - window.innerWidth), 16);
    const frameH = Math.min(Math.max(0, window.outerHeight - window.innerHeight), 80);
    const left = Math.round(a.left + x * a.width), top = Math.round(a.top + y * a.height);
    const width = Math.max(320, Math.round(w * a.width) - frameW), height = Math.max(240, Math.round(h * a.height) - frameH);
    return `popup=yes,left=${left},top=${top},width=${width},height=${height}`;
  }

  function open(link) {
    const win = window.open(link.href, link.target, features(link));
    if (win) { try { win.focus(); } catch { /* cửa sổ khác tên miền */ } }
    return !!win;
  }

  function showBlocked(n) {
    if (!blockedBox) return;
    blockedBox.classList.toggle('d-none', n === 0);
    const c = blockedBox.querySelector('[data-blocked-count]');
    if (c) c.textContent = String(n);
    if (n > 0) blockedBox.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }

  document.addEventListener('click', (e) => {
    const single = e.target.closest('a[data-demo-window]');
    if (single) {
      e.preventDefault();
      showBlocked(open(single) ? 0 : 1);
      return;
    }
    const btn = e.target.closest('[data-open-scenario]');
    if (!btn) return;
    const card = btn.closest('.demo-card');
    const links = Array.from(card.querySelectorAll('a[data-demo-window]'));
    let blocked = 0;
    links.forEach((l) => { if (!open(l)) blocked++; });
    showBlocked(blocked);
    if (blocked === 0) ui.toast.success(`Đã mở ${links.length} cửa sổ của kịch bản ${btn.dataset.openScenario}.`);
  });
})();
