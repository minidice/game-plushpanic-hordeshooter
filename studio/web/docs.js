// Studio md menu: list, full-text search and a small Markdown reader for the project's documents (T-26).
const $ = id => document.getElementById(id);
let docs = [], current = null, searchTimer;
const status = (text, error = false) => { $('status').textContent = text; $('status').classList.toggle('error', error); };
async function api(url) {
  const response = await fetch(url); const body = await response.json();
  if (!response.ok) throw new Error(body.error || '요청 실패'); return body;
}

// ---------- Markdown (headings, paragraphs, lists, tables, quotes, code, links) ----------
const esc = s => s.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');
export const slug = s => s.trim().toLowerCase().replace(/<[^>]+>/g, '').replace(/[^\p{L}\p{N}\s_-]/gu, '').replace(/\s/g, '-');
function inline(raw) {
  const codes = [];
  let s = esc(raw).replace(/`([^`]+)`/g, (_, c) => `\u0000${codes.push(c) - 1}\u0000`);
  s = s.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>').replace(/~~([^~]+)~~/g, '<del>$1</del>')
    .replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, (m, text, href) => /^(https?:|#|\.{0,2}\/|[\w.-])/.test(href) && !/^javascript:/i.test(href)
      ? `<a href="${href}" data-md-link>${text}</a>` : text);
  return s.replace(/\u0000(\d+)\u0000/g, (_, i) => `<code>${codes[i]}</code>`);
}
const isTableSep = l => /^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$/.test(l);
const cells = l => l.trim().replace(/^\|/, '').replace(/\|$/, '').split('|').map(c => c.trim());
const listItem = l => l.match(/^(\s*)([-*]|\d+\.)\s+(.*)$/);
export function render(text) {
  const lines = text.replace(/\r\n/g, '\n').split('\n'), out = [];
  let i = 0;
  const startsBlock = l => /^(#{1,6})\s|^```|^\s*>|^\s*\|/.test(l) || listItem(l) || /^<!--/.test(l.trim());
  while (i < lines.length) {
    const line = lines[i], at = i + 1;
    if (!line.trim()) { i++; continue; }
    if (line.trim().startsWith('<!--')) { while (i < lines.length && !lines[i].includes('-->')) i++; i++; continue; }
    if (line.startsWith('```')) {
      const lang = line.slice(3).trim(), body = [];
      for (i++; i < lines.length && !lines[i].startsWith('```'); i++) body.push(lines[i]);
      i++; out.push(`<pre data-line="${at}"${lang ? ` data-lang="${esc(lang)}"` : ''}><code>${esc(body.join('\n'))}</code></pre>`); continue;
    }
    const h = line.match(/^(#{1,6})\s+(.*)$/);
    if (h) { out.push(`<h${h[1].length} id="${esc(slug(h[2]))}" data-line="${at}">${inline(h[2])}</h${h[1].length}>`); i++; continue; }
    if (line.trim().startsWith('|') && isTableSep(lines[i + 1] || '')) {
      const head = cells(line); i += 2; const rows = [];
      for (; i < lines.length && lines[i].trim().startsWith('|'); i++) rows.push(`<tr data-line="${i + 1}">${cells(lines[i]).map(c => `<td>${inline(c)}</td>`).join('')}</tr>`);
      out.push(`<div class="table-wrap" data-line="${at}"><table><thead><tr>${head.map(c => `<th>${inline(c)}</th>`).join('')}</tr></thead><tbody>${rows.join('')}</tbody></table></div>`); continue;
    }
    if (/^\s*>/.test(line)) {
      const body = [];
      for (; i < lines.length && /^\s*>/.test(lines[i]); i++) body.push(lines[i].replace(/^\s*>\s?/, ''));
      out.push(`<blockquote data-line="${at}">${render(body.join('\n'))}</blockquote>`); continue;
    }
    if (listItem(line)) {
      const stack = [];
      const html = [];
      for (; i < lines.length; i++) {
        const m = listItem(lines[i]);
        if (!m) { if (lines[i].trim() && /^\s{2,}/.test(lines[i]) && html.length) { html[html.length - 1] = html[html.length - 1].replace(/<\/li>$/, ' ' + inline(lines[i].trim()) + '</li>'); continue; } break; }
        const depth = Math.floor(m[1].replace(/\t/g, '  ').length / 2), tag = /\d/.test(m[2]) ? 'ol' : 'ul';
        while (stack.length > depth + 1) html.push(`</${stack.pop()}>`);
        while (stack.length < depth + 1) { stack.push(tag); html.push(`<${tag}>`); }
        html.push(`<li data-line="${i + 1}">${inline(m[3])}</li>`);
      }
      while (stack.length) html.push(`</${stack.pop()}>`);
      // A nested list belongs inside the item before it.
      out.push(html.join('').replace(/<\/li>(<[uo]l>)/g, '$1').replace(/(<\/[uo]l>)(?=<li|<\/[uo]l>)/g, '$1</li>'));
      continue;
    }
    const para = [];
    for (; i < lines.length && lines[i].trim() && !startsBlock(lines[i]); i++) para.push(lines[i].trim());
    if (!para.length) { para.push(line.trim()); i++; }
    out.push(`<p data-line="${at}">${inline(para.join(' '))}</p>`);
  }
  return out.join('\n');
}

// ---------- list, open, search ----------
function resolve(from, href) {
  const parts = from.split('/').slice(0, -1);
  for (const part of href.split('/')) { if (part === '..') parts.pop(); else if (part && part !== '.') parts.push(part); }
  return parts.join('/');
}
function showList(items) {
  $('entries').replaceChildren();
  for (const doc of items) {
    const button = document.createElement('button'), name = document.createElement('strong'), meta = document.createElement('span');
    button.dataset.path = doc.path; button.setAttribute('aria-pressed', String(current === doc.path));
    name.textContent = doc.title; meta.textContent = doc.path; button.append(name, meta);
    button.addEventListener('click', () => open(doc.path)); $('entries').append(button);
  }
  if (!items.length) { const empty = document.createElement('p'); empty.className = 'empty'; empty.textContent = '해당하는 문서가 없습니다.'; $('entries').append(empty); }
}
function filtered() { const group = $('group').value; return docs.filter(d => !group || d.group === group); }
function list() { const items = filtered(); $('result-count').textContent = `${items.length} / ${docs.length}개 문서`; showList(items); }

async function search(query) {
  if (query.trim().length < 2) { list(); return; }
  const data = await api('/api/docs/search?q=' + encodeURIComponent(query));
  const group = $('group').value, results = data.results.filter(r => !group || r.group === group);
  $('result-count').textContent = `"${query}" · ${data.total}줄 일치 (${new Set(results.map(r => r.path)).size}개 문서, 문서당 최대 6줄 표시)`;
  $('entries').replaceChildren();
  for (const r of results) {
    const button = document.createElement('button'), name = document.createElement('strong'), meta = document.createElement('span'), hit = document.createElement('em');
    button.className = 'hit'; name.textContent = r.title; meta.textContent = `${r.path}:${r.line}`; hit.textContent = r.text;
    button.append(name, meta, hit); button.addEventListener('click', () => open(r.path, r.line, query)); $('entries').append(button);
  }
  if (!results.length) { const empty = document.createElement('p'); empty.className = 'empty'; empty.textContent = '검색 결과가 없습니다.'; $('entries').append(empty); }
}

async function open(path, line, query, hash) {
  try {
    const data = await api('/api/docs/file?path=' + encodeURIComponent(path));
    current = data.path;
    const meta = docs.find(d => d.path === current);
    $('doc-title').textContent = meta?.title || current; $('doc-path').textContent = current; $('doc-group').textContent = meta?.group || '';
    $('doc').innerHTML = render(data.text);
    for (const button of $('entries').children) if (button.dataset.path) button.setAttribute('aria-pressed', String(button.dataset.path === current));
    history.replaceState(null, '', `/docs?path=${encodeURIComponent(current)}${line ? '&line=' + line : ''}${hash ? '#' + hash : ''}`);
    let target = hash ? document.getElementById(decodeURIComponent(hash)) : null;
    if (!target && line) target = [...$('doc').querySelectorAll('[data-line]')].filter(e => Number(e.dataset.line) <= line).pop();
    if (query && target) target.classList.add('flash');
    // A search hit or a heading link scrolls to it; a plain open keeps the page (and its top bar) where it starts.
    if (target) target.scrollIntoView({ block: 'center' });
    status(`${current} · ${data.text.split('\n').length}줄 · 읽기 전용`);
  } catch (error) { status(error.message, true); }
}

$('doc').addEventListener('click', event => {
  const link = event.target.closest('a[data-md-link]');
  if (!link) return;
  const href = link.getAttribute('href');
  if (/^https?:/.test(href)) { link.target = '_blank'; link.rel = 'noopener'; return; }
  event.preventDefault();
  if (href.startsWith('#')) { document.getElementById(decodeURIComponent(href.slice(1)))?.scrollIntoView({ block: 'start' }); return; }
  const [file, hash] = href.split('#');
  const target = resolve(current, file);
  if (target.endsWith('.md')) open(target, null, null, hash);
  else status(`md가 아닌 파일은 여기서 열지 않습니다: ${target}`);
});

async function load() {
  const data = await api('/api/docs'); docs = data.docs;
  $('total-count').textContent = `${docs.length}개 문서 · 현재 작업 폴더`;
  const selected = $('group').value;
  $('group').replaceChildren(new Option('모든 묶음', ''));
  for (const group of [...new Set(docs.map(d => d.group))]) $('group').add(new Option(group, group));
  $('group').value = selected;
  $('loaded-at').textContent = `목록 ${new Date(data.loadedAt).toLocaleTimeString('ko-KR')}`;
  $('search').value ? await search($('search').value) : list();
  const params = new URLSearchParams(location.search), path = params.get('path') || current || 'doc/features/README.md';
  await open(path, Number(params.get('line')) || null, null, location.hash.slice(1) || null);
}
$('search').addEventListener('input', () => { clearTimeout(searchTimer); searchTimer = setTimeout(() => search($('search').value).catch(e => status(e.message, true)), 220); });
$('group').addEventListener('change', () => ($('search').value.trim().length >= 2 ? search($('search').value) : list()));
$('reload').addEventListener('click', () => load().catch(e => status(e.message, true)));
load().catch(error => status(error.message, true));
