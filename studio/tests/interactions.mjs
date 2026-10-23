import assert from 'node:assert/strict';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
const root = fileURLToPath(new URL('../../', import.meta.url));
const base = process.env.STUDIO_URL || 'http://127.0.0.1:4317';
const source = path.join(root, 'game/content/data/object_interactions.json');
const before = await readFile(source, 'utf8');
const policy = JSON.parse(before);
const response = await fetch(base + '/api/library');
assert.equal(response.status, 200);
const library = await response.json();
let checks = 0;
const check = (value, message) => { assert.ok(value, message); checks++; console.log('PASS ' + message); };
for (const entry of library.objects.filter(e => e.source.endsWith('/manifest.json'))) {
  const shape = entry.sourceData.collision.kind;
  const profile = policy.objects[entry.id] ?? (['box', 'cylinder'].includes(shape) ? policy.default_solid_profile : null);
  if (profile === null) {
    check(entry.interaction === null && entry.properties.some(p => p.label === '이동 차단' && p.value.includes('미연결')), `${entry.id}: custom geometry stays unresolved`);
    continue;
  }
  const expected = policy.profiles[profile];
  check(entry.interaction?.profile === profile && entry.interaction.blocks_movement === expected.blocks_movement
    && entry.interaction.blocks_projectiles === expected.blocks_projectiles, `${entry.id}: actual current data`);
  check(entry.properties.some(p => p.label === '이동 차단' && p.value === (expected.blocks_movement ? '차단' : '통과'))
    && entry.properties.some(p => p.label === '탄환 차단' && p.value === (expected.blocks_projectiles ? '차단' : '통과')), `${entry.id}: visible labels agree`);
  const cut = expected.destruction ?? null;
  check(entry.interaction.cover === (expected.cover === true) && entry.interaction.breakable_by === (cut?.breakable_by ?? null)
    && entry.interaction.max_health === (cut?.max_health ?? null), `${entry.id}: cover and destruction from current data`);
  check(entry.properties.some(p => p.label === '엄폐 (낮은 엄폐물)' && (expected.cover === true) === p.value.startsWith('엄폐'))
    && entry.properties.some(p => p.label === '파괴' && (cut === null) === p.value.startsWith('파괴 안 됨'))
    && entry.properties.some(p => p.label === '내구도' && p.value === (cut ? String(cut.max_health) : '—')), `${entry.id}: cover/destruction labels shown`);
  check(entry.properties.some(p => p.label === '차단 적용 범위' && p.value.includes('solid:false'))
    && entry.interaction.source === 'res://content/data/object_interactions.json', `${entry.id}: source and placement scope`);
}
const wire = library.objects.find(e => e.id === 'wire_fence').interaction;
const solid = library.objects.find(e => e.id === 'corrugated_fence').interaction;
check(wire.blocks_movement && !wire.blocks_projectiles && solid.blocks_movement && solid.blocks_projectiles, 'wire vs ordinary structure contract');
const sandbags = library.objects.find(e => e.id === 'sandbags').interaction;
const barrier = library.objects.find(e => e.id === 'concrete_barrier').interaction;
check(sandbags.cover && sandbags.breakable_by === 'gun' && sandbags.max_health > 0 && barrier.cover && barrier.breakable_by === null,
  'sandbags: breakable cover · concrete barrier: unbreakable cover (C-29)');
await mkdir(path.join(root, 'studio/output'), {recursive: true});
const images = [];
for (const id of ['wire_fence', 'corrugated_fence']) {
  const r = await fetch(base + '/api/library/render', {method:'POST', headers:{'Content-Type':'application/json'},
    body:JSON.stringify({kind:'object', id, rotation:-25, zoom:1})});
  const rendered = await r.json();
  check(r.ok && rendered.png && rendered.id === id, `${id}: real game render`);
  const image = path.join(root, 'studio/output', `interaction-${id}.png`);
  await writeFile(image, Buffer.from(rendered.png, 'base64')); images.push(image);
}
check(before === await readFile(source, 'utf8'), 'web inspection preserves game interaction data');
console.log(execFileSync('python', [path.join(root, 'studio/tests/check-library-render.py'), ...images], {encoding:'utf8'}).trim());
console.log(`STUDIO_INTERACTION_PASS ${checks}`);
