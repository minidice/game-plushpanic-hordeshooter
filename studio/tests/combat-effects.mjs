import assert from 'node:assert/strict';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
const root = fileURLToPath(new URL('../../', import.meta.url));
const base = process.env.STUDIO_URL || 'http://127.0.0.1:4317';
const source = path.join(root, 'game/content/data/combat_effects.json');
const before = await readFile(source, 'utf8');
const definitions = JSON.parse(before).effects;
const catalog = await (await fetch(base + '/api/media')).json();
let checks = 0;
const check = (condition, message) => { assert.ok(condition, message); checks++; console.log('PASS ' + message); };
const entries = catalog.effects.filter(e => e.id.startsWith('fx:'));
check(entries.length === definitions.length, 'Studio discovers every shared combat definition');
await mkdir(path.join(root, 'studio/output'), { recursive: true });
for (const def of definitions) {
  const entry = entries.find(e => e.id === 'fx:' + def.id);
  check(entry?.name === def.name && entry.source === 'res://content/data/combat_effects.json', def.id + ': identity and single source');
  check(JSON.stringify(entry.recipe) === JSON.stringify(def), def.id + ': complete raw recipe agrees with game JSON');
  check(entry.properties.some(p => p.label === '사용처' && p.value.includes(def.used_by[0])), def.id + ': runtime source displayed');
  if (process.argv.includes('--catalog-only')) continue;
  const response = await fetch(base + '/api/effects/render', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ id: entry.id }) });
  const result = await response.json();
  check(response.ok && result.frames.length > 3, def.id + ': actual game renderer');
  check(result.duration >= entry.duration - .001 && result.frames.at(-1).time >= entry.duration - .001, def.id + ': full lifetime without truncation');
  check(new Set(result.frames.map(f => f.png)).size > 3, def.id + ': animation advances');
  const folder = path.join(root, 'studio/output', 'combat-' + def.id);
  await mkdir(folder, {recursive: true});
  for (let i = 0; i < result.frames.length; i++) {
    const f = result.frames[i];
    await writeFile(path.join(folder, String(i).padStart(3, '0') + '.png'), Buffer.from(f.png, 'base64'));
  }
  await writeFile(path.join(folder, 'timing.json'), JSON.stringify({duration:result.duration,fps:result.fps,times:result.frames.map(f=>f.time)}));
}
check(await readFile(source, 'utf8') === before, 'inspection never changes shared definitions');
console.log(`STUDIO_COMBAT_EFFECTS_PASS ${checks} recipes=${definitions.length}`);
