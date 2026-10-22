"""Bundle only reproducible sources and deliverable assets, never Godot caches/logs."""
import hashlib
import json
import zipfile
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'delivery';OUT.mkdir(exist_ok=True)
files=[]
for path in ROOT.rglob('*'):
    if not path.is_file():continue
    rel=path.relative_to(ROOT)
    if any(part in ['.godot','output','delivery','__pycache__'] for part in rel.parts):continue
    if path.suffix in ['.import','.blend1','.pyc']:continue
    if rel.parts[0]=='gallery' and path.suffix=='.png':continue
    if path.suffix not in ['.md','.py','.ps1','.txt','.json','.glb','.gdshader','.gd','.tscn','.godot','.html','.webp','.blend','.uid'] and path.name not in ['.gdignore','.gitignore']:continue
    files.append(path)
files.sort(key=lambda p:p.relative_to(ROOT).as_posix())
digest={p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
archive=OUT/'plushpanic-outdoor-kit.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for p in files:z.write(p,'plushpanic-outdoor-kit/'+p.relative_to(ROOT).as_posix())
    z.writestr('plushpanic-outdoor-kit/SHA256.json',json.dumps(digest,indent=2))
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    names=z.namelist()
    assert len([n for n in names if n.endswith('.glb')])==len(json.loads((ROOT/'pack/manifest.json').read_text(encoding='utf-8'))['assets'])
    assert 'plushpanic-outdoor-kit/source/.gdignore' in names
    assert 'plushpanic-outdoor-kit/gallery/index.html' in names
    for rel,h in digest.items():assert hashlib.sha256(z.read('plushpanic-outdoor-kit/'+rel)).hexdigest()==h
hash_=hashlib.sha256(archive.read_bytes()).hexdigest()
(OUT/'plushpanic-outdoor-kit.zip.sha256').write_text(hash_+'  '+archive.name+'\n',encoding='ascii')
print(f'PASS ZIP: {len(files)+1} entries, {archive.stat().st_size:,} bytes; CRC and every entry SHA256 verified')
print('SHA256',hash_)
