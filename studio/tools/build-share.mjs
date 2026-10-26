// Feedback pack builder (studio/share/README.md). Two steps:
//   node tools/build-share.mjs export --studio http://127.0.0.1:4317   photographs + GLBs every model in share/selection.json
//   node tools/build-share.mjs pack [--three <three package dir>]      writes share/packs/NNN-models/ and NNN-models.zip
// The pack opens from a plain folder (file://): three.js sits inline as data: modules, models load as small classic scripts.
// Only the selected models, the scene shots and this page go in — no docs, no game data, no Studio.
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const shareDir = path.resolve(here, '..', 'share');
const buildDir = path.join(shareDir, '.build');
const packsDir = path.join(shareDir, 'packs');
const selection = JSON.parse(fs.readFileSync(path.join(shareDir, 'selection.json'), 'utf8'));

const args = process.argv.slice(2);
const option = (name, fallback) => { const i = args.indexOf(name); return i >= 0 ? args[i + 1] : fallback; };

function entries() {
  const list = [];
  for (const group of selection.groups) {
    group.items.forEach((item, index) => {
      const key = `${group.key}${index + 1}`;
      const name = `${key}-${item.id}${item.gear ? '-gear' : ''}`.toLowerCase().replace(/_/g, '-');
      list.push({ ...item, key, name, group: group.key });
    });
  }
  return list;
}

async function exportAll() {
  const studio = option('--studio', 'http://127.0.0.1:4317').replace(/\/$/, '');
  const only = option('--only', '');
  const library = await (await fetch(`${studio}/api/library`)).json();
  const names = new Map(library.objects.map((o) => [o.id, o.name]));
  for (const item of entries()) {
    if (only && !only.split(',').includes(item.key)) continue;
    const started = Date.now();
    const response = await fetch(`${studio}/api/share/item`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Origin: studio },
      body: JSON.stringify({ kind: item.kind, id: item.id, gear: !!item.gear, name: item.name, frames: 16, size: 768 }),
    });
    const result = await response.json();
    if (!response.ok || result.error) throw new Error(`${item.key} ${item.id}: ${result.error ?? response.status}`);
    let title = item.title ?? result.title;
    if (item.kind === 'object' && !item.title) title = names.get(item.id) ?? item.id;
    if (item.kind === 'player') title += item.gear ? ' · 장비' : ' · 맨몸';
    fs.writeFileSync(path.join(buildDir, item.name, 'info.json'), JSON.stringify({ ...result, key: item.key, title }, null, 2));
    console.log(`${item.key.padEnd(3)} ${title}  meshes ${result.meshes}  h ${result.height.toFixed(2)}m  glb ${(result.glb / 1024).toFixed(0)}KB  ${Date.now() - started}ms`);
  }
}

function dataModule(code) { return 'data:text/javascript;base64,' + Buffer.from(code).toString('base64'); }

function importMap(threeDir) {
  const read = (p) => fs.readFileSync(path.join(threeDir, p), 'utf8');
  if (!fs.existsSync(path.join(threeDir, 'build', 'three.module.min.js'))) throw new Error(`three.js not found in ${threeDir} (--three)`);
  return {
    imports: {
      'three-core': dataModule(read('build/three.core.min.js')),
      three: dataModule(read('build/three.module.min.js').replaceAll('"./three.core.min.js"', '"three-core"')),
      'three-bgu': dataModule(read('examples/jsm/utils/BufferGeometryUtils.js')),
      'three-gltf': dataModule(read('examples/jsm/loaders/GLTFLoader.js').replace("'../utils/BufferGeometryUtils.js'", "'three-bgu'")),
      'three-orbit': dataModule(read('examples/jsm/controls/OrbitControls.js')),
    },
  };
}

function nextPackName() {
  fs.mkdirSync(packsDir, { recursive: true });
  const used = fs.readdirSync(packsDir).map((n) => Number(/^(\d{3})-/.exec(n)?.[1] ?? 0));
  return `${String(Math.max(0, ...used) + 1).padStart(3, '0')}-models`;
}

function pack() {
  const threeDir = path.resolve(option('--three', path.resolve(here, '..', '..', 'prototypes', 'character-lab', 'node_modules', 'three')));
  const name = option('--name', nextPackName());
  const out = path.join(packsDir, name);
  fs.rmSync(out, { recursive: true, force: true });
  fs.mkdirSync(path.join(out, 'models'), { recursive: true });

  const cards = [];
  for (const item of entries()) {
    const source = path.join(buildDir, item.name);
    const infoPath = path.join(source, 'info.json');
    if (!fs.existsSync(infoPath)) throw new Error(`${item.key} not exported yet (${item.name}); run export first`);
    const info = JSON.parse(fs.readFileSync(infoPath, 'utf8'));
    const photos = path.join(out, 'photos', item.name);
    fs.mkdirSync(photos, { recursive: true });
    for (const file of fs.readdirSync(source).filter((f) => f.endsWith('.webp'))) fs.copyFileSync(path.join(source, file), path.join(photos, file));
    const glb = fs.readFileSync(path.join(source, 'model.glb')).toString('base64');
    fs.writeFileSync(path.join(out, 'models', `${item.name}.js`), `(window.__glb=window.__glb||{})[${JSON.stringify(item.name)}]=${JSON.stringify(glb)};\n`);
    cards.push({ key: item.key, group: item.group, name: item.name, title: info.title, kind: item.kind, frames: info.frames,
      rings: info.rings.map((r) => r.Name), height: Number(info.height.toFixed(2)), width: Number(info.width.toFixed(2)) });
  }

  const scenes = [];
  fs.mkdirSync(path.join(out, 'scenes'), { recursive: true });
  for (const scene of selection.scenes) {
    const file = path.join(buildDir, 'scenes', scene.file);
    if (!fs.existsSync(file)) { console.warn(`scene ${scene.key} missing (${file}), skipped`); continue; }
    fs.copyFileSync(file, path.join(out, 'scenes', scene.file));
    scenes.push(scene);
  }

  const page = {
    title: selection.title, intro: selection.intro, questions: selection.questions, pack: name,
    groups: selection.groups.map((g) => ({ key: g.key, title: g.title, note: g.note })), cards, scenes,
  };
  const template = fs.readFileSync(path.join(here, 'share-page.html'), 'utf8');
  const html = template
    .replace('/*IMPORTMAP*/', () => JSON.stringify(importMap(threeDir)))
    .replace('/*PAGE*/', () => JSON.stringify(page).replace(/</g, '\\u003c'))
    .replaceAll('{{TITLE}}', selection.title);
  fs.writeFileSync(path.join(out, 'index.html'), html);

  const zip = path.join(packsDir, `${name}.zip`);
  fs.rmSync(zip, { force: true });
  // Windows' own bsdtar writes zip with -a; a Git Bash GNU tar earlier on PATH would not.
  const tar = process.platform === 'win32' ? path.join(process.env.SystemRoot ?? 'C:\\Windows', 'System32', 'tar.exe') : 'bsdtar';
  execFileSync(tar, ['-a', '-c', '-f', path.basename(zip), name], { cwd: packsDir });
  const size = (p) => fs.statSync(p).size;
  const total = (dir) => fs.readdirSync(dir, { withFileTypes: true }).reduce((sum, e) => sum + (e.isDirectory() ? total(path.join(dir, e.name)) : size(path.join(dir, e.name))), 0);
  console.log(`pack ${out}\n  ${cards.length} models, ${scenes.length} scenes, folder ${(total(out) / 1048576).toFixed(1)}MB, zip ${(size(zip) / 1048576).toFixed(1)}MB\n  ${zip}`);
}

const command = args[0];
if (command === 'export') await exportAll();
else if (command === 'pack') pack();
else { console.log('usage: node tools/build-share.mjs export [--studio URL] [--only P1,M2] | pack [--three DIR] [--name NNN-models]'); process.exitCode = 1; }
