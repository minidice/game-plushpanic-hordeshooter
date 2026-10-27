// Studio's 기획서 › 레벨디자인 menu: every stage layout draft (game/content/data/stages/*_layout.json) with what each
// zone holds — the level guide map (doc/features/level/guide-map.md, L-33), packs, devices — and its floor plan drawn by
// tools/floorplan/render_floorplan.py. Read straight from the working folder, no renderer needed. Read only.
import { readFile, readdir, mkdir, stat } from 'node:fs/promises';
import { spawn } from 'node:child_process';
import path from 'node:path';

const stagesDir = 'game/content/data/stages';
const guideDir = stagesDir + '/guide';
const themes = { urban: '도시', forest: '숲' };
const markerNames = {
  start_room: '세이프룸', start: '출발', mission: '임무', safe_zone: '중간 안전 구역', safehouse: '세이프하우스',
  landmark: '랜드마크', blocked: '막힌 길', noise: '경보 장치', base: '기지', switch_gate: '버튼 셔터',
};
const reactions = { dormant: '잠든', watcher: '경계형', wave: '웨이브형' };

const readJson = async (root, rel) => JSON.parse(await readFile(path.join(root, rel), 'utf8'));
const exists = async (root, rel) => { try { await stat(path.join(root, rel)); return true; } catch { return false; } };

/** A layout id (the file name before _layout.json) the menu may read, or null. */
export function safeLevel(id) {
  const text = String(id || '');
  return /^[a-z0-9_]{1,40}$/.test(text) ? text : null;
}

async function stageNames(root) {
  const names = {};
  try {
    for (const line of (await readFile(path.join(root, 'game/content/lan/kor/ui.csv'), 'utf8')).split('\n')) {
      const m = line.match(/^ui_hud_stage(\d?),(.+)$/);
      if (m) names[Number(m[1] || 1)] = m[2].trim().replace(/^STAGE \d+ · /, '');
    }
  } catch { /* no names */ }
  return names;
}

async function guidesOf(root, id) {
  const guides = [];
  for (const [kind, label] of [['plan', '설계안'], ['asis', '현재 상태']]) {
    const file = `${guideDir}/${id}_${kind}_guide.json`;
    if (await exists(root, file)) guides.push({ kind, label, file });
  }
  return guides;
}

const routeLength = points => points.reduce((sum, p, i) => i ? sum + Math.hypot(p[0] - points[i - 1][0], p[1] - points[i - 1][1]) : 0, 0);

export async function listLevels(root) {
  const names = await stageNames(root);
  const levels = [];
  for (const file of (await readdir(path.join(root, stagesDir))).filter(f => f.endsWith('_layout.json')).sort()) {
    const id = file.slice(0, -'_layout.json'.length);
    const layout = await readJson(root, `${stagesDir}/${file}`);
    const zones = layout.zones || [];
    levels.push({
      id, file: `${stagesDir}/${file}`, stage: layout.stage, name: names[layout.stage] || '', theme: themes[layout.theme] || layout.theme || '',
      zones: zones.length, large: zones.filter(z => z.size === 'large').length,
      route: Math.round(routeLength(layout.routes?.main || [])), guides: await guidesOf(root, id),
    });
  }
  levels.sort((a, b) => a.stage - b.stage || a.id.localeCompare(b.id));
  return levels;
}

function inside(p, poly) {
  let hit = false;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    const [xi, zi] = poly[i], [xj, zj] = poly[j];
    if ((zi > p[1]) !== (zj > p[1]) && p[0] < (xj - xi) * (p[1] - zi) / (zj - zi) + xi) hit = !hit;
  }
  return hit;
}

/** The zone at a point; later zones cover earlier ones, as StageLayout.ZoneAt. */
const zoneAt = (zones, p) => [...zones].reverse().find(z => z.poly?.length && inside(p, z.poly))?.id || '';

function runPython(root, args) {
  return new Promise(resolve => {
    const task = spawn('python', args, { cwd: root, windowsHide: true, env: { ...process.env, PYTHONIOENCODING: 'utf-8' } });
    let out = '';
    const timer = setTimeout(() => task.kill(), 30000);
    task.stdout.on('data', d => out += d);
    task.stderr.on('data', d => out += d);
    task.on('error', error => { clearTimeout(timer); resolve({ code: -1, out: 'python을 실행하지 못했습니다: ' + error.message }); });
    task.on('exit', code => { clearTimeout(timer); resolve({ code, out }); });
  });
}

export async function readLevel(root, rawId) {
  const id = safeLevel(rawId);
  const file = id && `${stagesDir}/${id}_layout.json`;
  if (!file || !await exists(root, file)) return { status: 404, body: { error: '맵이 없습니다: ' + rawId } };
  const layout = await readJson(root, file);
  const zones = layout.zones || [];
  const guides = await guidesOf(root, id);
  const guide = guides[0] ? await readJson(root, guides[0].file) : null;
  const catalog = await readJson(root, `${guideDir}/experiences.json`).catch(() => null);
  const encountersFile = `${stagesDir}/${id}_encounters.json`;
  const encounters = await exists(root, encountersFile) ? await readJson(root, encountersFile) : {};

  const rows = zones.map(z => ({ id: z.id, label: z.label, size: z.size, branch: !!z.branch, packs: [], devices: [] }));
  const row = id => rows.find(r => r.id === id);
  for (const g of encounters.groups || []) {
    // A pack fills rect [x0, x1, z0, z1]; a named special stands at [x, z] (SpawnDatabase).
    const at = g.at || (g.rect ? [(g.rect[0] + g.rect[1]) / 2, (g.rect[2] + g.rect[3]) / 2] : null);
    const r = at && row(zoneAt(zones, at));
    if (r) r.packs.push({ reaction: reactions[g.reaction] || g.reaction, count: g.count || 0, special: Number.isInteger(g.special) && g.special >= 0 });
  }
  for (const m of layout.markers || []) {
    const r = row(zoneAt(zones, m.pos));
    if (r) r.devices.push({ type: markerNames[m.type] || m.type, label: m.label || '', model: m.model || '' });
  }
  if (guide) for (const entry of guide.zones || []) Object.assign(row(entry.id) || {}, { guide: entry });

  // The checker reads every stage's guide in order (plan before as-is) and carries what earlier stages taught.
  let check = null;
  if (guides.length) {
    const result = await runPython(root, ['tools/level-guide/guide.py', 'check']);
    const lines = result.out.split(/\r?\n/).filter(l => l.startsWith('읽음') || l.includes(`Stage ${layout.stage} `) || l.startsWith('GUIDE_CHECK'));
    check = { code: result.code, lines };
  }

  return {
    status: 200,
    body: {
      id, file, stage: layout.stage, theme: themes[layout.theme] || layout.theme || '', next: layout.next_stage || 0,
      bounds: layout.bounds, route: Math.round(routeLength(layout.routes?.main || [])),
      guides, guide: guide && { file: guides[0].file, kind: guides[0].kind, status: guide.status || '', approved: guide.approved || '', applied: guide.applied || '' },
      catalog: catalog && { experiences: Object.fromEntries((catalog.experiences || []).map(e => [e.id, e.name])), roles: catalog.roles || {}, wayfinding: catalog.wayfinding || {} },
      zones: rows, check,
    },
  };
}

/** The floor plan SVG, drawn fresh into studio/output/levels with or without a guide overlay. */
export async function levelFloor(root, rawId, guideKind) {
  const id = safeLevel(rawId);
  const layout = id && `${stagesDir}/${id}_layout.json`;
  if (!layout || !await exists(root, layout)) return { status: 404, error: '맵이 없습니다: ' + rawId };
  if (guideKind === 'space') {
    // Space check (L-36 · L-37): where no wall shows at the widest view, where nothing meaningful is near.
    const outDir = path.join(root, 'studio', 'output', 'levels');
    await mkdir(outDir, { recursive: true });
    const out = path.join(outDir, `${id}-space.svg`);
    const result = await runPython(root, ['tools/level-guide/space.py', layout, out]);
    if (result.code !== 0) return { status: 500, error: '공간 검사를 그리지 못했습니다. ' + result.out.trim().split('\n').slice(-3).join(' ') };
    return { status: 200, svg: await readFile(out), report: result.out.trim() };
  }
  const guide = ['plan', 'asis'].includes(guideKind) ? `${guideDir}/${id}_${guideKind}_guide.json` : null;
  if (guide && !await exists(root, guide)) return { status: 404, error: '가이드가 없습니다: ' + guide };
  const outDir = path.join(root, 'studio', 'output', 'levels');
  await mkdir(outDir, { recursive: true });
  const out = path.join(outDir, `${id}-${guide ? guideKind : 'plain'}.svg`);
  const args = ['tools/floorplan/render_floorplan.py', layout, out];
  if (guide) args.push('--guide', guide);
  const result = await runPython(root, args);
  if (result.code !== 0) return { status: 500, error: '평면도를 그리지 못했습니다. ' + result.out.trim().split('\n').slice(-3).join(' ') };
  return { status: 200, svg: await readFile(out), report: result.out.trim() };
}
