const $ = id => document.getElementById(id);
let catalog, species, tab = 'body', overrides = {}, revision = 0, busy = false, scheduled, locked = false;
async function api(url, data) {
  const response = await fetch(url, data === undefined ? {} : { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
  const result = await response.json();
  if (!response.ok) throw new Error(result.error || '요청을 처리하지 못했습니다.');
  return result;
}
const status = (text, error = false) => { $('status').textContent = text; $('status').classList.toggle('error', error); };
function invalidate() {
  revision++;
  $('pending').hidden = false;
  status('변경 사항을 실제 게임 렌더에 적용하고 있습니다…');
  clearTimeout(scheduled);
  scheduled = setTimeout(render, 180);
}
async function render() {
  if (busy || locked || !species) return;
  busy = true;
  $('reload').disabled = true;
  const current = revision;
  const started = performance.now();
  try {
    const result = await api('/api/render', { species: species.id, fields: { ...overrides },
      rotation: Number($('rotation').value), zoom: Number($('zoom').value), gear: $('gear').checked, weapon: $('weapon').checked });
    if (current === revision) {
      const source = `data:image/png;base64,${result.png}`;
      const decoded = new Image(); decoded.src = source; await decoded.decode();
      if (current === revision) {
        $('preview').src = source; $('preview').hidden = false; $('placeholder').hidden = true;
        $('render-time').textContent = `${((performance.now() - started) / 1000).toFixed(2)}s`;
        $('pending').hidden = true;
        status('현재 조합 렌더 완료 · 원본 데이터와 저장 파일은 변경되지 않습니다.');
      }
    }
  } catch (error) {
    if (current === revision) { status(error.message, true); $('pending').hidden = true; }
  } finally {
    busy = false; $('reload').disabled = false;
    if (current !== revision) render();
  }
}
function fieldList() {
  $('fields').replaceChildren();
  for (const field of species.fields.filter(f => f.tab === tab)) {
    const row = document.createElement('label'); row.className = 'field';
    const title = document.createElement('span'); title.textContent = field.label;
    const input = document.createElement(field.type === 'select' ? 'select' : 'input');
    input.id = `field-${field.key}`;
    const value = overrides[field.key] ?? field.value;
    if (field.type === 'select') field.options.forEach((label, index) => input.add(new Option(label, index)));
    else input.type = field.type;
    if (field.type === 'range') { input.min = field.min; input.max = field.max; input.step = field.step; }
    if (field.type === 'checkbox') input.checked = value;
    else input.value = value;
    const output = document.createElement('output');
    const update = () => { output.textContent = field.type === 'range' ? Number(input.value).toFixed(field.step === 1 ? 0 : 2) : field.type === 'color' ? input.value.toUpperCase() : ''; };
    update();
    input.addEventListener('input', () => {
      overrides[field.key] = field.type === 'checkbox' ? input.checked : ['range', 'select'].includes(field.type) ? Number(input.value) : input.value;
      update(); invalidate();
    });
    row.classList.add(`field-${field.type}`); row.append(title, input, output); $('fields').append(row);
  }
}
function selectSpecies(id) {
  species = catalog.species.find(item => item.id === id); overrides = {};
  $('character-name').textContent = species.name;
  $('character-source').textContent = species.source;
  for (const button of $('species').children) { button.classList.toggle('active', button.dataset.id === id); button.setAttribute('aria-pressed', String(button.dataset.id === id)); }
  fieldList(); invalidate();
}
async function load() {
  catalog = await api('/api/catalog');
  $('species').replaceChildren(); $('tabs').replaceChildren();
  for (const item of catalog.species) {
    const button = document.createElement('button'); button.textContent = item.name; button.dataset.id = item.id;
    button.addEventListener('click', () => selectSpecies(item.id)); $('species').append(button);
  }
  for (const item of catalog.tabs) {
    const button = document.createElement('button'); button.textContent = item.label; button.setAttribute('role', 'tab');
    button.setAttribute('aria-selected', String(item.key === tab));
    button.classList.toggle('active', item.key === tab);
    button.addEventListener('click', () => {
      tab = item.key;
      for (const other of $('tabs').children) { other.classList.toggle('active', other === button); other.setAttribute('aria-selected', String(other === button)); }
      fieldList();
    }); $('tabs').append(button);
  }
  $('loaded-at').textContent = `로컬 데이터 로드 ${new Date(catalog.loadedAt).toLocaleTimeString('ko-KR')}`;
  selectSpecies(catalog.species.some(s => s.id === species?.id) ? species.id : catalog.species[0].id);
}
for (const id of ['rotation', 'zoom', 'gear', 'weapon']) $(id).addEventListener('input', () => {
  $('rotation-value').textContent = `${$('rotation').value}°`;
  $('zoom-value').textContent = `${Number($('zoom').value).toFixed(2)}×`; invalidate();
});
$('reset').addEventListener('click', () => { overrides = {}; fieldList(); invalidate(); });
$('reload').addEventListener('click', async () => {
  if (busy || locked) return;
  locked = true; $('reload').disabled = true;
  document.querySelector('.workspace').inert = true;
  document.querySelector('.main-nav').inert = true; document.querySelector('.brand').inert = true;
  status('현재 소스를 빌드하고 변경된 에셋을 다시 가져옵니다…');
  let loaded = false;
  try { await api('/api/reload', {}); await load(); loaded = true; }
  catch (error) { status(error.message, true); }
  finally {
    locked = false; $('reload').disabled = false; document.querySelector('.workspace').inert = false;
    document.querySelector('.main-nav').inert = false; document.querySelector('.brand').inert = false;
    if (loaded) render();
  }
});
load().catch(error => status(error.message, true));
