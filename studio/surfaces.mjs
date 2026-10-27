import { readFile, open, rename, unlink } from 'node:fs/promises';
import { createHash, randomUUID } from 'node:crypto';
import { fileURLToPath } from 'node:url';

// The client never supplies a filename. Only this audio data can be written.
const source = fileURLToPath(new URL('../game/content/data/surface_profiles.json', import.meta.url));
const gains = ['step_db', 'hit_db', 'break_db'];
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const error = (status, message) => Object.assign(new Error(message), { status });
const record = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const exactKeys = (value, keys) => record(value) && Object.keys(value).length === keys.length && keys.every(key => Object.hasOwn(value, key));
let saving = false;

function state(bytes) {
  const data = JSON.parse(bytes);
  const usage = Object.fromEntries(Object.keys(data.profiles).map(id => [id, { objects: [], ground: [], layouts: [] }]));
  const add = (id, kind, label) => { if (id !== null && usage[id]) usage[id][kind].push(label); };
  for (const [id, profile] of Object.entries(data.objects)) add(profile, 'objects', id);
  add(data.default_profile, 'ground', '미등록 충돌체 기본값');
  for (const [map, ground] of Object.entries(data.ground)) {
    add(ground.default, 'ground', `${map} · 기본 지면`);
    ground.zones.forEach((zone, index) => add(zone.profile, 'ground', `${map} · 구역 ${index + 1}`));
  }
  for (const [map, layout] of Object.entries(data.layouts)) {
    add(layout.default, 'layouts', `Stage ${map} · 기본 지면`);
    add(layout.route_profile, 'layouts', `Stage ${map} · 길`);
    for (const [region, profile] of Object.entries(layout.regions)) add(profile, 'layouts', `Stage ${map} · ${region}`);
  }
  return { revision: hash(bytes), objects: data.objects, profiles: data.profiles, families: data.families, usage };
}

export async function readSurfaces() { return state(await readFile(source)); }

export async function saveSurfaces(patch) {
  if (!exactKeys(patch, ['revision', 'objectId', 'profileId', 'gains'])
      || typeof patch.revision !== 'string' || !/^[a-f0-9]{64}$/.test(patch.revision)
      || typeof patch.objectId !== 'string' || typeof patch.profileId !== 'string'
      || !exactKeys(patch.gains, gains)
      || gains.some(key => !Number.isFinite(patch.gains[key]) || patch.gains[key] < -24 || patch.gains[key] > 6)) {
    throw error(400, '기존 오브젝트·프리셋과 세 게인(-24~+6 dB의 유한한 숫자)이 필요합니다.');
  }
  if (saving) throw error(409, '다른 표면 저장을 처리 중입니다. 최신 값을 다시 읽고 변경을 확인하세요.');
  saving = true;
  let temporary;
  try {
    const before = await readFile(source);
    if (hash(before) !== patch.revision) throw error(409, '다른 탭이나 파일 편집으로 데이터가 바뀌었습니다. 최신 값을 다시 읽고 변경을 확인하세요.');
    const data = JSON.parse(before);
    if (!Object.hasOwn(data.objects, patch.objectId) || data.objects[patch.objectId] === null
        || /^(pet|sidekick|drone):/.test(patch.objectId) || !Object.hasOwn(data.profiles, patch.profileId)) {
      throw error(400, '편집 가능한 기존 환경 오브젝트와 프리셋만 선택할 수 있습니다. 무충돌 장식·동료는 조회 전용입니다.');
    }
    data.objects[patch.objectId] = patch.profileId;
    Object.assign(data.profiles[patch.profileId], patch.gains);
    const bytes = Buffer.from(JSON.stringify(data, null, 2) + '\n');
    temporary = source + '.' + randomUUID() + '.tmp';
    const file = await open(temporary, 'wx');
    try { await file.writeFile(bytes); await file.sync(); } finally { await file.close(); }
    // Include unrelated ground/layout/manual changes in optimistic concurrency checks.
    if (hash(await readFile(source)) !== patch.revision) throw error(409, '저장 도중 파일이 바뀌었습니다. 최신 값을 다시 읽고 변경을 확인하세요.');
    await rename(temporary, source); // Same-directory atomic replacement: readers see a whole JSON file.
    temporary = undefined;
    return await readSurfaces();
  } finally {
    if (temporary) await unlink(temporary).catch(() => {});
    saving = false;
  }
}
