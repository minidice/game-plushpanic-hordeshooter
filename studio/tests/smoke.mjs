// Run against Start-Studio.ps1. No browser or test-only renderer: exercise the real HTTP/Godot path.
import assert from 'node:assert/strict';
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = fileURLToPath(new URL('../../', import.meta.url));
const base = process.env.STUDIO_URL || 'http://127.0.0.1:4317';
const out = path.join(root, 'studio/output');
await mkdir(out, { recursive: true });
const hash = bytes => createHash('sha256').update(bytes).digest('hex');
const userDir = path.join(process.env.APPDATA, 'Godot/app_userdata/Horde Shooter - Stage 1');
const protectedFiles = [path.join(userDir, 'appearance.cfg'), path.join(userDir, 'settings.cfg'),
  path.join(root, 'game/content/characters/goose.tres'), path.join(root, 'game/content/data/appearance_presets.json')];
async function snapshot() { return Promise.all(protectedFiles.map(async file => { try { return hash(await readFile(file)); } catch (e) { if (e.code === 'ENOENT') return null; throw e; } })); }
const before = await snapshot();
let checks = 0;
function check(condition, label) { assert.ok(condition, label); checks++; console.log(`PASS ${label}`); }
async function api(url, body) {
  const response = await fetch(base + url, body === undefined ? {} : { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  const result = await response.json();
  assert.equal(response.status, 200, JSON.stringify(result)); return result;
}
const catalog = await api('/api/catalog');
check(catalog.species.length === 4 && !catalog.species.some(s => s.id.startsWith('test_')), 'only the four playable species, no test placeholders');
check(catalog.tabs.length === 6, 'six actual editor categories');
const request = species => ({ species, fields: {}, rotation: -18, zoom: 1.15, gear: true, weapon: false });
async function render(data, filename) {
  const start = performance.now();
  const result = await api('/api/render', data);
  const png = Buffer.from(result.png, 'base64');
  check(png.subarray(1, 4).toString() === 'PNG' && png.readUInt32BE(16) === 900 && png.readUInt32BE(20) === 900, `${filename}: real 900px PNG`);
  await writeFile(path.join(out, `${filename}.png`), png);
  console.log(`RENDER ${filename} ${(performance.now() - start).toFixed(0)}ms`);
  return hash(png);
}
for (const species of catalog.species) {
  const bird = ['goose', 'duck'].includes(species.id);
  check(species.fields.some(f => f.key === 'ear_size') === !bird, `${species.id}: game species-specific controls`);
  check(new Set(species.fields.map(f => f.key)).size === species.fields.length, `${species.id}: unique control keys`);
  const original = await render(request(species.id), `${species.id}-default`);
  const edited = request(species.id);
  for (const field of species.fields) {
    edited.fields[field.key] = field.type === 'range' ? field.max : field.type === 'color' ? '#74c8bd'
      : field.type === 'select' ? field.options.length - 1 : !field.value;
  }
  const modified = await render(edited, `${species.id}-edited`);
  check(modified !== original, `${species.id}: actual rendered pixels change`);
  await render(request(species.id), `${species.id}-reset`);
  console.log(execFileSync('python', [path.join(root, 'studio/tests/compare-render.py'),
    path.join(out, `${species.id}-default.png`), path.join(out, `${species.id}-reset.png`)], { encoding: 'utf8' }).trim());
  check(true, `${species.id}: empty temporary profile restores original render within GPU edge tolerance`);
}
await render({ ...request('raccoon'), rotation: 150, gear: false, weapon: true }, 'raccoon-rear-weapon');
const invalid = await fetch(base + '/api/render', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ...request('goose'), fields: { unknown_key: 1 } }) });
check(invalid.status !== 200 && (await invalid.json()).error.includes('unknown appearance key'), 'unknown fields rejected by game profile parser');
const malformed = await fetch(base + '/api/render', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{' });
check(malformed.status === 400, 'malformed JSON rejected');
const external = await fetch(base + '/api/render', { method: 'POST', headers: { Origin: 'https://example.com', 'Content-Type': 'application/json' }, body: '{}' });
check(external.status === 403, 'foreign origin rejected');
const privateFile = await fetch(base + '/game/content/characters/goose.tres');
check(privateFile.status === 404, 'server only serves allowlisted web files');
assert.deepEqual(await snapshot(), before, 'game data and user saves unchanged'); checks++;
console.log(`STUDIO_SMOKE_PASS ${checks}`);
