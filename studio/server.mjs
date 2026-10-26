import http from 'node:http';
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import { readFile, mkdir, appendFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { listDocs, readDoc, searchDocs } from './docs.mjs';

const root = fileURLToPath(new URL('../', import.meta.url));
const game = path.join(root, 'game');
const studio = path.join(root, 'studio');
const args = process.argv.slice(2);
const option = (key, fallback) => args.includes(key) ? args[args.indexOf(key) + 1] : fallback;
const godot = option('--godot', 'D:\\Util\\Godot\\Godot_v4.7.2-stable_mono_win64\\Godot_v4.7.2-stable_mono_win64_console.exe');
const port = Number(option('--port', '4317'));
if (!Number.isInteger(port) || port < 1024 || port > 65535) throw new Error('Invalid port');
const origin = `http://127.0.0.1:${port}`;
await mkdir(path.join(studio, 'output'), { recursive: true });
const logPath = path.join(studio, 'output', 'renderer.log');
const log = text => appendFile(logPath, text).catch(() => {});
let child, pending, sequence = 0, ready = false, reloading = false, stopping = false;

function run(command, arguments_) {
  return new Promise((resolve, reject) => {
    const task = spawn(command, arguments_, { cwd: game, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
    task.stdout.on('data', data => log(data));
    task.stderr.on('data', data => log(data));
    task.on('error', reject);
    task.on('exit', code => code === 0 ? resolve() : reject(new Error(`${command} failed (${code}); see studio/output/renderer.log`)));
  });
}

async function startRenderer() {
  ready = false;
  console.log('Building current local game and importing assets…');
  await run('dotnet', ['build', 'HordeShooter.csproj', '--nologo', '-v', 'q']);
  await run(godot, ['--headless', '--editor', '--import', '--path', game, '--quit']);
  await new Promise((resolve, reject) => {
    const renderer = spawn(godot, ['--path', game, '--position', '-10000,-10000', '--resolution', '64x64',
      '--max-fps', '30', '--audio-driver', 'Dummy', 'res://tests/StudioPreview.tscn'],
    { cwd: game, windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
    child = renderer;
    const timeout = setTimeout(() => { renderer.kill(); reject(new Error('Godot renderer startup timed out')); }, 60000);
    renderer.stdin.on('error', error => {
      if (child !== renderer || !pending) return;
      clearTimeout(pending.timeout);
      pending.reject(error); pending = null;
    });
    renderer.stderr.on('data', data => log(data));
    createInterface({ input: renderer.stdout }).on('line', line => {
      if (!line.startsWith('STUDIO:')) { log(line + '\n'); return; }
      let message;
      try { message = JSON.parse(line.slice(7)); } catch { return; }
      if (message.ready) { clearTimeout(timeout); ready = true; resolve(); }
      else if (pending && message.id === pending.id) {
        clearTimeout(pending.timeout);
        const current = pending; pending = null;
        message.error ? current.reject(new Error(message.error)) : current.resolve(message.result);
      }
    });
    renderer.on('error', error => { clearTimeout(timeout); reject(error); });
    renderer.on('exit', code => {
      clearTimeout(timeout);
      if (child === renderer) ready = false;
      if (pending) { clearTimeout(pending.timeout); pending.reject(new Error('Godot renderer stopped')); pending = null; }
      reject(new Error(`Godot renderer exited (${code})`));
    });
  });
}

function request(method, data = {}) {
  if (!ready || reloading) throw new Error('렌더러 준비 중입니다. 잠시 후 다시 시도하세요.');
  if (pending) throw new Error('다른 렌더 요청을 처리 중입니다.');
  const response = new Promise((resolve, reject) => {
    const id = ++sequence;
    const renderer = child;
    const timeout = setTimeout(() => {
      if (pending?.id !== id) return;
      pending = null; ready = false; renderer.kill();
      reject(new Error('렌더 시간이 초과됐습니다. 로컬 데이터 다시 읽기를 눌러 주세요.'));
    }, 30000);
    pending = { id, resolve, reject, timeout };
    renderer.stdin.write(JSON.stringify({ id, method, data }) + '\n');
  });
  pending.done = response;
  return response;
}

async function readCatalog(method) {
  // A page navigation can abandon HTTP while the old render still finishes in Godot.
  const deadline = Date.now() + 30000;
  while (pending) {
    await pending.done;
    if (Date.now() > deadline) throw new Error('목록 대기 시간이 초과됐습니다. 다시 시도하세요.');
  }
  return request(method);
}

async function stopRenderer() {
  const renderer = child;
  if (!renderer || renderer.exitCode !== null) return;
  await new Promise(resolve => {
    const timer = setTimeout(() => renderer.kill(), 3000);
    renderer.once('exit', () => { clearTimeout(timer); resolve(); });
    renderer.stdin.end();
  });
}

const staticFiles = new Map([
  ['/roster', ['roster.html', 'text/html; charset=utf-8']],
  ['/roster.js', ['roster.js', 'text/javascript; charset=utf-8']],
  ['/roster.css', ['roster.css', 'text/css; charset=utf-8']],
  ['/statuses', ['statuses.html', 'text/html; charset=utf-8']],
  ['/statuses.js', ['statuses.js', 'text/javascript; charset=utf-8']],
  ['/statuses.css', ['statuses.css', 'text/css; charset=utf-8']],
  ['/', ['index.html', 'text/html; charset=utf-8']],
  ['/app.js', ['app.js', 'text/javascript; charset=utf-8']],
  ['/style.css', ['style.css', 'text/css; charset=utf-8']],
  ['/monsters', ['library.html', 'text/html; charset=utf-8']],
  ['/objects', ['library.html', 'text/html; charset=utf-8']],
  ['/library.js', ['library.js', 'text/javascript; charset=utf-8']],
  ['/library.css', ['library.css', 'text/css; charset=utf-8']],
  ['/effects', ['media.html', 'text/html; charset=utf-8']],
  ['/sounds', ['media.html', 'text/html; charset=utf-8']],
  ['/media.js', ['media.js', 'text/javascript; charset=utf-8']],
  ['/media.css', ['media.css', 'text/css; charset=utf-8']],
  ['/weapons', ['weapons.html', 'text/html; charset=utf-8']],
  ['/weapons.js', ['weapons.js', 'text/javascript; charset=utf-8']],
  ['/weapons.css', ['weapons.css', 'text/css; charset=utf-8']],
  ['/docs', ['docs.html', 'text/html; charset=utf-8']],
  ['/docs.js', ['docs.js', 'text/javascript; charset=utf-8']],
  ['/docs.css', ['docs.css', 'text/css; charset=utf-8']],
]);
const server = http.createServer(async (req, res) => {
  res.setHeader('Cache-Control', 'no-store');
  res.setHeader('X-Content-Type-Options', 'nosniff');
  res.setHeader('Content-Security-Policy', "default-src 'self'; img-src 'self' data: blob:; media-src 'self' blob:; style-src 'self'; script-src 'self'; frame-ancestors 'none'");
  const json = (status, data) => { res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8' }); res.end(JSON.stringify(data)); };
  try {
    if (req.headers.host !== `127.0.0.1:${port}` || (req.headers.origin && req.headers.origin !== origin)) {
      json(403, { error: 'Local Studio origin only' }); return;
    }
    const url = new URL(req.url, origin);
    if (req.method === 'GET' && url.pathname === '/api/health') {
      json(200, { app: 'plush-panic-studio', workspace: root, ready: ready && !reloading }); return;
    }
    if (req.method === 'GET' && staticFiles.has(url.pathname)) {
      const [name, mime] = staticFiles.get(url.pathname);
      res.writeHead(200, { 'Content-Type': mime }); res.end(await readFile(path.join(studio, 'web', name))); return;
    }
    // md menu: read straight from the working folder, no renderer needed (T-26).
    if (req.method === 'GET' && url.pathname === '/api/docs') {
      json(200, { docs: await listDocs(root), loadedAt: new Date().toISOString() }); return;
    }
    if (req.method === 'GET' && url.pathname === '/api/docs/file') {
      const doc = await readDoc(root, url.searchParams.get('path')); json(doc.status, doc.body); return;
    }
    if (req.method === 'GET' && url.pathname === '/api/docs/search') {
      json(200, await searchDocs(root, url.searchParams.get('q'))); return;
    }
    if (req.method === 'GET' && url.pathname === '/api/catalog') {
      json(200, await readCatalog('catalog')); return;
    }
    if (req.method === 'GET' && url.pathname === '/api/roster') {
      json(200, await readCatalog('roster')); return;
    }
    if (req.method === 'GET' && url.pathname === '/api/statuses') {
      json(200, await readCatalog('statuses')); return;
    }
    if (req.method === 'GET' && url.pathname === '/api/library') {
      json(200, await readCatalog('library')); return;
    }
    if (req.method === 'GET' && url.pathname === '/api/weapons') {
      json(200, await readCatalog('weapons')); return;
    }
    if (req.method === 'GET' && url.pathname === '/api/media') {
      json(200, await readCatalog('media')); return;
    }
    if (req.method === 'POST' && url.pathname === '/api/reload') {
      if (pending || reloading) { json(409, { error: '렌더가 끝난 뒤 다시 시도하세요.' }); return; }
      reloading = true;
      try { await stopRenderer(); await startRenderer(); }
      finally { reloading = false; }
      json(200, { ready: true }); return;
    }
    if (req.method === 'POST' && ['/api/render', '/api/library/render', '/api/effects/render', '/api/sounds/preview', '/api/statuses/render', '/api/roster/render'].includes(url.pathname)) {
      if (req.headers['content-type'] !== 'application/json') { json(415, { error: 'JSON required' }); return; }
      let body = '';
      for await (const chunk of req) {
        body += chunk;
        if (Buffer.byteLength(body) > 32768) { json(413, { error: 'Request too large' }); return; }
      }
      let data;
      try { data = JSON.parse(body); } catch { json(400, { error: 'Invalid JSON' }); return; }
      if (url.pathname === '/api/roster/render') {
        if (!data || ['faction','species','palette','weapon'].some(key => typeof data[key] !== 'string' || data[key].length > 64)
            || (data.pack !== undefined && (typeof data.pack !== 'string' || data.pack.length > 64))
            || !['individual','pack','attack'].includes(data.mode) || !Number.isInteger(data.variant) || data.variant < 0 || data.variant > 9
            || (data.mode === 'attack' && (data.faction !== 'hunter' || !['club','knife','woodbow'].includes(data.weapon)))
            || !Number.isInteger(data.gear) || data.gear < 0 || data.gear > 3
            || !Number.isFinite(data.rotation) || data.rotation < -180 || data.rotation > 180
            || !Number.isFinite(data.zoom) || data.zoom < .75 || data.zoom > 1.45) {
          json(400, { error: 'Invalid roster preview request' }); return;
        }
        json(200, await request('roster_render', data)); return;
      }
      if (url.pathname === '/api/statuses/render') {
        if (!data || typeof data.target !== 'string' || data.target.length > 128 || !Array.isArray(data.states)
            || data.states.length > 12 || typeof data.hit !== 'boolean' || typeof data.release !== 'boolean'
            || data.states.some(s => !s || typeof s.id !== 'string' || s.id.length > 64 || !Number.isInteger(s.stacks) || s.stacks < 1 || s.stacks > 5)) {
          json(400, { error: 'Invalid status preview request' }); return;
        }
        json(200, await request('status_render', data)); return;
      }
      if (url.pathname === '/api/effects/render' || url.pathname === '/api/sounds/preview') {
        if (!data || typeof data.id !== 'string' || data.id.length > 128) {
          json(400, { error: 'Invalid media preview request' }); return;
        }
        json(200, await request(url.pathname === '/api/effects/render' ? 'effect_render' : 'sound_preview', data)); return;
      }
      if (url.pathname === '/api/library/render') {
        if (!data || !['monster', 'object'].includes(data.kind) || typeof data.id !== 'string'
            || !Number.isFinite(data.rotation) || !Number.isFinite(data.zoom)
            || (data.variant !== undefined && typeof data.variant !== 'string')) {
          json(400, { error: 'Invalid library preview request' }); return;
        }
        json(200, await request('library_render', data)); return;
      }
      if (!data || typeof data.species !== 'string' || !data.fields || Array.isArray(data.fields) || typeof data.fields !== 'object'
          || !Number.isFinite(data.rotation) || !Number.isFinite(data.zoom) || typeof data.gear !== 'boolean' || typeof data.weapon !== 'boolean'
          || (data.weapon_id !== undefined && typeof data.weapon_id !== 'string')) {
        json(400, { error: 'Invalid preview request' }); return;
      }
      json(200, await request('render', data)); return;
    }
    json(404, { error: 'Not found' });
  } catch (error) { if (!res.headersSent) json(503, { error: error.message }); else res.end(); }
});
server.requestTimeout = 45000;
await startRenderer();
server.on('error', error => { console.error(error.message); child?.kill(); process.exitCode = 1; });
server.listen(port, '127.0.0.1', () => console.log(`Studio ready: ${origin}`));
async function shutdown() {
  if (stopping) return;
  stopping = true;
  server.close(); await stopRenderer(); process.exit();
}
process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);
process.on('exit', () => child?.kill());
