import assert from 'node:assert/strict';
import {readFile, writeFile, mkdir} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import path from 'node:path';

const base = process.env.STUDIO_URL || 'http://127.0.0.1:4331';
const out = new URL('../output/goose-clean/', import.meta.url);
await mkdir(out, {recursive: true});
const realSave = path.join(process.env.APPDATA, 'Godot/app_userdata/Horde Shooter - Stage 1/appearance.cfg');
const preset = new URL('../../game/content/data/appearance_presets.json', import.meta.url);
async function hash(file) { try { return createHash('sha256').update(await readFile(file)).digest('hex'); } catch (error) { if (error.code === 'ENOENT') return null; throw error; } }
const before = await Promise.all([realSave, preset].map(hash));
let checks = 0;
function check(value, label) { assert.ok(value, label); checks++; }
const catalog = await (await fetch(base + '/api/catalog')).json();
const fields = new Set(catalog.species.find(item => item.id === 'goose').fields.map(item => item.key));
for (const key of ['body', 'body_secondary', 'beak_size', 'beak_style', 'face_style', 'eye_size', 'eye_spacing', 'wing_size', 'feet_size', 'body_width', 'body_height', 'hat', 'vest', 'goggles'])
  check(fields.has(key), 'goose control ' + key);

async function render(name, profile = {}, options = {}) {
  const response = await fetch(base + '/api/render', {method: 'POST', headers: {'Content-Type': 'application/json'},
    body: JSON.stringify({species: 'goose', fields: profile, rotation: 25, zoom: 1.15, gear: false, weapon: false, ...options})});
  const result = await response.json();
  check(response.ok && result.png?.length > 1000, 'render ' + name);
  const bytes = Buffer.from(result.png, 'base64');
  check(bytes.subarray(1, 4).toString() === 'PNG' && bytes.readUInt32BE(16) === 900, 'actual 900px PNG ' + name);
  await writeFile(new URL(name + '.png', out), bytes);
  return bytes;
}
const first = await render('default');
await render('front', {}, {rotation: 0});
await render('side', {}, {rotation: 90});
await render('rear', {}, {rotation: 180});
await render('weapon', {}, {weapon: true});
await render('equipped', {}, {gear: true, weapon: true});
for (const style of [1, 2]) await render('face-' + style, {face_style: style, beak_style: style});
for (const [name, profile] of Object.entries({
  small: {body_width: .88, body_height: 1.1, wing_size: .8, feet_size: .8, eye_size: .7, eye_spacing: .85, beak_size: .75},
  large: {body_width: 1.12, body_height: .9, wing_size: 1.25, feet_size: 1.25, eye_size: 1.45, eye_spacing: 1.12, beak_size: 1.25},
  recolored: {body: '#509cb2', body_secondary: '#edc581', feet: '#8150a6', eyes: '#222b35'},
})) await render(name, profile, {weapon: true});
const reset = await render('reset');
check(first.equals(reset), 'temporary profile restores exact default PNG');
assert.deepEqual(await Promise.all([realSave, preset].map(hash)), before); checks++;
console.log('STUDIO_GOOSE_CLEAN_PASS ' + checks);
