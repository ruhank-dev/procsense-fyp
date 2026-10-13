// Two small charts drawn as SVG by our own code (no chart library to download, keep up to date or allow through the content-security policy). Each comes with a title and a description for screen readers,
// and every chart is shown next to a table of the same numbers.
import { svg } from './dom.js';

const W = 420, H = 220, PAD = { l: 40, r: 12, t: 14, b: 34 };

function frame(title, description, maxY) {
  const root = svg('svg', { class: 'chart', viewBox: `0 0 ${W} ${H}`, role: 'img', 'aria-label': `${title}. ${description}` }, svg('title', {}, title));
  root.appendChild(svg('line', { class: 'axis', x1: PAD.l, y1: H - PAD.b, x2: W - PAD.r, y2: H - PAD.b }));
  root.appendChild(svg('line', { class: 'axis', x1: PAD.l, y1: PAD.t, x2: PAD.l, y2: H - PAD.b }));
  for (const frac of [0, 0.5, 1]) {
    const y = H - PAD.b - frac * (H - PAD.b - PAD.t);
    root.appendChild(svg('text', { x: PAD.l - 6, y: y + 4, 'text-anchor': 'end' }, String(Math.round(frac * maxY * 10) / 10)));
  }
  return root;
}

export function barChart(title, points) {   // points: [{label, value}]
  const max = Math.max(1, ...points.map((p) => p.value));
  const root = frame(title, points.length ? `${points.length} bars, highest ${max}` : 'no data yet', max);
  const slot = (W - PAD.l - PAD.r) / Math.max(1, points.length);
  points.forEach((p, i) => {
    const height = (p.value / max) * (H - PAD.t - PAD.b);
    const x = PAD.l + i * slot + slot * 0.15;
    root.appendChild(svg('rect', { class: 'bar', x, y: H - PAD.b - height, width: slot * 0.7, height }, svg('title', {}, `${p.label}: ${p.value}`)));
    if (points.length <= 12 || i % Math.ceil(points.length / 8) === 0) root.appendChild(svg('text', { x: x + slot * 0.35, y: H - PAD.b + 14, 'text-anchor': 'middle' }, p.label));
  });
  return root;
}

export function lineChart(title, points, maxY = 100) {   // points: [{label, value}] oldest first
  const root = frame(title, points.length ? `${points.length} points, latest ${points[points.length - 1].value}` : 'no data yet', maxY);
  const step = points.length > 1 ? (W - PAD.l - PAD.r) / (points.length - 1) : 0;
  const at = (p, i) => [PAD.l + (points.length > 1 ? i * step : (W - PAD.l - PAD.r) / 2), H - PAD.b - (Math.min(p.value, maxY) / maxY) * (H - PAD.t - PAD.b)];
  if (points.length > 1) root.appendChild(svg('polyline', { class: 'line', points: points.map((p, i) => at(p, i).join(',')).join(' ') }));
  points.forEach((p, i) => {
    const [x, y] = at(p, i);
    root.appendChild(svg('circle', { class: 'dot', cx: x, cy: y, r: 3.5 }, svg('title', {}, `${p.label}: ${p.value}`)));
  });
  return root;
}
