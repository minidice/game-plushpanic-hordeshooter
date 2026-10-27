"""Build the material listening page from verified current, unnormalized game captures."""
import hashlib
import html
import json
import re
import shutil
from build import ROOT, REPO


def main():
    data = json.loads((REPO/'game/content/data/surface_profiles.json').read_text(encoding='utf-8'))
    folder = REPO/'game/output/audio-surfaces'
    capture = json.loads((folder/'capture.json').read_text(encoding='utf-8'))
    assert capture['passed'] and capture['enforceCeiling']
    assert capture['soundManifestSha256'] == hashlib.sha256((REPO/'game/content/sounds/credits.json').read_bytes()).hexdigest()
    for path, digest in capture['audioSourceSha256'].items():
        assert digest == hashlib.sha256((REPO/'game'/path).read_bytes()).hexdigest(), 'Stale capture: '+path
    out = ROOT/'output/surface-mix'
    out.mkdir(parents=True, exist_ok=True)
    cards = []
    for row in capture['recordings']:
        assert row['overFullScaleSamples'] == 0 and -.8 >= row['peakDb'] > -80
        name = row['name']+'.wav'
        shutil.copyfile(folder/name, out/name)
        cards.append(f'<article><b>{html.escape(row["name"])}</b><p>출력 피크 {row["peakDb"]:.1f} dBFS</p><audio controls preload="none" src="surface-mix/{name}"></audio></article>')
    presets = ''.join('<tr><td>'+html.escape(p['label'])+'</td><td>'+data['families'][p['family']]+'</td><td>'+html.escape(id)+'</td><td>'+str(p['step_db'])+' / '+str(p['hit_db'])+' / '+str(p['break_db'])+'</td></tr>' for id,p in data['profiles'].items())
    objects = ''.join('<tr><td>'+html.escape(id)+'</td><td>'+('미적용 · 장식' if profile is None else html.escape(data['profiles'][profile]['label']))+'</td></tr>' for id,profile in data['objects'].items())
    text = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>재질별 소리 · 오브젝트 프리셋</title>
<style>body{font:16px/1.6 system-ui;background:#101719;color:#e4eceb;max-width:1150px;margin:auto;padding:24px}h2{margin-top:38px}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:16px}article{background:#1c2926;padding:16px;border-radius:12px;overflow-wrap:anywhere}audio{width:100%}p{color:#adc1bc}a{color:#9fe1c8}table{width:100%;border-collapse:collapse}td,th{text-align:left;padding:8px;border-bottom:1px solid #31413e}details{margin:24px 0}</style>
<h1>재질별 소리와 오브젝트 프리셋</h1><p>7계열 · 15프리셋 · 새 소리17종 / 34 WAV. 재질별 A/B 변형, 발밑 접촉을 통한 10회 발소리, 군중 중첩을 실제 게임 Master 출력에서 녹음했습니다. 추가 정규화 없이 재생합니다. 원본 단발과 반복 발소리는 거리와 호출 조건이 다릅니다.</p>
<p>무료 CC0 Kenney Impact Sounds의 발소리·충돌 소재를 편집했습니다. 흙·자갈 명중/파손 등은 여러 소재를 섞은 제작음이며 해당 재질을 직접 녹음한 것으로 표현하지 않습니다. 아스팔트·콘크리트 발소리는 기존 합성을 유지합니다. 개별 프리셋은 계열 소리를 공유하며 이름별 고유 녹음은 아닙니다.</p>
<p>기존 체력·차단·관통·질량·마찰·탄성값은 유지했습니다. 소리 프리셋만 바꾸어 내구도나 물리를 추론하지 않습니다. 자동 검사 통과는 최종 청음 승인을 뜻하지 않습니다.</p>
<h2>프리셋 값</h2><table><tr><th>이름</th><th>계열</th><th>ID</th><th>추가 게인 dB · 발/명중/파손</th></tr>'''+presets+'''</table><details><summary>오브젝트 연결 전체 보기</summary><table>'''+objects+'''</table></details><h2>실제 출력 녹음</h2><div class="cards">'''+''.join(cards)+'''</div><p><a href="index.html">기존14종 비교</a> · <a href="../sources.json">출처와 라이선스</a></p>
<script>document.querySelectorAll('audio').forEach(a=>a.addEventListener('play',()=>document.querySelectorAll('audio').forEach(b=>{if(a!==b)b.pause()})));</script></html>'''
    (ROOT/'output/surfaces.html').write_text(text, encoding='utf-8')
    for link in re.findall(r'(?:src|href)="([^"]+)"',text):
        assert (ROOT/'output'/link).is_file(), link
    print(f'SURFACE_PAGE_PASS {len(capture["recordings"])} captures; {len(data["objects"])} object bindings')


if __name__ == '__main__': main()
