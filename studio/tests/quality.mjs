import assert from 'node:assert/strict';
import { readFile, writeFile, mkdir, readdir } from 'node:fs/promises';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = fileURLToPath(new URL('../../', import.meta.url));
const base = process.env.STUDIO_URL || 'http://127.0.0.1:4317';
const protectedFiles = [path.join(process.env.APPDATA, 'Godot/app_userdata/Horde Shooter - Stage 1/settings.cfg'),
  path.join(process.env.APPDATA, 'Godot/app_userdata/Horde Shooter - Stage 1/appearance.cfg'),
  path.join(root, 'game/content/data/sidekick_presets.json')];
const snapshot = () => Promise.all(protectedFiles.map(async file => {
  try { return createHash('sha256').update(await readFile(file)).digest('hex'); }
  catch (error) { if (error.code === 'ENOENT') return null; throw error; }
}));
const before = await snapshot();
const library = await (await fetch(base + '/api/library')).json();
let checks = 0;
const check = (yes, message) => { assert.ok(yes, message); checks++; console.log('PASS ' + message); };
const expected = [];
for (const [prefix, folder] of [['pet', 'pets'], ['drone', 'drones']])
  for (const file of await readdir(path.join(root, 'game/assets', folder)))
    if (file.endsWith('_low.glb')) expected.push(`${prefix}:${file.replace('_low.glb', '')}`);
const presets = JSON.parse(await readFile(path.join(root, 'game/content/data/sidekick_presets.json'), 'utf8'));
expected.push(...presets.map(p => 'sidekick:' + p.id));
check(expected.every(id => library.objects.some(e => e.id === id && e.qualityMode !== 'single')), 'all actual quality pairs and sidekick presets grouped by object');
check(library.objects.find(e => e.id === 'container_open').properties.some(p => p.label === '탄환 차단' && p.value.includes('미연결')), 'custom map collision is not presented as pass-through');
await mkdir(path.join(root, 'studio/output'), { recursive: true });
const images = [];
for (const id of expected) {
  const response = await fetch(base + '/api/library/render', { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify({kind:'object',id,rotation:-25,zoom:1}) });
  const result = await response.json();
  check(response.ok && result.comparison?.length === 2, `${id}: paired render`);
  const [high, low] = result.comparison;
  check(high.quality === 'high' && low.quality === 'low' && high.baseTriangles > 0 && low.baseTriangles > 0, `${id}: quality labels and measured mesh data`);
  if (!id.startsWith('sidekick:')) check(low.baseTriangles < high.baseTriangles && low.png !== high.png, `${id}: real reduced mesh and different pixels`);
  for (const view of result.comparison) {
    const file = path.join(root, 'studio/output', `quality-${id.replace(':','-')}-${view.quality}.png`);
    await writeFile(file, Buffer.from(view.png,'base64')); images.push(file);
  }
}
const response = await fetch(base + '/api/library/render', {method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({kind:'object',id:'wooden_crate',rotation:-25,zoom:1})});
const single = await response.json();
check(response.ok && !single.comparison && single.png, 'single model still renders after comparison');
check(JSON.stringify(before) === JSON.stringify(await snapshot()), 'quality comparison preserves settings, appearance and source presets');
console.log(execFileSync('python',[path.join(root,'studio/tests/check-library-render.py'),...images],{encoding:'utf8'}).trim());
console.log(`STUDIO_QUALITY_PASS ${checks} pairs=${expected.length}`);
