// Sinh SVG ERD mức 1 gọn (Hình 2, Lab 2). Chạy: node gen.mjs > erd.svg
const W = 150, H = 36;
const G = {
  P1: { name: 'P1 Tài khoản và nhân sự', fill: '#dbeafe', stroke: '#3b82f6', bg: '#f3f8ff' },
  P2: { name: 'P2 Bắp nước và kho', fill: '#fdecc8', stroke: '#d4a017', bg: '#fffaf0' },
  P3: { name: 'P3 Phim và nhà phát hành', fill: '#e9ddfb', stroke: '#8b5cf6', bg: '#faf7ff' },
  P4: { name: 'P4 Rạp, phòng, ghế, suất chiếu, giá', fill: '#d5f2e3', stroke: '#22a06b', bg: '#f4fbf7' },
  P5: { name: 'P5 Bán hàng và thanh toán', fill: '#fbd9d9', stroke: '#e05252', bg: '#fff7f7' },
  P6: { name: 'P6 Khách hàng thân thiết', fill: '#fbdcef', stroke: '#d6409f', bg: '#fff6fb' },
  P7: { name: 'P7 Vận hành và phê duyệt', fill: '#dfe4ec', stroke: '#64748b', bg: '#f7f8fb' },
  P8: { name: 'P8 Hệ thống', fill: '#e8e8e8', stroke: '#8a8a8a', bg: '#fafafa' },
};
// Khung phân hệ: x, y, w, h
const F = {
  P3: [20, 40, 430, 360], P4: [470, 40, 900, 360], P1: [1390, 40, 450, 360],
  P2: [20, 420, 430, 660], P5: [470, 420, 900, 480], P7: [1390, 420, 450, 480],
  P6: [470, 920, 900, 160], P8: [1390, 920, 450, 160],
};
// Thực thể: [x, y, nhóm, w?, h?, proxy?]; proxy = bảng tham chiếu (nét đứt)
const E = {
  // P3
  KdmLicenses: [285, 102, 'P3'], FilmRentalRules: [45, 210, 'P3'], Distributors: [45, 320, 'P3'], Movies: [285, 320, 'P3'],
  // P4
  Cinemas: [790, 102, 'P4'], PricingRules: [540, 210, 'P4'], Auditoriums: [790, 210, 'P4'],
  Holidays: [1190, 150, 'P4'], AudienceDiscounts: [1190, 210, 'P4'],
  Showtimes: [540, 320, 'P4'], ShowtimeSeats: [790, 320, 'P4'], Seats: [1040, 320, 'P4'],
  // P1
  Users: [1440, 100, 'P1', 180, 40], PasswordResetOtps: [1455, 250, 'P1'],
  // P2
  'Cinemas@P2': [45, 465, 'P4', W, H, true], 'Users@P2': [285, 465, 'P1', W, H, true],
  WastageLogs: [45, 582, 'P2'], InventoryTransactions: [285, 582, 'P2', 160],
  WastageLogItems: [45, 700, 'P2'], Drinks: [285, 700, 'P2'],
  RawMaterials: [45, 820, 'P2'], Categories: [320, 820, 'P2', 120],
  ProductRecipes: [45, 940, 'P2'],
  // P5
  Tickets: [1000, 470, 'P5'], 'Users@P5': [1200, 452, 'P1', W, H, true],
  Bills: [700, 560, 'P5', 220, 120],
  BillDetails: [485, 700, 'P5'], Payments: [1200, 700, 'P5'],
  'Cinemas@P5': [485, 770, 'P4', W, H, true], Vouchers: [840, 770, 'P5'], 'Users@P5b': [1080, 770, 'P1', W, H, true],
  'CorporateBookings@P5': [825, 850, 'P7', 180, H, true],
  // P7
  SupervisorApprovals: [1580, 470, 'P7', 160], CorporateBookings: [1580, 590, 'P7', 160, 56],
  'Showtimes@P7': [1410, 660, 'P4', W, H, true], PosShifts: [1580, 700, 'P7', 160],
  'Cinemas@P7': [1410, 760, 'P4', W, H, true], Notifications: [1580, 830, 'P7', 160],
  // P6
  WalletTransactions: [705, 1000, 'P6'], PointTransactions: [925, 1000, 'P6'], 'Users@P6': [1150, 1000, 'P1', W, H, true],
  // P8
  'Users@P8': [1415, 975, 'P1', W, H, true], AuditLogs: [1640, 975, 'P8'], EmailOutbox: [1415, 1035, 'P8'], SchemaVersions: [1640, 1035, 'P8'],
};

// Quan hệ: [cha, con, bản số phía cha ('1' | '01'), bản số phía con ('n' | '01'), điểm gấp...]
const R = [];
const rel = (p, c, pc, cc, pts, jumps = []) => R.push({ p, c, pc, cc, pts, jumps });

// P3
rel('Distributors', 'Movies', '01', 'n', [[195, 338], [285, 338]]);
rel('Movies', 'KdmLicenses', '1', 'n', [[370, 320], [370, 138]]);
rel('Movies', 'FilmRentalRules', '1', 'n', [[310, 320], [310, 228], [195, 228]]);
rel('Movies', 'Showtimes', '1', 'n', [[435, 338], [540, 338]]);
rel('Cinemas', 'KdmLicenses', '1', 'n', [[790, 114], [435, 114]]);
// P4
rel('Cinemas', 'PricingRules', '1', 'n', [[790, 128], [615, 128], [615, 210]]);
rel('Cinemas', 'Auditoriums', '1', 'n', [[865, 138], [865, 210]]);
rel('Cinemas', 'Users', '01', 'n', [[940, 120], [1440, 120]]);
rel('Auditoriums', 'Showtimes', '1', 'n', [[820, 246], [820, 282], [660, 282], [660, 320]]);
rel('Auditoriums', 'Seats', '1', 'n', [[910, 246], [910, 282], [1115, 282], [1115, 320]]);
rel('Showtimes', 'ShowtimeSeats', '1', 'n', [[690, 338], [790, 338]]);
rel('Seats', 'ShowtimeSeats', '1', 'n', [[1040, 338], [940, 338]]);
// P1
rel('Users', 'PasswordResetOtps', '1', 'n', [[1530, 140], [1530, 250]]);
// P4 -> P5
rel('Showtimes', 'Tickets', '1', 'n', [[670, 356], [670, 488], [1000, 488]]);
rel('Showtimes', 'Bills', '01', 'n', [[600, 356], [600, 530], [730, 530], [730, 560]]);
rel('Seats', 'Tickets', '1', 'n', [[1080, 356], [1080, 470]]);
rel('Tickets', 'ShowtimeSeats', '01', 'n', [[1020, 470], [1020, 420], [900, 420], [900, 356]]);
rel('Bills', 'ShowtimeSeats', '01', 'n', [[850, 560], [850, 356]], [[850, 488]]);
rel('Tickets', 'Tickets', '01', 'n', [[1150, 498], [1175, 498], [1175, 532], [1120, 532], [1120, 506]]);
// P5
rel('Bills', 'Tickets', '1', 'n', [[900, 560], [900, 548], [1060, 548], [1060, 506]]);
rel('Users@P5', 'Tickets', '01', 'n', [[1200, 480], [1150, 480]]);
rel('Users@P5', 'Bills', '01', 'n', [[1230, 488], [1230, 574], [920, 574]]);
rel('Bills', 'InventoryTransactions', '01', 'n', [[700, 600], [445, 600]]);
rel('Bills', 'BillDetails', '1', 'n', [[700, 650], [560, 650], [560, 700]]);
rel('Cinemas@P5', 'Bills', '1', 'n', [[635, 788], [730, 788], [730, 680]]);
rel('Vouchers', 'Bills', '01', 'n', [[870, 770], [870, 680]]);
rel('Bills', 'Payments', '1', 'n', [[905, 680], [905, 718], [1200, 718]]);
rel('Users@P5b', 'Vouchers', '01', 'n', [[1080, 788], [990, 788]]);
rel('CorporateBookings@P5', 'Vouchers', '01', 'n', [[915, 850], [915, 806]]);
rel('Bills', 'WalletTransactions', '01', 'n', [[780, 680], [780, 1000]]);
rel('Bills', 'PointTransactions', '01', 'n', [[820, 680], [820, 965], [1000, 965], [1000, 1000]]);
// P5 <-> P7
rel('Bills', 'SupervisorApprovals', '1', 'n', [[920, 590], [1540, 590], [1540, 488], [1580, 488]]);
rel('CorporateBookings', 'Bills', '01', 'n', [[1580, 608], [920, 608]]);
rel('Bills', 'CorporateBookings', '01', 'n', [[920, 626], [1580, 626]]);
rel('PosShifts', 'Bills', '01', 'n', [[1580, 710], [1380, 710], [1380, 650], [920, 650]]);
rel('PosShifts', 'Payments', '01', 'n', [[1580, 726], [1350, 726]]);
rel('Showtimes@P7', 'CorporateBookings', '01', 'n', [[1560, 678], [1640, 678], [1640, 646]]);
rel('Cinemas@P7', 'PosShifts', '1', 'n', [[1560, 778], [1660, 778], [1660, 736]]);
// Users -> P7 (bó dây bên phải)
rel('Users', 'Notifications', '1', 'n', [[1620, 106], [1820, 106], [1820, 848], [1740, 848]]);
rel('Users', 'PosShifts', '1', '01', [[1620, 116], [1805, 116], [1805, 718], [1740, 718]]);
rel('Users', 'CorporateBookings', '1', 'n', [[1620, 126], [1790, 126], [1790, 618], [1740, 618]]);
rel('Users', 'SupervisorApprovals', '1', 'n', [[1620, 136], [1775, 136], [1775, 488], [1740, 488]]);
// P2
rel('Cinemas@P2', 'WastageLogs', '1', 'n', [[120, 501], [120, 582]]);
rel('Cinemas@P2', 'RawMaterials', '1', 'n', [[45, 483], [32, 483], [32, 838], [45, 838]]);
rel('Users@P2', 'WastageLogs', '1', 'n', [[285, 490], [235, 490], [235, 592], [195, 592]]);
rel('Users@P2', 'InventoryTransactions', '01', 'n', [[360, 501], [360, 582]]);
rel('WastageLogs', 'InventoryTransactions', '01', 'n', [[195, 608], [285, 608]]);
rel('WastageLogs', 'WastageLogItems', '1', 'n', [[120, 618], [120, 700]]);
rel('RawMaterials', 'WastageLogItems', '1', 'n', [[120, 820], [120, 736]]);
rel('RawMaterials', 'ProductRecipes', '1', 'n', [[120, 856], [120, 940]]);
rel('RawMaterials', 'InventoryTransactions', '1', 'n', [[195, 830], [250, 830], [250, 655], [300, 655], [300, 618]]);
rel('Categories', 'Drinks', '1', 'n', [[400, 820], [400, 736]]);
rel('Drinks', 'ProductRecipes', '1', 'n', [[300, 736], [300, 958], [195, 958]]);
rel('Drinks', 'BillDetails', '1', 'n', [[435, 718], [485, 718]]);
// P6
rel('Users@P6', 'PointTransactions', '1', 'n', [[1150, 1018], [1075, 1018]]);
rel('Users@P6', 'WalletTransactions', '1', 'n', [[1225, 1036], [1225, 1058], [740, 1058], [740, 1036]]);
// P8
rel('Users@P8', 'AuditLogs', '01', 'n', [[1565, 993], [1640, 993]]);

// ---- vẽ ----
const out = [];
const S = s => out.push(s);
const totalW = 1860, totalH = 1140;
S(`<svg xmlns="http://www.w3.org/2000/svg" width="${totalW}" height="${totalH}" viewBox="0 0 ${totalW} ${totalH}" font-family="Segoe UI, Arial, sans-serif">`);
S(`<rect width="100%" height="100%" fill="#fff"/>`);
for (const [k, [x, y, w, h]] of Object.entries(F)) {
  const g = G[k];
  S(`<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="10" fill="${g.bg}" stroke="${g.stroke}" stroke-width="1.5" stroke-dasharray="6 4"/>`);
  S(`<text x="${x + 12}" y="${y + 22}" font-size="15" font-weight="700" fill="${g.stroke}">${g.name}</text>`);
}
// đường
const dirOf = (a, b) => (a[0] === b[0] ? (b[1] > a[1] ? [0, 1] : [0, -1]) : (b[0] > a[0] ? [1, 0] : [-1, 0]));
function pathD(pts, jumps) {
  let d = `M${pts[0][0]},${pts[0][1]}`;
  for (let i = 1; i < pts.length; i++) {
    const a = pts[i - 1], b = pts[i];
    const js = jumps.filter(([jx, jy]) => (a[0] === b[0] && jx === a[0] && (jy - a[1]) * (jy - b[1]) < 0) || (a[1] === b[1] && jy === a[1] && (jx - a[0]) * (jx - b[0]) < 0));
    const [dx, dy] = dirOf(a, b);
    js.sort((p, q) => ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) - ((q[0] - a[0]) * dx + (q[1] - a[1]) * dy));
    for (const [jx, jy] of js) {
      const r = 6;
      d += ` L${jx - dx * r},${jy - dy * r}`;
      const sweep = (dx === 1 || dy === -1) ? 1 : 0;
      d += ` A${r},${r} 0 0 ${sweep} ${jx + dx * r},${jy + dy * r}`;
    }
    d += ` L${b[0]},${b[1]}`;
  }
  return d;
}
// ký hiệu chân chim tại điểm p, hướng u (từ ngoài đi vào bảng)
function mark(p, u, kind) {
  const [x, y] = p, [ux, uy] = u, nx = -uy, ny = ux; // pháp tuyến
  const at = (t, s = 0) => [x - ux * t + nx * s, y - uy * t + ny * s];
  const line = (a, b) => `<line x1="${a[0]}" y1="${a[1]}" x2="${b[0]}" y2="${b[1]}"/>`;
  const circ = t => { const c = at(t); return `<circle cx="${c[0]}" cy="${c[1]}" r="4" fill="#fff"/>`; };
  let s = '';
  if (kind === '1') s += line(at(6, -6), at(6, 6)) + line(at(11, -6), at(11, 6));
  if (kind === '01') s += line(at(6, -6), at(6, 6)) + circ(15);
  if (kind === 'n') s += line(at(12), at(0, -7)) + line(at(12), at(0, 7)) + line(at(12), at(0, 0)) + circ(17);
  if (kind === 'c01') s += line(at(6, -6), at(6, 6)) + circ(15);
  return s;
}
S(`<g fill="none" stroke="#3a3a3a" stroke-width="1.3">`);
for (const r of R) {
  S(`<path d="${pathD(r.pts, r.jumps)}"/>`);
  const p = r.pts, n = p.length;
  // hướng đi vào bảng cha: ngược chiều đoạn đầu
  const d0 = dirOf(p[1], p[0]);
  const d1 = dirOf(p[n - 2], p[n - 1]);
  S(mark(p[0], d0, r.pc));
  S(mark(p[n - 1], d1, r.cc === '01' ? 'c01' : 'n'));
}
S(`</g>`);
// bảng
for (const n of Object.keys(E)) {
  const [x, y, gk, w = W, h = H, proxy] = E[n];
  const g = G[gk];
  const label = n.split('@')[0];
  if (proxy) {
    S(`<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="6" fill="#fff" stroke="${g.stroke}" stroke-width="1.4" stroke-dasharray="5 3"/>`);
    S(`<text x="${x + w / 2}" y="${y + h / 2 + 5}" text-anchor="middle" font-size="13.5" font-style="italic" fill="#444">${label} (${gk})</text>`);
  } else {
    const big = n === 'Users' || n === 'Bills';
    S(`<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="6" fill="${g.fill}" stroke="${g.stroke}" stroke-width="${big ? 2.2 : 1.5}"/>`);
    S(`<text x="${x + w / 2}" y="${y + h / 2 + 5}" text-anchor="middle" font-size="${big ? 16 : 14}" font-weight="${big ? 700 : 600}" fill="#1f2937">${label}</text>`);
  }
}
// chú giải
const ly = 1100;
S(`<g font-size="13" fill="#333">`);
S(`<rect x="20" y="${ly}" width="120" height="26" rx="5" fill="#fff" stroke="#666" stroke-dasharray="5 3"/><text x="80" y="${ly + 18}" text-anchor="middle" font-style="italic">Users (P1)</text>`);
S(`<text x="150" y="${ly + 18}">Bảng tham chiếu: bảng chính nằm ở phân hệ ghi trong ngoặc, vẽ lại để đường nối ngắn.</text>`);
// ký hiệu bản số
const lx = 860;
S(`<g fill="none" stroke="#3a3a3a" stroke-width="1.3"><line x1="${lx}" y1="${ly + 13}" x2="${lx + 50}" y2="${ly + 13}"/>${mark([lx, ly + 13], [-1, 0], '1')}${mark([lx + 50, ly + 13], [1, 0], 'n')}</g>`);
S(`<text x="${lx + 60}" y="${ly + 18}">1 – 0..n</text>`);
const lx2 = lx + 140;
S(`<g fill="none" stroke="#3a3a3a" stroke-width="1.3"><line x1="${lx2}" y1="${ly + 13}" x2="${lx2 + 50}" y2="${ly + 13}"/>${mark([lx2, ly + 13], [-1, 0], '01')}${mark([lx2 + 50, ly + 13], [1, 0], 'n')}</g>`);
S(`<text x="${lx2 + 60}" y="${ly + 18}">0..1 – 0..n (khóa ngoại cho phép NULL)</text>`);
S(`<text x="${lx2 + 330}" y="${ly + 18}">Chân chim ở phía bảng con.</text>`);
S(`</g>`);
S(`</svg>`);
console.error("edges",R.length,"proxies",Object.keys(E).filter(k=>k.includes("@")).length,"entities",Object.keys(E).filter(k=>!k.includes("@")).length);console.log(out.join('\n'));
