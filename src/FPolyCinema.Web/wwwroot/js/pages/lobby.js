/* lobby.js – màn hình sảnh (không cần đăng nhập).
   - Bảng gọi số P5: tham gia fnb_kds_{cinemaId} ẩn danh (JoinKds isBoard = true). KDS_ORDER_QUEUED thêm số vào "Đang chuẩn bị";
     KDS_ORDER_UPDATED: READY_FOR_PICKUP chuyển số sang "Mời nhận đồ" (nhấp nháy nhẹ 3 lần, có âm báo), FULFILLED hoặc VOIDED gỡ số.
   - Bảng sảnh P6: tham gia cinema_ops_{cinemaId} (JoinOps); SHOWTIME_OPS_CHANGED hoặc mỗi phút tải lại các dòng; bảng tự cuộn khi nhiều dòng. */
(function () {
  'use strict';

  // ---------------------------------------------------------------- bảng gọi số
  const board = document.querySelector('[data-pickup-board]');
  if (board) {
    const cinemaId = Number(board.dataset.cinemaId);
    const list = (s) => board.querySelector(`[data-board="${s}"]`);
    let audio = null;
    const ding = () => {
      try {
        audio = audio || new (window.AudioContext || window.webkitAudioContext)();
        [[988, 0], [1319, 0.18]].forEach(([f, at]) => {
          const o = audio.createOscillator(), g = audio.createGain();
          o.frequency.value = f; g.gain.setValueAtTime(0.0001, audio.currentTime + at);
          g.gain.exponentialRampToValueAtTime(0.2, audio.currentTime + at + 0.03);
          g.gain.exponentialRampToValueAtTime(0.0001, audio.currentTime + at + 0.5);
          o.connect(g).connect(audio.destination); o.start(audio.currentTime + at); o.stop(audio.currentTime + at + 0.55);
        });
      } catch { /* màn hình không phát được âm thanh */ }
    };
    const place = (o) => {
      board.querySelectorAll(`[data-bill-id="${o.billId}"]`).forEach((x) => x.remove());
      if (o.fnbStatus !== 'PREPARING' && o.fnbStatus !== 'READY_FOR_PICKUP') return;
      const li = document.createElement('li');
      li.dataset.billId = o.billId;
      li.className = 'num';
      li.textContent = o.pickupNumber || '';
      if (o.fnbStatus === 'READY_FOR_PICKUP') { li.classList.add('is-new'); list('READY_FOR_PICKUP').prepend(li); ding(); }
      else list('PREPARING').appendChild(li);
    };
    const reload = async () => {
      try {
        const res = await fetch(`/lobby/pickup-board/data?cinemaId=${cinemaId}`, { headers: { Accept: 'application/json' } });
        const json = await res.json();
        list('PREPARING').innerHTML = ''; list('READY_FOR_PICKUP').innerHTML = '';
        json.data.forEach((o) => {
          const li = document.createElement('li');
          li.dataset.billId = o.billId; li.className = 'num'; li.textContent = o.pickupNumber || '';
          list(o.fnbStatus)?.appendChild(li);
        });
      } catch { /* thử lại khi kết nối lại */ }
    };
    if (window.realtime) {
      realtime.join('JoinKds', cinemaId, true);
      realtime.on('KDS_ORDER_QUEUED', place);
      realtime.on('KDS_ORDER_UPDATED', place);
      realtime.onReload(reload);
    }
  }

  // ---------------------------------------------------------------- bảng sảnh
  const sign = document.querySelector('[data-signage]');
  if (sign) {
    const cinemaId = Number(sign.dataset.cinemaId);
    const rows = sign.querySelector('[data-signage-rows]');
    const viewport = sign.querySelector('[data-signage-viewport]');
    const reload = async () => {
      try {
        const res = await fetch(`/lobby/digital-signage/rows?cinemaId=${cinemaId}`, { headers: { Accept: 'text/html' } });
        if (res.ok) { rows.innerHTML = await res.text(); scroll(); }
      } catch { /* giữ dữ liệu cũ */ }
    };
    // Tự cuộn khi nhiều dòng: cuộn chậm xuống cuối, dừng 4 giây rồi về đầu
    let pos = 0, pause = 0;
    function scroll() { pos = 0; viewport.scrollTop = 0; pause = 4; }
    setInterval(() => {
      const max = viewport.scrollHeight - viewport.clientHeight;
      if (max <= 0) return;
      if (pause > 0) { pause -= 0.05; return; }
      pos += 1;
      if (pos >= max) { viewport.scrollTop = max; pause = 4; pos = -1; return; }
      if (pos === 0) { viewport.scrollTop = 0; return; }
      viewport.scrollTop = pos;
    }, 50);
    if (window.realtime) {
      realtime.join('JoinOps', cinemaId);
      realtime.on('SHOWTIME_OPS_CHANGED', reload);
      realtime.onReload(reload);
    }
    setInterval(reload, 60000);
    scroll();
  }
})();
