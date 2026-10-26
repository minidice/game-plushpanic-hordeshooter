// Studio's md menu (decisions.md T-26): every project Markdown document — list, read, full-text search — straight
// from the working folder on disk, so it works while the Godot renderer is still starting. Read only.
import { readFile, readdir, stat } from 'node:fs/promises';
import path from 'node:path';

const folders = ['doc', 'docs'];
const singles = ['AGENTS.md', 'README.md', 'game/README.md', 'studio/README.md'];
const indexes = ['doc/decisions.md', 'doc/issues.md', 'doc/feature-checklist.md'];

/** Which shelf a document sits on in the list. */
export function docGroup(file) {
  if (file.startsWith('doc/features/')) return '기능 · ' + file.split('/')[2];
  if (indexes.includes(file)) return '기록 색인 · 옛 기록';
  if (file.startsWith('doc/_archive/')) return '보관';
  if (file.startsWith('doc/model-briefs/')) return '모델 지시문';
  if (file.startsWith('doc/plans/') || file.startsWith('docs/')) return '계획';
  if (file.startsWith('doc/')) return '규격 · 설계';
  return '저장소 안내';
}

/** A project-relative .md path inside the allowed places, or null. */
export function safeDoc(rel) {
  const text = String(rel || '').replaceAll('\\', '/');
  if (!text || text.includes('\0') || text.startsWith('/') || /^[a-z]:/i.test(text)) return null;
  const norm = path.posix.normalize(text);
  if (norm.startsWith('..') || !norm.endsWith('.md')) return null;
  if (!folders.some(f => norm.startsWith(f + '/')) && !singles.includes(norm)) return null;
  return norm;
}

async function walk(root, rel, out) {
  let entries;
  try { entries = await readdir(path.join(root, rel), { withFileTypes: true }); } catch { return; }
  for (const entry of entries) {
    const child = rel + '/' + entry.name;
    if (entry.isDirectory()) await walk(root, child, out);
    else if (entry.name.endsWith('.md')) out.push(child);
  }
}

async function allFiles(root) {
  const files = [];
  for (const folder of folders) await walk(root, folder, files);
  for (const single of singles) { try { await stat(path.join(root, single)); files.push(single); } catch { /* not there */ } }
  return files;
}

const titleOf = (text, file) => (text.match(/^#\s+(.+)$/m) || [])[1]?.trim() || file;

export async function listDocs(root) {
  const docs = [];
  for (const file of await allFiles(root)) {
    const text = await readFile(path.join(root, file), 'utf8');
    docs.push({ path: file, title: titleOf(text, file), group: docGroup(file), lines: text.split('\n').length });
  }
  docs.sort((a, b) => a.group.localeCompare(b.group, 'ko') || a.path.localeCompare(b.path));
  return docs;
}

export async function readDoc(root, rel) {
  const file = safeDoc(rel);
  if (!file) return { status: 400, body: { error: '문서 경로가 올바르지 않습니다 (doc/ · docs/ · 저장소 안내의 .md만)' } };
  try { return { status: 200, body: { path: file, text: await readFile(path.join(root, file), 'utf8') } }; }
  catch { return { status: 404, body: { error: '문서가 없습니다: ' + file } }; }
}

/** Lines that contain every word of the query (case-insensitive), a few per document. */
export async function searchDocs(root, query, perFile = 6, limit = 400) {
  const words = String(query || '').toLocaleLowerCase('ko').split(/\s+/).filter(Boolean);
  if (!words.length || words.join('').length < 2) return { query, total: 0, results: [] };
  const results = [];
  let total = 0;
  for (const file of await allFiles(root)) {
    const text = await readFile(path.join(root, file), 'utf8');
    const lines = text.split('\n');
    let found = 0;
    for (let i = 0; i < lines.length; i++) {
      const low = lines[i].toLocaleLowerCase('ko');
      if (!words.every(w => low.includes(w))) continue;
      total++;
      if (found++ < perFile && results.length < limit)
        results.push({ path: file, title: titleOf(text, file), group: docGroup(file), line: i + 1, text: lines[i].trim().slice(0, 240) });
    }
  }
  return { query, total, results };
}
