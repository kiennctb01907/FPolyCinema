/* seatmap.js – hành vi của _SeatMap (UI mục 8) dùng chung cho POS và web khách.
   - Mỗi ghế là <button>; phím mũi tên di chuyển giữa các ghế, Enter hoặc Space để chọn (UI U8-13).
   - Ghế đôi: hai nút dính nhau, hover và chọn luôn áp cho cả cặp (U8-07).
   - Tooltip khi rê chuột hoặc focus: loại ghế, điểm góc nhìn và âm thanh, giá (U8-08).
   - Thời gian thực: tham gia nhóm showtime_{id} bằng JoinShowtime và nhận SEAT_MAP_UPDATED; ghế đổi trạng thái chuyển màu mờ dần 200ms;
     ghế vừa định chọn bị người khác lấy thì báo "Ghế G7 vừa có người chọn trước." (U8-12).
   - Vi phạm luật: ghế liên quan rung nhẹ 320ms và nháy viền đỏ 2 lần (U8-11), không xóa lựa chọn.
   Sơ đồ phát sự kiện DOM 'seat-toggle' { seatIds, seats, selecting } để trang (POS, đặt vé) gọi API giữ ghế. */
(function () {
  'use strict';
  const STATUS = {
    AVAILABLE: { key: 'free', text: 'còn trống' },
    SELECTED: { key: 'selected', text: 'đang chọn' },
    HELD: { key: 'held', text: 'người khác đang giữ' },
    BOOKED: { key: 'booked', text: 'đã bán' },
    HOUSE_HOLD: { key: 'house', text: 'ghế nội bộ' },
    MAINTENANCE: { key: 'maint', text: 'đang bảo trì' },
  };
  const LOCKED = new Set(['HELD', 'BOOKED', 'HOUSE_HOLD', 'MAINTENANCE']);
  const money = (n) => Number(n).toLocaleString('vi-VN') + ' ₫';
  let tip = null;

  function tipEl() {
    if (tip) return tip;
    tip = document.createElement('div');
    tip.className = 'seat-tip';
    tip.setAttribute('role', 'tooltip');
    tip.id = 'seat-tip';
    document.body.appendChild(tip);
    return tip;
  }
  function showTip(btn) {
    const t = tipEl();
    t.textContent = btn.dataset.tip || btn.getAttribute('aria-label') || '';
    t.classList.add('is-visible');
    const r = btn.getBoundingClientRect();
    const w = t.offsetWidth, h = t.offsetHeight;
    let left = r.left + r.width / 2 - w / 2;
    left = Math.max(8, Math.min(left, window.innerWidth - w - 8));
    let top = r.top - h - 8;
    if (top < 8) top = r.bottom + 8;
    t.style.setProperty('--tip-x', left + 'px');
    t.style.setProperty('--tip-y', top + 'px');
    btn.setAttribute('aria-describedby', 'seat-tip');
  }
  function hideTip(btn) { tip?.classList.remove('is-visible'); btn?.removeAttribute('aria-describedby'); }

  function describe(btn) {
    const s = STATUS[btn.dataset.status] || STATUS.AVAILABLE;
    const parts = ['Ghế ' + btn.dataset.label, (btn.dataset.typeLabel || '').replace(/^Ghế /, ''), s.text];
    if (btn.dataset.score) parts.push(btn.dataset.score + ' điểm');
    if (btn.dataset.price) parts.push(money(btn.dataset.price));
    btn.setAttribute('aria-label', parts.join(', '));
    const head = (btn.dataset.type === 'STANDARD' ? 'Ghế thường' : btn.dataset.typeLabel) + ' ' + btn.dataset.label;
    const tparts = [head];
    if (btn.dataset.score) tparts.push(`Điểm góc nhìn và âm thanh ${btn.dataset.score}/100` + (btn.dataset.scoreNote ? ` (${btn.dataset.scoreNote})` : ''));
    if (btn.dataset.price) tparts.push(money(btn.dataset.price));
    if (!['AVAILABLE', 'SELECTED'].includes(btn.dataset.status)) tparts.push(s.text);
    btn.dataset.tip = tparts.join(' · ');
  }

  function render(btn, status) {
    const s = STATUS[status] || STATUS.AVAILABLE;
    btn.dataset.status = status;
    btn.className = btn.className.replace(/\bs-\w+\b/, 's-' + s.key);
    btn.setAttribute('aria-pressed', status === 'SELECTED' ? 'true' : 'false');
    if (LOCKED.has(status)) btn.setAttribute('aria-disabled', 'true'); else btn.removeAttribute('aria-disabled');
    let inner;
    if (status === 'HELD') inner = '<i class="bi bi-clock" aria-hidden="true"></i>';
    else if (status === 'HOUSE_HOLD') inner = '<i class="bi bi-lock" aria-hidden="true"></i>';
    else if (status === 'MAINTENANCE') inner = '<i class="bi bi-wrench" aria-hidden="true"></i>';
    else if (btn.dataset.type === 'WHEELCHAIR' && status === 'AVAILABLE') inner = '<i class="bi bi-person-wheelchair" aria-hidden="true"></i>';
    else {
      const sp = document.createElement('span');
      sp.setAttribute('aria-hidden', 'true');
      sp.textContent = status === 'SELECTED' ? '✓' : status === 'BOOKED' ? '✕' : btn.dataset.label.slice(1);
      inner = sp.outerHTML;
    }
    btn.innerHTML = inner;
    describe(btn);
  }

  class SeatMapController {
    constructor(root) {
      this.root = root;
      this.showtimeId = Number(root.dataset.showtimeId) || 0;
      this.billId = Number(root.dataset.billId) || 0;
      this.pending = new Set();
      this.seats = Array.from(root.querySelectorAll('.seat[data-seat-id]'));
      this.byId = new Map(this.seats.map((b) => [Number(b.dataset.seatId), b]));
      this.seats.forEach(describe);
      const first = this.seats.find((b) => b.dataset.status === 'SELECTED') || this.seats.find((b) => !LOCKED.has(b.dataset.status)) || this.seats[0];
      if (first) first.tabIndex = 0;
      root.addEventListener('click', (e) => { const b = e.target.closest('.seat[data-seat-id]'); if (b) this.toggle(b); });
      root.addEventListener('keydown', (e) => this.onKey(e));
      root.addEventListener('mouseover', (e) => { const b = e.target.closest('.seat[data-seat-id]'); if (b) { showTip(b); this.pairHover(b, true); } });
      root.addEventListener('mouseout', (e) => { const b = e.target.closest('.seat[data-seat-id]'); if (b) { hideTip(b); this.pairHover(b, false); } });
      root.addEventListener('focusin', (e) => { const b = e.target.closest('.seat[data-seat-id]'); if (b) { showTip(b); this.pairHover(b, true); } });
      root.addEventListener('focusout', (e) => { const b = e.target.closest('.seat[data-seat-id]'); if (b) { hideTip(b); this.pairHover(b, false); } });
      root.closest('.pos-col, .seatmap-scroll')?.addEventListener('scroll', () => hideTip(), { passive: true });
      if (this.showtimeId && window.realtime) {
        this.onUpdate = (p) => { if (Number(p.showtimeId) === this.showtimeId) this.apply(p.changes || []); };
        realtime.on('SEAT_MAP_UPDATED', this.onUpdate);
        realtime.join('JoinShowtime', this.showtimeId).then((k) => { this.groupKey = k; });
      }
    }

    partnerOf(btn) { return btn.dataset.pair ? this.byId.get(Number(btn.dataset.pair)) : null; }
    pairHover(btn, on) { const p = this.partnerOf(btn); if (p) p.classList.toggle('is-pair-hover', on); btn.classList.toggle('is-pair-hover', on && !!p); }

    /** Ghế đang chọn (thuộc hóa đơn của người đang xem). */
    selectedIds() { return this.seats.filter((b) => b.dataset.status === 'SELECTED').map((b) => Number(b.dataset.seatId)); }

    toggle(btn) {
      if (btn.getAttribute('aria-disabled') === 'true' || btn.classList.contains('is-pending')) return;
      const group = [btn, this.partnerOf(btn)].filter(Boolean);
      const selecting = btn.dataset.status !== 'SELECTED';
      const ids = group.map((b) => Number(b.dataset.seatId));
      if (!this.showtimeId) { group.forEach((b) => render(b, selecting ? 'SELECTED' : 'AVAILABLE')); return; } // sơ đồ mẫu
      const current = new Set(this.selectedIds());
      ids.forEach((id) => (selecting ? current.add(id) : current.delete(id)));
      this.root.dispatchEvent(new CustomEvent('seat-toggle', {
        bubbles: true, detail: { seatIds: Array.from(current), changed: ids, seats: group.map((b) => b.dataset.label), selecting, types: group.map((b) => b.dataset.type) },
      }));
    }

    /** Đánh dấu các ghế đang chờ máy chủ trả lời. */
    setPending(ids, on) { ids.forEach((id) => { const b = this.byId.get(id); if (!b) return; b.classList.toggle('is-pending', on); if (on) this.pending.add(id); else this.pending.delete(id); }); }

    /** Đặt lựa chọn theo dữ liệu máy chủ (ghế trong hóa đơn là SELECTED, ghế của mình bị bỏ là AVAILABLE). */
    setSelection(ids, billId) {
      if (billId) { this.billId = billId; this.root.dataset.billId = billId; }
      const set = new Set(ids);
      this.seats.forEach((b) => {
        const id = Number(b.dataset.seatId);
        if (set.has(id) && b.dataset.status !== 'SELECTED') render(b, 'SELECTED');
        else if (!set.has(id) && b.dataset.status === 'SELECTED') render(b, 'AVAILABLE');
      });
    }

    /** Áp thay đổi từ SEAT_MAP_UPDATED { seatId, status, holdExpiresAt, holdBillId }. */
    apply(changes) {
      const taken = [];
      changes.forEach((c) => {
        const b = this.byId.get(Number(c.seatId));
        if (!b) return;
        let status = c.status;
        const pending = this.pending.has(Number(c.seatId));
        // Ghế đang chờ phản hồi giữ ghế mà chưa biết hóa đơn của mình (hóa đơn vừa được tạo): để phản hồi HTTP quyết định
        if (pending && status === 'HELD' && !this.billId) return;
        if (status === 'HELD' && this.billId && Number(c.holdBillId) === this.billId) status = 'SELECTED';
        if (b.dataset.status === status) return;
        if (pending && LOCKED.has(status)) taken.push(b.dataset.label);
        render(b, status);
        b.classList.add('is-updated');
        setTimeout(() => b.classList.remove('is-updated'), 220);
      });
      if (taken.length && window.ui) ui.toast.warning(`Ghế ${taken.join(', ')} vừa có người chọn trước.`);
      this.root.dispatchEvent(new CustomEvent('seatmap-change', { bubbles: true }));
    }

    /** Vi phạm luật: rung nhẹ và nháy viền đỏ 2 lần các ghế liên quan (không đổi lựa chọn). */
    violate(ids) {
      ids.forEach((id) => {
        const b = this.byId.get(Number(id));
        if (!b) return;
        b.classList.remove('is-violation');
        void b.offsetWidth;
        b.classList.add('is-violation');
        if (window.ui) ui.shake(b);
        setTimeout(() => b.classList.remove('is-violation'), 700);
      });
    }

    onKey(e) {
      const b = e.target.closest('.seat[data-seat-id]');
      if (!b) return;
      if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); this.toggle(b); return; }
      const dir = { ArrowLeft: [0, -1], ArrowRight: [0, 1], ArrowUp: [-1, 0], ArrowDown: [1, 0] }[e.key];
      if (!dir) return;
      e.preventDefault();
      const r = Number(b.dataset.row), c = Number(b.dataset.col);
      let best = null, bestD = Infinity;
      this.seats.forEach((o) => {
        if (o === b) return;
        const dr = Number(o.dataset.row) - r, dc = Number(o.dataset.col) - c;
        if (dir[0] !== 0 && Math.sign(dr) !== dir[0]) return;
        if (dir[1] !== 0 && (dr !== 0 || Math.sign(dc) !== dir[1])) return;
        const d = dir[0] !== 0 ? Math.abs(dr) * 100 + Math.abs(dc) : Math.abs(dc);
        if (d < bestD) { bestD = d; best = o; }
      });
      if (!best) return;
      b.tabIndex = -1;
      best.tabIndex = 0;
      best.focus();
    }

    destroy() {
      hideTip();
      if (this.onUpdate && window.realtime) realtime.off('SEAT_MAP_UPDATED', this.onUpdate);
      if (this.groupKey && window.realtime) realtime.leave(this.groupKey);
    }
  }

  window.SeatMap = {
    /** Gắn hành vi cho mọi sơ đồ [data-seatmap] trong vùng; trả controller của sơ đồ đầu tiên. */
    attach(root = document) {
      let first = null;
      root.querySelectorAll('[data-seatmap]').forEach((el) => {
        if (!el.seatMap) el.seatMap = new SeatMapController(el);
        first = first || el.seatMap;
      });
      return first;
    },
  };
  document.addEventListener('DOMContentLoaded', () => window.SeatMap.attach(document));
})();
