#!/usr/bin/env node
// Chụp giao diện để kiểm tra (UI mục 13). App phải đang chạy (dotnet run --project src/FPolyCinema.Web).
// Cài một lần: npm i -D playwright, rồi npx playwright install chromium.
//
//   node tools/ui-snapshots/snap.mjs --phase 2            chụp mọi màn hình của Phase 2
//   node tools/ui-snapshots/snap.mjs --phase 2 --only A3  chỉ chụp màn hình A3
//   node tools/ui-snapshots/snap.mjs --phase 8            chụp lại mọi màn hình của mọi phase (kiểm tra tổng thể)
//   node tools/ui-snapshots/snap.mjs --base http://localhost:5080
//
// Đọc routes.json: mã màn hình, vai trò, đường dẫn, khu vực (kích thước theo UI mục 11).
// Đăng nhập bằng /demo/switch-role (vai trò guest thì không đăng nhập), chờ trang tải xong và font sẵn sàng,
// Đường dẫn có mã sinh ngẫu nhiên hoặc phụ thuộc ngày (mã vé, suất chiếu): khai báo "resolveFrom" (trang bắt đầu) và
// "resolveLink" (CSS selector hoặc mảng selector); script mở resolveFrom, lần lượt theo liên kết đầu tiên khớp từng selector rồi chụp.
// chụp toàn trang vào artifacts/ui/phase-N/{mãMànHình}-{vai-tro}[-{biến thể}]-{rong}x{cao}.png.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..', '..');
const args = process.argv.slice(2);
const opt = (name, def) => { const i = args.indexOf(name); return i >= 0 && args[i + 1] ? args[i + 1] : def; };

const config = JSON.parse(fs.readFileSync(path.join(here, 'routes.json'), 'utf8'));
const phase = Number(opt('--phase', '0'));
const only = opt('--only', null);
const base = opt('--base', config.baseUrl).replace(/\/$/, '');
if (!phase) { console.error('Cần --phase N, ví dụ: node tools/ui-snapshots/snap.mjs --phase 2'); process.exit(2); }

let chromium;
try { ({ chromium } = await import('playwright')); }
catch {
  console.error('Chưa cài Playwright. Chạy một lần: npm i -D playwright, rồi npx playwright install chromium');
  process.exit(2);
}

// Phase 8 (kiểm tra tổng thể, SPEC 8.8-02): chụp lại mọi màn hình của mọi phase, bỏ dòng trùng (cùng mã, vai trò, biến thể, đường dẫn).
const seen = new Set();
const routes = config.routes.filter((r) => (phase === 8 ? r.phase <= 8 : r.phase === phase) && (!only || r.screen === only))
  .filter((r) => { const k = [r.screen, r.role, r.variant, r.path || r.resolveFrom, r.resolveLink].join('|'); if (seen.has(k)) return false; seen.add(k); return true; });
if (!routes.length) { console.error(`routes.json không có màn hình nào của Phase ${phase}${only ? ' mã ' + only : ''}.`); process.exit(2); }

// Kiểm tra app đang chạy
try { const res = await fetch(base + '/'); if (!res.ok && res.status !== 404) throw new Error(String(res.status)); }
catch (e) { console.error(`Không gọi được ${base}. Hãy chạy app trước: dotnet run --project src/FPolyCinema.Web (${e.message})`); process.exit(2); }

const outDir = path.join(root, 'artifacts', 'ui', `phase-${phase}`);
fs.mkdirSync(outDir, { recursive: true });

const browser = await chromium.launch();
const results = [];
for (const r of routes) {
  const area = config.areas[r.area];
  if (!area) { console.error(`Khu vực "${r.area}" của ${r.screen} không có trong routes.json`); continue; }
  for (const size of area.sizes) {
    const [width, height] = size.split('x').map(Number);
    const context = await browser.newContext({ viewport: { width, height }, deviceScaleFactor: 1, locale: 'vi-VN', timezoneId: 'Asia/Ho_Chi_Minh', reducedMotion: 'reduce' });
    const page = await context.newPage();
    const consoleErrors = [];
    // Bỏ qua thông báo "Failed to load resource" của chính trang khi trang cố ý trả 403, 404 (trang lỗi, từ chối truy cập)
    page.on('console', (m) => { if (m.type() === 'error' && !(m.location().url === page.url() && /Failed to load resource/.test(m.text()))) consoleErrors.push(m.text()); });
    page.on('pageerror', (e) => consoleErrors.push(e.message));
    const entry = r.resolveFrom || r.path;
    const url = r.role === 'guest'
      ? base + entry
      : `${base}/demo/switch-role?role=${encodeURIComponent(r.role)}&returnUrl=${encodeURIComponent(entry)}`;
    let status = 0;
    try {
      let resp = await page.goto(url, { waitUntil: 'networkidle', timeout: 30000 });
      if (r.resolveFrom) {
        // resolveLink là một selector hoặc mảng selector: đi lần lượt qua từng liên kết (ví dụ chọn ngày rồi chọn suất).
        for (const sel of [].concat(r.resolveLink)) {
          const href = await page.evaluate((q) => document.querySelector(q)?.getAttribute('href'), sel);
          if (!href) throw new Error(`không tìm thấy liên kết "${sel}" từ ${r.resolveFrom}`);
          resp = await page.goto(new URL(href, page.url()).href, { waitUntil: 'networkidle', timeout: 30000 });
        }
      }
      status = resp ? resp.status() : 0;
      await page.evaluate(() => document.fonts.ready);
      await page.waitForTimeout(300);
      const name = [r.screen, r.role, r.variant, size].filter(Boolean).join('-') + '.png';
      const file = path.join(outDir, name);
      await page.screenshot({ path: file, fullPage: true });
      const hScroll = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1);
      const brokenImgs = await page.evaluate(() => Array.from(document.images).filter((i) => i.getAttribute('src') && i.offsetParent !== null && i.complete && i.naturalWidth === 0).map((i) => i.getAttribute('src')));
      results.push({ file: path.relative(root, file), status, hScroll, brokenImgs, consoleErrors });
      console.log(`${path.relative(root, file)}  HTTP ${status}${hScroll ? '  CẢNH BÁO: có thanh cuộn ngang' : ''}${brokenImgs.length ? '  ẢNH VỠ: ' + brokenImgs.join(', ') : ''}${consoleErrors.length ? '  LỖI JS: ' + consoleErrors.join(' | ') : ''}`);
    } catch (e) {
      console.error(`Lỗi khi chụp ${r.screen} ${r.path} ${size}: ${e.message}`);
      results.push({ screen: r.screen, path: r.path, size, error: e.message });
    }
    await context.close();
  }
}
await browser.close();
fs.writeFileSync(path.join(outDir, 'report.json'), JSON.stringify({ base, phase, at: new Date().toISOString(), results }, null, 2));
const bad = results.filter((x) => x.error || x.hScroll || (x.brokenImgs && x.brokenImgs.length) || (x.consoleErrors && x.consoleErrors.length) || (x.status && x.status >= 500));
console.log(`\nĐã chụp ${results.length} ảnh vào ${path.relative(root, outDir)}. ${bad.length ? bad.length + ' ảnh cần xem lại (xem report.json).' : 'Không phát hiện lỗi tự động; mở từng ảnh để đối chiếu danh sách UI mục 13.'}`);
process.exitCode = results.some((x) => x.error) ? 1 : 0;
