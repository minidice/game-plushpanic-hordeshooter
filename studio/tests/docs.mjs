// Studio md menu (T-26): document list, reading, full-text search, and the read-only path guard.
import assert from 'node:assert/strict';
const origin = process.env.STUDIO_URL || 'http://127.0.0.1:4317';
let checks = 0;
const check = (value, message) => { assert.ok(value, message); checks++; console.log('PASS ' + message); };
const get = async path => { const r = await fetch(origin + path); return { status: r.status, body: r.headers.get('content-type')?.includes('json') ? await r.json() : await r.text() }; };

const page = await get('/docs');
check(page.status === 200 && page.body.includes('문서 · md') && page.body.includes('aria-current="page">md'), 'md page served with md marked current');
for (const file of ['/docs.js', '/docs.css']) check((await get(file)).status === 200, `${file} served`);
const home = await get('/');
check(home.body.includes('class="top-nav"') && home.body.includes('href="/docs"') && home.body.includes('aria-current="page">리소스'), 'resource pages carry the top bar: 리소스 | md');

const list = await get('/api/docs');
const paths = list.body.docs.map(d => d.path);
for (const p of ['AGENTS.md', 'doc/decisions.md', 'doc/issues.md', 'doc/feature-checklist.md', 'doc/features/README.md', 'doc/features/combat/log/README.md', 'doc/work-process.md'])
  check(paths.includes(p), `listed: ${p}`);
check(list.body.docs.filter(d => d.path.startsWith('doc/features/')).length >= 45, 'all 11 feature folders listed (README, decisions, issues, log)');
check(list.body.docs.every(d => d.title && d.group), 'every document has a title and a group');
check(list.body.docs.find(d => d.path === 'doc/issues.md').group.includes('옛 기록'), 'the three index docs are grouped as index and old records');

const read = await get('/api/docs/file?path=' + encodeURIComponent('doc/features/README.md'));
check(read.status === 200 && read.body.text.includes('# 기능별 기록') && read.body.text.includes('옛 기록은 옮기지 않았다'), 'reads a document, with the old-records note');
for (const bad of ['../game/project.godot', 'doc/../../game/project.godot', 'game/project.godot', 'doc/../AGENTS.txt', 'C:/Windows/win.ini', '/etc/passwd', 'doc/../../outside.md'])
  check((await get('/api/docs/file?path=' + encodeURIComponent(bad))).status === 400, `refuses ${bad}`);
check((await get('/api/docs/file?path=' + encodeURIComponent('doc/no-such-file.md'))).status === 404, 'a missing document is 404');

const hit = await get('/api/docs/search?q=' + encodeURIComponent('T-26 작업 방식'));
check(hit.body.total > 0 && hit.body.results.some(r => r.path === 'doc/decisions.md' && r.line > 0), 'search finds every word on a line, with path and line number');
check((await get('/api/docs/search?q=a')).body.results.length === 0, 'a one-letter search returns nothing');
console.log(`STUDIO_DOCS_PASS ${checks}`);
