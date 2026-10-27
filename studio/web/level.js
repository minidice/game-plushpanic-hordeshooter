// Studio 기획서 › 레벨디자인: pick a stage layout, read its zones' features (level guide map, packs, devices) and look at
// its floor plan, drawn fresh by the server from the working folder (studio/levels.mjs).
const $ = id => document.getElementById(id);
const esc = s => String(s ?? '').replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
let levels = [], current = null, tab = 'features', zoom = 1, fitted = false;
const status = (text, error = false) => { $('status').textContent = text; $('status').classList.toggle('error', error); };
async function api(url) {
  const response = await fetch(url); const body = await response.json();
  if (!response.ok) throw new Error(body.error || '요청 실패'); return body;
}
const levelTitle = l => `Stage ${l.stage}${l.name ? ' · ' + l.name : ''}`;

function showMaps() {
  $('maps').innerHTML = levels.map(l => `<button data-id="${esc(l.id)}" aria-pressed="${current?.id === l.id}">
    <strong>${esc(levelTitle(l))}</strong><span>${esc(l.id)} · 구역 ${l.zones}(큰 ${l.large}) · 주 경로 ${l.route} m · ${l.guides.length ? '가이드 ' + l.guides.map(g => g.label).join(' · ') : '가이드 없음'}</span></button>`).join('');
  $('total-count').textContent = `맵 ${levels.length}개`;
}

// The catalog's long names carry an explanation in brackets ("조명(빛으로 이끎)"): show the short part, keep all as a tooltip.
function pills(ids, names, cls = '') {
  return (ids || []).map(id => {
    const name = names?.[id] || id;
    return `<span class="pill ${cls}" title="${esc(name)} · ${esc(id)}">${esc(name.replace(/\s*\(.*\)$/, ''))}</span>`;
  }).join('');
}

function zoneCommand(stage, zone) { return `.\\game\\Start-Game.ps1 -Zone ${stage > 1 ? stage + ':' : ''}${zone}`; }

function showFeatures(map) {
  const c = map.catalog || { experiences: {}, roles: {}, wayfinding: {} };
  const guide = map.guide;
  const chips = [
    `구역 ${map.zones.length}(큰 ${map.zones.filter(z => z.size === 'large').length})`, `주 경로 ${map.route} m`,
    map.theme && `테마 ${map.theme}`, map.next ? `다음 Stage ${map.next}` : '마지막 스테이지',
    guide ? `가이드: ${guide.kind === 'plan' ? '설계안' : '현재 상태'}${guide.approved ? ' · 확정 ' + guide.approved : ''}${guide.applied ? ' · 반영 ' + guide.applied : ''}` : '가이드 맵 없음',
  ].filter(Boolean);
  const check = map.check ? `<div class="check"><strong>가이드 검사 (tools/level-guide/guide.py check)</strong>${map.check.lines.map(l =>
    `<div class="${l.startsWith('오류') ? 'err' : l.startsWith('경고') ? 'warn' : l.startsWith('GUIDE_CHECK') ? 'ok' : ''}">${esc(l)}</div>`).join('') || '<div>이 스테이지 줄 없음</div>'}</div>`
    : `<div class="check"><strong>가이드 맵 없음</strong><div>이 맵은 아직 구역별 경험 기록(가이드 맵)이 없습니다. 무리 · 장치는 맵 데이터에서 읽은 것입니다 — doc/features/level/guide-map.md 1장 순서로 만든 뒤 여기 표시됩니다.</div></div>`;
  const rows = map.zones.map(z => {
    const g = z.guide || {};
    // Packs by reaction: how many packs and enemies; named specials on their own.
    const byReaction = new Map();
    for (const p of z.packs.filter(p => !p.special)) {
      const sum = byReaction.get(p.reaction) || { packs: 0, count: 0 };
      byReaction.set(p.reaction, { packs: sum.packs + 1, count: sum.count + p.count });
    }
    const packs = [...byReaction].map(([reaction, s]) => `<span class="pill" title="무리 ${s.packs}개">${esc(reaction)} ${s.packs}무리 · ${s.count}</span>`).join('')
      + z.packs.filter(p => p.special).map(p => `<span class="pill special">특수 적(${esc(p.reaction)})</span>`).join('');
    const total = z.packs.reduce((s, p) => s + (p.special ? 0 : p.count), 0);
    const devices = z.devices.map(d => `<span class="pill device">${esc(d.type)}${d.label ? ': ' + esc(d.label) : ''}</span>`).join('');
    const mix = (g.mix || []).map(m => `<span class="pill mix">${m.map(id => esc(c.experiences[id] || id)).join(' + ')}</span>`).join('');
    return `<tr class="${z.size}">
      <td><span class="zone-id">${esc(z.id)}</span><span class="zone-label">${esc(z.label)}</span><span class="zone-label">${z.size === 'large' ? '큰 구역' : '작은 구역'}${z.branch ? ' · 곁길' : ''}</span></td>
      <td>${g.role ? `<span class="pill role">${esc(c.roles[g.role] || g.role)}</span>` : '—'}</td>
      <td>${pills(g.new, c.experiences, 'new') || '—'}</td>
      <td>${pills(g.reuse, c.experiences) || '—'}</td>
      <td>${mix || '—'}</td>
      <td>${pills(g.wayfinding, c.wayfinding) || '—'}</td>
      <td>${packs || '—'}${total ? `<span class="zone-label">일반 ${total}마리</span>` : ''}</td>
      <td>${devices || '—'}</td>
      <td><button class="zone-test" data-command="${esc(zoneCommand(map.stage, z.id))}" title="눌러서 명령 복사">-Zone ${esc(map.stage > 1 ? map.stage + ':' : '')}${esc(z.id)}</button></td>
    </tr>${g.note ? `<tr class="${z.size} note-row"><td></td><td colspan="8">${esc(g.note)}</td></tr>` : ''}`;
  }).join('');
  $('features').innerHTML = `<div class="chips">${chips.map(t => `<span>${esc(t)}</span>`).join('')}</div>${check}
    <div class="zone-table-wrap"><table class="zone-table"><colgroup>${['zone', 'role', 'new', 'reuse', 'mix', 'way', 'packs', 'devices', 'test'].map(c => `<col class="col-${c}">`).join('')}</colgroup><thead><tr><th>구역</th><th>역할</th><th>새 경험</th><th>다시 쓰기</th><th>섞기</th><th>길 안내</th><th>무리</th><th>장치 · 표식</th><th>구역 테스트</th></tr></thead><tbody>${rows}</tbody></table></div>`;
}

function applyZoom() {
  const img = $('floor-img');
  if (!img.naturalWidth) return;
  img.style.width = Math.round(img.naturalWidth * zoom) + 'px';
  $('zoom-value').textContent = Math.round(zoom * 100) + '%';
}
function fit() {
  const img = $('floor-img');
  if (!img.naturalWidth) return;
  zoom = Math.max(.2, Math.min(4, ($('floor-view').clientWidth - 4) / img.naturalWidth));
  applyZoom();
}

function loadFloor() {
  if (!current) return;
  const guide = $('guide').value;
  const url = `/api/levels/floor?id=${encodeURIComponent(current.id)}&guide=${encodeURIComponent(guide)}&t=${Date.now()}`;
  $('floor-open').href = url;
  $('floor-note').textContent = '평면도를 그리는 중…';
  $('floor-note').hidden = false;
  fitted = false;
  $('floor-img').src = url;
}

function showTab(next) {
  tab = next;
  for (const [name, panel] of [['features', 'features'], ['floor', 'floor']]) {
    const on = name === tab;
    $('tab-' + name).classList.toggle('active', on);
    $('tab-' + name).setAttribute('aria-selected', on);
    $(panel).hidden = !on;
  }
  if (tab === 'floor' && current && !$('floor-img').src) loadFloor();
  if (tab === 'floor' && !fitted) fit();
  if (current) history.replaceState(null, '', `#${current.id}${tab === 'floor' ? '/floor' : ''}`);
}

async function openMap(id) {
  status('맵을 읽는 중…');
  const map = await api('/api/levels/map?id=' + encodeURIComponent(id));
  current = map;
  const level = levels.find(l => l.id === id) || map;
  $('map-eyebrow').textContent = `STAGE ${map.stage}`;
  $('map-title').textContent = levelTitle(level);
  $('map-facts').textContent = `크기 ${map.bounds ? `${map.bounds[2] - map.bounds[0]} × ${map.bounds[3] - map.bounds[1]} m` : '?'} · 주 경로 ${map.route} m`;
  $('map-file').textContent = map.file;
  $('guide').innerHTML = map.guides.map(g => `<option value="${g.kind}">${esc(g.label)}</option>`).join('') + '<option value="none">가이드 없이</option><option value="space">공간 검사(벽 · 빈 곳)</option>';
  $('floor-img').removeAttribute('src');
  showMaps();
  showFeatures(map);
  showTab(tab);
  status(`${levelTitle(level)} — 구역 ${map.zones.length}개`);
}

async function load() {
  status('맵 목록을 읽는 중…');
  const body = await api('/api/levels');
  levels = body.levels;
  $('loaded-at').textContent = '읽은 시각 ' + new Date(body.loadedAt).toLocaleTimeString('ko');
  showMaps();
  const [hashId, hashTab] = decodeURIComponent(location.hash.slice(1)).split('/');
  if (hashTab === 'floor') tab = 'floor';
  const pick = levels.find(l => l.id === (current?.id || hashId)) || levels[0];
  if (pick) await openMap(pick.id); else status('맵이 없습니다', true);
}

$('maps').addEventListener('click', e => {
  const button = e.target.closest('button[data-id]');
  if (button) openMap(button.dataset.id).catch(error => status(error.message, true));
});
$('features').addEventListener('click', async e => {
  const button = e.target.closest('button[data-command]');
  if (!button) return;
  try { await navigator.clipboard.writeText(button.dataset.command); status('복사했습니다: ' + button.dataset.command); }
  catch { status('복사하지 못했습니다. 직접 입력하세요: ' + button.dataset.command, true); }
});
$('tab-features').addEventListener('click', () => showTab('features'));
$('tab-floor').addEventListener('click', () => showTab('floor'));
$('guide').addEventListener('change', loadFloor);
$('zoom-in').addEventListener('click', () => { zoom = Math.min(4, zoom * 1.25); applyZoom(); });
$('zoom-out').addEventListener('click', () => { zoom = Math.max(.2, zoom / 1.25); applyZoom(); });
$('zoom-fit').addEventListener('click', fit);
$('floor-img').addEventListener('load', () => { $('floor-note').hidden = true; if (!fitted) { fitted = true; fit(); } else applyZoom(); });
$('floor-img').addEventListener('error', async () => {
  let message = '평면도를 그리지 못했습니다';
  try { message = (await api($('floor-img').src)).error || message; } catch (error) { message = error.message; }
  $('floor-note').textContent = message; $('floor-note').hidden = false;
});
// Drag to pan the floor plan.
const view = $('floor-view');
let drag = null;
view.addEventListener('pointerdown', e => { drag = { x: e.clientX, y: e.clientY, left: view.scrollLeft, top: view.scrollTop }; view.classList.add('dragging'); view.setPointerCapture(e.pointerId); });
view.addEventListener('pointermove', e => { if (drag) { view.scrollLeft = drag.left - (e.clientX - drag.x); view.scrollTop = drag.top - (e.clientY - drag.y); } });
view.addEventListener('pointerup', () => { drag = null; view.classList.remove('dragging'); });
window.addEventListener('hashchange', () => {
  const [id, next] = decodeURIComponent(location.hash.slice(1)).split('/');
  tab = next === 'floor' ? 'floor' : 'features';
  if (id && id !== current?.id) openMap(id).catch(e => status(e.message, true)); else showTab(tab);
});
$('reload').addEventListener('click', () => { $('floor-img').removeAttribute('src'); load().catch(e => status(e.message, true)); });
load().catch(e => status(e.message, true));
