import { surfaceEditor } from '/surface-editor.js';
const $ = id => document.getElementById(id);
const isMonster = location.pathname === '/monsters';
const kind = isMonster ? 'monster' : 'object';
const title = isMonster ? '몬스터 도감' : '오브젝트 도감';
let entries = [], selected, revision = 0, busy = false, locked = false, timer;
const surfaces = isMonster ? null : surfaceEditor(api, load);
$('page-title').textContent = title; document.title = `Plush Panic Studio · ${title}`;
$('eyebrow').textContent = isMonster ? 'MONSTER LIBRARY / 02' : 'OBJECT LIBRARY / 03';
$('intro').textContent = isMonster ? '일반 감염체부터 보스까지, 실제 외형과 기본 능력치를 확인하세요.' : '등록된 모델 소품과 코드 생성 소품의 외형·규격을 확인하세요.';
document.querySelector(`.main-nav a[href="${location.pathname}"]`).setAttribute('aria-current', 'page');
const status = (text, error = false) => { $('status').textContent = text; $('status').classList.toggle('error', error); };
async function api(url, data) {
  const response = await fetch(url, data === undefined ? {} : { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
  const body = await response.json(); if (!response.ok) throw new Error(body.error || '요청 실패'); return body;
}
function invalidate() { revision++; $('pending').hidden = false; clearTimeout(timer); timer = setTimeout(render, 180); }
async function render() {
  if (busy || locked || !selected) return;
  busy = true; $('reload').disabled = true;
  const current = revision, item = selected, started = performance.now();
  status('선택 항목을 실제 게임 코드로 렌더합니다…');
  try {
    const result = await api('/api/library/render', { kind, id: item.id, variant: $('variant').value || 'goose', rotation: Number($('rotation').value), zoom: Number($('zoom').value) });
    if (revision === current) {
      const views = result.comparison || [{ png: result.png }];
      const images = await Promise.all(views.map(async view => {
        const image = new Image(); image.src = 'data:image/png;base64,' + view.png;
        image.alt = `${item.name} ${view.label || ''} 실제 게임 렌더`; await image.decode(); return image;
      }));
      if (revision === current) {
        $('comparison').replaceChildren();
        if (result.comparison) {
          views.forEach((view, i) => {
            const figure = document.createElement('figure'), caption = document.createElement('figcaption');
            caption.textContent = `${view.label} · ${view.baseTriangles.toLocaleString('ko-KR')} 삼각형`;
            figure.append(caption, images[i]); $('comparison').append(figure);
          });
        } else { $('preview').src = images[0].src; $('preview').alt = images[0].alt; }
        $('preview').hidden = !!result.comparison; $('comparison').hidden = !result.comparison;
        $('placeholder').hidden = true; $('pending').hidden = true;
        $('render-error').hidden = true;
        $('render-time').textContent = `${((performance.now() - started) / 1000).toFixed(2)}s`;
        status('렌더 완료 · 현재 로컬 데이터' + (isMonster ? ' · 조회 전용' : ' · 표면·소리 편집 가능'));
      }
    }
  } catch (error) {
    if (revision === current) {
      $('preview').hidden = true; $('placeholder').hidden = false; $('placeholder').textContent = '렌더를 완료하지 못했습니다.';
      $('comparison').hidden = true;
      $('pending').hidden = true; $('render-error').hidden = false; $('render-error').textContent = error.message;
      status(error.message, true);
    }
  } finally { busy = false; $('reload').disabled = locked; if (current !== revision) render(); }
}
function select(id) {
  selected = entries.find(e => e.id === id);
  if (!selected) return;
  history.replaceState(null, '', `${location.pathname}?id=${encodeURIComponent(id)}`);
  $('item-name').textContent = selected.name; $('item-id').textContent = selected.id;
  $('item-category').textContent = selected.category; $('summary').textContent = selected.summary;
  surfaces?.select(selected);
  $('source').textContent = selected.source;
  const comparing = selected.qualityMode !== 'single';
  document.querySelector('.library-main').classList.toggle('comparing', comparing);
  $('quality-note').hidden = !comparing; $('comparison').hidden = true;
  $('preview').hidden = true; $('placeholder').hidden = false; $('placeholder').textContent = '실제 모델을 불러오는 중…';
  $('render-error').hidden = true;
  $('properties').replaceChildren();
  for (const property of selected.properties) {
    const row = document.createElement('tr'), key = document.createElement('th'), value = document.createElement('td');
    key.scope = 'row'; key.textContent = property.label; value.textContent = property.value; row.append(key, value); $('properties').append(row);
  }
  $('details').replaceChildren();
  for (const section of selected.details) {
    const details = document.createElement('details'), summary = document.createElement('summary'), pre = document.createElement('pre');
    summary.textContent = section.title; pre.textContent = JSON.stringify(section.data, null, 2);
    details.append(summary, pre); $('details').append(details);
  }
  $('variant').replaceChildren();
  $('variant-label').textContent = isMonster ? '감염체 기준 종' : '파괴 상태';
  for (const variant of selected.variants) $('variant').add(new Option(variant.name, variant.id));
  $('variant-row').hidden = selected.variants.length === 0;
  for (const button of $('entries').children) button.setAttribute('aria-pressed', String(button.dataset.id === id));
  invalidate();
}
function list() {
  const query = $('search').value.trim().toLocaleLowerCase(), category = $('category').value;
  const matches = entries.filter(e => (!category || e.category === category) && (!query || `${e.name} ${e.id} ${e.category}`.toLocaleLowerCase().includes(query)));
  $('result-count').textContent = `${matches.length} / ${entries.length}개`;
  $('entries').replaceChildren();
  for (const entry of matches) {
    const button = document.createElement('button'), name = document.createElement('strong'), id = document.createElement('span');
    button.dataset.id = entry.id; button.setAttribute('aria-pressed', String(selected?.id === entry.id));
    name.textContent = entry.name; id.textContent = entry.id; button.append(name, id);
    button.addEventListener('click', () => select(entry.id)); $('entries').append(button);
  }
  if (!matches.length) { const empty = document.createElement('p'); empty.className = 'empty'; empty.textContent = '검색 결과가 없습니다.'; $('entries').append(empty); }
}
async function load() {
  const data = await api('/api/library'); entries = isMonster ? data.monsters : data.objects;
  if (surfaces) surfaces.setData(await api('/api/surfaces'), entries);
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
for (const id of ['rotation', 'zoom', 'variant']) $(id).addEventListener('input', () => {
  $('rotation-value').textContent = `${$('rotation').value}°`; $('zoom-value').textContent = `${Number($('zoom').value).toFixed(2)}×`; invalidate();
});
$('reload').addEventListener('click', async () => {
  if (busy || locked || surfaces?.isSaving()) return;
  locked = true; $('reload').disabled = true; document.querySelector('.library-layout').inert = true;
  document.querySelector('.main-nav').inert = true; document.querySelector('.brand').inert = true;
  status('현재 게임을 다시 빌드하고 데이터를 읽습니다…');
  let loaded = false;
  try { await api('/api/reload', {}); await load(); loaded = true; }
  catch (error) { status(error.message, true); }
  finally {
    locked = false; $('reload').disabled = false; document.querySelector('.library-layout').inert = false;
    document.querySelector('.main-nav').inert = false; document.querySelector('.brand').inert = false;
    if (loaded) render();
  }
});
load().catch(error => status(error.message, true));
