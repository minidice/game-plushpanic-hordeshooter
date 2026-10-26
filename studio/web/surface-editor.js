const gainFields = [['step_db', '발소리', 'step'], ['hit_db', '명중', 'hit'], ['break_db', '파손', 'breaking']];
const keyFor = id => id.startsWith('workshop:') ? 'runtime:' + id : id;

export function surfaceEditor(api, refreshLibrary) {
  const $ = id => document.getElementById(id);
  const drafts = new Map();
  let state, selected, entries = [], saving = false;
  const message = (text, error = false) => { $('surface-status').textContent = text; $('surface-status').classList.toggle('error', error); };
  function draft() {
    const key = keyFor(selected.id);
    if (!drafts.has(key)) {
      const profileId = state.objects[key], profile = state.profiles[profileId];
      drafts.set(key, { revision: state.revision, objectId: key, profileId,
        gains: Object.fromEntries(gainFields.map(([key]) => [key, profile[key]])) });
    }
    return drafts.get(key);
  }
  function changed(value) {
    return value.profileId !== state.objects[value.objectId]
      || gainFields.some(([key]) => value.gains[key] !== state.profiles[value.profileId]?.[key]);
  }
  function setState(data) {
    // Only actual edits retain their old version. Simply visiting another item is not a stale draft.
    if (state) for (const [key, value] of drafts) if (!changed(value)) drafts.delete(key);
    state = data;
  }
  function drawGains() {
    const value = draft(), profile = state.profiles[value.profileId];
    $('surface-family').textContent = `${profile.label} · ${state.families[profile.family]} (${value.profileId})`;
    for (const [key, , sound] of gainFields) {
      $(key).value = value.gains[key]; $(`${key}-sound`).textContent = profile[sound];
    }
    const usage = state.usage[value.profileId];
    const objects = new Set(usage.objects); objects.add(value.objectId);
    const names = [...objects].map(id => entries.find(item => keyFor(item.id) === id)?.name || id);
    $('surface-impact').textContent = `이 게인은 선택 물체 전용이 아닙니다. 저장 후 같은 프리셋을 쓰는 오브젝트 ${objects.size}종, 지면 ${usage.ground.length}곳, 맵 구역 ${usage.layouts.length}곳에 함께 적용됩니다.`;
    $('surface-usage').textContent = [...names, ...usage.ground, ...usage.layouts].join(' · ');
    $('surface-save').disabled = saving || !changed(value);
  }
  function select(item) {
    if (selected?.id !== item?.id) message('');
    selected = item;
    $('surface-editor').hidden = !item;
    if (!item || !state) return;
    const key = keyFor(item.id), mapped = Object.hasOwn(state.objects, key);
    const editable = mapped && state.objects[key] !== null && !/^(pet|sidekick|drone):/.test(key);
    $('surface-form').hidden = !editable; $('surface-readonly').hidden = editable;
    $('surface-readonly').textContent = mapped && state.objects[key] === null
      ? '무충돌 장식 · 접촉음 미적용(null). 표면을 지정하거나 충돌을 만드는 편집은 지원하지 않습니다.'
      : '환경 접촉음 편집 대상이 아닙니다. 동료·소환수 외형은 조회 전용입니다.';
    if (!editable) return;
    const value = draft();
    $('surface-profile').replaceChildren(...Object.entries(state.profiles).map(([id, p]) => new Option(`${p.label} · ${id}`, id)));
    $('surface-profile').value = value.profileId;
    $('surface-fields').disabled = saving;
    drawGains();
    if (value.revision !== state.revision) message('이 편집 초안 이후 파일이 바뀌었습니다. 최신 값 다시 읽기로 편집을 취소하고 확인하세요.', true);
  }
  $('surface-profile').addEventListener('change', () => {
    const value = draft(); value.profileId = $('surface-profile').value;
    value.gains = Object.fromEntries(gainFields.map(([key]) => [key, state.profiles[value.profileId][key]]));
    message('저장 전 편집 · 프리셋 선택은 이 오브젝트 종류에만 적용됩니다.'); drawGains();
  });
  for (const [key] of gainFields) $(key).addEventListener('input', () => {
    draft().gains[key] = $(key).value === '' ? null : Number($(key).value);
    message('저장 전 편집 · 게인 변경은 이 프리셋을 공유하는 모든 대상에 적용됩니다.');
    $('surface-save').disabled = saving || !changed(draft());
  });
  $('surface-reset').addEventListener('click', async () => {
    if (saving) return;
    saving = true; $('surface-fields').disabled = true;
    try {
      const next = await api('/api/surfaces');
      drafts.clear(); state = next; await refreshLibrary();
      message('최신 값을 다시 읽었습니다. 저장 전 편집은 취소했습니다.');
    } catch (error) { message(error.message, true); }
    finally { saving = false; select(selected); }
  });
  $('surface-form').addEventListener('submit', async event => {
    event.preventDefault();
    if (saving || !$('surface-form').reportValidity()) return;
    const value = structuredClone(draft());
    saving = true; $('surface-fields').disabled = true; message('표면 데이터를 저장합니다…');
    let committed = false;
    try {
      setState(await api('/api/surfaces', value)); committed = true; drafts.delete(value.objectId);
      await refreshLibrary();
      message('저장 완료 · 실제 파일과 Studio 속성을 다시 읽었습니다. 실행 중인 게임은 재시작하세요. 멀티는 양쪽 PC의 데이터가 같아야 합니다.');
    } catch (error) { message((committed ? '파일 저장 완료, Studio 속성 갱신 실패. 최신 값 다시 읽기를 누르세요. ' : '저장하지 못했습니다. ') + error.message, true); }
    finally { saving = false; select(selected); }
  });
  window.addEventListener('beforeunload', event => {
    if (state && [...drafts.values()].some(changed)) { event.preventDefault(); event.returnValue = ''; }
  });
  return { select, setData(data, items) { setState(data); entries = items; }, isSaving: () => saving };
}
