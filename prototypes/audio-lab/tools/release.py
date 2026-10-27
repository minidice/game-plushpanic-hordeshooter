"""Rebuild, validate and optionally install the recorded sound release (no network/API calls)."""
import argparse
import hashlib
import html
import json
import math
import re
from pathlib import Path
import shutil
import wave

from build import ROOT, REPO, RATE, decode, ffmpeg_path, shape, scale, save, level, db, verify_source_hashes


def render(recipe, ffmpeg):
    mixed = [0.] * round(recipe['seconds'] * RATE)
    for layer in recipe.get('layers', []):
        filters = (f"atrim=start={layer['start']}:duration={layer['seconds']},asetpts=PTS-STARTPTS,"
                   f"asetrate={round(RATE * layer['speed'])},aresample={RATE}," + layer['filters'])
        values = shape(decode(ffmpeg, ROOT / 'source' / layer['file'], filters))
        offset = round(layer.get('offset', 0) * RATE)
        gain = 10 ** (layer['gain_db'] / 20)
        for i, value in enumerate(values[:max(0, len(mixed) - offset)]):
            mixed[offset + i] += value * gain
    # Original short musical arrangements. Smooth additive partials, no saw/square or distortion.
    for start, frequency, duration, gain in recipe.get('notes', []):
        offset = round(start * RATE)
        for i in range(min(round(duration * RATE), len(mixed) - offset)):
            t = i / RATE
            envelope = (1 - math.exp(-t * 180)) * math.exp(-t * 3.8) * min(1, (duration-t)/.12)
            tone = sum(amplitude * math.sin(2 * math.pi * frequency * harmonic * t) * math.exp(-t * decay)
                       for harmonic, amplitude, decay in ((1, 1, 0), (2, .24, 2), (3, .08, 5), (4, .025, 8)))
            mixed[offset+i] += gain * envelope * tone
    return scale(shape(mixed), target_db=recipe.get('target_rms_db', -18))


def validate(path, expected_hash=None):
    with wave.open(str(path)) as w:
        assert (w.getnchannels(), w.getsampwidth(), w.getframerate()) == (1, 2, RATE), path
        import struct
        data = w.readframes(w.getnframes())
        samples = struct.unpack('<' + 'h' * (len(data)//2), data)
    assert .15 <= len(samples)/RATE <= 2, path
    assert .01 < max(map(abs, samples))/32768 <= 10**(-5.99/20), path
    assert abs(sum(samples)/len(samples)/32768) < .005, path
    assert samples[0] == samples[-1] == 0, path
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    if expected_hash: assert digest == expected_hash, f'Stale installed asset: {path}'
    return digest


def page(clips, ffmpeg):
    titles = {'shot_rifle':'소총 발사 · 3변형', 'zombie_hit':'좀비 피격 · 3변형',
              'reload_start':'장전 시작', 'reload_end':'장전 완료', 'dry_fire':'빈 탄창',
              'honk':'피격', 'honk_long':'쓰러짐', 'groan':'좀비 신음', 'zombie_die':'좀비 사망',
              'balloon_inflate':'풍선 팽창', 'balloon_pop':'풍선 파열', 'bull_bonk':'황소 충돌',
              'game_over':'게임 오버', 'stage_clear':'스테이지 클리어'}
    cards = []
    for sid, title in titles.items():
        before = ROOT / 'output/before' / (sid+'.wav')
        if not before.exists():
            before.parent.mkdir(parents=True, exist_ok=True)
            # Integration exports the retained synthesis recipes; never substitute the installed WAV.
            shutil.copyfile(REPO / 'game/output/sounds' / before.name, before)
        old = decode(ffmpeg, before)
        versions = [('before', old), ('applied', clips[sid])]
        for number in range(2, 4):
            if sid+'_'+str(number) in clips:
                versions.append(('variant'+str(number), clips[sid+'_'+str(number)]))
        target = min(-20, *(db(level(v)) - max(0, db(max(map(abs, v)))+3) for _, v in versions))
        rows, matched = [], []
        for label, values in versions:
            gain = 10 ** ((target-db(level(values)))/20)
            adjusted = [v*gain for v in values]
            matched.append(adjusted)
            save(ROOT / 'output/release-matched' / (sid+'-'+label+'.wav'), adjusted)
            name = {'before':'변경 전', 'applied':'적용본 A', 'variant2':'변형 B', 'variant3':'변형 C'}[label]
            rows.append(f'<article><b>{name}</b><audio controls preload="none" src="release-matched/{sid}-{label}.wav"></audio></article>')
        from check import measure
        measured = [measure(ROOT/'output/release-matched'/(sid+'-'+label+'.wav'))[2] for label, _ in versions]
        assert max(measured)-min(measured) < .05, f'Comparison gain mismatch: {sid}'
        if sid in ('shot_rifle', 'zombie_hit'):
            interval = .1 if sid == 'shot_rifle' else .16
            sequences = []
            for takes in ([matched[0]], matched[1:]):
                sequence = [0.] * (round(29*interval*RATE) + max(map(len, takes)))
                for i in range(30):
                    offset = round(i*interval*RATE)
                    for j, value in enumerate(takes[i % len(takes)]): sequence[offset+j] += value
                sequences.append(sequence)
            # One shared attenuation preserves the per-event RMS match; overlapping tails can add up.
            gain = min(1, 10**(-3/20) / max(max(map(abs, values)) for values in sequences))
            for label, values in zip(('before', 'applied'), sequences):
                name = sid+'-repeat-'+label+'.wav'
                save(ROOT/'output/release-matched'/name, [v*gain for v in values])
                rows.append(f'<article><b>{"변경 전" if label == "before" else "적용본 변형 순환"} · 30회 반복</b><audio controls preload="none" src="release-matched/{name}"></audio></article>')
        cards.append(f'<section><h2>{title}</h2><p>비교용 50 ms RMS {target:.1f} dBFS · 게임 재생 음량과 별도</p><div class="row">'+''.join(rows)+'</div></section>')
    capture_path = REPO / 'game/output/audio-quality/capture.json'
    capture_html = ''
    if capture_path.exists():
        capture = json.loads(capture_path.read_text(encoding='utf-8'))
        assert capture['passed'], 'Current mixer capture failed'
        assert capture['soundManifestSha256'] == hashlib.sha256((REPO/'game/content/sounds/credits.json').read_bytes()).hexdigest(), 'Mixer capture predates installed assets; rerun AudioQualityCapture'
        audio_sources = ('scripts/Audio/SoundRecipes.cs', 'scripts/Audio/Sfx.cs',
                         'scripts/Audio/AudioSetup.cs', 'tests/AudioPilotCapture.cs',
                         'scripts/Audio/SurfaceAudio.cs', 'scripts/Audio/SurfaceProfiles.cs', 'content/data/surface_profiles.json')
        assert capture.get('audioSourceSha256') == {
            path:hashlib.sha256((REPO/'game'/path).read_bytes()).hexdigest() for path in audio_sources
        }, 'Mixer capture predates audio implementation/scenario; rerun AudioQualityCapture'
        rows = []
        out = ROOT / 'output/release-mix'; out.mkdir(exist_ok=True)
        for row in capture['recordings']:
            assert row['overFullScaleSamples'] == 0 and row['peakDb'] <= -.8
            name = row['name']+'.wav'
            shutil.copyfile(capture_path.parent/name, out/name)
            with wave.open(str(out/name)) as wav:
                assert (wav.getnchannels(), wav.getsampwidth(), wav.getframerate(), wav.getnframes()) == (2, 2, row['rate'], row['frames'])
            rows.append(f'<article><b>{html.escape(row["name"])}</b><p>{row["peakDb"]:.1f} dBFS · 초과 0</p><audio controls preload="none" src="release-mix/{name}"></audio></article>')
        capture_html = '<section><h2>적용 후 실제 게임 출력</h2><p>Stage 리스너·거리·중첩·최종 limiter 포함. 배경음은 끈 통제 장면이며, 위 원음 비교와 음량 기준이 다릅니다. 추가 정규화 없이 실제 출력으로 재생합니다.</p><div class="row">'+''.join(rows)+'</div></section>'
    text = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>사운드 14종 적용 비교</title>
<style>body{font:16px/1.6 system-ui;background:#101719;color:#e4eceb;max-width:1100px;margin:auto;padding:24px}section{border-top:1px solid #31413e;padding:20px 0}.row{display:grid;grid-template-columns:repeat(auto-fit,minmax(230px,1fr));gap:14px}article{background:#1c2926;padding:14px;border-radius:10px}audio{display:block;width:100%;margin-top:12px}p{color:#adc1bc}a{color:#9fe1c8}</style>
<h1>전투 반복음 5종 추가 · 전체 14종</h1><p>현재 작업 브랜치 적용본입니다. 소총·좀비 피격은 3변형, 피격·신음·사망은 2변형을 연속 중복 없이 재생합니다. 먼저 단발의 음색을, 이어 30회 반복의 피로도를 비교하세요. 반복 자료는 변형을 순서대로 재생한 편집본이며, 실제 게임 출력은 아래 별도 녹음입니다.</p>
<p>무료 CC0 총성·에어소프트 장전·충돌·발성·공기 소재를 편집했습니다. 빈 탄창은 기계음 일부를 편집한 클릭입니다. 게임 오버/클리어는 직접 합성한 악구입니다. 거위·공기는 공개 MP3 미리듣기라 압축·배경음 한계가 남습니다. 자동 검증과 최종 청음 취향 확정은 별개입니다.</p>'''+''.join(cards)+capture_html+'''<p><a href="../sources.json">소재 출처</a> · <a href="release-report.json">배포본 측정</a> · <a href="pilot.html">이전 3종 시제품·변경 전 믹스</a></p>
<script>document.querySelectorAll('audio').forEach(a=>a.addEventListener('play',()=>document.querySelectorAll('audio').forEach(b=>{if(a!==b)b.pause()})));</script></html>'''
    index = ROOT/'output/index.html'
    pilot = ROOT/'output/pilot.html'
    if index.exists() and not pilot.exists() and '사운드 비교 시제품' in index.read_text(encoding='utf-8'):
        shutil.copyfile(index, pilot)
    if not pilot.exists():
        text = text.replace(' · <a href="pilot.html">이전 3종 시제품·변경 전 믹스</a>', '')
    if (ROOT/'output/surfaces.html').exists():
        text = text.replace('<h1>', '<p><a href="surfaces.html">재질별 소리 · 오브젝트 프리셋</a></p><h1>', 1)
    if (ROOT/'output/followup.html').exists():
        text = text.replace('<h1>', '<p><a href="followup.html">거리별 음량 · 5분 반복 전투</a></p><h1>', 1)
    index.write_text(text, encoding='utf-8')
    for link in re.findall(r'(?:src|href)="([^"]+)"', text):
        assert (ROOT/'output'/link).is_file(), f'Broken page link: {link}'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true', help='Copy validated assets and credits into game/content/sounds')
    parser.add_argument('--page', action='store_true', help='Build comparison using preserved output/before baseline')
    args = parser.parse_args()
    verify_source_hashes()
    ffmpeg = ffmpeg_path()
    recipes = json.loads((ROOT/'release-recipes.json').read_text(encoding='utf-8'))
    pilot = {r['id']: r for r in json.loads((ROOT/'recipes.json').read_text(encoding='utf-8'))['candidates']}
    all_recipes = {sid:pilot[cid] for sid, cid in recipes['selected'].items()}
    all_recipes.update({r['id']:r for r in recipes['clips']})
    clips, reports, credits = {}, [], {}
    dest = REPO/'game/content/sounds'
    for sid, recipe in all_recipes.items():
        clips[sid] = render(recipe, ffmpeg)
        path = ROOT/'output/release'/ (sid+'.wav')
        row = save(path, clips[sid]); reports.append(row)
        validate(path)
        if args.install: shutil.copyfile(path, dest/path.name)
        validate(dest/path.name, row['sha256'])
        imported = dest/(path.name+'.import')
        if imported.exists():
            settings = imported.read_text(encoding='utf-8')
            assert 'compress/mode=0' in settings and 'edit/normalize=false' in settings, imported
        credits[path.name] = {'sha256':row['sha256'], 'sources':[l['file'] for l in recipe.get('layers',[])],
                             'original_arrangement': bool(recipe.get('notes'))}
    (ROOT/'output/release-report.json').write_text(json.dumps(reports,indent=2),encoding='utf-8')
    if args.install:
        manifest = {'recipe':'prototypes/audio-lab/release-recipes.json', 'files':credits,
                    'sources':json.loads((ROOT/'sources.json').read_text(encoding='utf-8'))['files'],
                    'note':'CC0-derived edits. Original additive arrangements for game_over and stage_clear are project-authored, with no external music sample.'}
        (dest/'credits.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    else:
        manifest = json.loads((dest/'credits.json').read_text(encoding='utf-8'))
        assert manifest['files'] == credits, 'Stale credits/asset manifest'
        assert manifest['sources'] == json.loads((ROOT/'sources.json').read_text(encoding='utf-8'))['files']
    if args.page: page(clips, ffmpeg)
    print(f'AUDIO_RELEASE_PASS {len(clips)} assets; source hashes, installed hashes, PCM, peaks, DC, endpoints verified')


if __name__ == '__main__': main()
