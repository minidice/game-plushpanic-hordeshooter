"""Build review-only sounds from licensed recordings. Never installs game overrides."""
from __future__ import annotations

import argparse
import array
import hashlib
import html
import json
import math
from pathlib import Path
import struct
import subprocess
import sys
import wave

ROOT = Path(__file__).resolve().parents[1]
REPO = ROOT.parents[1]
RATE = 48000


def ffmpeg_path():
    sys.path.insert(0, str(ROOT / "output/vendor"))
    import imageio_ffmpeg
    return imageio_ffmpeg.get_ffmpeg_exe()


def decode(ffmpeg, path, filters=""):
    command = [ffmpeg, "-v", "error", "-i", str(path), "-vn", "-ac", "1",
               "-af", "aresample=48000" + ("," + filters if filters else ""),
               "-ar", str(RATE), "-f", "f32le", "-"]
    data = subprocess.run(command, capture_output=True, check=True).stdout
    values = list(struct.unpack("<" + "f" * (len(data) // 4), data))
    if not values or any(not math.isfinite(v) for v in values):
        raise ValueError(f"Empty or non-finite audio: {path}")
    return values


def level(values):
    if not values:
        raise ValueError("Empty audio")
    # Full 50 ms windows: a single loud onset sample must not become an RMS window.
    window = min(round(RATE * .05), len(values))
    squares = [v * v for v in values]
    total = sum(squares[:window])
    best = total
    for i in range(window, len(values)):
        total += squares[i] - squares[i - window]
        best = max(best, total)
    return math.sqrt(best / window)


def db(value):
    return 20 * math.log10(max(value, 1e-12))


def shape(values, fade_in=.008, fade_out=.035):
    mean = sum(values) / len(values)
    values = [v - mean for v in values]
    a = min(round(fade_in * RATE), len(values) // 2)
    b = min(round(fade_out * RATE), len(values) // 2)
    for i in range(a):
        values[i] *= i / max(a - 1, 1)
    for i in range(b):
        values[-i - 1] *= i / max(b - 1, 1)
    return values


def scale(values, target_db=-18, peak_db=-6):
    rms = level(values)
    peak = max(map(abs, values))
    if rms < 1e-6:
        raise ValueError("Silent source cannot be normalized")
    gain = min(10 ** (target_db / 20) / rms, 10 ** (peak_db / 20) / peak)
    return [v * gain for v in values]


def save(path, values):
    path.parent.mkdir(parents=True, exist_ok=True)
    if max(map(abs, values)) >= 1:
        raise ValueError(f"Refusing clipped output: {path}")
    pcm = array.array("h", [round(v * 32767) for v in values])
    if sys.byteorder != "little":
        pcm.byteswap()
    with wave.open(str(path), "wb") as wav:
        wav.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
        wav.writeframes(pcm.tobytes())
    return {
        "file": path.relative_to(ROOT).as_posix(), "seconds": len(values) / RATE,
        "peak_dbfs": db(max(map(abs, values))), "rms50_dbfs": db(level(values)),
        "rms_dbfs": db(math.sqrt(sum(v * v for v in values) / len(values))),
        "dc": sum(values) / len(values), "first": values[0], "last": values[-1],
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
    }


def verify_source_hashes():
    manifest = json.loads((ROOT / "sources.json").read_text(encoding="utf-8"))
    for source in manifest["files"]:
        path = ROOT / "source" / source["file"]
        if hashlib.sha256(path.read_bytes()).hexdigest() != source["sha256"]:
            raise ValueError(f"Source hash mismatch: {path.name}")


def render_page(recipes, report):
    titles = {"honk": "피격 · 거위 시제품", "groan": "좀비 신음", "zombie_die": "좀비 사망"}
    cards = []
    for group, title in titles.items():
        rows = [(group + "-original", "현재 합성음")]
        rows += [(r["id"], r["label"]) for r in recipes if r["group"] == group]
        players = "".join(f'<article><b>{html.escape(label)}</b><audio controls preload="none" src="matched/{sid}.wav"></audio></article>' for sid, label in rows)
        cards.append(f'<section><h2>{title}</h2><p>같은 종류의 50 ms RMS: {report["comparison_targets"][group]:.1f} dBFS. 게임의 거리·버스 효과 미적용.</p><div class="row">{players}</div></section>')
    capture = report["capture"]
    captures = "".join(f'<article><b>{html.escape(r["name"])}</b><small>원래 믹스 피크 {r["peakDb"]:.1f} dBFS · 0 dBFS 이상 샘플 {r["overFullScaleSamples"]}</small><audio controls preload="none" src="capture-listen/{r["name"]}.wav"></audio></article>' for r in capture["recordings"])
    page = '''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>사운드 비교 시제품</title>
<style>body{font:16px/1.6 system-ui,sans-serif;background:#101719;color:#e4eceb;max-width:1100px;margin:auto;padding:32px}h1{font-size:32px}h2{margin:0}p,small{color:#adc1bc}section{padding:24px 0;border-top:1px solid #31413e}.row{display:grid;grid-template-columns:repeat(3,1fr);gap:14px}article{padding:16px;background:#1c2926;border-radius:12px}audio{display:block;width:100%;margin-top:16px}small{display:block}a{color:#9fe1c8}.notice{background:#29362f;padding:18px;border-radius:12px}@media(max-width:800px){.row{grid-template-columns:1fr}}</style>
<h1>피격 · 신음 · 사망 사운드 비교</h1><p>Duckov의 동물 반응 / Left 4 Dead의 감염체 질감을 목표로 한 무료 소재 편집 시제품.</p>
<p class="notice">먼저 낮은 기기 볼륨으로 재생하세요. A/B는 최종 승인 전이며 게임에는 적용하지 않았습니다. 거위는 현장 녹음의 공개 MP3 미리듣기 소재로 배경음·압축 한계가 있습니다. 자동 측정 통과는 청음 품질 승인이 아닙니다.</p>
''' + "".join(cards) + f'''<section><h2>현재 게임 믹스 · 변경 전</h2><p>실제 Stage 리스너와 Sfx를 사용한 통제 장면입니다. 일반 플레이 전체 녹화는 아닙니다. 주변 배경음을 끄고 녹음했습니다. 청취 파일은 모두 같은 {report["capture_listen_gain_db"]:.1f} dB 감쇠로 저장해 측정한 0 dBFS 초과 파형을 잘라내지 않습니다. 위 A/B 원음과 직접 음량 비교하지 마세요.</p><div class="row">''' + captures + '''</div></section><p><a href="../README.md">재현·제약</a> · <a href="../sources.json">소재 출처</a> · <a href="report.json">측정 결과</a></p>
<script>document.querySelectorAll('audio').forEach(a=>a.addEventListener('play',()=>document.querySelectorAll('audio').forEach(b=>{if(a!==b)b.pause()})));</script></html>'''
    (ROOT / "output/index.html").write_text(page, encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline", type=Path, default=REPO / "game/output/audio-pilot")
    args = parser.parse_args()
    verify_source_hashes()
    recipes = json.loads((ROOT / "recipes.json").read_text(encoding="utf-8"))["candidates"]
    ffmpeg = ffmpeg_path()
    report = {"ffmpeg": subprocess.check_output([ffmpeg, "-version"], text=True).splitlines()[0],
              "candidates": [], "matched": [], "comparison_targets": {}}
    clips = {}
    for recipe in recipes:
        mixed = [0.] * round(recipe["seconds"] * RATE)
        for layer in recipe["layers"]:
            speed = layer["speed"]
            filters = (f'atrim=start={layer["start"]}:duration={layer["seconds"]},asetpts=PTS-STARTPTS,'
                       f'asetrate={round(RATE * speed)},aresample={RATE},' + layer["filters"])
            values = shape(decode(ffmpeg, ROOT / "source" / layer["file"], filters))
            offset = round(layer.get("offset", 0) * RATE)
            gain = 10 ** (layer["gain_db"] / 20)
            for i, value in enumerate(values[:max(0, len(mixed) - offset)]):
                mixed[i + offset] += value * gain
        clips[recipe["id"]] = scale(shape(mixed))
        report["candidates"].append(save(ROOT / "candidates" / (recipe["id"] + ".wav"), clips[recipe["id"]]))
    for group in ("honk", "groan", "zombie_die"):
        original = group + "-original"
        clips[original] = decode(ffmpeg, args.baseline / (group + ".wav"))
        names = [original] + [r["id"] for r in recipes if r["group"] == group]
        target = min(-20, *(db(level(clips[n])) for n in names))
        # One common target for the group; no candidate gets a separate peak limiter.
        target = min(target, *(db(level(clips[n])) - db(max(map(abs, clips[n]))) - 3 for n in names))
        report["comparison_targets"][group] = target
        for name in names:
            gain = 10 ** ((target - db(level(clips[name]))) / 20)
            report["matched"].append(save(ROOT / "output/matched" / (name + ".wav"), [v * gain for v in clips[name]]))
    report["capture"] = json.loads((args.baseline / "capture.json").read_text(encoding="utf-8"))
    if not report["capture"]["passed"]:
        raise ValueError("Baseline capture did not pass")
    capture_out = ROOT / "output/capture-listen"
    capture_out.mkdir(parents=True, exist_ok=True)
    # Keep the same gain across the entire capture batch, with 3 dB of headroom.
    report["capture_listen_gain_db"] = min(-12, -3 - max(r["peakDb"] for r in report["capture"]["recordings"]))
    gain = 10 ** (report["capture_listen_gain_db"] / 20)
    for recording in report["capture"]["recordings"]:
        name = recording["name"] + ".wav"
        raw = (args.baseline / (recording["name"] + ".f32le")).read_bytes()
        values = struct.unpack("<" + "f" * (len(raw) // 4), raw)
        if len(values) != recording["frames"] * 2 or any(not math.isfinite(v) for v in values):
            raise ValueError("Invalid raw capture: " + name)
        pcm = array.array("h", [round(v * gain * 32767) for v in values])
        if sys.byteorder != "little": pcm.byteswap()
        with wave.open(str(capture_out / name), "wb") as wav:
            wav.setparams((2, 2, recording["rate"], 0, "NONE", "not compressed"))
            wav.writeframes(pcm.tobytes())
    (ROOT / "output/report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    render_page(recipes, report)
    print(f'AUDIO_PILOT_BUILD_PASS {len(recipes)} candidates, {len(report["matched"])} matched clips')


if __name__ == "__main__":
    main()
