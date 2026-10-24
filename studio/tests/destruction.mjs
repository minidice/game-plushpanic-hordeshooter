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
for (const id of ['sandbags', 'concrete_barrier', 'runtime:breach_wall', 'runtime:barrel']) {
  const entry = library.objects.find(o => o.id === id);
  const profile = policy.profiles[policy.objects[id]];
  check(entry.interaction.breakable_by === (profile.destruction?.breakable_by ?? null)
    && entry.interaction.max_health === (profile.destruction?.max_health ?? null), id + ': actual destruction data');
  assert.deepEqual(entry.interaction.explosion, profile.explosion ?? null); checks++;
  check(entry.properties.some(p => p.label === '폭발 (부서질 때)' && p.value.includes(profile.explosion ? '폭발' : '없음')), id + ': visible explosion property');
}
const wall = library.objects.find(o => o.id === 'runtime:breach_wall');
assert.deepEqual(wall.variants.map(v => v.id), ['intact', 'breaking', 'broken']); checks++;
await mkdir(path.join(root, 'studio/output'), { recursive: true });
const images = [], renders = [];
for (const variant of ['intact', 'breaking', 'broken']) {
  const r = await fetch(base + '/api/library/render', { method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ kind: 'object', id: wall.id, variant, rotation: -25, zoom: 1 }) });
  const rendered = await r.json();
  check(r.ok && rendered.png && rendered.variant === variant, variant + ': actual game model rendered');
  const png = Buffer.from(rendered.png, 'base64');
  check(png.readUInt32BE(16) === 900 && png.readUInt32BE(20) === 900, variant + ': 900px image');
  const file = path.join(root, 'studio/output', `destruction-wall-${variant}.png`);
  await writeFile(file, png); images.push(file); renders.push(rendered);
}
check(new Set(renders.map(r => r.png)).size === 3, 'three visibly distinct model states');
for (const r of renders.slice(1)) { assert.deepEqual(r.bounds, renders[0].bounds); checks++; }
const invalid = await fetch(base + '/api/library/render', { method: 'POST', headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ kind: 'object', id: wall.id, variant: 'unknown', rotation: 0, zoom: 1 }) });
check(!invalid.ok, 'unknown wall state rejected');
check(before === await readFile(source, 'utf8'), 'preview preserves game data');
console.log(execFileSync('python', [path.join(root, 'studio/tests/check-library-render.py'), ...images], { encoding: 'utf8' }).trim());
console.log(`STUDIO_DESTRUCTION_PASS ${checks}`);
