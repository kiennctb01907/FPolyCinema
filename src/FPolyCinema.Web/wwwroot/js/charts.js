/* charts.js – vẽ các biểu đồ khai báo bằng <script type="application/json" data-chart="id"> (UI U3-04).
   Màu lấy từ token CSS (--primary, --warning, --success, --info, --border, --text-muted), lưới mờ, tooltip định dạng tiền Việt "125.000 ₫",
   không hiệu ứng 3D; tôn trọng prefers-reduced-motion. */
(function () {
  'use strict';
  if (!window.Chart) return;
  const css = getComputedStyle(document.documentElement);
  const token = (name) => css.getPropertyValue('--' + name).trim();
  const money = (n) => Number(n).toLocaleString('vi-VN') + ' ₫';
  const short = (n) => {
    const a = Math.abs(n);
    if (a >= 1e9) return (n / 1e9).toLocaleString('vi-VN', { maximumFractionDigits: 1 }) + ' tỷ';
    if (a >= 1e6) return (n / 1e6).toLocaleString('vi-VN', { maximumFractionDigits: 1 }) + ' tr';
    if (a >= 1e3) return (n / 1e3).toLocaleString('vi-VN', { maximumFractionDigits: 0 }) + 'k';
    return n.toLocaleString('vi-VN');
  };
  const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  Chart.defaults.font.family = token('font-sans') || 'inherit';
  Chart.defaults.color = token('text-muted');
  Chart.defaults.borderColor = token('border');

  document.querySelectorAll('script[data-chart]').forEach((el) => {
    const cfg = JSON.parse(el.textContent);
    const canvas = document.getElementById(el.dataset.chart);
    if (!canvas) return;
    const fmt = (v) => (cfg.money ? money(v) : Number(v).toLocaleString('vi-VN'));
    const datasets = cfg.series.map((s) => {
      const color = token(s.tone) || token('primary');
      return {
        label: s.label,
        data: s.data,
        borderColor: color,
        backgroundColor: cfg.type === 'line' ? 'transparent' : color,
        borderWidth: 2,
        pointRadius: cfg.type === 'line' ? 2 : 0,
        tension: 0.25,
        borderRadius: cfg.type === 'bar' ? 4 : 0,
        maxBarThickness: 28,
      };
    });
    const valueAxis = { grid: { color: token('border') }, ticks: { callback: (v) => (cfg.money ? short(v) : Number(v).toLocaleString('vi-VN')) }, beginAtZero: true };
    // Nhãn danh mục dài (tên phim, tên món) rút gọn để không bị cắt; tooltip vẫn hiện đủ tên
    const shortLabel = (l) => (String(l).length > 18 ? String(l).slice(0, 17) + '…' : l);
    const catAxis = { grid: { display: false }, ticks: { autoSkip: true, maxRotation: 0, callback(v) { return shortLabel(this.getLabelForValue(v)); } } };
    // eslint-disable-next-line no-new
    new Chart(canvas, {
      type: cfg.type,
      data: { labels: cfg.labels, datasets },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: reduce ? false : { duration: 300 },
        indexAxis: cfg.horizontal ? 'y' : 'x',
        interaction: { mode: 'index', intersect: false },
        plugins: {
          legend: { display: datasets.length > 1, position: 'bottom', labels: { boxWidth: 12, usePointStyle: true } },
          tooltip: { callbacks: { label: (ctx) => `${ctx.dataset.label}: ${fmt(ctx.parsed[cfg.horizontal ? 'x' : 'y'])}` } },
        },
        scales: cfg.horizontal ? { x: valueAxis, y: catAxis } : { x: catAxis, y: valueAxis },
      },
    });
  });
})();
