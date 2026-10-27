// Studio 기획서 › 레벨디자인 menu: the map list, each map's zone features and the floor plan, read from the working folder.
import assert from 'node:assert/strict';
const origin = process.env.STUDIO_URL || 'http://127.0.0.1:4317';
let checks = 0;
const check = (value, message) => { assert.ok(value, message); checks++; console.log('PASS ' + message); };
const get = async (path, redirect = 'follow') => {
  const r = await fetch(origin + path, { redirect });
  const type = r.headers.get('content-type') || '';
  return { status: r.status, type, location: r.headers.get('location'), body: type.includes('json') ? await r.json() : await r.text() };
};

const plan = await get('/plan', 'manual');
check(plan.status === 302 && plan.location === '/plan/level', '기획서 opens its first section, 레벨디자인');
const page = await get('/plan/level');
check(page.status === 200 && page.body.includes('aria-current="page">기획서') && page.body.includes('aria-current="page">레벨디자인'), 'level page marks 기획서 and 레벨디자인 current');
for (const file of ['/level.js', '/level.css']) check((await get(file)).status === 200, `${file} served`);
for (const p of ['/', '/docs', '/monsters']) check((await get(p)).body.includes('href="/plan/level">기획서'), `top bar on ${p} has 기획서`);

const health = await get('/api/health');
check(['starting', 'ready', 'stopped'].includes(health.body.renderer) && typeof health.body.error === 'string', 'health tells the renderer state (starting / ready / stopped) and its last error');

const list = (await get('/api/levels')).body.levels;
const ids = list.map(l => l.id);
check(['stage1_large', 'stage2_large', 'stage3'].every(id => ids.includes(id)), 'every stage layout is listed');
check(list.every((l, i) => !i || list[i - 1].stage <= l.stage) && list.every(l => l.zones > 0 && l.route > 0), 'maps in stage order with zones and route length');
check(list.find(l => l.id === 'stage1_large').guides.map(g => g.kind).join() === 'plan,asis', 'Stage 1 has its plan and as-is guides');

const map = (await get('/api/levels/map?id=stage1_large')).body;
const zone = id => map.zones.find(z => z.id === id);
check(map.zones.length === 15 && map.guide.kind === 'plan' && zone('D').guide?.role === 'peak' && zone('P1').guide?.new?.[0] === 'melee_only', 'Stage 1 reads the tutorial plan guide: 15 zones, D is the peak, P1 teaches melee only');
check(zone('C1').devices.some(d => d.type === '기지') && zone('F').devices.some(d => d.type === '경보 장치') && zone('D').devices.some(d => d.label === '창고 간판') && zone('D').devices.some(d => d.type === '버튼 셔터'), 'devices land in their zones (base C1, the yard alarm truck in F, warehouse sign and the pallet lane shutter in D)');
check(zone('B').packs.length > 0 && zone('G').packs.some(p => p.special), 'packs land in their zones, the named special in G');
check(map.check && map.check.lines.some(l => l.startsWith('GUIDE_CHECK')), 'the guide check result is attached');
const bare = (await get('/api/levels/map?id=stage3')).body;
check(bare.guide === null && bare.check === null && bare.zones.length > 0 && bare.zones.every(z => !z.guide), 'a map without a guide still lists zones, packs and devices');

const floor = await get('/api/levels/floor?id=stage1_large&guide=plan');
check(floor.status === 200 && floor.type.includes('image/svg+xml') && floor.body.includes('<svg'), 'floor plan with the guide overlay is an SVG');
check((await get('/api/levels/floor?id=stage3&guide=none')).body.includes('<svg'), 'floor plan without a guide');
const space = await get('/api/levels/floor?id=stage1_large&guide=space');
check(space.status === 200 && space.body.includes('<svg') && space.body.includes('L-36'), 'space check (no wall in view, nothing meaningful near) draws as an SVG');
for (const bad of ['../x', 'stage1_large/../../x', 'C:/x', 'no_such_map'])
  check((await get('/api/levels/map?id=' + encodeURIComponent(bad))).status === 404, `refuses map ${bad}`);
check((await get('/api/levels/floor?id=stage3&guide=plan')).status === 404, 'a missing guide is 404');
console.log(`STUDIO_LEVELS_PASS ${checks}`);
