// Real loopback API + Godot cache reload. Run only on an isolated Studio workspace.
import assert from 'node:assert/strict';
import { readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { request } from 'node:http';
const base = process.env.STUDIO_URL || 'http://127.0.0.1:4329';
const source = fileURLToPath(new URL('../../game/content/data/surface_profiles.json', import.meta.url));
const physics = fileURLToPath(new URL('../../game/content/data/object_interactions.json', import.meta.url));
const original = await readFile(source), physicsBefore = await readFile(physics);
const originalData = JSON.parse(original);
let checks = 0;
async function api(route, body, want = 200, headers = {}) {
  const response = await fetch(base + route, body === undefined ? {} : { method: 'POST',
    headers: { 'Content-Type': 'application/json', ...headers }, body: JSON.stringify(body) });
  const result = await response.json();
  assert.equal(response.status, want, JSON.stringify(result)); checks++; return result;
}
try {
  let state = await api('/api/surfaces');
  assert.match(state.revision, /^[a-f0-9]{64}$/);
  assert.equal(state.objects.sandbags, 'sandbag');
  assert.ok(state.usage.concrete.ground.length && state.usage.concrete.layouts.length);
  const patch = { revision: state.revision, objectId: 'sandbags', profileId: 'metal_sheet',
    gains: { step_db: -24, hit_db: 6, break_db: -3.5 } };
  for (const bad of [null, {}, { ...patch, revision: '' }, { ...patch, objectId: '../x' },
    { ...patch, objectId: 'pet:fox' }, { ...patch, objectId: 'runtime:fixture' },
    { ...patch, profileId: null }, { ...patch, profileId: 'unknown' },
    { ...patch, path: '../x' }, { ...patch, gains: { ...patch.gains, hp: 5 } },
    ...[-24.1, 6.1, null, '0'].map(value => ({ ...patch, gains: { ...patch.gains, hit_db: value } }))]) {
    await api('/api/surfaces', bad, 400);
    assert.deepEqual(await readFile(source), original);
  }
  await api('/api/surfaces', patch, 403, { Origin: 'https://example.com' });
  // fetch normalizes Host; use raw HTTP so the boundary under test receives the forged value.
  const foreignHost = await new Promise((resolve, reject) => {
    const req = request(base + '/api/surfaces', { headers: { Host: 'example.com' } }, res => { res.resume(); resolve(res.statusCode); });
    req.on('error', reject); req.end();
  });
  assert.equal(foreignHost, 403); checks++;
  await api('/api/surfaces', patch, 415, { 'Content-Type': 'text/plain' });
  const malformed = await fetch(base + '/api/surfaces', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{' });
  assert.equal(malformed.status, 400); checks++;
  const huge = await fetch(base + '/api/surfaces', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ text: 'x'.repeat(40000) }) });
  assert.equal(huge.status, 413); checks++;
  const infinite = await fetch(base + '/api/surfaces', { method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(patch).replace('"hit_db":6', '"hit_db":1e309') });
  assert.equal(infinite.status, 400); checks++;
  // Seed runtime cache before save; fresh catalog must reflect disk, not cached properties.
  const beforeLibrary = await api('/api/library');
  const saved = await api('/api/surfaces', patch);
  assert.notEqual(saved.revision, state.revision);
  assert.equal(saved.objects.sandbags, 'metal_sheet');
  assert.equal(saved.profiles.metal_sheet.hit_db, 6);
  const after = JSON.parse(await readFile(source));
  const expected = structuredClone(originalData);
  expected.objects.sandbags = 'metal_sheet'; Object.assign(expected.profiles.metal_sheet, patch.gains);
  assert.deepEqual(after, expected, 'only the selected mapping and three shared gains change'); checks++;
  await api('/api/surfaces', patch, 409);
  const afterLibrary = await api('/api/library');
  const sand = afterLibrary.objects.find(x => x.id === 'sandbags');
  assert.ok(sand.properties.find(x => x.label === '표면·소리 프리셋').value.includes('(metal_sheet)'));
  assert.ok(sand.properties.find(x => x.label.startsWith('추가 게인')).value.includes('6'));
  assert.deepEqual(sand.interaction, beforeLibrary.objects.find(x => x.id === 'sandbags').interaction);
  assert.equal(sand.interaction.max_health, 50); checks++;
  // Two tabs saving the same version: exactly one commit, one conflict.
  const writes = await Promise.all([1, 2].map(hit_db => fetch(base + '/api/surfaces', { method: 'POST',
    headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ...patch, revision: saved.revision,
      gains: { ...patch.gains, hit_db } }) })));
  assert.deepEqual(writes.map(r => r.status).sort(), [200, 409]); checks++;
  state = await api('/api/surfaces');
  // External edits to unrelated ground/layout data also invalidate the whole-file token.
  const external = JSON.parse(await readFile(source)); external.note += ' external fixture';
  await writeFile(source, JSON.stringify(external, null, 2));
  await api('/api/surfaces', { ...patch, revision: state.revision }, 409);
  assert.deepEqual(JSON.parse(await readFile(source)), external); checks++;
  external.objects['pet:fixture'] = 'metal_sheet';
  external.profiles.metal_sheet.future_metadata = { retained: ['unknown', 12] };
  await writeFile(source, JSON.stringify(external, null, 2));
  state = await api('/api/surfaces');
  await api('/api/surfaces', { ...patch, revision: state.revision, objectId: 'pet:fixture' }, 400);
  await api('/api/surfaces', { ...patch, revision: state.revision });
  const preserved = JSON.parse(await readFile(source));
  assert.equal(preserved.objects['pet:fixture'], 'metal_sheet');
  assert.deepEqual(preserved.profiles.metal_sheet.future_metadata, { retained: ['unknown', 12] });
  assert.deepEqual(preserved.ground, external.ground); assert.deepEqual(preserved.layouts, external.layouts); checks++;
  assert.deepEqual(await readFile(physics), physicsBefore); checks++;
} finally {
  await writeFile(source, original);
  // Restore the actual renderer cache too, even after a failing assertion.
  await api('/api/library');
}
assert.deepEqual(await readFile(source), original);
console.log(`STUDIO_SURFACE_EDITOR_PASS ${checks}; saved/reloaded, validation, conflicts, shared data and HP preserved; fixture restored`);
