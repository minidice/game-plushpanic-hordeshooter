import assert from 'node:assert/strict';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const root = fileURLToPath(new URL('../../', import.meta.url));
const origin = process.env.STUDIO_URL || 'http://127.0.0.1:4317';
const data = JSON.parse(await readFile(path.join(root, 'game/content/data/surface_profiles.json'), 'utf8'));
const response = await fetch(origin + '/api/library');
assert.equal(response.status, 200);
const library = await response.json();
let checks = 0;
for (const item of library.objects) {
  // Companion actors are listed in the same UI, but are not environment contact surfaces.
  if (/^(drone|sidekick|pet):/.test(item.id)) continue;
  const key = item.id.startsWith('workshop:') ? 'runtime:' + item.id : item.id;
  assert.ok(Object.hasOwn(data.objects, key), 'Unassigned Studio object: ' + key);
  const profile = data.objects[key];
  const value = item.properties.find(p => p.label === '표면·소리 프리셋')?.value;
  assert.ok(profile === null ? value?.includes('미적용') : value?.includes(`(${profile})`), key);
  assert.ok(item.details.some(d => d.title === '표면·소리 공통 데이터'), key);
  checks++;
}
const sand = library.objects.find(p => p.id === 'sandbags');
assert.equal(sand.interaction.max_health, 50);
assert.equal(sand.interaction.cover, true);
await mkdir(path.join(root, 'studio/output'), { recursive: true });
for (const id of ['sandbags', 'corrugated_fence', 'workshop:Workbench']) {
  const response = await fetch(origin + '/api/library/render', { method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ kind: 'object', id, rotation: -25, zoom: 1 }) });
  assert.equal(response.status, 200);
  const result = await response.json();
  const png = Buffer.from(result.png, 'base64');
  assert.equal(png.readUInt32BE(16), 900);
  assert.equal(png.readUInt32BE(20), 900);
  await writeFile(path.join(root, 'studio/output', 'surface-' + id.replaceAll(':', '-') + '.png'), png);
  checks++;
}
console.log(`STUDIO_SURFACE_PASS ${checks}; material properties, HP50/cover preserved, 3 live renders`);
