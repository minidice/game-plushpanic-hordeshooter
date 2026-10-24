import assert from 'node:assert/strict';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
const root = fileURLToPath(new URL('../../', import.meta.url));
const origin = process.env.STUDIO_URL || 'http://127.0.0.1:4317';
let checks = 0;
const check = (value, message) => { assert.ok(value, message); checks++; console.log('PASS ' + message); };
const response = await fetch(origin + '/api/library');
check(response.status === 200, 'library endpoint available');
const data = await response.json();
// Navigating while the old page is rendering must not strand the new page without a catalog.
const oldPageRender = fetch(origin + '/api/library/render', { method: 'POST', headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ kind: 'monster', id: 'plush', rotation: 0, zoom: 1 }) });
// Let the POST body arrive before simulating navigation (simultaneous GET may arrive first).
await new Promise(resolve => setTimeout(resolve, 20));
const overlap = await Promise.all([oldPageRender, fetch(origin + '/api/library'), fetch(origin + '/api/catalog')]);
check(overlap.every(r => r.status === 200), 'menu catalog loads while the previous page is rendering');
await Promise.all(overlap.map(r => r.arrayBuffer()));
const ids = (await readFile(path.join(root, 'game/content/data/monsters.csv'), 'utf8')).split(/\r?\n/).slice(1).filter(l => l && !l.startsWith('#')).map(l => l.split(',')[0]);
assert.deepEqual(data.monsters.map(x => x.id).sort(), ids.sort()); checks++;
for (const folder of ['outdoor-kit', 'stage-props', 'edge-kit', 'ground-bits', 'stage2-props']) {
  const manifest = JSON.parse(await readFile(path.join(root, `game/assets/${folder}/manifest.json`), 'utf8'));
  for (const item of manifest.assets) {
    const entry = data.objects.find(o => o.id === item.id);
    check(entry && entry.sourceData.id === item.id && entry.sourceData.dimensions_m.every((v, i) => v === item.dimensions_m[i]), `${item.id}: current manifest and dimensions`);
  }
}
check(data.monsters.find(x => x.id === 'boss').details.some(d => d.title.includes('보스')), 'boss skills and phases exposed');
check(data.monsters.find(x => x.id === 'plush').variants.length === 4, 'infected preview offers the four actual player species');
check(data.objects.filter(x => x.id.startsWith('workshop:')).length === 7, 'code-built workshop props included');
check(data.objects.some(x => x.id === 'runtime:barrel'), 'runtime explosive barrel included');
for (const kind of ['monsters', 'objects']) {
  check(new Set(data[kind].map(x => x.id)).size === data[kind].length, `${kind}: unique IDs`);
  for (const item of data[kind]) check(item.properties.length > 0 && item.source, `${item.id}: properties and source`);
}
await mkdir(path.join(root, 'studio/output'), { recursive: true });
const targets = process.argv.includes('--all') ? [
  ...data.monsters.map(x => ['monster', x.id]), ...data.objects.map(x => ['object', x.id]),
] : [
  ['monster', 'plush'], ['monster', 'armed_veteran'], ['monster', 'bull'], ['monster', 'boss_balloon_king'],
  ['object', 'pine_tall'], ['object', 'gondola_cabin'], ['object', 'workshop:Workbench'], ['object', 'runtime:barrel'],
];
for (const [kind, id] of targets) {
  const r = await fetch(origin + '/api/library/render', { method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ kind, id, variant: kind === 'monster' ? 'goose' : data.objects.find(o => o.id === id).variants[0]?.id, rotation: -25, zoom: 1 }) });
  const result = await r.json();
  check(r.status === 200 && result.png && result.bounds?.size.every(Number.isFinite), `${kind}/${id}: live game render and bounds`);
  const png = Buffer.from(result.png, 'base64');
  check(png.readUInt32BE(16) === 900 && png.readUInt32BE(20) === 900, `${kind}/${id}: 900px image`);
  await writeFile(path.join(root, 'studio/output', `library-${id.replaceAll(':', '-')}.png`), png);
}
console.log(execFileSync('python', [path.join(root, 'studio/tests/check-library-render.py'),
  ...targets.map(([, id]) => path.join(root, 'studio/output', `library-${id.replaceAll(':', '-')}.png`))], { encoding: 'utf8' }).trim());
const invalid = await fetch(origin + '/api/library/render', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ kind: 'object', id: '../project.godot', rotation: 0, zoom: 1 }) });
check(invalid.status !== 200, 'unknown object cannot load arbitrary resources');
console.log(`STUDIO_LIBRARY_PASS ${checks} renders=${targets.length}`);
