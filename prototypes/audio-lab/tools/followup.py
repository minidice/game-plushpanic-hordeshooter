"""Publish only current, verified balance/300-second combat captures to the local listening page."""
import hashlib
import html
import json
import re
import shutil
import wave
from build import ROOT, REPO


def current_report(path):
    report = json.loads(path.read_text(encoding='utf-8'))
    assert report['passed'], path
    sound_dir = REPO/'game/content/sounds'
    manifest_bytes = (sound_dir/'credits.json').read_bytes()
    assert report['soundManifestSha256'] == hashlib.sha256(manifest_bytes).hexdigest()
    for name, entry in json.loads(manifest_bytes)['files'].items():
        assert hashlib.sha256((sound_dir/name).read_bytes()).hexdigest() == entry['sha256'], 'Changed sound: '+name
    for source, digest in report['audioSourceSha256'].items():
        assert digest == hashlib.sha256((REPO/'game'/source).read_bytes()).hexdigest(), 'Stale capture: '+source
    return report


def main():
    balance_dir = REPO/'game/output/audio-balance'
    soak_dir = REPO/'game/output/audio-combat-soak'
    balance = current_report(balance_dir/'capture.json')
    soak = current_report(soak_dir/'report.json')
    boss_dir = REPO/'game/output/audio-boss-gain'
    boss = current_report(boss_dir/'capture.json')
    assert soak['seconds'] == 300 and soak['recordedSeconds'] >= 280
    assert soak['afterOver'] == 0 and soak['afterPeakDb'] <= -.8
    assert 30 < soak['distance'] < 2000
    assert len(soak['combatIntervals']) == 5
    assert all(r['shots'] > 50 and r['hits'] > 5 and r['deaths'] > 0 for r in soak['combatIntervals'])
    out = ROOT/'output/followup-mix'
    out.mkdir(parents=True, exist_ok=True)
    cards = []
    for row in balance['recordings']:
        master = row['buses']['Master']
        assert master['overFullScaleSamples'] == 0 and -.8 >= master['peakDb'] > -80
        name = row['name']+'.wav'
        with wave.open(str(balance_dir/name)) as wav:
            assert (wav.getnchannels(),wav.getsampwidth(),wav.getframerate(),wav.getnframes()) == (2,2,row['rate'],master['frames'])
        shutil.copyfile(balance_dir/name,out/name)
        cards.append(f'<article><b>{html.escape(row["name"])}</b><p>SFX {row["buses"]["SFX"]["peakDb"]:.1f} / 최종 {master["peakDb"]:.1f} dBFS</p><audio controls preload="none" src="followup-mix/{name}"></audio></article>')
    assert boss['overFullScaleSamples'] == 0 and boss['peakDb'] <= -.8 and boss['shotMaxDb'] <= 3
    with wave.open(str(boss_dir/'boss-fan.wav')) as wav:
        assert (wav.getnchannels(),wav.getsampwidth(),wav.getframerate(),wav.getnframes()) == (2,2,boss['rate'],boss['frames'])
    shutil.copyfile(boss_dir/'boss-fan.wav',out/'boss-fan.wav')
    cards.append(f'<article><b>보스 산탄 · 실제 스킬 호출</b><p>준비음 포함 · 최종 {boss["peakDb"]:.1f} dBFS</p><audio controls preload="none" src="followup-mix/boss-fan.wav"></audio></article>')
    excerpts=[]
    with wave.open(str(soak_dir/'combat.wav')) as src:
        assert src.getnframes() == soak['frames'] and src.getnchannels() == 2 and src.getsampwidth() == 2
        for start,label in [(0,'기본 전투'),(125,'헬륨 상태'),(185,'먹먹한 상태'),(260,'후반 반복 전투')]:
            src.setpos(start*src.getframerate())
            name=f'combat-{start}s.wav'
            frames = src.readframes(20*src.getframerate())
            assert len(frames) == 20*src.getframerate()*4, 'Incomplete 20-second excerpt: '+name
            with wave.open(str(out/name),'wb') as dest:
                dest.setparams(src.getparams());dest.writeframes(frames)
            excerpts.append(f'<article><b>{label} · {start}초부터 20초</b><audio controls preload="none" src="followup-mix/{name}"></audio></article>')
    page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>사운드 후속 검증 · 거리와 반복 전투</title>
<style>body{font:16px/1.6 system-ui;background:#101719;color:#e4eceb;max-width:1150px;margin:auto;padding:24px}h2{margin-top:36px}.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(260px,1fr));gap:16px}article{background:#1c2926;padding:16px;border-radius:12px;overflow-wrap:anywhere}audio{width:100%}p{color:#adc1bc}a{color:#9fe1c8}</style>
<h1>거리별 음량과 5분 반복 전투</h1><p>추가 정규화 없이 실제 게임 Master 출력으로 재생합니다. 개별 장면은 거리·추가 게인·반복 조건을 통제한 검사이며, SFX는 limiter 이전 / 최종은 limiter 이후 값입니다.</p>
<p>아래 5분 전투는 자동 이동·사격, 무적/회복·탄약 보충·12초마다 적 교체와 플레이어 재배치·구역 이탈 복귀를 사용하는 통제 검사입니다. 매 분 실제 명중·처치가 이어지는지 확인합니다. 사람의 음색·피로도 수락이나 실제 두 PC 네트워크 검증을 대신하지 않습니다. 렌더 오류·남은 제약은 작업 기록009에서 구분합니다.</p>
<h2>300초 전투 발췌</h2><p>녹음 '''+f'{soak["recordedSeconds"]:.1f}초 · 최종 피크 {soak["afterPeakDb"]:.2f} dBFS · 초과 {soak["afterOver"]}'+'''</p><div class="cards">'''+''.join(excerpts)+'''</div><h2>무기·경고·재질 발소리 조건별 출력</h2><div class="cards">'''+''.join(cards)+'''</div><p><a href="index.html">기존14종 전후 비교</a> · <a href="surfaces.html">재질별 소리·프리셋</a> · <a href="../sources.json">소재 출처</a></p>
<script>document.querySelectorAll('audio').forEach(a=>a.addEventListener('play',()=>document.querySelectorAll('audio').forEach(b=>{if(a!==b)b.pause()})));</script></html>'''
    (ROOT/'output/followup.html').write_text(page,encoding='utf-8')
    for link in re.findall(r'(?:src|href)="([^"]+)"',page): assert (ROOT/'output'/link).is_file(),link
    print(f'FOLLOWUP_PAGE_PASS {len(cards)} balance recordings, 300s combat, {len(excerpts)} unnormalized excerpts')


if __name__=='__main__': main()
