"""Validate portable links, rendered differences, and non-overwriting copy behavior."""
import ast
import hashlib
import json
import re
import shutil
import subprocess
import uuid
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote
from PIL import Image, ImageChops

ROOT=Path(__file__).resolve().parents[1]

class Links(HTMLParser):
    def __init__(self):
        super().__init__();self.links=[];self.cards=0
    def handle_starttag(self,tag,attrs):
        a=dict(attrs)
        if tag=='article' and a.get('class')=='card':self.cards+=1
        for key in ['href','src','data-solid','data-faded']:
            if key in a:self.links.append(a[key])

def check_path(base,link):
    if link.startswith(('http:','https:','#','mailto:')):return
    link=unquote(link.split('#')[0])
    if link:assert (base/link).exists(), f'Missing link: {base} -> {link}'

def main():
    data=json.loads((ROOT/'pack/manifest.json').read_text(encoding='utf-8'))
    parser=Links();parser.feed((ROOT/'gallery/index.html').read_text(encoding='utf-8'))
    assert parser.cards==len(data['assets'])
    for link in parser.links:check_path(ROOT/'gallery',link)
    for path in [ROOT/'README.md',*sorted((ROOT/'doc').glob('*.md'))]:
        for target in re.findall(r'\]\(([^)]+)\)',path.read_text(encoding='utf-8')):
            check_path(path.parent,target)
    for path in (ROOT/'tools').glob('*.py'):ast.parse(path.read_text(encoding='utf-8-sig'))
    print(f'PASS HTML: {parser.cards} cards, {len(parser.links)} local link/image references; Markdown links and Python syntax')
    comparisons=0
    for a in data['assets']:
        im=Image.open(ROOT/f"gallery/images/{a['id']}.webp").convert('RGB')
        assert min(im.size)>=700
        if a['occlusion_parts']:
            other=Image.open(ROOT/f"gallery/images/{a['id']}-ghost.webp").convert('RGB')
            assert ImageChops.difference(im,other).getbbox() is not None, 'Ghost identical to solid: '+a['id']
            comparisons+=1
    print(f'PASS RENDERS: {len(data['assets'])} thumbnails; {comparisons} distinct ghost states')
    shell=shutil.which('pwsh')
    assert shell,'PowerShell 7 is required for copy-tool tests'
    fake=ROOT/'output'/('copy-test-'+uuid.uuid4().hex[:8])
    (fake/'game').mkdir(parents=True)
    project=fake/'game/project.godot';project.write_text('config_version=5\n',encoding='utf-8')
    command=[shell,'-NoProfile','-File',str(ROOT/'Copy-OutdoorKit.ps1'),'-RepoRoot',str(fake)]
    dry=subprocess.run(command,capture_output=True,text=True,encoding='utf-8')
    assert dry.returncode==0,dry.stderr
    assert not (fake/'game/assets/outdoor-kit').exists(),'Dry run wrote files'
    run=subprocess.run(command+['-Apply'],capture_output=True,text=True,encoding='utf-8')
    assert run.returncode==0,run.stderr
    destination=fake/'game/assets/outdoor-kit'
    copied=list(p for p in destination.rglob('*') if p.is_file())
    assert len(copied)==len(data['assets'])+1+len(list((ROOT/'pack/shaders').glob('*.gdshader'))),len(copied)
    for p in copied:
        src=ROOT/'pack'/p.relative_to(destination)
        assert hashlib.sha256(p.read_bytes()).digest()==hashlib.sha256(src.read_bytes()).digest()
    snapshot={str(p):p.read_bytes() for p in copied}
    blocked=subprocess.run(command+['-Apply'],capture_output=True,text=True,encoding='utf-8')
    assert blocked.returncode!=0,'Copy must reject existing destination'
    assert all(Path(p).read_bytes()==content for p,content in snapshot.items())
    assert project.read_text(encoding='utf-8')=='config_version=5\n','Project was changed'
    invalid=subprocess.run(command[:-1]+[str(ROOT/'output')],capture_output=True,text=True,encoding='utf-8')
    assert invalid.returncode!=0,'Invalid repository should be rejected'
    print('PASS COPY: dry run writes nothing; all copied files match SHA256; overwrite/invalid root rejected; project unchanged')

if __name__=='__main__':main()
