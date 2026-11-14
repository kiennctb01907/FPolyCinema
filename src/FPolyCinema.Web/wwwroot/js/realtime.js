/* realtime.js – module thời gian thực dùng chung (SPEC 5, CONTRACTS mục 6).
   Kết nối CinemaHub tại /hubs/cinema bằng @microsoft/signalr 8.0.x với withAutomaticReconnect. Kết nối lại xong thì tham gia lại mọi nhóm
   đã tham gia và gọi các hàm tải lại trạng thái bằng HTTP (onReload). Chỉ báo kết nối cập nhật qua ui.connection.set.
   Cách dùng: realtime.join('JoinShowtime', 12); realtime.on('SEAT_MAP_UPDATED', (p) => …); realtime.onReload(() => …). */
(function () {
  'use strict';
  // Không có thư viện SignalR, hoặc trang đã nạp module này (layout quản trị nạp sẵn cho quản lý ca) thì không tạo kết nối thứ hai
  if (!window.signalR || window.realtime) return;

  const groups = new Map(); // khóa → { join, leave, args }
  const reloaders = new Set();
  const conn = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/cinema')
    .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 15000, 30000])
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  const setState = (s) => { if (window.ui) ui.connection.set(s); };
  const connected = () => conn.state === signalR.HubConnectionState.Connected;

  async function joinAll() {
    for (const g of groups.values()) {
      try { await conn.invoke(g.join, ...g.args); } catch (e) { console.warn('Không tham gia được nhóm', g.join, e); }
    }
  }
  function reloadAll() { reloaders.forEach((f) => { try { f(); } catch (e) { console.warn(e); } }); }

  let starting = null;
  async function start() {
    if (starting) return starting;
    starting = (async () => {
      setState('reconnecting');
      for (let attempt = 0; ; attempt++) {
        try {
          await conn.start();
          setState('connected');
          await joinAll();
          if (attempt > 0) reloadAll();
          return;
        } catch {
          setState(attempt >= 2 ? 'lost' : 'reconnecting');
          await new Promise((r) => setTimeout(r, Math.min(15000, 2000 * (attempt + 1))));
        }
      }
    })().finally(() => { starting = null; });
    return starting;
  }

  conn.onreconnecting(() => setState('reconnecting'));
  conn.onreconnected(async () => { setState('connected'); await joinAll(); reloadAll(); });
  // Hết lượt tự kết nối lại: báo mất kết nối rồi tiếp tục thử định kỳ, kết nối được thì tải lại trạng thái
  conn.onclose(async () => { setState('lost'); await start(); reloadAll(); });

  const api = {
    connection: conn,
    get isConnected() { return connected(); },
    start,
    /** Tham gia nhóm bằng hàm Join… của hub; được ghi nhớ để tham gia lại sau khi kết nối lại. */
    async join(method, ...args) {
      const key = method + ':' + args.join(',');
      groups.set(key, { join: method, leave: method.replace(/^Join/, 'Leave'), args });
      if (connected()) { try { await conn.invoke(method, ...args); } catch (e) { console.warn(e); } } else start();
      return key;
    },
    /** Rời nhóm đã tham gia. */
    async leave(key) {
      const g = groups.get(key);
      if (!g) return;
      groups.delete(key);
      if (connected()) { try { await conn.invoke(g.leave, ...g.args); } catch { /* bỏ qua */ } }
    },
    /** Đăng ký nhận sự kiện (tên theo CONTRACTS mục 6). */
    on(event, handler) { conn.on(event, handler); },
    off(event, handler) { conn.off(event, handler); },
    /** Hàm tải lại trạng thái bằng HTTP sau khi kết nối lại. */
    onReload(fn) { reloaders.add(fn); return () => reloaders.delete(fn); },
  };
  window.realtime = api;
  document.addEventListener('DOMContentLoaded', () => start());
})();
