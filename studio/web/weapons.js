const $ = id => document.getElementById(id);
const NS = 'http://www.w3.org/2000/svg';
let entries = [], selected, locked = false;
const status = (text, error = false) => { $('status').textContent = text; $('status').classList.toggle('error', error); };
async function api(url, data) {
  const response = await fetch(url, data === undefined ? {} : { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
  const body = await response.json(); if (!response.ok) throw new Error(body.error || '요청 실패'); return body;
}
function el(name, attrs = {}, parent) {
  const node = document.createElementNS(NS, name);
  for (const [key, value] of Object.entries(attrs)) node.setAttribute(key, value);
  parent?.append(node); return node;
}
const fmt = n => Number(n.toFixed(2)).toString();

// Top view: the shooter on the left, the aim to the right. Scale fits the aimed maximum range.
function drawCone(w) {
  const svg = $('cone'); svg.replaceChildren();
  const x0 = 26, y0 = 150, span = 470;
  const far = Math.max(w.cone.range * w.cone.aimRange, 1), px = span / far;
  const wedge = (deg, cls) => {
    const half = Math.min(deg, 120) * Math.PI / 360, len = span;
    const dy = Math.tan(half) * len;
    el('path', { d: `M${x0},${y0} L${x0 + len},${y0 - dy} L${x0 + len},${y0 + dy} Z`, class: cls }, svg);
  };
  if (w.melee) { el('text', { x: 260, y: 150, class: 'note', 'text-anchor': 'middle' }, svg).textContent = '근접 무기: 퍼짐 · 반동 없음'; return; }
  wedge(w.cone.hip + w.cone.move, 'move'); wedge(w.cone.hip, 'hip'); wedge(w.cone.aim, 'aim');
  const mark = (meters, cls, label, row) => {
    const x = x0 + meters * px;
    el('line', { x1: x, y1: 20, x2: x, y2: 280, class: cls }, svg);
    const end = x > 400;
    el('text', { x: end ? x - 4 : x + 4, y: 32 + row * 14, class: 'label', 'text-anchor': end ? 'end' : 'start' }, svg).textContent = `${label} ${fmt(meters)} m`;
  };
  mark(w.cone.effective, 'eff', '유효', 0); mark(w.cone.range, 'max', '최대', 1);
  if (w.cone.aimRange > 1) { mark(w.cone.effective * w.cone.aimRange, 'eff aimed', '조준 유효', 2); mark(w.cone.range * w.cone.aimRange, 'max aimed', '조준 최대', 3); }
  el('circle', { cx: x0, cy: y0, r: 6, class: 'shooter' }, svg);
}

// The aim point after each shot, in pixels of a 1080-high screen (up is up the screen).
function drawTrace(w) {
  const svg = $('trace'); svg.replaceChildren();
  const points = w.recoil.trace;
  $('trace-note').textContent = points.length ? `${points.length}발 연사 · 수직 ${fmt(w.recoil.up)} / 수평 ±${fmt(w.recoil.side)} px` : '';
  if (!points.length || (w.recoil.up === 0 && w.recoil.side === 0)) {
    el('text', { x: 150, y: 150, class: 'note', 'text-anchor': 'middle' }, svg).textContent = '반동 없음'; return;
  }
  const reach = Math.max(100, ...points.map(([x, y]) => Math.max(Math.abs(x) * 2, -y)));
  const s = 250 / reach, cx = 150, cy = 275;
  for (const r of [100, 200, 300]) if (r <= reach * 1.05) el('circle', { cx, cy, r: r * s, class: 'ring' }, svg);
  el('line', { x1: cx, y1: 10, x2: cx, y2: 290, class: 'axis' }, svg);
  el('polyline', { points: [[0, 0], ...points].map(([x, y]) => `${cx + x * s},${cy + y * s}`).join(' '), class: 'path' }, svg);
  points.forEach(([x, y], i) => el('circle', { cx: cx + x * s, cy: cy + y * s, r: i === 0 ? 4 : 2.5, class: i === 0 ? 'first' : 'shot' }, svg));
  el('circle', { cx, cy, r: 4, class: 'origin' }, svg);
}

function select(id) {
  selected = entries.find(e => e.id === id);
  if (!selected) return;
  history.replaceState(null, '', `${location.pathname}?id=${encodeURIComponent(id)}`);
  $('item-name').textContent = selected.name; $('item-id').textContent = selected.id;
  $('item-category').textContent = selected.category; $('summary').textContent = selected.summary;
  $('source').textContent = selected.source;
  drawCone(selected); drawTrace(selected);
  $('groups').replaceChildren();
  const groups = new Map();
  for (const row of selected.rows) { if (!groups.has(row.group)) groups.set(row.group, []); groups.get(row.group).push(row); }
  for (const [name, rows] of groups) {
    const section = document.createElement('section'), title = document.createElement('h3'), table = document.createElement('table'), body = document.createElement('tbody');
    section.className = 'properties'; title.textContent = name;
    for (const row of rows) {
      const tr = document.createElement('tr'), th = document.createElement('th'), td = document.createElement('td');
      th.scope = 'row'; th.textContent = row.label; td.textContent = row.value; tr.append(th, td); body.append(tr);
    }
    table.append(body); section.append(title, table); $('groups').append(section);
  }
  for (const button of $('entries').children) button.setAttribute('aria-pressed', String(button.dataset.id === id));
  status('현재 로컬 데이터 · 조회 전용');
}
function list() {
  const query = $('search').value.trim().toLocaleLowerCase(), category = $('category').value;
  const matches = entries.filter(e => (!category || e.category === category) && (!query || `${e.name} ${e.id} ${e.category} ${e.type}`.toLocaleLowerCase().includes(query)));
  $('result-count').textContent = `${matches.length} / ${entries.length}개`;
  $('entries').replaceChildren();
  for (const entry of matches) {
    const button = document.createElement('button'), name = document.createElement('strong'), id = document.createElement('span');
    button.dataset.id = entry.id; button.setAttribute('aria-pressed', String(selected?.id === entry.id));
    name.textContent = entry.name; id.textContent = `${entry.id} · ${entry.category}`; button.append(name, id);
    button.addEventListener('click', () => select(entry.id)); $('entries').append(button);
  }
  if (!matches.length) { const empty = document.createElement('p'); empty.className = 'empty'; empty.textContent = '검색 결과가 없습니다.'; $('entries').append(empty); }
}
async function load() {
  const data = await api('/api/weapons'); entries = data.weapons;
  $('warnings').hidden = !data.warnings.length; $('warnings').textContent = data.warnings.join('\n');
  $('total-count').textContent = `${entries.length}개 · 현재 로컬 데이터`;
  $('category').replaceChildren(new Option('모든 분류', ''));
  for (const category of [...new Set(entries.map(e => e.category))]) $('category').add(new Option(category, category));
  $('loaded-at').textContent = `로컬 데이터 로드 ${new Date(data.loadedAt).toLocaleTimeString('ko-KR')}`;
  list();
  const id = selected?.id || new URLSearchParams(location.search).get('id');
  select(entries.some(e => e.id === id) ? id : entries[0]?.id);
}
$('search').addEventListener('input', list); $('category').addEventListener('change', list);
$('reload').addEventListener('click', async () => {
  if (locked) return;
  locked = true; $('reload').disabled = true; document.querySelector('.library-layout').inert = true;
  status('현재 게임을 다시 빌드하고 데이터를 읽습니다…');
  try { await api('/api/reload', {}); await load(); }
  catch (error) { status(error.message, true); }
  finally { locked = false; $('reload').disabled = false; document.querySelector('.library-layout').inert = false; }
});
load().catch(error => status(error.message, true));
