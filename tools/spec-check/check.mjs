#!/usr/bin/env node
/*
 * tools/spec-check/check.mjs – FPolyCinema
 * Kiểm tra dự án theo spec để không bỏ sót yêu cầu. Chỉ cần Node 18+, không cần thư viện ngoài.
 *
 *   node tools/spec-check/check.mjs begin N        bắt đầu Phase N: in danh sách yêu cầu, tạo file truy vết
 *   node tools/spec-check/check.mjs next N         in nguyên văn nhóm yêu cầu kế tiếp còn thiếu
 *   node tools/spec-check/check.mjs list N [--full] [--pending] [--group K]
 *   node tools/spec-check/check.mjs verify N [--brief] [--final]
 *   node tools/spec-check/check.mjs mark ID[,ID2] --ev "bằng chứng" [--note "…"] [--status Xong|Bỏ|Chưa]   ghi dòng truy vết
 *   node tools/spec-check/check.mjs status         tiến độ mọi phase
 *   node tools/spec-check/check.mjs sync           đồng bộ docs/trace với spec
 *   node tools/spec-check/check.mjs test [--reset-db]   dotnet build, dotnet test (và tạo lại DB), ghi dấu kết quả
 *   node tools/spec-check/check.mjs facts          sinh docs/generated/* từ code (nguồn cho tài liệu lab)
 *   node tools/spec-check/check.mjs selftest       kiểm tra tính nhất quán của chính bộ spec
 *   node tools/spec-check/check.mjs untagged       liệt kê gạch đầu dòng chưa gắn mã
 *   node tools/spec-check/check.mjs hook-session | hook-stop   dùng trong .claude/settings.json
 */
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const STATE_FILE = path.join(ROOT, '.claude', 'state', 'phase.json');
const STAMP_FILE = path.join(ROOT, '.claude', 'state', 'tests.json');
const SCOPE_FILE = path.join(ROOT, 'tools', 'spec-check', 'scope.json');
const WEB = 'src/FPolyCinema.Web';

// ---------------------------------------------------------------- tiện ích
const readText = f => { try { return fs.readFileSync(f, 'utf8').replace(/^\uFEFF/, ''); } catch { return null; } };
const exists = f => { try { fs.accessSync(f); return true; } catch { return false; } };
const rel = f => path.relative(ROOT, f).split(path.sep).join('/');
const abs = p => path.join(ROOT, ...String(p).replace(/\\/g, '/').replace(/^\.\//, '').split('/').filter(Boolean));
const writeText = (f, s) => { fs.mkdirSync(path.dirname(f), { recursive: true }); fs.writeFileSync(f, s, 'utf8'); };
const readJson = (f, d) => { try { return JSON.parse(readText(f)); } catch { return d; } };
const clip = (s, n = 100) => { const t = String(s ?? '').replace(/\*\*/g, '').replace(/`/g, '').replace(/\s+/g, ' ').trim(); return t.length > n ? t.slice(0, n - 1) + '…' : t; };
const uniq = a => [...new Set(a)];
const esc = s => String(s ?? '').replace(/\|/g, '/').replace(/\r?\n/g, ' ').trim();
const SKIP_DIRS = new Set(['bin', 'obj', 'node_modules', '.git', '.vs', '.idea', 'artifacts', 'TestResults', '.claude']);

function walk(dir, exts, out = []) {
  let ents;
  try { ents = fs.readdirSync(dir, { withFileTypes: true }); } catch { return out; }
  for (const e of ents) {
    const f = path.join(dir, e.name);
    if (e.isDirectory()) {
      if (SKIP_DIRS.has(e.name)) continue;
      if (e.name === 'lib' && path.basename(dir) === 'wwwroot') continue;
      walk(f, exts, out);
    } else if (!exts || exts.some(x => e.name.toLowerCase().endsWith(x))) out.push(f);
  }
  return out;
}

function parseArgs(argv) {
  const pos = [], flags = {};
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a.startsWith('--')) {
      const k = a.slice(2);
      if (i + 1 < argv.length && !argv[i + 1].startsWith('--') && ['group', 'ev', 'note', 'status'].includes(k)) flags[k] = argv[++i];
      else flags[k] = true;
    } else pos.push(a);
  }
  return { pos, flags };
}

// ---------------------------------------------------------------- đọc spec
const ID_PAT = '[SUC]\\d+(?:\\.[0-9A-Za-z]+)*-\\d{2}';
const TAG_RE = new RegExp(`^\\[(${ID_PAT}) P([1-8])(?: auto:([a-z-]+))?\\]\\s*(.*)$`);
const ROW_ID_RE = new RegExp(`^(${ID_PAT}|UC\\d{2})$`);
const BULLET_RE = /^(\s*)([-*]|\d+\.)\s+(.*)$/;
const HEADING_RE = /^(#{1,6})\s+(.*?)\s*$/;
const EXEMPT_RE = /<!--\s*spec-check:\s*không gắn mã\s*-->/i;

function docFiles() {
  const out = [];
  for (const d of ['docs/spec', 'docs/ui']) for (const f of walk(abs(d), ['.md'])) if (path.basename(f).toLowerCase() !== 'readme.md') out.push(f);
  out.sort();
  const c = abs('docs/CONTRACTS.md');
  if (exists(c)) out.push(c);
  return out;
}

function parseDoc(file) {
  const text = readText(file) ?? '';
  const L = text.split(/\r?\n/);
  const reqs = [], cand = [], tables = [], headings = [];
  let inCode = false, fence = null;
  const hstack = [];
  let bstack = [], last = null, table = null;
  const codeBlocks = [];
  for (let i = 0; i < L.length; i++) {
    const line = L[i];
    if (/^\s*```/.test(line)) {
      if (!inCode) { inCode = true; fence = { line: i + 1, lang: line.trim().slice(3).trim(), body: [] }; }
      else { inCode = false; codeBlocks.push(fence); fence = null; }
      last = null; continue;
    }
    if (inCode) { fence.body.push(line); continue; }
    const h = line.match(HEADING_RE);
    if (h) {
      const level = h[1].length, title = h[2];
      while (hstack.length && hstack[hstack.length - 1].level >= level) hstack.pop();
      const m = title.match(/^(\d+(?:\.\d+)*)\.?\s/);
      const parentNum = hstack.length ? hstack[hstack.length - 1].num : '';
      hstack.push({ level, title, num: m ? m[1] : parentNum, exempt: false });
      headings.push({ line: i + 1, level, title, num: m ? m[1] : parentNum });
      bstack = []; last = null; table = null; continue;
    }
    const cur = hstack[hstack.length - 1] || { num: '', title: '' };
    if (EXEMPT_RE.test(line)) { if (hstack.length) hstack[hstack.length - 1].exempt = true; continue; }
    const exempt = hstack.some(x => x.exempt);
    if (/^\s*\|/.test(line)) {
      const cells = line.trim().replace(/^\|/, '').replace(/\|$/, '').split('|').map(s => s.trim());
      if (!table) { table = { line: i + 1, header: cells, rows: [], section: cur.num }; tables.push(table); }
      else if (!cells.every(c => /^:?-{2,}:?$/.test(c))) table.rows.push({ line: i + 1, cells });
      last = null; continue;
    } else if (line.trim() !== '') table = null;
    const b = line.match(BULLET_RE);
    if (b) {
      const indent = b[1].replace(/\t/g, '    ').length;
      let content = b[3];
      while (bstack.length && bstack[bstack.length - 1].indent >= indent) bstack.pop();
      const parents = bstack.map(x => x.text);
      const t = content.match(TAG_RE);
      if (t) {
        const r = { id: t[1], phase: +t[2], auto: t[3] || null, text: t[4], file, line: i + 1, section: cur.num, sectionTitle: cur.title, parents, indent };
        reqs.push(r); last = r; content = t[4];
      } else {
        last = null;
        cand.push({ file, line: i + 1, idx: i, indent, text: content, section: cur.num, exempt });
      }
      bstack.push({ indent, text: clip(content, 70) });
      continue;
    }
    if (line.trim() === '') continue;
    if (last && /^\s{2,}\S/.test(line)) { last.text += ' ' + line.trim(); continue; }
    last = null;
  }
  // gạch đầu dòng chỉ làm nhãn (kết thúc bằng ":" và có dòng con) thì không cần mã
  const untagged = [];
  for (const c of cand) {
    if (c.exempt) continue;
    if (/:\s*$/.test(c.text)) {
      let j = c.idx + 1;
      while (j < L.length && L[j].trim() === '') j++;
      const nb = j < L.length ? L[j].match(BULLET_RE) : null;
      if (nb && nb[1].replace(/\t/g, '    ').length > c.indent) continue;
    }
    untagged.push(c);
  }
  return { file, text, lines: L, reqs, untagged, tables, headings, codeBlocks };
}

let _docs = null;
function docs() {
  if (_docs) return _docs;
  const files = docFiles().map(parseDoc);
  const reqs = files.flatMap(f => f.reqs);
  const byId = new Map(), dup = [];
  for (const r of reqs) { if (byId.has(r.id)) dup.push(r); else byId.set(r.id, r); }
  const tables = files.flatMap(f => f.tables.map(t => ({ ...t, file: f.file })));
  const ucs = [];
  for (const t of tables) {
    if (!/^Mã$/i.test(t.header[0] || '') || !t.header.some(h => /^use case$/i.test(h))) continue;
    const ix = re => t.header.findIndex(h => re.test(h));
    const iName = ix(/^use case$/i), iAct = ix(/tác nhân/i), iRel = ix(/quan hệ/i), iSpec = ix(/^spec$/i), iScr = ix(/màn hình/i), iPh = ix(/^phase$/i);
    for (const row of t.rows) {
      const c = row.cells;
      if (!/^UC\d{2}$/.test(c[0])) continue;
      const phases = (c[iPh] || '').split(/[,\s]+/).map(Number).filter(n => n >= 1 && n <= 8);
      ucs.push({
        id: c[0], name: c[iName], actors: c[iAct], rel: c[iRel], spec: c[iSpec],
        screens: (c[iScr] || '').split(/[,\s]+/).filter(Boolean), phases, phase: phases.length ? Math.max(...phases) : 8,
        file: t.file, line: row.line, text: `${c[0]} ${c[iName]} (màn hình ${c[iScr]}, phase ${c[iPh]})`, auto: null, isUC: true,
        section: '10', sectionTitle: 'Danh mục use case', parents: [],
      });
    }
  }
  const phaseNames = {}, labTags = {};
  for (const t of tables) {
    if (!/^phase$/i.test(t.header[0] || '') || !t.header.some(h => /nội dung/i.test(h))) continue;
    for (const row of t.rows) { const n = +row.cells[0]; if (n) phaseNames[n] = row.cells[1]; }
  }
  const all = files.map(f => f.text).join('\n');
  for (const m of all.matchAll(/(lab-\d)(?:\s+và\s+(lab-\d))?\s+sau\s+Phase\s+(\d)/gi)) {
    const p = +m[3];
    labTags[p] = uniq([...(labTags[p] || []), m[1], ...(m[2] ? [m[2]] : [])]);
  }
  const specTables = new Map();
  for (const r of reqs.filter(r => r.auto === 'table')) { const st = parseSpecTable(r.text); if (st) specTables.set(st.name.toLowerCase(), st); }
  const screens = new Map();
  for (const r of reqs) { const m = r.id.match(/^U7\.([A-Z]+\d*)-/); if (m) { const s = screens.get(m[1]) || { id: m[1], phases: new Set(), routes: [] }; s.phases.add(r.phase); screens.set(m[1], s); } }
  _docs = { files, reqs, byId, dup, tables, ucs, phaseNames, labTags, specTables, screens };
  return _docs;
}

function parseSpecTable(text) {
  const m = text.match(/^([A-Z][A-Za-z]+)\s*\(/);
  if (!m) return null;
  const st = text.indexOf('(');
  const en = matchParen(text, st);
  if (en < 0) return null;
  const items = splitTop(text.slice(st + 1, en));
  const cols = [], uniqueCols = [];
  for (const it of items) {
    const n = it.match(/^([A-Za-z]\w*)/);
    if (!n) continue;
    cols.push(n[1]);
    if (/^\w+\s+UNIQUE\b/.test(it)) uniqueCols.push(n[1]);
  }
  const tail = text.slice(en + 1);
  const uniques = [...tail.matchAll(/UNIQUE\s*\(([^)]*)\)/g)].map(x => x[1].split(',').map(s => s.trim()));
  const pk = tail.match(/khóa chính\s*\(([^)]*)\)/i)?.[1].split(',').map(s => s.trim()) || null;
  const indexes = [...tail.matchAll(/index\s*\(([^)]*)\)/gi)].map(x => x[1].split(',').map(s => s.trim()));
  return { name: m[1], cols, uniqueCols, uniques, pk, indexes };
}

function scope() { const s = readJson(SCOPE_FILE, {}) || {}; return { skipPhases: s.skipPhases || [], skipIds: s.skipIds || [] }; }
function inScope(r, sc = scope()) {
  if (sc.skipPhases.includes(r.phase)) return false;
  return !sc.skipIds.some(p => (p.endsWith('*') ? r.id.startsWith(p.slice(0, -1)) : r.id === p));
}
function phaseItems(n) {
  const d = docs(), sc = scope();
  return [...d.reqs.filter(r => r.phase === n), ...d.ucs.filter(u => u.phase === n)].map(r => ({ ...r, skipped: !inScope(r, sc) }));
}
const screenOf = id => id.match(/^U7\.([A-Z]+\d*)-/)?.[1] || null;
function screenTitle(scr) {
  const r = docs().byId.get(`U7.${scr}-01`);
  if (scr === 'ART') return 'Ảnh mẫu (SvgArtGenerator)';
  return r ? clip(r.text.replace(/^[A-Z]+\d*\.\s*/, ''), 60) : scr;
}
function groupsOf(items) {
  const gs = [];
  let cur = null;
  for (const it of items.filter(i => !i.isUC)) {
    const scr = screenOf(it.id);
    const key = it.file + '#' + (scr ? 'screen-' + scr : it.section);
    if (!cur || cur.key !== key) { cur = { key, file: it.file, section: scr ? `7, màn hình ${scr}` : it.section, title: scr ? screenTitle(scr) : it.sectionTitle, items: [] }; gs.push(cur); }
    cur.items.push(it);
  }
  const uc = items.filter(i => i.isUC);
  if (uc.length) gs.push({ key: 'UC', file: uc[0].file, section: '10', title: 'Use case phải chạy được khi xong phase', items: uc });
  return gs.map((g, i) => ({ ...g, no: i + 1 }));
}

// ---------------------------------------------------------------- truy vết
const traceFile = n => path.join(ROOT, 'docs', 'trace', `phase-${n}.md`);
function readTrace(n) {
  const t = readText(traceFile(n));
  const m = new Map();
  if (!t) return m;
  let removed = false;
  t.split(/\r?\n/).forEach((line, i) => {
    if (/^##\s+Dòng không còn trong spec/i.test(line)) removed = true;
    if (!/^\s*\|/.test(line)) return;
    const c = line.trim().replace(/^\|/, '').replace(/\|$/, '').split('|').map(s => s.trim());
    if (!ROW_ID_RE.test(c[0])) return;
    m.set(c[0], { id: c[0], req: c[1] || '', status: c[2] || 'Chưa', evidence: c[3] || '', note: c[4] || '', line: i + 1, removed, phase: n });
  });
  return m;
}
function allTrace() {
  const m = new Map();
  for (let n = 1; n <= 8; n++) for (const [k, v] of readTrace(n)) if (!m.has(k) || (m.get(k).removed && !v.removed)) m.set(k, v);
  return m;
}
const normStatus = s => { const t = String(s || '').trim().toLowerCase(); return t === 'xong' ? 'Xong' : t === 'bỏ' || t === 'bo' ? 'Bỏ' : 'Chưa'; };
function traceHeader(n, name) {
  return `# Truy vết yêu cầu – Phase ${n}${name ? ' – ' + name : ''}

File do \`node tools/spec-check/check.mjs sync\` tạo và giữ đồng bộ với spec. Chỉ sửa 3 cột Trạng thái, Bằng chứng, Ghi chú, nên dùng lệnh \`node tools/spec-check/check.mjs mark MÃ --ev \"…\"\` thay vì sửa tay. Không thêm, xóa hay sửa cột Mã và Yêu cầu.

- Trạng thái: Chưa, Xong, Bỏ. Chỉ dùng Bỏ khi yêu cầu đã được đổi và PROGRESS.md mục "Thay đổi mô hình" có nhắc mã này, hoặc mã nằm trong tools/spec-check/scope.json. Ghi lý do ở cột Ghi chú.
- Bằng chứng: nhiều mục cách nhau bằng dấu chấm phẩy.
  - Đường dẫn file tính từ gốc repo, có thể thêm #chuỗi phải có trong file. Ví dụ: src/FPolyCinema.Web/Services/SeatHoldService.cs#Hold
  - test:TênHàmTest hoặc test:TênLớp.TênHàm
  - sql:TênBảng hoặc sql:TênStoredProcedure
  - route:/duong-dan
  - ui:MãMànHình (đã có ảnh chụp trong artifacts/ui)
- Dòng Xong phải có ít nhất một bằng chứng là file code, test: hoặc sql:. Yêu cầu có chữ "logic thuần" hoặc "unit test" phải có test:.
`;
}
function syncTraces() {
  const d = docs();
  const old = allTrace();
  const current = new Set([...d.reqs.filter(r => !r.auto).map(r => r.id), ...d.ucs.map(u => u.id)]);
  const autoIds = new Set(d.reqs.filter(r => r.auto).map(r => r.id));
  const changed = [];
  for (let n = 1; n <= 8; n++) {
    const items = [...d.reqs.filter(r => r.phase === n && !r.auto), ...d.ucs.filter(u => u.phase === n)];
    const rows = items.map(r => {
      const o = old.get(r.id);
      return `| ${r.id} | ${esc(clip(r.isUC ? `${r.name} (màn hình ${r.screens.join(', ')})` : r.text, 120))} | ${o ? normStatus(o.status) : 'Chưa'} | ${o ? esc(o.evidence) : ''} | ${o ? esc(o.note) : ''} |`;
    });
    const gone = [...readTrace(n).values()].filter(v => !current.has(v.id) && !autoIds.has(v.id))
      .map(v => `| ${v.id} | ${esc(v.req)} | ${esc(v.status)} | ${esc(v.evidence)} | ${esc(v.note)} |`);
    let s = traceHeader(n, d.phaseNames[n]) + '\n| Mã | Yêu cầu | Trạng thái | Bằng chứng | Ghi chú |\n|---|---|---|---|---|\n' + rows.join('\n') + '\n';
    if (gone.length) s += '\n## Dòng không còn trong spec\n\n| Mã | Yêu cầu | Trạng thái | Bằng chứng | Ghi chú |\n|---|---|---|---|---|\n' + gone.join('\n') + '\n';
    const f = traceFile(n);
    if (readText(f) !== s) { writeText(f, s); changed.push(rel(f)); }
  }
  return changed;
}
function progressText() { return readText(abs('PROGRESS.md')) || ''; }
function progressMentions(id) {
  const t = progressText();
  const sec = t.split(/^##\s+/m).find(x => /^Thay đổi mô hình/i.test(x)) || '';
  return sec.includes(id);
}

// ---------------------------------------------------------------- chỉ mục code
let _code = null;
function code() {
  if (_code) return _code;
  const files = walk(abs('src'), null);
  const tests = walk(abs('tests'), ['.cs']);
  const cache = new Map();
  const get = f => { if (!cache.has(f)) cache.set(f, readText(f) ?? ''); return cache.get(f); };
  const by = (list, ext) => list.filter(f => f.toLowerCase().endsWith(ext));
  _code = {
    files, tests, get,
    cs: by(files, '.cs'), cshtml: by(files, '.cshtml'), js: by(files, '.js'), css: by(files, '.css'), sql: by(files, '.sql'),
    csproj: [...by(files, '.csproj'), ...walk(abs('tests'), ['.csproj'])],
    json: by(files, '.json'),
    has: () => files.length > 0,
  };
  return _code;
}
const anyContains = (list, needle) => code().files && list.some(f => code().get(f).includes(needle));

// ---------------------------------------------------------------- SQL
function stripSqlComments(s) {
  let out = '', i = 0, inStr = false;
  const n = s.length;
  while (i < n) {
    const c = s[i], d = s[i + 1];
    if (inStr) { out += c; if (c === "'") { if (d === "'") { out += d; i += 2; continue; } inStr = false; } i++; continue; }
    if (c === "'") { inStr = true; out += c; i++; continue; }
    if (c === '-' && d === '-') { while (i < n && s[i] !== '\n') i++; continue; }
    if (c === '/' && d === '*') { i += 2; while (i < n && !(s[i] === '*' && s[i + 1] === '/')) i++; i += 2; continue; }
    out += c; i++;
  }
  return out;
}
function matchParen(s, start) {
  let depth = 0, inStr = false;
  for (let i = start; i < s.length; i++) {
    const c = s[i];
    if (inStr) { if (c === "'") { if (s[i + 1] === "'") { i++; continue; } inStr = false; } continue; }
    if (c === "'") { inStr = true; continue; }
    if (c === '(') depth++;
    else if (c === ')') { depth--; if (depth === 0) return i; }
  }
  return -1;
}
function splitTop(s, sep = ',') {
  const parts = [];
  let depth = 0, inStr = false, cur = '';
  for (let i = 0; i < s.length; i++) {
    const c = s[i];
    if (inStr) { cur += c; if (c === "'") { if (s[i + 1] === "'") { cur += s[++i]; continue; } inStr = false; } continue; }
    if (c === "'") { inStr = true; cur += c; continue; }
    if (c === '(') depth++; else if (c === ')') depth--;
    if (c === sep && depth === 0) { parts.push(cur.trim()); cur = ''; } else cur += c;
  }
  if (cur.trim()) parts.push(cur.trim());
  return parts;
}
const unbracket = s => String(s).trim().replace(/[\[\]"]/g, '').replace(/^dbo\./i, '');
const quotedValues = expr => [...String(expr).matchAll(/N?'((?:[^']|'')*)'/g)].map(m => m[1].replace(/''/g, "'"));
function findCheck(s) {
  const m = /\bCHECK\s*\(/i.exec(s);
  if (!m) return null;
  const st = m.index + m[0].length - 1, en = matchParen(s, st);
  return en > 0 ? s.slice(st + 1, en) : null;
}
const colList = s => s.split(',').map(x => unbracket(x.trim().split(/\s+/)[0])).filter(Boolean);
function parseTableItem(t, item) {
  let s = item.trim();
  if (!s) return;
  const cm = s.match(/^CONSTRAINT\s+[\[\]\w]+\s+([\s\S]*)$/i);
  if (cm) s = cm[1];
  if (/^PRIMARY\s+KEY/i.test(s)) { const p = s.match(/\(([^)]*)\)/); if (p) t.pk = colList(p[1]); return; }
  if (/^FOREIGN\s+KEY/i.test(s)) {
    const p = s.match(/FOREIGN\s+KEY\s*\(([^)]*)\)\s*REFERENCES\s+([\[\]\w.]+)\s*\(([^)]*)\)/i);
    if (p) t.fks.push({ cols: colList(p[1]), refTable: unbracket(p[2]), refCols: colList(p[3]) });
    return;
  }
  if (/^UNIQUE/i.test(s)) { const p = s.match(/\(([^)]*)\)/); if (p) t.uniques.push(colList(p[1])); return; }
  if (/^CHECK/i.test(s)) { const e = findCheck(s); if (e) t.checks.push({ expr: e, values: quotedValues(e) }); return; }
  if (/^DEFAULT\b/i.test(s)) { const d = s.match(/^DEFAULT\s+([\s\S]+?)\s+FOR\s+([\[\]\w]+)/i); if (d) { const c = t.columns.get(unbracket(d[2]).toLowerCase()); if (c) c.default = d[1]; } return; }
  if (/^INDEX\b/i.test(s)) { const p = s.match(/\(([^)]*)\)/); if (p) t.indexes.push(colList(p[1])); return; }
  const nm = s.match(/^([\[\]\w]+)\s+([\s\S]*)$/);
  if (!nm) return;
  const name = unbracket(nm[1]), rest = nm[2];
  const typeM = rest.match(/^([A-Za-z]\w*(?:\s*\([^)]*\))?)/);
  const col = {
    name, type: typeM ? typeM[1].replace(/\s+/g, '').toUpperCase() : '',
    nullable: !/\bNOT\s+NULL\b|\bPRIMARY\s+KEY\b|\bIDENTITY\b/i.test(rest), identity: /\bIDENTITY\b/i.test(rest), pk: /\bPRIMARY\s+KEY\b/i.test(rest),
    unique: /\bUNIQUE\b/i.test(rest), ref: null, check: null, checkValues: [], default: null,
  };
  const r = rest.match(/REFERENCES\s+([\[\]\w.]+)\s*\(\s*([\[\]\w]+)\s*\)/i);
  if (r) col.ref = { table: unbracket(r[1]), col: unbracket(r[2]) };
  const e = findCheck(rest);
  if (e) { col.check = e; col.checkValues = quotedValues(e); }
  const d = rest.match(/\bDEFAULT\s+(\((?:[^()]|\([^()]*\))*\)|N?'[^']*'|[\w.+-]+)/i);
  if (d) col.default = d[1];
  if (col.pk) t.pk = [name];
  if (col.ref) t.fks.push({ cols: [name], refTable: col.ref.table, refCols: [col.ref.col] });
  t.columns.set(name.toLowerCase(), col);
}
let _schema = null;
function schema() {
  if (_schema) return _schema;
  const tables = new Map(), procs = new Map();
  for (const f of code().sql) {
    const s = stripSqlComments(code().get(f));
    const re = /CREATE\s+TABLE\s+([\[\]\w.]+)\s*\(/gi;
    let m;
    while ((m = re.exec(s))) {
      const name = unbracket(m[1]);
      const st = m.index + m[0].length - 1, en = matchParen(s, st);
      if (en < 0) continue;
      const t = { name, file: f, columns: new Map(), pk: [], fks: [], uniques: [], checks: [], indexes: [] };
      for (const item of splitTop(s.slice(st + 1, en))) parseTableItem(t, item);
      tables.set(name.toLowerCase(), t);
      re.lastIndex = en;
    }
    const re2 = /ALTER\s+TABLE\s+([\[\]\w.]+)\s+(?:WITH\s+(?:NO)?CHECK\s+)?ADD\s+/gi;
    while ((m = re2.exec(s))) {
      const t = tables.get(unbracket(m[1]).toLowerCase());
      if (!t) continue;
      let rest = s.slice(m.index + m[0].length);
      const stop = rest.search(/;|\n\s*GO\b|\n\s*(ALTER|CREATE|INSERT|IF|PRINT|EXEC|DECLARE)\b/i);
      if (stop >= 0) rest = rest.slice(0, stop);
      for (const item of splitTop(rest)) parseTableItem(t, item);
    }
    const re3 = /CREATE\s+(UNIQUE\s+)?(?:(?:NON)?CLUSTERED\s+)?INDEX\s+([\[\]\w]+)\s+ON\s+([\[\]\w.]+)\s*\(([^)]*)\)/gi;
    while ((m = re3.exec(s))) {
      const t = tables.get(unbracket(m[3]).toLowerCase());
      if (!t) continue;
      const cols = colList(m[4]);
      t.indexes.push(cols);
      if (m[1]) t.uniques.push(cols);
    }
    const re4 = /CREATE\s+(?:OR\s+ALTER\s+)?PROC(?:EDURE)?\s+([\[\]\w.]+)([\s\S]*?)\bAS\b/gi;
    while ((m = re4.exec(s))) procs.set(unbracket(m[1]).toLowerCase(), { name: unbracket(m[1]), params: [...m[2].matchAll(/@(\w+)/g)].map(x => x[1]), file: f });
  }
  _schema = { tables, procs };
  return _schema;
}
function columnCheckValues(t, colName) {
  const c = t.columns.get(colName.toLowerCase());
  const vals = [...(c?.checkValues || [])];
  const re = new RegExp(`\\b${colName}\\b`, 'i');
  for (const ck of t.checks) if (re.test(ck.expr)) vals.push(...ck.values);
  return uniq(vals);
}

// ---------------------------------------------------------------- C#: controller và route
function attrsAbove(L, idx) {
  const out = [];
  for (let i = idx - 1; i >= 0; i--) {
    const t = L[i].trim();
    if (t === '' || t.startsWith('//')) continue;
    if (t.startsWith('[')) { out.unshift(t); continue; }
    break;
  }
  return out.join(' ');
}
const attrArg = (attrs, name) => attrs.match(new RegExp(`\\b${name}\\(\\s*"([^"]*)"`))?.[1] ?? null;
function authOf(attrs) {
  return {
    anonymous: /\bAllowAnonymous\b/.test(attrs),
    authorize: /\bAuthorize\b/.test(attrs),
    policy: attrs.match(/\bAuthorize\(\s*(?:Policy\s*=\s*)?"([^"]+)"/)?.[1] || attrs.match(/\bAuthorize\(\s*Policy\s*=\s*([\w.]+)/)?.[1] || null,
    roles: attrs.match(/\bAuthorize\([^)]*Roles\s*=\s*"([^"]+)"/)?.[1] || null,
  };
}
let _ctl = null;
function controllers() {
  if (_ctl) return _ctl;
  const out = [];
  for (const f of code().cs) {
    const src = code().get(f);
    if (!/class\s+\w+Controller\b/.test(src)) continue;
    const L = src.split(/\r?\n/);
    for (let i = 0; i < L.length; i++) {
      const cm = L[i].match(/\bclass\s+(\w+)Controller\b(?:\s*:\s*([\w<>, .]+))?/);
      if (!cm) continue;
      const cname = cm[1];
      const cattrs = attrsAbove(L, i) + ' ' + L[i].slice(0, L[i].indexOf('class'));
      const c = { name: cname, file: f, line: i + 1, attrs: cattrs, routeTpl: attrArg(cattrs, 'Route'), area: attrArg(cattrs, 'Area'), auth: authOf(cattrs), actions: [] };
      for (let j = i + 1; j < L.length; j++) {
        if (/\bclass\s+\w+/.test(L[j])) break;
        const mm = L[j].match(/^\s*(?:\[[^\]]*\]\s*)*public\s+(?:async\s+|virtual\s+|override\s+)*([\w<>\[\],.?\s]+?)\s+(\w+)\s*\(/);
        if (!mm) continue;
        const ret = mm[1], aname = mm[2];
        if (aname === cname + 'Controller' || !/(ActionResult|IResult|Result\b|Result>|Task)/.test(ret)) continue;
        const aattrs = attrsAbove(L, j) + ' ' + (L[j].match(/^\s*((?:\[[^\]]*\]\s*)*)public/)?.[1] || '');
        if (/\bNonAction\b/.test(aattrs)) continue;
        const verbs = [...aattrs.matchAll(/\bHttp(Get|Post|Put|Delete|Patch)\b(?:\(\s*"([^"]*)"[^)]*\))?/g)].map(x => ({ verb: x[1].toUpperCase(), tpl: x[2] ?? null }));
        const rts = [...aattrs.matchAll(/\bRoute\(\s*"([^"]*)"/g)].map(x => x[1]);
        c.actions.push({ name: aname, line: j + 1, attrs: aattrs, verbs, routes: rts, auth: authOf(aattrs) });
      }
      out.push(c);
    }
  }
  _ctl = out;
  return out;
}
function normRoute(r) {
  let s = String(r).toLowerCase().split('?')[0].split('#')[0].replace(/\{[^}]*\}/g, '{}').replace(/\/+/g, '/');
  if (!s.startsWith('/')) s = '/' + s;
  if (s.length > 1) s = s.replace(/\/$/, '');
  return s;
}
function actionRoutes(c, a) {
  const ctl = c.name.toLowerCase(), act = a.name.toLowerCase().replace(/async$/, '');
  const sub = t => t.replace(/\[controller\]/gi, ctl).replace(/\[action\]/gi, act).replace(/\[area\]/gi, (c.area || '').toLowerCase());
  const out = [];
  const actTpls = [...a.verbs.filter(v => v.tpl !== null).map(v => v.tpl), ...a.routes];
  const base = c.routeTpl != null ? sub(c.routeTpl) : null;
  if (actTpls.length) {
    for (const t0 of actTpls) {
      const t = sub(t0);
      if (t.startsWith('/') || t.startsWith('~/')) out.push(t.replace(/^~/, ''));
      else if (base != null) out.push('/' + [base, t].filter(Boolean).join('/'));
      else out.push('/' + t);
    }
  } else if (base != null) out.push('/' + base);
  else {
    const prefix = c.area ? '/' + c.area.toLowerCase() : '';
    out.push(`${prefix}/${ctl}/${act}`);
    if (act === 'index') { out.push(`${prefix}/${ctl}`); if (ctl === 'home' && !c.area) out.push('/'); }
  }
  return uniq(out.map(normRoute));
}
let _routes = null;
function routes() {
  if (_routes) return _routes;
  const list = [];
  for (const c of controllers()) for (const a of c.actions) for (const r of actionRoutes(c, a)) {
    const auth = a.auth.anonymous ? 'AllowAnonymous' : a.auth.policy ? `Policy ${a.auth.policy}` : a.auth.roles ? `Roles ${a.auth.roles}` : a.auth.authorize ? 'Authorize' : c.auth.anonymous ? 'AllowAnonymous' : c.auth.policy ? `Policy ${c.auth.policy}` : c.auth.roles ? `Roles ${c.auth.roles}` : c.auth.authorize ? 'Authorize' : '(chưa khai báo)';
    list.push({ route: r, verbs: a.verbs.map(v => v.verb).join(',') || 'GET', controller: c.name, action: a.name, auth, file: rel(c.file), line: a.line });
  }
  _routes = { list, set: new Set(list.map(x => x.route)) };
  return _routes;
}
const routeExists = r => { const n = normRoute(r), s = routes().set; return s.has(n) || s.has(n + '/index') || s.has(n.replace(/\/index$/, '')); };

// ---------------------------------------------------------------- appsettings
function appsettings() {
  const t = readText(abs(`${WEB}/appsettings.json`));
  if (t == null) return null;
  const clean = t.replace(/("(?:[^"\\]|\\.)*")|\/\/[^\n]*|\/\*[\s\S]*?\*\//g, (m, s) => s || '').replace(/,(\s*[}\]])/g, '$1');
  try { return JSON.parse(clean); } catch { return { __raw: t }; }
}
function configHas(cfg, keyPath) {
  if (!cfg) return false;
  if (cfg.__raw) return keyPath.split(':').every(k => cfg.__raw.includes(`"${k}"`));
  let o = cfg;
  for (const k of keyPath.split(':')) { if (o == null || typeof o !== 'object' || !(k in o)) return false; o = o[k]; }
  return true;
}

// ---------------------------------------------------------------- kiểm tra ngầm từ nội dung yêu cầu
const FRAMEWORK_NAMES = new Set(['BackgroundService', 'IHostedService', 'Controller', 'ControllerBase', 'IHubContext', 'PasswordHasher', 'HttpClient', 'CultureInfo', 'IFormFile', 'SqlConnection', 'SqlTransaction', 'DataTable']);
const CLASS_RE = /\b((?:I[A-Z])?[A-Z][A-Za-z0-9]*(?:Service|Controller|Validator|Calculator|Checker|Machine|DAL|Util|Sweeper|Agent|Parser|Generator|Initializer|Seeder|Exception|Hub|Finder|Scoring|Sender|Clock|Display|Rule|Rules))\b/g;
function extractPaths(text) {
  const out = [];
  if (/`\/`/.test(text)) out.push('/');
  for (const m of text.matchAll(/(?:^|[\s(`"'])(\/[A-Za-z][A-Za-z0-9\-_/{}.]*(?:\?[^\s`)'"]*)?)/g)) out.push(m[1].replace(/[.,;:]+$/, ''));
  return uniq(out);
}
function extractClasses(text) { const t = docs().specTables; return uniq([...text.matchAll(CLASS_RE)].map(m => m[1])).filter(n => !FRAMEWORK_NAMES.has(n) && !t.has(n.toLowerCase())); }
function extractTableCols(text) {
  const d = docs(), out = [];
  for (const m of text.matchAll(/\b([A-Z][A-Za-z]+)\.([A-Z][A-Za-z0-9]+)\b/g)) if (d.specTables.has(m[1].toLowerCase())) out.push([m[1], m[2]]);
  return out;
}
function extractConfigKeys(text) { return uniq([...text.matchAll(/\b((?:ConnectionStrings|Demo|Llm|Payment|Uploads|Email|Auth|CinemaRules):[A-Z][A-Za-z0-9]*(?::[A-Z][A-Za-z0-9]*)*)\b/g)].map(m => m[1])); }
const classExists = n => code().cs.some(f => new RegExp(`\\b(class|interface|record|struct|enum)\\s+${n}\\b`).test(code().get(f)));
function pathCheck(p) {
  if (/\.[a-z0-9]{2,5}$/i.test(p)) return exists(abs(`${WEB}/wwwroot${p}`)) ? null : `chưa có file tĩnh wwwroot${p}`;
  if (/^\/hubs\//i.test(p)) return anyContains(code().cs, p.replace(/^\//, '')) || anyContains(code().cs, p) ? null : `chưa map hub ${p}`;
  return routeExists(p) ? null : `chưa có action nào phục vụ đường dẫn ${p}`;
}
function implicitChecks(r) {
  if (!code().has()) return [];
  const msgs = [];
  const text = r.isUC ? '' : r.text;
  for (const p of extractPaths(text)) { const e = pathCheck(p); if (e) msgs.push(e); }
  for (const n of extractClasses(text)) if (!classExists(n)) msgs.push(`chưa có lớp hoặc interface ${n}`);
  const sc = schema();
  if (sc.tables.size) for (const [t, c] of extractTableCols(text)) { const tb = sc.tables.get(t.toLowerCase()); if (!tb) msgs.push(`chưa có bảng ${t}`); else if (!tb.columns.has(c.toLowerCase())) msgs.push(`bảng ${t} chưa có cột ${c}`); }
  const cfg = appsettings();
  if (cfg) for (const k of extractConfigKeys(text)) if (!configHas(cfg, k)) msgs.push(`appsettings.json chưa có khóa ${k}`);
  return uniq(msgs);
}
const needsTest = r => !r.isUC && (/unit test|logic thuần/i.test(r.text) || r.parents.some(p => /unit test/i.test(p)));

// ---------------------------------------------------------------- bằng chứng
function snapshotFiles() { return walk(abs('artifacts/ui'), ['.png']).map(rel); }
function checkEvidence(e) {
  const m = e.match(/^(test|sql|route|ui):\s*(.+)$/i);
  if (m) {
    const kind = m[1].toLowerCase(), v = m[2].trim();
    if (kind === 'test') {
      const parts = v.split('.'), meth = parts[parts.length - 1];
      const ok = code().tests.some(f => { const s = code().get(f); return new RegExp(`\\b(void|Task)\\s+${meth}\\s*\\(`).test(s) || new RegExp(`\\bclass\\s+${meth}\\b`).test(s); });
      return ok ? { ok, kind } : { ok, kind, msg: `không tìm thấy test ${v} trong tests/` };
    }
    if (kind === 'sql') { const s = schema(); const ok = s.tables.has(v.toLowerCase()) || s.procs.has(v.toLowerCase()); return ok ? { ok, kind } : { ok, kind, msg: `không có bảng hay stored procedure ${v}` }; }
    if (kind === 'route') { const ok = routeExists(v); return ok ? { ok, kind } : { ok, kind, msg: `không có action phục vụ ${v}` }; }
    const ok = snapshotFiles().some(f => path.basename(f).startsWith(v + '-'));
    return ok ? { ok, kind } : { ok, kind, msg: `chưa có ảnh artifacts/ui/**/${v}-*.png` };
  }
  const [p, sym] = e.split('#');
  const f = abs(p.trim());
  if (!exists(f)) return { ok: false, kind: 'code', msg: `không có file ${p.trim()}` };
  if (sym && !(readText(f) || '').includes(sym.trim())) return { ok: false, kind: 'code', msg: `file ${p.trim()} không chứa "${sym.trim()}"` };
  return { ok: true, kind: 'code' };
}
const parseEvidence = s => String(s || '').split(';').map(x => x.trim()).filter(Boolean);

// ---------------------------------------------------------------- luật tự động (auto:...)
function tableBefore(r) { return docs().tables.filter(t => t.file === r.file && t.line < r.line).sort((a, b) => b.line - a.line)[0]; }
function tableAfter(r) { return docs().tables.filter(t => t.file === r.file && t.line > r.line).sort((a, b) => a.line - b.line)[0]; }
const columnsOf = text => (text.match(/\(cột:\s*([^)]*)\)/i)?.[1] || '').split(',').map(s => s.trim()).filter(x => /^\w+\.\w+$/.test(x));
function enumValues(text) {
  const t = text.replace(/\(cột:[^)]*\)/i, '');
  const i = t.indexOf(':');
  if (i < 0) return [];
  const body = t.slice(i + 1).split(/;|\s\(/)[0];
  return body.split(',').map(s => s.trim().replace(/`/g, '')).filter(v => /^[A-Za-z0-9_]+$/.test(v));
}
const constFiles = () => code().cs.filter(f => /[\\/]Domain[\\/]Constants[\\/]/.test(f));
function checkEnum(name, values, cols) {
  const msgs = [];
  const cf = constFiles();
  if (!cf.length) msgs.push('chưa có thư mục Domain/Constants');
  else for (const v of values) if (!cf.some(f => code().get(f).includes(`"${v}"`))) msgs.push(`Domain/Constants chưa có giá trị "${v}" của ${name}`);
  const sc = schema();
  for (const tc of cols) {
    const [t, c] = tc.split('.');
    const tb = sc.tables.get(t.toLowerCase());
    if (!tb) { msgs.push(`chưa có bảng ${t}`); continue; }
    if (!tb.columns.has(c.toLowerCase())) { msgs.push(`bảng ${t} chưa có cột ${c}`); continue; }
    const got = columnCheckValues(tb, c);
    if (!got.length) { msgs.push(`cột ${tc} chưa có CHECK liệt kê giá trị ${name}`); continue; }
    const miss = values.filter(v => !got.includes(v)), extra = got.filter(v => !values.includes(v));
    if (miss.length) msgs.push(`CHECK của ${tc} thiếu: ${miss.join(', ')}`);
    if (extra.length) msgs.push(`CHECK của ${tc} có giá trị lạ: ${extra.join(', ')}`);
  }
  return msgs;
}
const MONEY_COLS = new Set(['price', 'listprice', 'finalprice', 'amount', 'total', 'ticketsubtotal', 'fnbsubtotal', 'discounttotal', 'walletbalance', 'balanceafter', 'openingcashfloat', 'actualcashcounted', 'expectedcash', 'discrepancy', 'minimumticketprice', 'contractamount', 'minbillamount', 'linefinalamount']);
const PCT_COLS = new Set(['discountpercent', 'legalminpercent', 'distributorsharepercent', 'vatrate']);
const STOCK_TABLES = new Set(['rawmaterials', 'productrecipes', 'inventorytransactions', 'wastagelogitems']);
const STOCK_COLS = new Set(['actualstock', 'reservedstock', 'minthreshold', 'quantity']);
function checkTable(r) {
  const st = parseSpecTable(r.text);
  if (!st) return ['không đọc được tên bảng trong spec'];
  const tb = schema().tables.get(st.name.toLowerCase());
  if (!tb) return [`chưa có CREATE TABLE ${st.name} trong Database/*.sql`];
  const msgs = [];
  for (const c of st.cols) if (!tb.columns.has(c.toLowerCase())) msgs.push(`bảng ${st.name} chưa có cột ${c}`);
  const sameSet = (a, b) => a.length === b.length && a.every(x => b.map(y => y.toLowerCase()).includes(x.toLowerCase()));
  for (const u of st.uniqueCols) if (!tb.columns.get(u.toLowerCase())?.unique && !tb.uniques.some(x => sameSet(x, [u]))) msgs.push(`cột ${st.name}.${u} chưa UNIQUE`);
  for (const u of st.uniques) if (!tb.uniques.some(x => sameSet(x, u))) msgs.push(`bảng ${st.name} thiếu UNIQUE(${u.join(', ')})`);
  if (st.pk && !sameSet(tb.pk, st.pk)) msgs.push(`khóa chính của ${st.name} phải là (${st.pk.join(', ')})`);
  for (const ix of st.indexes) if (!tb.indexes.some(x => x.slice(0, ix.length).every((c, i) => c.toLowerCase() === ix[i].toLowerCase()))) msgs.push(`bảng ${st.name} thiếu index (${ix.join(', ')})`);
  for (const col of tb.columns.values()) {
    const n = col.name.toLowerCase(), ty = col.type;
    if (MONEY_COLS.has(n) && ty !== 'BIGINT') msgs.push(`${st.name}.${col.name} là tiền, phải BIGINT (đang ${ty})`);
    if (PCT_COLS.has(n) && !/^DECIMAL\(5,2\)$/.test(ty)) msgs.push(`${st.name}.${col.name} là phần trăm, phải DECIMAL(5,2) (đang ${ty})`);
    if (STOCK_TABLES.has(st.name.toLowerCase()) && STOCK_COLS.has(n) && !/^DECIMAL\(18,3\)$/.test(ty)) msgs.push(`${st.name}.${col.name} là tồn kho hoặc định mức, phải DECIMAL(18,3) (đang ${ty})`);
    if (/(At|Time|Until|From)$/.test(col.name) && !/^(DATETIME2\(0\))$/.test(ty)) msgs.push(`${st.name}.${col.name} là thời điểm, phải DATETIME2(0) (đang ${ty})`);
    if (/Date$/.test(col.name) && ty !== 'DATE') msgs.push(`${st.name}.${col.name} là ngày, phải DATE (đang ${ty})`);
  }
  return msgs;
}
function cssTokens(block) {
  const out = [];
  let sel = ':root';
  for (const line of block) {
    const s = line.match(/^\s*([^{]+?)\s*\{/);
    if (s) sel = s[1].trim();
    for (const m of line.matchAll(/(--[\w-]+)\s*:\s*([^;]+);/g)) out.push({ sel, name: m[1], value: m[2].trim() });
  }
  return out;
}
function checkTokens(r) {
  const f = docs().files.find(x => x.file === r.file);
  const blk = f.codeBlocks.find(b => b.line > r.line && /css/i.test(b.lang));
  if (!blk) return ['không thấy khối css trong spec'];
  const css = readText(abs(`${WEB}/wwwroot/css/tokens.css`));
  if (css == null) return [`chưa có ${WEB}/wwwroot/css/tokens.css`];
  const have = cssTokens(css.replace(/\/\*[\s\S]*?\*\//g, '').split(/\r?\n/));
  const norm = v => v.replace(/\s+/g, ' ').trim().toLowerCase();
  const msgs = [];
  for (const t of cssTokens(blk.body)) {
    const h = have.find(x => x.name === t.name && x.sel.replace(/\s+/g, '') === t.sel.replace(/\s+/g, ''));
    if (!h) msgs.push(`tokens.css thiếu ${t.name} trong ${t.sel}`);
    else if (norm(h.value) !== norm(t.value)) msgs.push(`tokens.css: ${t.name} trong ${t.sel} phải là "${t.value}" (đang "${h.value}")`);
  }
  return msgs;
}
function checkPolicies(r) {
  const t = tableBefore(r);
  if (!t) return ['không thấy bảng policy'];
  const cs = code().cs.map(f => code().get(f)).join('\n');
  const msgs = [];
  for (const row of t.rows) {
    const name = row.cells[0].replace(/`/g, '');
    const roles = row.cells[1].split(',').map(s => s.trim()).filter(Boolean);
    const i = cs.search(new RegExp(`AddPolicy\\(\\s*(?:"${name}"|\\w+\\.${name}\\b)`));
    if (i < 0) { msgs.push(`chưa có AddPolicy("${name}")`); continue; }
    const seg = cs.slice(i, i + 400).split(/AddPolicy\(/)[1] || '';
    for (const ro of roles) if (!new RegExp(`\\b${ro}\\b`).test(seg)) msgs.push(`policy ${name} chưa có vai trò ${ro}`);
  }
  return msgs;
}
function checkConfig(r) {
  const cfg = appsettings();
  if (!cfg) return [`chưa có ${WEB}/appsettings.json`];
  if (r.auto === 'config-table') {
    const t = tableAfter(r);
    if (!t) return ['không thấy bảng cấu hình'];
    const sec = (r.text.match(/\b([A-Z][A-Za-z]+)\b/) || [])[1] || 'CinemaRules';
    const keys = t.rows.flatMap(row => row.cells[0].split(',').map(s => s.trim()).filter(k => /^[A-Z]\w*$/.test(k)));
    return keys.filter(k => !configHas(cfg, `${sec}:${k}`)).map(k => `appsettings.json chưa có ${sec}:${k}`);
  }
  return extractConfigKeys(r.text).filter(k => !configHas(cfg, k)).map(k => `appsettings.json chưa có ${k}`);
}
const CODE_TOKEN_RE = /\b[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+\b/g;
function checkCodes(r) {
  const cs = code().cs;
  return uniq(r.text.match(CODE_TOKEN_RE) || []).filter(c => !cs.some(f => code().get(f).includes(`"${c}"`))).map(c => `code C# chưa dùng chuỗi "${c}"`);
}
function checkEvents(r) {
  const msgs = [];
  const cs = code().cs, js = code().js;
  for (const ev of uniq(r.text.match(CODE_TOKEN_RE) || [])) {
    if (!cs.some(f => code().get(f).includes(`"${ev}"`))) msgs.push(`server chưa phát sự kiện "${ev}"`);
    if (!js.some(f => code().get(f).includes(ev))) msgs.push(`JavaScript chưa xử lý sự kiện ${ev}`);
  }
  const hubs = cs.filter(f => /[\\/]Hubs[\\/]/.test(f));
  for (const m of uniq((r.text.match(/\bJoin[A-Z]\w*/g) || []))) if (!hubs.some(f => new RegExp(`\\b${m}\\s*\\(`).test(code().get(f)))) msgs.push(`CinemaHub chưa có hàm ${m}`);
  return msgs;
}
function checkSp(r) {
  const m = r.text.match(/\b(sp_\w+)\b/);
  if (!m) return ['không đọc được tên stored procedure'];
  const p = schema().procs.get(m[1].toLowerCase());
  if (!p) return [`chưa có stored procedure ${m[1]}`];
  return [...r.text.matchAll(/@(\w+)/g)].map(x => x[1]).filter(x => !p.params.some(y => y.toLowerCase() === x.toLowerCase())).map(x => `${m[1]} thiếu tham số @${x}`);
}
function checkAccounts(r) {
  const t = tableBefore(r);
  if (!t) return ['không thấy bảng tài khoản mẫu'];
  const cs = code().cs;
  const msgs = t.rows.map(row => row.cells[0]).filter(e => /@/.test(e)).filter(e => !cs.some(f => code().get(f).includes(e))).map(e => `DbSeeder chưa tạo tài khoản ${e}`);
  for (const s of (r.text.match(/\b\d{6}\b/g) || [])) if (!cs.some(f => code().get(f).includes(s))) msgs.push(`DbSeeder chưa dùng mật khẩu hoặc PIN mẫu ${s}`);
  return msgs;
}
function autoCheck(r) {
  if (!code().has()) return ['chưa có code trong src/'];
  switch (r.auto) {
    case 'table': return checkTable(r);
    case 'tokens': return checkTokens(r);
    case 'enum': return checkEnum(r.text.match(/^(\w+)/)?.[1] || '?', enumValues(r.text), columnsOf(r.text));
    case 'enum-table': { const t = tableBefore(r); if (!t) return ['không thấy bảng giá trị']; return checkEnum('bảng giá trị', t.rows.map(x => x.cells[0].replace(/`/g, '')).filter(v => /^[A-Za-z0-9_]+$/.test(v)), columnsOf(r.text)); }
    case 'policies': return checkPolicies(r);
    case 'config': case 'config-table': return checkConfig(r);
    case 'codes': return checkCodes(r);
    case 'events': return checkEvents(r);
    case 'sp': return checkSp(r);
    case 'accounts': return checkAccounts(r);
    default: return [`luật tự động không rõ: ${r.auto}`];
  }
}

// ---------------------------------------------------------------- lint
function suppressed(L, i, rule) { return [L[i], L[i - 1] || ''].some(s => new RegExp(`spec-check:\\s*allow\\s+${rule}\\b`).test(s)); }
function lineHits(files, re, rule, msg, filter) {
  const out = [];
  for (const f of files) {
    if (filter && !filter(f)) continue;
    const L = code().get(f).split(/\r?\n/);
    L.forEach((line, i) => { if (re.test(line) && !suppressed(L, i, rule)) out.push(`${rule} ${rel(f)}:${i + 1} ${msg}`); re.lastIndex = 0; });
  }
  return out;
}
function lintControllers() {
  const out = [];
  for (const c of controllers()) {
    if (c.auth.anonymous || c.auth.authorize) continue;
    for (const a of c.actions) if (!a.auth.anonymous && !a.auth.authorize) out.push(`L07 ${rel(c.file)}:${a.line} ${c.name}Controller.${a.name} chưa có [Authorize] hoặc [AllowAnonymous]`);
  }
  return out;
}
function lintStyle() {
  const out = [];
  for (const f of code().cshtml) {
    const L = code().get(f).split(/\r?\n/);
    L.forEach((line, i) => {
      for (const m of line.matchAll(/\sstyle\s*=\s*("([^"]*)"|'([^']*)')/g)) {
        const v = (m[2] ?? m[3] ?? '').trim();
        if (/^(\s*--[\w-]+\s*:[^;]*;?)+\s*$/.test(v)) continue;
        if (!suppressed(L, i, 'L10')) out.push(`L10 ${rel(f)}:${i + 1} có style inline (chỉ được đặt biến CSS --tên)`);
      }
    });
  }
  return out;
}
function lintTodo(N) {
  const out = [];
  const files = [...code().cs, ...code().cshtml, ...code().js, ...code().css];
  for (const f of files) {
    const L = code().get(f).split(/\r?\n/);
    L.forEach((line, i) => {
      const m = line.match(/\b(TODO|FIXME|HACK)\b(?:\(Phase\s*(\d)\))?/);
      if (!m || suppressed(L, i, 'L18')) return;
      if (!m[2] || +m[2] <= N) out.push(`L18 ${rel(f)}:${i + 1} còn ${m[1]}${m[2] ? `(Phase ${m[2]})` : ''}: phần của phase đã làm phải xong; việc của phase sau ghi TODO(Phase k) với k > ${N}`);
    });
  }
  return out;
}
function lintSecrets() {
  const out = [];
  for (const f of code().json.filter(f => /appsettings[^\\/]*\.json$/i.test(f))) {
    const L = code().get(f).split(/\r?\n/);
    L.forEach((line, i) => { if (/"(ApiKey|SmtpPassword)"\s*:\s*"[^"]+"/.test(line)) out.push(`L19 ${rel(f)}:${i + 1} có bí mật ghi thẳng trong file; để trống và đặt qua biến môi trường`); });
  }
  return out;
}
function lintAntiforgery() {
  const ok = code().cs.some(f => /AutoValidateAntiforgeryToken/.test(code().get(f)));
  return ok ? [] : ['L08 Program.cs chưa đăng ký bộ lọc toàn cục AutoValidateAntiforgeryTokenAttribute'];
}
function lint(N) {
  const c = code();
  if (!c.has()) return [];
  const out = [];
  const notClock = f => !/clock/i.test(path.basename(f));
  const notDb = f => !/[\\/]Utils[\\/]DBUtil\.cs$|DbInitializer/i.test(f);
  out.push(...lineHits(c.csproj, /EntityFrameworkCore/, 'L01', 'không dùng Entity Framework'));
  out.push(...lineHits([...c.csproj, ...c.cs], /System\.Drawing/, 'L02', 'không dùng System.Drawing (không chạy trên Linux)'));
  out.push(...lineHits(c.cs, /\bDateTime(Offset)?\.(Now|UtcNow|Today)\b/, 'L03', 'lấy thời gian qua IClock', notClock));
  out.push(...lineHits(c.cs, /\b(GETDATE|SYSDATETIME|GETUTCDATE|SYSUTCDATETIME)\s*\(|\bCURRENT_TIMESTAMP\b/i, 'L04', 'SQL nghiệp vụ nhận @Now từ IClock'));
  out.push(...lineHits(c.sql, /\b(GETDATE|SYSDATETIME|GETUTCDATE|SYSUTCDATETIME)\s*\(|\bCURRENT_TIMESTAMP\b/i, 'L04', 'stored procedure nhận @Now hoặc khoảng ngày làm tham số', f => /procedure|02_/i.test(path.basename(f))));
  out.push(...lineHits(c.cs, /new\s+Sql(Connection|Command|DataAdapter)\s*\(/, 'L05', 'chỉ Utils/DBUtil được mở kết nối và tạo lệnh SQL', notDb));
  const notIn = f => notDb(f);
  out.push(...lineHits(c.cs, /^(?!.*\bInParams\().*\$@?"[^"]*\b(SELECT|INSERT|UPDATE|DELETE|EXEC|WHERE)\b/, 'L06', 'không ghép dữ liệu vào chuỗi SQL bằng $"…"; dùng tham số @1, @2 (mệnh đề IN dùng DBUtil.InParams)', notIn));
  out.push(...lineHits(c.cs, /^(?!.*\bInParams\().*"[^"]*\b(SELECT|INSERT INTO|UPDATE|DELETE FROM|WHERE)\b[^"]*"\s*\+\s*[A-Za-z_]/, 'L06', 'không nối chuỗi SQL với biến; dùng tham số @1, @2', notIn));
  out.push(...lineHits(c.cs, /\bthrow\s+new\s+NotImplementedException\b/, 'L17', 'còn hàm chưa làm'));
  out.push(...lintTodo(N));
  out.push(...lintSecrets());
  if (N >= 2) {
    out.push(...lintControllers());
    out.push(...lintAntiforgery());
    out.push(...lineHits([...c.cshtml, ...c.js], /(?<![\w.$])(?<!function\s)(alert|confirm|prompt)\s*\((?![^)]*\)\s*\{)|\bwindow\.(alert|confirm|prompt)\s*\(/, 'L09', 'không dùng alert/confirm/prompt; dùng ui.toast, ui.confirm'));
    out.push(...lintStyle());
    out.push(...lineHits(c.css, /#[0-9a-fA-F]{3,8}\b|\brgba?\(|\bhsla?\(/, 'L11', 'màu phải lấy từ token trong tokens.css', f => !/tokens\.css$/i.test(f)));
    out.push(...lineHits(c.cshtml, /\b(fill|stroke|color)\s*=\s*"#/, 'L11', 'màu phải lấy từ token (var(--…))'));
    out.push(...lineHits(c.cshtml, /ViewData\["Title"\]\s*=\s*"(Index|Create|Edit|Details|Delete|Privacy|Home Page)"|<h[1-3][^>]*>\s*(Index|Create|Edit|Details|Delete)\s*<\/h/, 'L12', 'còn tiêu đề scaffold tiếng Anh'));
    out.push(...lineHits([...c.cshtml, ...c.js, ...c.cs], /lorem ipsum/i, 'L13', 'còn chữ lorem ipsum'));
    out.push(...lineHits(c.cshtml, /<(script|img)[^>]+src\s*=\s*"https?:\/\/|<link[^>]+href\s*=\s*"https?:\/\//i, 'L14', 'thư viện và ảnh phải nằm trong wwwroot, không gọi CDN hay ảnh ngoài'));
    out.push(...lineHits([...c.cshtml, ...c.css, ...c.json], /font-?awesome|tailwind/i, 'L15', 'chỉ dùng Bootstrap Icons, không dùng FontAwesome hay Tailwind'));
    out.push(...lineHits(c.cshtml, /<(button|a)\b(?![^>]*\baria-label\s*=)[^>]*>\s*<i\s+class="bi\s[^"]*"[^>]*>\s*<\/i>\s*<\/\1>/, 'L16', 'nút chỉ có icon phải có aria-label'));
    out.push(...lineHits(c.cshtml, /Html\.Raw\(/, 'L20', 'không dùng Html.Raw; dữ liệu cho JS dùng Json.Serialize'));
    out.push(...lineHits(c.cshtml, /\bDBUtil\b|\b\w+DAL\.\w+\(|SqlConnection/, 'L21', 'View không được truy cập dữ liệu'));
    out.push(...lineHits(c.cs, /\bDBUtil\.\w+\(/, 'L22', 'Controller không gọi thẳng DBUtil', f => /[\\/]Controllers[\\/]/.test(f)));
    out.push(...lineHits(c.cshtml, />\s*(Create New|Back to List|Edit|Delete|Details|Save|Cancel|Submit|Search|Login|Log in|Logout|Register|Home|Privacy)\s*</, 'L24', 'còn chữ tiếng Anh trên giao diện'));
  }
  for (const f of c.cs.filter(f => /[\\/]Entities[\\/]/.test(f))) {
    const L = c.get(f).split(/\r?\n/);
    L.forEach((line, i) => { for (const m of line.matchAll(/public\s+(int|decimal|double|float)\??\s+(\w+)\b/g)) if (MONEY_COLS.has(m[2].toLowerCase()) && !suppressed(L, i, 'L23')) out.push(`L23 ${rel(f)}:${i + 1} ${m[2]} là tiền, dùng long`); });
  }
  return out;
}

// ---------------------------------------------------------------- kiểm tra quy trình cuối phase
function git(args) { const r = spawnSync('git', args, { cwd: ROOT, encoding: 'utf8' }); return r.status === 0 ? r.stdout.trim() : null; }
function codeHash() {
  const h = crypto.createHash('sha1');
  const files = [...walk(abs('src'), null), ...walk(abs('tests'), null)]
    .filter(f => !/[\\/]wwwroot[\\/]uploads[\\/]/.test(f)).map(rel).sort();
  for (const f of files) { h.update(f + '\0'); h.update(fs.readFileSync(abs(f))); }
  return h.digest('hex').slice(0, 12);
}
function screensOfPhase(n) { return [...docs().screens.values()].filter(s => s.phases.has(n)).map(s => s.id).filter(id => id !== 'ART'); }
function processChecks(N) {
  const out = [];
  const prog = progressText();
  if (!prog) out.push('chưa có PROGRESS.md');
  else if (!new RegExp(`\\[x\\]\\s*Phase\\s*${N}\\b`, 'i').test(prog)) out.push(`PROGRESS.md chưa đánh dấu [x] Phase ${N}`);
  const ts = readText(abs('docs/TEST-SCENARIOS.md'));
  if (!ts) out.push('chưa có docs/TEST-SCENARIOS.md');
  else {
    const part = ts.split(/^#{2,3}\s+/m).find(x => new RegExp(`^Phase\\s*${N}\\b`, 'i').test(x)) || '';
    const rows = part.split(/\r?\n/).filter(l => /^\s*\|/.test(l) && !/^\s*\|\s*:?-{2,}/.test(l)).length - 1;
    if (rows < 3) out.push(`docs/TEST-SCENARIOS.md chưa có mục "## Phase ${N}" với ít nhất 3 kịch bản đã chạy`);
  }
  if (!exists(abs('docs/BUG-LOG.md'))) out.push('chưa có docs/BUG-LOG.md');
  const ct = readText(abs('docs/CONTRACTS.md')) || '';
  const m12 = ct.split(/^##\s+/m).find(x => /^12\./.test(x)) || '';
  if (!new RegExp(`Phase\\s*${N}\\b`).test(m12)) out.push(`docs/CONTRACTS.md mục 12 chưa ghi phần Phase ${N}`);
  const au = readText(abs(`docs/audit/phase-${N}.md`));
  if (!au) out.push(`chưa có docs/audit/phase-${N}.md (kết quả của agent spec-auditor)`);
  else if (!/Kết quả:\s*Đạt/i.test(au)) out.push(`docs/audit/phase-${N}.md chưa ghi "Kết quả: Đạt"`);
  const stamp = readJson(STAMP_FILE, null);
  const hash = codeHash();
  if (!stamp || stamp.hash !== hash) out.push('code đã đổi sau lần chạy test gần nhất: chạy node tools/spec-check/check.mjs test --reset-db');
  else {
    if (!stamp.build?.ok) out.push('dotnet build chưa đạt');
    if (!stamp.test?.ok) out.push('dotnet test chưa đạt');
    if (!stamp.resetDb?.ok) out.push('chưa chạy thành công --reset-db --init-only (chạy test --reset-db)');
  }
  const facts = readText(abs('docs/generated/README.md')) || '';
  if (!facts.includes(`Mã băm code: ${hash}`)) out.push('docs/generated chưa cập nhật theo code hiện tại: chạy node tools/spec-check/check.mjs facts');
  const shots = snapshotFiles();
  for (const s of screensOfPhase(N)) if (!shots.some(f => f.includes(`phase-${N}/`) && path.basename(f).startsWith(s + '-'))) out.push(`chưa có ảnh kiểm tra giao diện artifacts/ui/phase-${N}/${s}-*.png`);
  if (git(['rev-parse', '--is-inside-work-tree']) === 'true') {
    const dirty = (git(['status', '--porcelain']) || '').split(/\r?\n/).filter(Boolean);
    if (dirty.length) out.push(`còn ${dirty.length} file chưa commit (git status)`);
    const msg = git(['log', '-1', '--pretty=%s']) || '';
    if (!new RegExp(`^Phase\\s*${N}\\b`).test(msg)) out.push(`commit cuối phải có tiêu đề "Phase ${N}: …" (đang là "${clip(msg, 60)}")`);
    const head = git(['rev-parse', 'HEAD']);
    for (const tag of docs().labTags[N] || []) { const t = git(['rev-parse', `${tag}^{commit}`]); if (!t) out.push(`chưa gắn git tag ${tag}`); else if (t !== head) out.push(`tag ${tag} chưa trỏ vào commit cuối (git tag -f ${tag})`); }
  } else out.push('thư mục dự án chưa là git repo');
  return out;
}

// ---------------------------------------------------------------- verify
function verify(N, o = {}) {
  const d = docs(), sc = scope();
  const fails = [];
  const F = (cat, msg, id) => fails.push({ cat, msg, id });
  for (const r of d.dup) F('docs', `mã ${r.id} bị trùng (${rel(r.file)}:${r.line})`);
  for (const u of d.files.flatMap(f => f.untagged)) F('docs', `${rel(u.file)}:${u.line} gạch đầu dòng chưa có mã yêu cầu (thêm mã mới [Sx.y-nn Pk] theo docs/README.md rồi chạy sync): ${clip(u.text, 70)}`);
  const tr = allTrace();
  const items = [...d.reqs, ...d.ucs].filter(r => r.phase <= N && inScope(r, sc));
  for (const r of items) {
    if (r.auto) { for (const m of autoCheck(r)) F('auto', `${r.id}: ${m}`, r.id); for (const m of implicitChecks(r)) F('implicit', `${r.id}: ${m}`, r.id); continue; }
    const t = tr.get(r.id);
    if (!t || t.removed) { F('trace', `${r.id} chưa có dòng trong docs/trace/phase-${r.phase}.md (chạy sync)`, r.id); continue; }
    const st = normStatus(t.status);
    if (st === 'Bỏ') {
      if (!t.note) F('trace', `${r.id} ghi Bỏ nhưng chưa có lý do ở cột Ghi chú`, r.id);
      else if (!progressMentions(r.id)) F('trace', `${r.id} ghi Bỏ nhưng PROGRESS.md mục "Thay đổi mô hình" chưa nhắc mã này`, r.id);
      continue;
    }
    if (st !== 'Xong') { F('trace', `${r.id} chưa xong – ${clip(r.isUC ? r.name : r.text, 90)}`, r.id); continue; }
    const ev = parseEvidence(t.evidence);
    if (!ev.length) { F('trace', `${r.id} ghi Xong nhưng chưa có bằng chứng`, r.id); continue; }
    let strong = 0, hasTest = false;
    for (const e of ev) {
      const res = checkEvidence(e);
      if (!res.ok) F('trace', `${r.id}: bằng chứng "${e}" sai – ${res.msg}`, r.id);
      else { if (['code', 'test', 'sql'].includes(res.kind)) strong++; if (res.kind === 'test') hasTest = true; }
    }
    if (!strong) F('trace', `${r.id}: cần ít nhất một bằng chứng là file code, test: hoặc sql:`, r.id);
    if (needsTest(r) && !hasTest) F('trace', `${r.id}: yêu cầu có logic thuần hoặc unit test nên phải có bằng chứng test:`, r.id);
    for (const m of implicitChecks(r)) F('implicit', `${r.id}: ${m}`, r.id);
  }
  for (const x of lint(N)) F('lint', x);
  if (o.final) for (const x of processChecks(N)) F('process', x);
  return { fails };
}
const CAT_NAME = { docs: 'Spec', trace: 'Truy vết', auto: 'Tự động', implicit: 'Đối chiếu code', lint: 'Quy tắc code', process: 'Quy trình cuối phase' };
function printVerify(N, res, o) {
  const by = {};
  for (const f of res.fails) (by[f.cat] ||= []).push(f);
  if (!res.fails.length) { console.log(`KẾT QUẢ PHASE ${N}${o.final ? ' (kiểm tra cuối)' : ''}: ĐẠT`); return; }
  const lim = o.brief ? 15 : 400;
  for (const [cat, list] of Object.entries(by)) {
    console.log(`\n## ${CAT_NAME[cat] || cat}: ${list.length} lỗi`);
    list.slice(0, lim).forEach(f => console.log(`- ${f.msg}`));
    if (list.length > lim) console.log(`- … còn ${list.length - lim} lỗi nữa (bỏ --brief để xem hết)`);
  }
  console.log(`\nKẾT QUẢ PHASE ${N}${o.final ? ' (kiểm tra cuối)' : ''}: CHƯA ĐẠT – ${res.fails.length} lỗi`);
}

// ---------------------------------------------------------------- trạng thái phiên
const loadState = () => readJson(STATE_FILE, null);
const saveState = s => writeText(STATE_FILE, JSON.stringify(s, null, 2));
function phaseStats(n) {
  const tr = allTrace(), sc = scope();
  const items = phaseItems(n).filter(i => !i.skipped);
  const manual = items.filter(i => !i.auto);
  const done = manual.filter(i => ['Xong', 'Bỏ'].includes(normStatus(tr.get(i.id)?.status))).length;
  return { total: items.length, manual: manual.length, auto: items.length - manual.length, done, skipped: sc.skipPhases.includes(n) };
}

// ---------------------------------------------------------------- lệnh
function fmtItem(r, tr, full) {
  const st = r.auto ? 'tự động' : normStatus(tr.get(r.id)?.status);
  const where = `${rel(r.file)}:${r.line}`;
  const head = `- ${r.id} [${st}] (${where})`;
  if (!full) return `${head} ${clip(r.isUC ? r.name : r.text, 95)}`;
  const ctx = r.parents?.length ? `\n    thuộc: ${r.parents.join(' › ')}` : '';
  const body = r.isUC ? `${r.name}. Tác nhân: ${r.actors}. Quan hệ: ${r.rel || 'không'}. SPEC ${r.spec}. Màn hình ${r.screens.join(', ')}. Phase ${r.phases.join(', ')}.` : r.text;
  const hints = [];
  if (!r.isUC) {
    const p = extractPaths(r.text), c = extractClasses(r.text), k = extractTableCols(r.text).map(x => x.join('.'));
    if (p.length) hints.push(`đường dẫn ${p.join(', ')}`);
    if (c.length) hints.push(`lớp ${c.join(', ')}`);
    if (k.length) hints.push(`cột ${k.join(', ')}`);
    if (needsTest(r)) hints.push('cần test:');
    if (r.auto) hints.push(`luật auto:${r.auto}`);
  }
  return `${head}${ctx}\n    ${body}${hints.length ? `\n    script sẽ đối chiếu: ${hints.join('; ')}` : ''}`;
}
function cmdList(N, flags) {
  const tr = allTrace();
  const items = phaseItems(N).filter(i => !i.skipped);
  let gs = groupsOf(items);
  if (flags.group) gs = gs.filter(g => String(g.no) === String(flags.group));
  for (const g of gs) {
    const its = flags.pending ? g.items.filter(i => !i.auto && normStatus(tr.get(i.id)?.status) === 'Chưa') : g.items;
    if (!its.length) continue;
    console.log(`\n### Nhóm ${g.no}: mục ${g.section} – ${clip(g.title, 70)} – ${rel(g.file)}`);
    for (const r of its) console.log(fmtItem(r, tr, !!flags.full));
  }
}
function cmdBegin(N) {
  const d = docs();
  const st = loadState();
  const token = st && st.phase === N && st.token ? st.token : `P${N}-${crypto.randomBytes(4).toString('hex')}`;
  saveState({ phase: N, token, startedAt: st?.phase === N ? st.startedAt : new Date().toISOString(), lastBlock: null });
  const changed = syncTraces();
  const s = phaseStats(N);
  console.log(`PHASE-TOKEN: ${token}`);
  console.log(`=== PHASE ${N}: ${d.phaseNames[N] || ''} ===`);
  console.log(`${s.total} yêu cầu: ${s.manual} cần bằng chứng trong docs/trace/phase-${N}.md, ${s.auto} được script kiểm tự động. Đã xong ${s.done}/${s.manual}.`);
  if (s.skipped) console.log('Phase này nằm trong scope.json (người dùng đã cắt). Không cần làm.');
  if (changed.length) console.log(`Đã cập nhật: ${changed.join(', ')}`);
  if (N > 1) {
    const prev = verify(N - 1);
    if (prev.fails.length) console.log(`\nCẢNH BÁO: các phase trước còn ${prev.fails.length} lỗi (chạy verify ${N - 1}). Sửa trước khi làm tiếp, trừ khi lỗi đó thuộc việc của phase này.`);
  }
  console.log(`\nCách làm: lặp "next ${N}" → đọc nguyên văn → code và test → điền docs/trace/phase-${N}.md → "verify ${N} --brief". Không bỏ nhóm nào.`);
  console.log('Danh sách đầy đủ (mỗi dòng một yêu cầu, xem nguyên văn bằng list N --full --group K):');
  cmdList(N, {});
}
function cmdNext(N) {
  const tr = allTrace();
  const items = phaseItems(N).filter(i => !i.skipped);
  for (const g of groupsOf(items)) {
    const pend = g.items.filter(i => !i.auto && normStatus(tr.get(i.id)?.status) === 'Chưa');
    if (!pend.length) continue;
    const autos = g.items.filter(i => i.auto);
    console.log(`### Nhóm ${g.no}: mục ${g.section} – ${clip(g.title, 70)} – ${rel(g.file)}`);
    console.log(`Còn ${pend.length} yêu cầu. Mở file trên để đọc cả đoạn trước khi code.`);
    for (const r of [...pend, ...autos]) console.log(fmtItem(r, tr, true));
    const left = groupsOf(items).filter(x => x.no > g.no && x.items.some(i => !i.auto && normStatus(tr.get(i.id)?.status) === 'Chưa')).length;
    console.log(`\nXong nhóm này thì điền docs/trace/phase-${N}.md, chạy verify ${N} --brief, rồi next ${N}. Còn ${left} nhóm sau nhóm này.`);
    return;
  }
  console.log(`Không còn yêu cầu nào ghi "Chưa" trong Phase ${N}. Chạy: node tools/spec-check/check.mjs verify ${N}, rồi làm các bước kết thúc phase.`);
}
function cmdMark(pos, flags) {
  const ids = String(pos[0] || '').split(',').map(x => x.trim()).filter(Boolean);
  if (!ids.length) { console.log('Cách dùng: node tools/spec-check/check.mjs mark S3.6-04 --ev "src/FPolyCinema.Web/Services/BillService.cs#Recalculate; test:Recalculate_SumsLines" [--note "…"] [--status Xong|Bỏ|Chưa]'); return; }
  const status = flags.status ? normStatus(flags.status) : 'Xong';
  let bad = 0;
  for (const id of ids) {
    let n = null;
    for (let k = 1; k <= 8 && !n; k++) { const t = readTrace(k).get(id); if (t && !t.removed) n = k; }
    if (!n) { console.log(`${id}: không có trong docs/trace (kiểm tra mã hoặc chạy sync)`); bad++; continue; }
    const f = traceFile(n), raw = readText(f), eol = raw.includes('\r\n') ? '\r\n' : '\n';
    const L = raw.split(/\r?\n/);
    const idx = readTrace(n).get(id).line - 1;
    const c = L[idx].trim().replace(/^\|/, '').replace(/\|$/, '').split('|').map(x => x.trim());
    while (c.length < 5) c.push('');
    c[2] = status;
    if (typeof flags.ev === 'string') c[3] = esc(flags.ev);
    if (typeof flags.note === 'string') c[4] = esc(flags.note);
    L[idx] = `| ${c.slice(0, 5).join(' | ')} |`;
    writeText(f, L.join(eol));
    const probs = status === 'Xong' ? parseEvidence(c[3]).map(e => [e, checkEvidence(e)]).filter(([, r]) => !r.ok).map(([e, r]) => `"${e}" ${r.msg}`) : [];
    if (status === 'Xong' && !parseEvidence(c[3]).length) probs.push('chưa có bằng chứng');
    if (status === 'Bỏ' && !c[4]) probs.push('cần --note ghi lý do');
    if (probs.length) bad++;
    console.log(`${id} (phase ${n}) → ${status}${probs.length ? ' – cần sửa: ' + probs.join('; ') : ''}`);
  }
  process.exitCode = bad ? 1 : 0;
}
function cmdStatus() {
  const d = docs(), st = loadState();
  console.log('| Phase | Nội dung | Đã xong | Tự động | Ghi chú |');
  console.log('|---|---|---|---|---|');
  for (let n = 1; n <= 8; n++) { const s = phaseStats(n); console.log(`| ${n} | ${d.phaseNames[n] || ''} | ${s.done}/${s.manual} | ${s.auto} | ${s.skipped ? 'đã cắt (scope.json)' : ''}${st?.phase === n ? ' đang làm' : ''} |`); }
}
function runCmd(cmd, args, timeoutMin = 20) {
  const r = spawnSync(cmd, args, { cwd: ROOT, encoding: 'utf8', timeout: timeoutMin * 60000, maxBuffer: 64 * 1024 * 1024 });
  const outp = `${r.stdout || ''}\n${r.stderr || ''}`;
  return { ok: r.status === 0, tail: outp.split(/\r?\n/).filter(Boolean).slice(-25).join('\n'), err: r.error?.message };
}
function cmdTest(flags) {
  const stamp = { hash: codeHash(), at: new Date().toISOString() };
  console.log('dotnet build …');
  stamp.build = runCmd('dotnet', ['build']);
  if (stamp.build.ok) { console.log('dotnet test …'); stamp.test = runCmd('dotnet', ['test', '--no-build']); }
  else stamp.test = { ok: false, tail: 'bỏ qua vì build lỗi' };
  if (flags['reset-db'] && stamp.build.ok) { console.log('dotnet run -- --reset-db --init-only …'); stamp.resetDb = runCmd('dotnet', ['run', '--no-build', '--project', WEB, '--', '--reset-db', '--init-only'], 10); }
  writeText(STAMP_FILE, JSON.stringify(stamp, null, 2));
  for (const k of ['build', 'test', 'resetDb']) if (stamp[k]) { console.log(`\n== ${k}: ${stamp[k].ok ? 'ĐẠT' : 'LỖI'}`); if (!stamp[k].ok) console.log(stamp[k].err || stamp[k].tail); }
  console.log(`\nĐã ghi dấu kết quả cho mã băm code ${stamp.hash}.`);
  process.exitCode = stamp.build.ok && stamp.test.ok && (!stamp.resetDb || stamp.resetDb.ok) ? 0 : 1;
}

// ---------------------------------------------------------------- facts (nguồn cho tài liệu lab)
function tree(dir, depth, prefix = '') {
  if (depth < 0) return [];
  let ents;
  try { ents = fs.readdirSync(dir, { withFileTypes: true }).filter(e => !SKIP_DIRS.has(e.name) && !(e.name === 'lib' && path.basename(dir) === 'wwwroot') && e.name !== 'uploads').sort((a, b) => (b.isDirectory() - a.isDirectory()) || a.name.localeCompare(b.name)); } catch { return []; }
  const out = [];
  const files = ents.filter(e => !e.isDirectory());
  for (const e of ents.filter(e => e.isDirectory())) { out.push(`${prefix}${e.name}/`); out.push(...tree(path.join(dir, e.name), depth - 1, prefix + '  ')); }
  if (files.length > 12) out.push(`${prefix}(${files.length} file: ${files.slice(0, 6).map(f => f.name).join(', ')}, …)`);
  else for (const e of files) out.push(`${prefix}${e.name}`);
  return out;
}
function cmdFacts() {
  const d = docs(), sc = schema(), hash = codeHash(), now = new Date().toISOString();
  const out = {};
  // schema
  const order = [...d.specTables.keys()];
  const tbls = [...sc.tables.values()].sort((a, b) => { const ia = order.indexOf(a.name.toLowerCase()), ib = order.indexOf(b.name.toLowerCase()); return (ia < 0 ? 999 : ia) - (ib < 0 ? 999 : ib) || a.name.localeCompare(b.name); });
  let s = `# Cơ sở dữ liệu (sinh từ Database/*.sql)\n\n${tbls.length} bảng, ${sc.procs.size} stored procedure.\n`;
  for (const t of tbls) {
    s += `\n## ${t.name}\n\n| Cột | Kiểu | NULL | Khóa | Tham chiếu | Ràng buộc khác |\n|---|---|---|---|---|---|\n`;
    for (const c of t.columns.values()) {
      const key = [t.pk.map(x => x.toLowerCase()).includes(c.name.toLowerCase()) ? 'PK' : '', t.fks.some(f => f.cols.map(x => x.toLowerCase()).includes(c.name.toLowerCase())) ? 'FK' : ''].filter(Boolean).join(', ');
      const fk = t.fks.find(f => f.cols.length === 1 && f.cols[0].toLowerCase() === c.name.toLowerCase());
      const other = [c.identity ? 'IDENTITY' : '', c.unique || t.uniques.some(u => u.length === 1 && u[0].toLowerCase() === c.name.toLowerCase()) ? 'UNIQUE' : '', columnCheckValues(t, c.name).length ? `CHECK: ${columnCheckValues(t, c.name).join(', ')}` : (c.check ? `CHECK: ${esc(c.check)}` : ''), c.default ? `DEFAULT ${esc(c.default)}` : ''].filter(Boolean).join('; ');
      s += `| ${c.name} | ${c.type} | ${c.nullable ? 'Có' : 'Không'} | ${key} | ${fk ? `${fk.refTable}.${fk.refCols[0]}` : ''} | ${other} |\n`;
    }
    const multi = [...t.uniques.filter(u => u.length > 1).map(u => `UNIQUE(${u.join(', ')})`), ...(t.pk.length > 1 ? [`Khóa chính (${t.pk.join(', ')})`] : []), ...t.indexes.map(i => `Index (${i.join(', ')})`)];
    if (multi.length) s += `\nRàng buộc nhiều cột: ${uniq(multi).join('; ')}\n`;
  }
  s += '\n## Quan hệ khóa ngoại (dùng cho ERD)\n\n| Bảng con | Cột | Bảng cha | Bản số |\n|---|---|---|---|\n';
  for (const t of tbls) for (const f of t.fks) { const one = t.uniques.some(u => u.length === f.cols.length && u.every(x => f.cols.map(y => y.toLowerCase()).includes(x.toLowerCase()))) || (t.pk.length === f.cols.length && t.pk.every(x => f.cols.map(y => y.toLowerCase()).includes(x.toLowerCase()))); const nul = f.cols.some(c => t.columns.get(c.toLowerCase())?.nullable); s += `| ${t.name} | ${f.cols.join(', ')} | ${f.refTable} | ${f.refTable} ${nul ? '0..1' : '1'} – ${one ? '0..1' : '0..n'} ${t.name} |\n`; }
  s += '\n## Stored procedure\n\n' + ([...sc.procs.values()].map(p => `- ${p.name}(${p.params.map(x => '@' + x).join(', ')}) – ${rel(p.file)}`).join('\n') || '- (chưa có)') + '\n';
  out['schema.md'] = s;
  // routes
  const rt = routes();
  let r = `# Controller, route và quyền (sinh từ Controllers/*.cs)\n\n${controllers().length} controller, ${rt.list.length} route.\n\n| Route | HTTP | Controller.Action | Quyền | File |\n|---|---|---|---|---|\n`;
  for (const x of rt.list.sort((a, b) => a.route.localeCompare(b.route))) r += `| ${x.route} | ${x.verbs} | ${x.controller}.${x.action} | ${x.auth} | ${x.file}:${x.line} |\n`;
  r += '\n## Màn hình trong UI mục 7 và route tương ứng\n\n| Màn hình | Phase | Đường dẫn trong spec | Có trong code |\n|---|---|---|---|\n';
  for (const scr of [...d.screens.values()].sort((a, b) => a.id.localeCompare(b.id))) {
    const ps = d.reqs.filter(q => q.id.startsWith(`U7.${scr.id}-`));
    const paths = uniq(ps.flatMap(q => extractPaths(q.text)).filter(p => !/\.[a-z0-9]{2,5}$/i.test(p)));
    r += `| ${scr.id} | ${[...scr.phases].sort().join(', ')} | ${paths.join(', ')} | ${paths.map(p => (routeExists(p) ? 'có' : 'chưa')).join(', ')} |\n`;
  }
  out['routes.md'] = r;
  // use cases
  const tr = allTrace();
  let u = '# Use case và bằng chứng trong code (sinh từ docs/trace)\n\n| Mã | Use case | Tác nhân | Quan hệ | Màn hình | Phase | Trạng thái | Bằng chứng |\n|---|---|---|---|---|---|---|---|\n';
  for (const x of d.ucs) { const t = tr.get(x.id); u += `| ${x.id} | ${x.name} | ${x.actors} | ${x.rel} | ${x.screens.join(', ')} | ${x.phases.join(', ')} | ${normStatus(t?.status)} | ${esc(t?.evidence || '')} |\n`; }
  u += '\n## Tiến độ yêu cầu theo phase\n\n| Phase | Nội dung | Đã xong | Tự động |\n|---|---|---|---|\n';
  for (let n = 1; n <= 8; n++) { const st = phaseStats(n); u += `| ${n} | ${d.phaseNames[n] || ''} | ${st.done}/${st.manual} | ${st.auto} |\n`; }
  out['usecases.md'] = u;
  // tech
  let t = '# Công nghệ, thư viện, cấu trúc mã nguồn (sinh từ code)\n\n## Gói NuGet\n\n| Project | Gói | Phiên bản |\n|---|---|---|\n';
  for (const f of code().csproj) { const s2 = readText(f) || ''; const tf = s2.match(/<TargetFramework>([^<]+)/)?.[1]; t += `| ${rel(f)} (${tf || '?'}) | | |\n`; for (const m of s2.matchAll(/<PackageReference\s+Include="([^"]+)"\s+Version="([^"]+)"/g)) t += `| | ${m[1]} | ${m[2]} |\n`; }
  const lm = readJson(abs(`${WEB}/libman.json`), null);
  t += '\n## Thư viện giao diện (libman.json)\n\n' + (lm?.libraries?.map(x => `- ${x.library}${x.destination ? ` → ${x.destination}` : ''}`).join('\n') || '- (không có libman.json)') + '\n';
  const libDir = abs(`${WEB}/wwwroot/lib`);
  if (exists(libDir)) t += '\nThư mục wwwroot/lib: ' + fs.readdirSync(libDir).join(', ') + '\n';
  const hubs = code().cs.filter(f => /[\\/]Hubs[\\/]/.test(f));
  t += '\n## SignalR\n\n' + (hubs.map(f => `- ${rel(f)}: ${uniq([...code().get(f).matchAll(/public\s+(?:async\s+)?Task\s+(\w+)\s*\(/g)].map(m => m[1])).join(', ')}`).join('\n') || '- (chưa có hub)') + '\n';
  const tcount = code().tests.map(f => ({ f, n: (code().get(f).match(/\[(Fact|Theory)\b/g) || []).length })).filter(x => x.n);
  t += `\n## Kiểm thử\n\n${tcount.reduce((a, x) => a + x.n, 0)} test trong ${tcount.length} file.\n\n` + tcount.map(x => `- ${rel(x.f)}: ${x.n}`).join('\n') + '\n';
  t += '\n## Cây thư mục\n\n```\n' + ['src/', ...tree(abs('src'), 3, '  '), 'tests/', ...tree(abs('tests'), 2, '  '), 'tools/', ...tree(abs('tools'), 1, '  ')].join('\n') + '\n```\n';
  out['tech.md'] = t;
  const readme = `# Dữ kiện sinh từ code\n\nSinh bởi \`node tools/spec-check/check.mjs facts\` lúc ${now}.\nMã băm code: ${hash}\n\nKhông sửa tay các file trong thư mục này. Tài liệu lab vẽ sơ đồ từ đây (xem LAB-PROMPT.md mục 2).\n\n- schema.md: bảng, cột, kiểu, khóa, CHECK, quan hệ khóa ngoại, stored procedure (Lab 2, Lab 3, Lab 8)\n- routes.md: controller, action, route, policy; màn hình và route (Lab 2 luồng giao diện, Lab 4, Lab 8)\n- usecases.md: use case, trạng thái, bằng chứng trong code; tiến độ (Lab 1, Lab 7)\n- tech.md: gói NuGet, thư viện giao diện, SignalR, test, cây thư mục (Lab 3, Lab 8)\n`;
  out['README.md'] = readme;
  for (const [k, v] of Object.entries(out)) writeText(abs(`docs/generated/${k}`), v);
  console.log(`Đã sinh docs/generated/ (${Object.keys(out).join(', ')}) cho mã băm ${hash}.`);
}

// ---------------------------------------------------------------- tự kiểm tra bộ spec
function cmdSelftest() {
  const d = docs(), errs = [], info = [];
  for (const r of d.dup) errs.push(`mã trùng ${r.id} (${rel(r.file)}:${r.line})`);
  const contracts = readText(abs('docs/CONTRACTS.md')) || '';
  const known = new Set([...(contracts.match(CODE_TOKEN_RE) || []), 'SCOPE_IDENTITY']);
  for (const f of d.files.filter(f => !f.file.endsWith('CONTRACTS.md'))) {
    f.lines.forEach((line, i) => { for (const tok of line.match(CODE_TOKEN_RE) || []) if (!known.has(tok)) errs.push(`${rel(f.file)}:${i + 1} dùng tên ${tok} chưa có trong CONTRACTS.md`); });
  }
  for (const r of d.reqs) for (const [t, c] of extractTableCols(r.text)) if (!d.specTables.get(t.toLowerCase()).cols.includes(c)) errs.push(`${r.id} nhắc ${t}.${c} nhưng mục 6 không có cột này`);
  const screenIds = new Set([...d.screens.keys()]);
  for (const u of d.ucs) for (const s of u.screens) if (!screenIds.has(s)) errs.push(`${u.id} nhắc màn hình ${s} chưa có trong UI mục 7`);
  for (let n = 1; n <= 8; n++) if (!phaseItems(n).length) errs.push(`Phase ${n} không có yêu cầu nào`);
  for (const f of d.files) { const kb = Buffer.byteLength(f.text, 'utf8') / 1024; if (kb > 24) errs.push(`${rel(f.file)} nặng ${kb.toFixed(1)} KB, nên dưới 24 KB để Claude Code đọc một lần`); }
  for (const r of d.reqs.filter(r => r.auto)) if (!['table', 'tokens', 'enum', 'enum-table', 'policies', 'config', 'config-table', 'codes', 'events', 'sp', 'accounts'].includes(r.auto)) errs.push(`${r.id} có luật lạ auto:${r.auto}`);
  for (const r of d.reqs.filter(r => r.auto === 'table')) if (!parseSpecTable(r.text)) errs.push(`${r.id} không đọc được định nghĩa bảng`);
  const untag = d.files.flatMap(f => f.untagged);
  if (untag.length) errs.push(`${untag.length} gạch đầu dòng chưa gắn mã (chạy untagged)`);
  const per = []; for (let n = 1; n <= 8; n++) { const s = phaseStats(n); per.push(`P${n}: ${s.total} (${s.manual} cần bằng chứng, ${s.auto} tự động)`); }
  info.push(`Tổng ${d.reqs.length} yêu cầu có mã, ${d.ucs.length} use case, ${d.screens.size} màn hình, ${d.specTables.size} bảng.`);
  info.push(per.join(' · '));
  const paths = uniq(d.reqs.flatMap(r => extractPaths(r.text)));
  info.push(`Đường dẫn sẽ đối chiếu (${paths.length}): ${paths.join(' ')}`);
  const classes = uniq(d.reqs.flatMap(r => extractClasses(r.text)));
  info.push(`Lớp sẽ đối chiếu (${classes.length}): ${classes.join(' ')}`);
  info.forEach(x => console.log(x));
  if (errs.length) { console.log(`\n${errs.length} lỗi:`); errs.forEach(e => console.log(`- ${e}`)); process.exitCode = 1; } else console.log('\nSpec nhất quán.');
}
function cmdUntagged() {
  const list = docs().files.flatMap(f => f.untagged);
  for (const u of list) console.log(`${rel(u.file)}:${u.line} [${u.section}] ${clip(u.text, 110)}`);
  console.log(`${list.length} gạch đầu dòng chưa gắn mã.`);
}

// ---------------------------------------------------------------- hook
function readStdinJson() { try { const s = fs.readFileSync(0, 'utf8'); return s.trim() ? JSON.parse(s) : {}; } catch { return {}; } }
function transcriptHas(p, token) { if (!p || !token) return false; const t = readText(p); return !!t && t.includes(token); }
function lastAssistantText(input) {
  if (typeof input.last_assistant_message === 'string') return input.last_assistant_message;
  const t = readText(input.transcript_path || '');
  if (!t) return '';
  const L = t.trim().split(/\r?\n/).slice(-60).reverse();
  for (const line of L) {
    try {
      const o = JSON.parse(line);
      const msg = o.message || o;
      if ((o.type === 'assistant' || msg.role === 'assistant') && msg.content) return Array.isArray(msg.content) ? msg.content.map(c => c.text || '').join('\n') : String(msg.content);
    } catch { /* bỏ qua */ }
  }
  return '';
}
function cmdHookSession() {
  const input = readStdinJson();
  const st = loadState();
  const d = docs();
  const isPhaseSession = st?.token && transcriptHas(input.transcript_path, st.token);
  if (isPhaseSession && ['compact', 'resume'].includes(input.source)) {
    const N = st.phase, tr = allTrace();
    const pend = phaseItems(N).filter(i => !i.skipped && !i.auto && normStatus(tr.get(i.id)?.status) === 'Chưa');
    let s = `[FPolyCinema – nhắc lại sau khi ${input.source === 'compact' ? 'nén ngữ cảnh' : 'mở lại phiên'}]\nBạn đang làm Phase ${N} (${d.phaseNames[N] || ''}), mã phiên ${st.token}. Quy trình bắt buộc nằm ở .claude/skills/phase/SKILL.md; đọc lại file đó trước khi làm tiếp.\nCòn ${pend.length} yêu cầu ghi "Chưa" trong docs/trace/phase-${N}.md:\n`;
    for (const r of pend.slice(0, 70)) s += `- ${r.id} ${clip(r.isUC ? r.name : r.text, 80)}\n`;
    if (pend.length > 70) s += `- … và ${pend.length - 70} yêu cầu nữa\n`;
    s += `Việc tiếp theo: chạy "node tools/spec-check/check.mjs next ${N}" để đọc nguyên văn nhóm kế tiếp. Chỉ báo xong khi "verify ${N} --final" ĐẠT.`;
    console.log(s.slice(0, 9500));
    return;
  }
  const parts = [];
  for (let n = 1; n <= 8; n++) { const s = phaseStats(n); parts.push(`P${n} ${s.done}/${s.manual}${s.skipped ? ' (cắt)' : ''}`); }
  console.log(`[FPolyCinema] Tiến độ (yêu cầu đã có bằng chứng): ${parts.join(' · ')}.${st?.phase ? ` Phase gần nhất: ${st.phase}.` : ''}\nLàm phase: gõ /phase N. Làm tiếp phase đang dở: /tiep-tuc. Làm bài lab: /lab N. Kiểm tra lại một phase: /kiem-tra N.`);
}
function cmdHookStop() {
  const input = readStdinJson();
  const st = loadState();
  if (!st?.token || !transcriptHas(input.transcript_path, st.token)) return;
  const N = st.phase;
  const res = verify(N, { final: true });
  const count = res.fails.length;
  if (!count) { if (st.lastBlock) saveState({ ...st, lastBlock: null }); return; }
  if (/CẦN NGƯỜI DÙNG:/.test(lastAssistantText(input))) return;
  if (input.stop_hook_active && st.lastBlock && count >= st.lastBlock.count) return;
  saveState({ ...st, lastBlock: { count, at: new Date().toISOString() } });
  const by = {};
  for (const f of res.fails) (by[f.cat] ||= []).push(f);
  let reason = `Phase ${N} chưa đạt nên chưa được dừng. Còn ${count} lỗi: ${Object.entries(by).map(([k, v]) => `${CAT_NAME[k] || k} ${v.length}`).join(', ')}.\n`;
  for (const f of res.fails.slice(0, 25)) reason += `- ${clip(f.msg, 160)}\n`;
  if (count > 25) reason += `- … còn ${count - 25} lỗi (chạy verify ${N} --final)\n`;
  reason += `Làm tiếp theo .claude/skills/phase/SKILL.md: "node tools/spec-check/check.mjs next ${N}" cho nhóm kế tiếp, "verify ${N} --final" khi kết thúc. Nếu thật sự cần người dùng quyết định (thiếu SQL Server, spec mâu thuẫn…), viết một dòng bắt đầu bằng "CẦN NGƯỜI DÙNG:" nêu rõ câu hỏi rồi dừng.`;
  console.log(JSON.stringify({ decision: 'block', reason }));
}

// ---------------------------------------------------------------- main
function phaseArg(pos) { const n = +pos[0]; if (!(n >= 1 && n <= 8)) { console.log('Cần số phase từ 1 đến 8, ví dụ: node tools/spec-check/check.mjs begin 2'); return null; } return n; }
function main() {
  const [cmd, ...argv] = process.argv.slice(2);
  const { pos, flags } = parseArgs(argv);
  let N;
  switch (cmd) {
    case 'begin': if ((N = phaseArg(pos))) cmdBegin(N); break;
    case 'next': if ((N = phaseArg(pos))) cmdNext(N); break;
    case 'list': if ((N = phaseArg(pos))) cmdList(N, flags); break;
    case 'verify': if ((N = phaseArg(pos))) { const res = verify(N, { final: !!flags.final }); printVerify(N, res, { brief: !!flags.brief, final: !!flags.final }); process.exitCode = res.fails.length ? 1 : 0; } break;
    case 'status': cmdStatus(); break;
    case 'mark': cmdMark(pos, flags); break;
    case 'sync': { const ch = syncTraces(); console.log(ch.length ? `Đã cập nhật: ${ch.join(', ')}` : 'docs/trace đã khớp với spec.'); break; }
    case 'test': cmdTest(flags); break;
    case 'facts': cmdFacts(); break;
    case 'selftest': cmdSelftest(); break;
    case 'untagged': cmdUntagged(); break;
    case 'hook-session': try { cmdHookSession(); } catch (e) { console.error(`spec-check: ${e.message}`); } break;
    case 'hook-stop': try { cmdHookStop(); } catch (e) { console.error(`spec-check: ${e.message}`); } break;
    default: console.log(fs.readFileSync(fileURLToPath(import.meta.url), 'utf8').split('*/')[0].replace(/^#!.*\n\/\*\n?/, '').replace(/^ \* ?/gm, ''));
  }
}
main();
