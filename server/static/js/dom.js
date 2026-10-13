// Building pages without ever turning text into markup: every string a person or the database supplied (names, case ids, reasons) goes in as text, so it cannot become a tag or a script.
// There is no innerHTML, no insertAdjacentHTML, no eval and no inline handler anywhere in /static (a test scans for them).

export function h(tag, props = {}, ...children) {
  const el = document.createElement(tag);
  for (const [key, value] of Object.entries(props || {})) {
    if (value === undefined || value === null || value === false) continue;
    if (key === 'class') el.className = value;
    else if (key === 'text') el.textContent = String(value);
    else if (key.startsWith('on') && typeof value === 'function') el.addEventListener(key.slice(2), value);
    else if (key === 'value') el.value = value;
    else if (key === 'checked' || key === 'disabled' || key === 'selected') el[key] = Boolean(value);
    else el.setAttribute(key, value === true ? '' : String(value));
  }
  append(el, children);
  return el;
}

export function append(el, children) {
  for (const child of children.flat(Infinity)) {
    if (child === undefined || child === null || child === false) continue;
    el.appendChild(typeof child === 'string' || typeof child === 'number' ? document.createTextNode(String(child)) : child);
  }
  return el;
}

export function clear(el) {
  while (el.firstChild) el.removeChild(el.firstChild);
  return el;
}

export function svg(tag, attrs = {}, ...children) {
  const el = document.createElementNS('http://www.w3.org/2000/svg', tag);
  for (const [key, value] of Object.entries(attrs)) el.setAttribute(key, String(value));
  append(el, children);
  return el;
}

export function table(headers, rows, caption) {
  return h('div', { class: 'scroll' }, h('table', {},
    caption ? h('caption', { class: 'muted', text: caption }) : null,
    h('thead', {}, h('tr', {}, headers.map((t) => h('th', { scope: 'col', text: t })))),
    h('tbody', {}, rows.map((r) => h('tr', {}, r.map((c) => h('td', {}, c)))))));
}

export function when(iso) {
  if (!iso) return '';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? '' : d.toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' });
}

export function number(value, digits = 1) {
  return value === null || value === undefined ? '—' : Number(value).toFixed(digits).replace(/\.0+$/, '');
}
