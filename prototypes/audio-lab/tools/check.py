"""Validate the deliverable WAVs and provenance, independent of in-memory render stats."""
import array
import hashlib
import json
import math
from pathlib import Path
import re
import wave

ROOT = Path(__file__).resolve().parents[1]


def measure(path):
    with wave.open(str(path), "rb") as wav:
        assert wav.getsampwidth() == 2, f"Not PCM16: {path}"
        rate, channels, count = wav.getframerate(), wav.getnchannels(), wav.getnframes()
        values = array.array("h", wav.readframes(count))
    assert rate == 48000 and channels == 1, f"Wrong candidate format: {path}"
    assert values and max(map(abs, values)) < 32767, f"Clipped/empty: {path}"
    window = min(round(rate * .05), len(values))
    total = sum(v*v for v in values[:window]); best = total
    for i in range(window, len(values)):
        total += values[i]*values[i] - values[i-window]*values[i-window]
        best = max(best, total)
    rms = math.sqrt(best / window) / 32768
    assert rms > .0001, f"Silent: {path}"
    return values, count / rate, 20 * math.log10(rms)


def main():
    report = json.loads((ROOT / "output/report.json").read_text(encoding="utf-8"))
    manifest = json.loads((ROOT / "sources.json").read_text(encoding="utf-8"))
    recipes = json.loads((ROOT / "recipes.json").read_text(encoding="utf-8"))["candidates"]
    for source in manifest["files"]:
        assert source["license"] == "CC0-1.0" and source["page"].startswith("https://")
        assert hashlib.sha256((ROOT / "source" / source["file"]).read_bytes()).hexdigest() == source["sha256"]
    bounds = {"honk": (.15,.35), "groan": (.65,1.5), "zombie_die": (.25,.65)}
    assert len(recipes) == len(report["candidates"]) == 6
    for recipe in recipes:
        path = ROOT / "candidates" / (recipe["id"] + ".wav")
        values, duration, _ = measure(path)
        lo, hi = bounds[recipe["group"]]
        assert lo <= duration <= hi, f"Duration: {path}"
        assert abs(values[0]) <= 1 and abs(values[-1]) <= 1, f"Endpoint discontinuity: {path}"
        assert abs(sum(values) / len(values) / 32768) < .005, f"DC: {path}"
        assert max(map(abs, values)) / 32768 <= 10**(-5.9/20), f"Peak headroom: {path}"
    for group in bounds:
        names = [group + "-original"] + [r["id"] for r in recipes if r["group"] == group]
        levels = [measure(ROOT / "output/matched" / (n + ".wav"))[2] for n in names]
        assert max(levels) - min(levels) < .05, f"RMS mismatch: {group}: {levels}"
    assert report["capture"]["passed"] and len(report["capture"]["recordings"]) == 11
    for capture in report["capture"]["recordings"]:
        with wave.open(str(ROOT / "output/capture-listen" / (capture["name"] + ".wav"))) as wav:
            assert wav.getnchannels() == 2 and wav.getsampwidth() == 2
            values = array.array("h", wav.readframes(wav.getnframes()))
        assert values and 0 < max(map(abs, values)) < 32767, f"Invalid listen copy: {capture['name']}"
    page = (ROOT / "output/index.html").read_text(encoding="utf-8")
    assert "autoplay" not in page
    for link in re.findall(r'(?:src|href)="([^"]+)"', page):
        assert (ROOT / "output" / link).is_file(), f"Broken preview link: {link}"
    print(f"AUDIO_PILOT_CHECK_PASS {len(manifest['files'])} source hashes; 6 candidates; 9 RMS-matched clips; 11 listen captures; preview links")


if __name__ == "__main__":
    main()
