const $ = id => document.getElementById(id);
const effects = location.pathname === '/effects';
const title = effects ? '이펙트 도감' : '사운드 도감';
let entries = [], selected, revision = 0, locked = true, departed = false;
let pending = null, work = null, urls = [], frames = [], duration = 0, playhead = 0;
let playing = false, raf = 0, anchor = 0, anchorTime = 0, speed = 1, displayed = -1;
const audio = $('audio');
audio.volume = .5;
$('page-title').textContent = title; document.title = `Plush Panic Studio · ${title}`;
$('eyebrow').textContent = effects ? 'EFFECT LIBRARY / 04' : 'SOUND LIBRARY / 05';
$('intro').textContent = effects ? '실제 게임 코드로 캡처한 효과를 프레임 단위로 확인하세요.' : '현재 로컬 게임에 등록된 원본 음원을 직접 들어 보세요.';
$('preview-kind').textContent = effects ? 'GODOT · ACTUAL FRAMES' : 'LOCAL · SOURCE AUDIO';
$('effect-view').hidden = !effects; $('effect-controls').hidden = !effects; $('sound-view').hidden = effects;
document.querySelector(`.main-nav a[href="${location.pathname}"]`)?.setAttribute('aria-current', 'page');

function status(text, error = false) { $('status').textContent = text; $('status').classList.toggle('error', error); }
function failure(error) { $('error').textContent = error.message || String(error); $('error').hidden = false; status($('error').textContent, true); }
async function api(url, data) {
  const response = await fetch(url, data === undefined ? {} : { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
  const body = await response.json();
  if (!response.ok) throw new Error(body.error || `요청 실패 (${response.status})`);
  return body;
}
function blobUrl(base64, mime) {
  const binary = atob(base64), bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  const url = URL.createObjectURL(new Blob([bytes], { type: mime })); urls.push(url); return url;
}
function position(now) { return anchorTime + (now - anchor) / 1000 * speed; }
function draw() {
  let index = 0;
  while (index + 1 < frames.length && frames[index + 1].time <= playhead) index++;
  if (frames.length && index !== displayed) { $('preview').src = frames[index].url; displayed = index; }
  $('scrubber').value = String(playhead);
  $('time').textContent = `${playhead.toFixed(2)} / ${duration.toFixed(2)}초`;
  $('frame-time').textContent = frames.length ? `캡처 ${index + 1} / ${frames.length} · 실제 프레임 시각 ${frames[index].time.toFixed(3)}초 · 보간 없음` : '실제 캡처 프레임만 표시합니다. 프레임 보간은 하지 않습니다.';
}
function pause() {
  if (playing) playhead = Math.min(duration, position(performance.now()));
  playing = false; cancelAnimationFrame(raf); raf = 0; $('play').textContent = '재생';
}
function tick(now) {
  if (!playing || departed) return;
  playhead = position(now);
  if (playhead >= duration) {
    if ($('effect-loop').checked && duration > 0) { playhead %= duration; anchorTime = playhead; anchor = now; }
    else { pause(); playhead = duration; draw(); return; }
  }
  draw(); raf = requestAnimationFrame(tick);
}
function play() {
  if (!frames.length || duration <= 0 || locked || departed) return;
  if (playhead >= duration) playhead = 0;
  anchorTime = playhead; anchor = performance.now(); playing = true; $('play').textContent = '일시 정지';
  cancelAnimationFrame(raf); raf = requestAnimationFrame(tick);
}
function clearPreview() {
  pause(); audio.pause(); audio.removeAttribute('src'); audio.load(); audio.hidden = true;
  audio.volume = .5; audio.playbackRate = 1; audio.loop = false;
  $('preview').removeAttribute('src'); $('preview').hidden = true;
  for (const url of urls) URL.revokeObjectURL(url);
  urls = []; frames = []; duration = 0; playhead = 0; displayed = -1;
  $('effect-controls').disabled = true; $('sound-loop').disabled = true; $('sound-loop').checked = false;
  $('pending').hidden = true; $('retry').hidden = true; $('preview-meta').textContent = '';
  $('placeholder').hidden = false; draw();
}
function showProperties(item) {
  $('item-name').textContent = item.name; $('item-id').textContent = item.id;
  $('item-category').textContent = item.category; $('summary').textContent = item.summary;
  $('source').textContent = item.source; $('properties').replaceChildren();
  const values = [...(item.properties || [])];
  if (item.duration != null) values.push({ label: '미리보기 길이', value: `${Number(item.duration.toFixed(3))}초` });
  if (item.loop != null) values.push({ label: '원본 반복 설정', value: item.loop ? '반복' : '단발' });
  if (item.volumeDb != null) values.push({ label: '게임 볼륨 설정', value: `${item.volumeDb} dB (이 화면의 청취 볼륨과 별도)` });
  for (const property of values) {
    const row = document.createElement('tr'), key = document.createElement('th'), value = document.createElement('td');
    key.scope = 'row'; key.textContent = property.label; value.textContent = property.value;
    row.append(key, value); $('properties').append(row);
  }
}
function list() {
  const query = $('search').value.trim().toLocaleLowerCase(), category = $('category').value;
  const matches = entries.filter(e => (!category || e.category === category) && (!query || `${e.name} ${e.id} ${e.category}`.toLocaleLowerCase().includes(query)));
  $('result-count').textContent = `${matches.length} / ${entries.length}개`; $('entries').replaceChildren();
  for (const entry of matches) {
    const button = document.createElement('button'), name = document.createElement('strong'), id = document.createElement('span');
    button.dataset.id = entry.id; button.setAttribute('aria-pressed', String(selected?.id === entry.id));
    name.textContent = entry.name; id.textContent = entry.id; button.append(name, id);
    button.addEventListener('click', () => { if (!locked) select(entry.id); }); $('entries').append(button);
  }
  if (!matches.length) { const empty = document.createElement('p'); empty.className = 'empty'; empty.textContent = '검색 결과가 없습니다.'; $('entries').append(empty); }
}
function select(id) {
  revision++; pending = null; clearPreview(); $('error').hidden = true;
  selected = entries.find(e => e.id === id);
  if (!selected) {
    $('item-name').textContent = '등록된 항목이 없습니다.';
    for (const id of ['item-id', 'item-category', 'summary', 'source']) $(id).textContent = '';
    $('properties').replaceChildren(); $('placeholder').textContent = '등록된 항목이 없습니다.'; $('audio-state').textContent = '등록된 항목이 없습니다.';
    list(); status('현재 로컬 데이터에 등록된 항목이 없습니다.'); return;
  }
  history.replaceState(null, '', `${location.pathname}?id=${encodeURIComponent(id)}`);
  showProperties(selected); list();
  $('placeholder').textContent = '실제 프레임을 불러오는 중…'; $('pending').hidden = !effects;
  $('audio-state').textContent = '원본 음원을 불러오는 중…';
  pending = { item: selected, revision }; pump();
}
function current(job) { return !departed && !locked && revision === job.revision && selected?.id === job.item.id; }
// A new selection replaces the queued job. The current request finishes before the next begins.
function pump() {
  if (work || locked || departed || !pending) return;
  work = drain().finally(() => { work = null; pump(); });
}
async function drain() {
  while (pending && !locked && !departed) {
    const job = pending; pending = null;
    status(effects ? '실제 게임 프레임을 렌더합니다…' : '현재 로컬 원본 음원을 읽습니다…');
    try {
      const result = await api(effects ? '/api/effects/render' : '/api/sounds/preview', { id: job.item.id });
      if (!current(job)) continue;
      if (result.id !== job.item.id) throw new Error('선택 항목과 미리보기 응답이 일치하지 않습니다.');
      if (effects) {
        if (!Array.isArray(result.frames) || !result.frames.length || !Number.isFinite(result.duration) || result.duration <= 0)
          throw new Error('재생할 실제 캡처 프레임이 없습니다.');
        let previous = -1;
        for (const frame of result.frames) {
          if (!Number.isFinite(frame.time) || frame.time < 0 || frame.time < previous || frame.time > result.duration)
            throw new Error('캡처 프레임의 시각 정보가 올바르지 않습니다.');
          previous = frame.time;
        }
        const captured = result.frames.map(frame => {
          const image = new Image(), url = blobUrl(frame.png, 'image/png'); image.src = url;
          return { time: frame.time, url, image };
        });
        frames = captured;
        status('캡처 프레임을 재생할 수 있도록 준비합니다…');
        await Promise.all(captured.map(frame => frame.image.decode()));
        if (!current(job)) continue;
        duration = result.duration; $('scrubber').max = String(duration);
        $('preview').alt = `${job.item.name} 실제 게임 이펙트 캡처`;
        $('preview').hidden = false; $('placeholder').hidden = true; $('pending').hidden = true;
        $('effect-controls').disabled = false; $('preview-meta').textContent = `${result.width}×${result.height} · ${result.fps} fps · ${frames.length}장`;
        draw(); status('렌더 완료 · 재생 버튼으로 시작하세요.');
      } else {
        if (typeof result.audio !== 'string' || !result.audio || typeof result.mime !== 'string' || !result.mime.startsWith('audio/') || !Number.isFinite(result.duration) || result.duration < 0)
          throw new Error('재생할 원본 음원을 받지 못했습니다.');
        audio.src = blobUrl(result.audio, result.mime); audio.loop = !!result.loop;
        audio.hidden = false; $('sound-loop').checked = audio.loop; $('sound-loop').disabled = false;
        $('audio-state').textContent = '재생 버튼을 누르면 원본 음원이 재생됩니다.';
        $('preview-meta').textContent = `${Number(result.duration).toFixed(2)}초 · 피치 1×`;
        if (result.source) $('source').textContent = result.source;
        status('원본 음원 준비 완료 · 자동으로 재생하지 않습니다.');
      }
    } catch (error) {
      if (current(job)) {
        clearPreview(); failure(error); $('placeholder').textContent = '미리보기를 완료하지 못했습니다.';
        $('audio-state').textContent = '음원을 불러오지 못했습니다.'; $('retry').hidden = false;
      }
    }
  }
}
async function load() {
  const data = await api('/api/media'); if (departed) return;
  const id = selected?.id || new URLSearchParams(location.search).get('id'), category = $('category').value;
  const loadedEntries = effects ? data.effects : data.sounds;
  if (!Array.isArray(loadedEntries)) throw new Error('미디어 목록을 읽지 못했습니다.');
  entries = loadedEntries;
  $('total-count').textContent = `${entries.length}개 · 현재 로컬 데이터`;
  $('category').replaceChildren(new Option('모든 분류', ''));
  for (const name of [...new Set(entries.map(e => e.category))]) $('category').add(new Option(name, name));
  if (entries.some(e => e.category === category)) $('category').value = category;
  $('loaded-at').textContent = `로컬 데이터 로드 ${new Date(data.loadedAt).toLocaleTimeString('ko-KR')}`;
  $('renderer').textContent = effects ? (typeof data.renderer === 'string' ? data.renderer : '현재 로컬 Godot 렌더러') : '원본 음원 · 게임 믹스 미적용';
  select(entries.some(e => e.id === id) ? id : entries[0]?.id);
}
function lock(value) {
  locked = value; $('reload').disabled = value;
  document.querySelector('.library-layout').inert = value;
  document.querySelector('.main-nav').inert = value; document.querySelector('.brand').inert = value;
}
$('search').addEventListener('input', list); $('category').addEventListener('change', list);
$('play').addEventListener('click', () => { if (playing) { pause(); draw(); } else play(); });
$('replay').addEventListener('click', () => { pause(); playhead = 0; draw(); play(); });
$('speed').addEventListener('change', () => {
  const resume = playing; pause(); speed = Number($('speed').value); draw(); if (resume) play();
});
$('scrubber').addEventListener('input', () => { pause(); playhead = Number($('scrubber').value); draw(); });
$('sound-loop').addEventListener('change', () => { audio.loop = $('sound-loop').checked; });
$('retry').addEventListener('click', () => { if (!locked && selected) select(selected.id); });
audio.addEventListener('error', () => {
  if (audio.getAttribute('src') && audio.error && !departed && !locked) {
    failure(new Error('브라우저가 이 원본 음원을 재생하지 못했습니다.'));
    $('audio-state').textContent = '음원 재생 오류'; $('retry').hidden = false;
  }
});
$('preview').addEventListener('error', () => {
  if (!$('preview').getAttribute('src') || departed || locked) return;
  clearPreview(); failure(new Error('브라우저가 캡처 프레임을 표시하지 못했습니다.'));
  $('placeholder').textContent = '프레임을 표시하지 못했습니다.'; $('retry').hidden = false;
});
$('reload').addEventListener('click', async () => {
  if (locked || departed) return;
  lock(true); revision++; pending = null; clearPreview(); $('error').hidden = true;
  status('진행 중인 요청을 마친 뒤 로컬 데이터와 게임을 다시 읽습니다…');
  try { if (work) await work; await api('/api/reload', {}); await load(); }
  catch (error) { failure(error); }
  finally { if (!departed) { lock(false); pump(); } }
});
window.addEventListener('pagehide', () => { departed = true; revision++; pending = null; clearPreview(); });
// A restored browser history entry must fetch fresh metadata instead of reusing revoked previews.
window.addEventListener('pageshow', event => { if (event.persisted) location.reload(); });
lock(true);
load().catch(failure).finally(() => { if (!departed) { lock(false); pump(); } });
