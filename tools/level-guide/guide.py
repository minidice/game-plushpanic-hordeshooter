"""Level design guide maps (doc/features/level/guide-map.md, L-33): check them and draw a guide board.

    python tools/level-guide/guide.py check                      # every guide, in stage order
    python tools/level-guide/guide.py check --strict             # warnings fail too
    python tools/level-guide/guide.py board game/content/data/stages/guide/stage1_large_plan_guide.json doc/images/stage1-large-guide-plan.svg

A guide lists a stage's zones in walking order with what each zone introduces (new), uses again (reuse) and
combines (mix), its role in the pacing and how the player finds the way. Experiences come from experiences.json.
Standard library only.
"""
import glob
import json
import os
import sys
from xml.sax.saxutils import escape

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
GUIDE_DIR = os.path.join(ROOT, "game", "content", "data", "stages", "guide")
STAGE_DIR = os.path.dirname(GUIDE_DIR)
CATALOG = os.path.join(GUIDE_DIR, "experiences.json")

# [임시] thresholds, see guide-map.md 4장
MAX_NEW_PER_LARGE = 3       # more new experiences than this in one large zone is "too many at once"
REST_AFTER_PEAK = 2         # a rest or safe zone within this many zones after a peak
PEAK_ROLES = ("peak", "finale")
REST_ROLES = ("rest", "safe")


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def guides(paths):
    """Given paths as they are; by default every guide, one per stage: the plan when a stage has one, else as-is."""
    if paths:
        loaded = [(p, load(p)) for p in paths]
    else:
        by_stage = {}
        for p in sorted(glob.glob(os.path.join(GUIDE_DIR, "*_guide.json"))):
            g = load(p)
            by_stage.setdefault(g["stage"], []).append((p, g))
        loaded = []
        for stage, items in by_stage.items():
            plans = [pg for pg in items if pg[1].get("status") == "plan"]
            loaded += plans or items
    return sorted(loaded, key=lambda pg: (pg[1]["stage"], pg[0]))


class Report:
    def __init__(self):
        self.errors, self.warnings, self.infos = [], [], []

    def error(self, where, code, text):
        self.errors.append(f"[{code}] {where}: {text}")

    def warn(self, where, code, text):
        self.warnings.append(f"[{code}] {where}: {text}")

    def info(self, text):
        self.infos.append(text)


def check(paths, catalog):
    exp = {e["id"]: e for e in catalog["experiences"]}
    roles, ways = catalog["roles"], catalog["wayfinding"]
    rep = Report()
    known = set()           # introduced in an earlier zone or stage (campaign order)
    later_use = {}          # experience -> list of (stage, zone) where it was reused or mixed
    introduced_at = {}      # experience -> (stage, zone)
    planned = {}
    stages = guides(paths)
    for path, g in stages:
        stage = f"Stage {g['stage']}"
        rep.info(f"읽음: {os.path.relpath(path, ROOT).replace(os.sep, '/')} ({g.get('status', '?')})")
        zones = sorted(g["zones"], key=lambda z: z["order"])
        _check_layout(rep, stage, g, zones)
        last_progress = -1.0
        orders = set()
        mixes = 0
        for i, z in enumerate(zones):
            where = f"{stage} {z['id']}"
            if z["order"] in orders:
                rep.error(where, "G5", f"순서 {z['order']}가 겹침")
            orders.add(z["order"])
            if not 0.0 <= z["progress"] <= 1.0 or z["progress"] < last_progress:
                rep.error(where, "G5", f"진행 위치 {z['progress']}가 0~1 밖이거나 앞 구역({last_progress})보다 작음")
            last_progress = z["progress"]
            if z["role"] not in roles:
                rep.error(where, "G1", f"알 수 없는 역할 '{z['role']}'")
            for w in z.get("wayfinding", []):
                if w not in ways:
                    rep.error(where, "G1", f"알 수 없는 길 안내 '{w}'")
            used = list(z.get("new", [])) + list(z.get("reuse", [])) + [e for m in z.get("mix", []) for e in m]
            for e in used:
                if e not in exp:
                    rep.error(where, "G1", f"경험 목록에 없는 '{e}'")
                elif exp[e]["status"] == "planned":
                    planned.setdefault(e, []).append(where)
            for e in z.get("new", []):
                if e in known:
                    rep.error(where, "G7", f"'{_name(exp, e)}': {introduced_at[e][0]} {introduced_at[e][1]}에서 이미 소개됨 — 다시 쓰기(reuse)로 옮길 것")
                if e in z.get("reuse", []):
                    rep.error(where, "G7", f"'{_name(exp, e)}': 같은 구역에서 새 경험과 다시 쓰기에 함께 넣음")
            for e in list(z.get("reuse", [])) + [e for m in z.get("mix", []) for e in m]:
                if e in exp and e not in known:
                    rep.error(where, "G3", f"'{_name(exp, e)}': 소개(new)하기 전에 다시 쓰거나 섞음")
                later_use.setdefault(e, []).append((stage, z["id"]))
            for m in z.get("mix", []):
                mixes += 1
                if len(set(m)) < 2:
                    rep.error(where, "G4", f"섞기 {m}는 서로 다른 경험이 2개 이상이어야 함")
            if z["size"] == "large":
                if not z.get("new"):
                    rep.error(where, "G2", "큰 구역에 새 경험이 없음(큰 구역마다 1개 이상)")
                elif len(z["new"]) > MAX_NEW_PER_LARGE:
                    rep.warn(where, "W1", f"새 경험 {len(z['new'])}개가 한꺼번에 나옴(큰 구역 {MAX_NEW_PER_LARGE}개 이하 권장 — 하나씩 소개)")
                if not z.get("wayfinding"):
                    rep.warn(where, "W5", "큰 구역에 길 안내 수단이 없음")
            if g.get("tutorial") and len(z.get("new", [])) != 1 and not z.get("branch"):
                # A tutorial stage teaches one thing per zone (decisions.md L-34).
                rep.warn(where, "W9", f"튜토리얼 스테이지: 새 경험이 {len(z.get('new', []))}개(구역마다 정확히 1개)")
            if not z.get("new") and not z.get("reuse") and not z.get("mix") and z["role"] not in ("safe", "transit"):
                rep.warn(where, "W7", "목적 없는 구역(새 · 다시 · 섞기가 모두 비어 있음)")
            for e in z.get("new", []):
                if e in exp and e not in known:
                    known.add(e)
                    introduced_at[e] = (stage, z["id"])
            if z["role"] in PEAK_ROLES and z["role"] != "finale":
                after = zones[i + 1:i + 1 + REST_AFTER_PEAK]
                if not any(a["role"] in REST_ROLES for a in after):
                    rep.warn(where, "W4", f"정점 뒤 {REST_AFTER_PEAK}구역 안에 휴식 · 안전 구역이 없음")
        if not any(z["role"] in PEAK_ROLES for z in zones):
            rep.warn(stage, "W3", "정점(peak · finale) 구역이 없음")
        if mixes == 0:
            rep.warn(stage, "W6", "섞는 경험(mix)이 하나도 없음 — 앞에서 배운 것을 함께 쓰게 하는 상황이 없음")
        new_count = sum(len(z.get("new", [])) for z in zones)
        large = [z for z in zones if z["size"] == "large"]
        rep.info(f"{stage} ({g.get('status', '?')}): 구역 {len(zones)}(큰 {len(large)}) · 새 경험 {new_count} · 다시 쓰기 "
                 f"{sum(len(z.get('reuse', [])) for z in zones)} · 섞기 {mixes} · 정점 {sum(1 for z in zones if z['role'] in PEAK_ROLES)}")
    for e, (stage, zone) in introduced_at.items():
        uses = [u for u in later_use.get(e, []) if (u[0], u[1]) != (stage, zone)]
        if not uses and exp[e]["kind"] != "flow":
            rep.warn(f"{stage} {zone}", "W2", f"'{_name(exp, e)}': 소개한 뒤 다시 쓰거나 섞는 곳이 없음(불러온 가이드 범위)")
    for e, wheres in sorted(planned.items()):
        rep.warn(", ".join(wheres), "W8", f"'{_name(exp, e)}': 아직 구현되지 않은 계획 경험 — 만들어야 함")
    return rep


def _name(exp, e):
    return exp[e]["name"] if e in exp else e


def _check_layout(rep, stage, g, zones):
    layout = g.get("layout")
    if not layout:
        return
    path = os.path.join(STAGE_DIR, layout)
    if not os.path.exists(path):
        rep.warn(stage, "G6", f"맵 데이터 {layout}가 아직 없음(가이드만 있는 계획 단계)")
        return
    built = {z["id"]: z["size"] for z in load(path).get("zones", [])}
    planned = {z["id"]: z["size"] for z in zones}
    for zid in sorted(set(built) | set(planned)):
        if zid not in built:
            rep.error(f"{stage} {zid}", "G6", f"가이드에만 있고 맵 데이터 {layout}에 없음")
        elif zid not in planned:
            rep.error(f"{stage} {zid}", "G6", f"맵 데이터에만 있고 가이드에 없음")
        elif built[zid] != planned[zid]:
            rep.error(f"{stage} {zid}", "G6", f"크기가 다름(가이드 {planned[zid]}, 맵 {built[zid]})")


# ---------- board ----------

ROLE_COLORS = {"safe": "#2f8f5b", "explore": "#2f6fb5", "fight": "#c8642a", "peak": "#c8322a",
               "rest": "#59a96a", "transit": "#8a8f98", "finale": "#7d1d6f"}
KIND_COLORS = {"move": "#1f6f8b", "device": "#b5651d", "enemy": "#a02c2c", "space": "#5b4a9c", "flow": "#3f7f3f"}


def board(guide_path, out_path, catalog):
    g = load(guide_path)
    exp = {e["id"]: e for e in catalog["experiences"]}
    rep = check([guide_path], catalog)
    zones = sorted(g["zones"], key=lambda z: z["order"])
    cols = [("순서 · 구역", 190), ("역할", 70), ("새 경험", 330), ("다시 쓰기", 300), ("섞기", 190), ("길 안내", 170), ("검사", 150)]
    width = sum(w for _, w in cols) + 40
    row_h = []
    for z in zones:
        lines = max(len(z.get("new", [])), len(z.get("reuse", [])), len(z.get("mix", [])), len(z.get("wayfinding", [])), 2)
        row_h.append(18 + lines * 17 + (22 if z.get("note") else 6))
    top = 150
    height = top + sum(row_h) + 120
    out = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}" '
           f'font-family="Noto Sans KR, Malgun Gothic, sans-serif">', f'<rect width="100%" height="100%" fill="#f7f5ef"/>']

    def text(x, y, s, size=12, color="#1f1f1f", weight="normal", anchor="start"):
        out.append(f'<text x="{x}" y="{y}" font-size="{size}" fill="{color}" font-weight="{weight}" text-anchor="{anchor}">{escape(s)}</text>')

    text(20, 30, f"{g['name']} — 레벨 디자인 가이드 맵 ({g.get('status', '?')}, {g.get('date', '')})", 18, weight="bold")
    summary = next((i for i in rep.infos if not i.startswith("읽음")), "")
    text(20, 52, summary, 12, "#444")
    text(20, 72, f"오류 {len(rep.errors)} · 경고 {len(rep.warnings)} — 규칙: 큰 구역마다 새 경험 1개 이상(G2), 소개 전에 쓰지 않음(G3), 정점 뒤 휴식(W4), 길 안내(W5), 섞기(W6). 자세히: guide-map.md", 11, "#8a2a20" if rep.errors else "#2f6f3f")
    # progress bar: zones placed by progress, colored by role
    bx, bw, by = 20, width - 40, 92
    out.append(f'<rect x="{bx}" y="{by}" width="{bw}" height="14" fill="#e3ded0" rx="3"/>')
    for i, z in enumerate(zones):
        nxt = zones[i + 1]["progress"] if i + 1 < len(zones) else 1.0
        x0 = bx + z["progress"] * bw
        x1 = min(bx + bw, bx + max(nxt, z["progress"] + .02) * bw)
        x0 = min(x0, bx + bw - 6)
        out.append(f'<rect x="{x0:.1f}" y="{by}" width="{max(3, x1 - x0):.1f}" height="14" fill="{ROLE_COLORS.get(z["role"], "#999")}" opacity=".85"/>')
        text(x0 + 2, by + 28, z["id"], 10, "#333")
        for k in range(len(z.get("new", []))):
            out.append(f'<circle cx="{min(x0 + 6 + k * 7, bx + bw - 3):.1f}" cy="{by - 5 - (8 if x0 + 6 + k * 7 > bx + bw - 3 else 0)}" r="2.8" fill="#c8322a"/>')
    x = 20
    for (name, w) in cols:
        text(x + 4, top - 8, name, 12, "#333", "bold")
        x += w
    y = top
    zone_issues = {}
    for line in rep.errors + rep.warnings:
        code = line[1:line.index("]")]
        for z in zones:
            if f" {z['id']}:" in line or f" {z['id']}," in line:
                zone_issues.setdefault(z["id"], []).append(code)
    for z, h in zip(zones, row_h):
        out.append(f'<rect x="20" y="{y}" width="{width - 40}" height="{h - 4}" fill="#ffffff" stroke="#00000014"/>')
        size = "큰" if z["size"] == "large" else "작은"
        text(28, y + 18, f"{z['order']}. {z['id']} ({size})", 13, weight="bold")
        text(28, y + 35, f"진행 {z['progress']:.0%}", 11, "#555")
        cx = 20 + cols[0][1]
        role = catalog["roles"].get(z["role"], z["role"])
        out.append(f'<rect x="{cx + 4}" y="{y + 6}" width="58" height="18" rx="9" fill="{ROLE_COLORS.get(z["role"], "#999")}"/>')
        text(cx + 33, y + 19, role, 11, "#fff", "bold", "middle")
        cx += cols[1][1]
        for col, items in ((cols[2][1], z.get("new", [])), (cols[3][1], z.get("reuse", []))):
            for k, e in enumerate(items):
                color = KIND_COLORS.get(exp.get(e, {}).get("kind"), "#555")
                mark = " (계획)" if exp.get(e, {}).get("status") == "planned" else ""
                text(cx + 6, y + 18 + k * 17, f"• {_name(exp, e)}{mark}", 12, color)
            cx += col
        for k, m in enumerate(z.get("mix", [])):
            text(cx + 6, y + 18 + k * 17, " + ".join(_name(exp, e) for e in m), 12, "#5b4a9c")
        cx += cols[4][1]
        for k, w in enumerate(z.get("wayfinding", [])):
            text(cx + 6, y + 18 + k * 17, catalog["wayfinding"].get(w, w), 12, "#2f6f3f")
        if not z.get("wayfinding"):
            text(cx + 6, y + 18, "없음", 12, "#b0463a")
        cx += cols[5][1]
        for k, code in enumerate(sorted(set(zone_issues.get(z["id"], [])))):
            color = "#c8322a" if code.startswith("G") else "#c07a12"
            text(cx + 6, y + 18 + k * 17, code, 12, color, "bold")
        if z.get("note"):
            text(28, y + h - 10, z["note"], 10, "#777")
        y += h
    y += 16
    text(20, y, "색: 경험 종류 — " + " · ".join(f"{catalog['kinds'][k]}" for k in KIND_COLORS), 11, "#444")
    for i, (k, c) in enumerate(KIND_COLORS.items()):
        out.append(f'<rect x="{20 + i * 120}" y="{y + 10}" width="12" height="12" fill="{c}"/>')
        text(36 + i * 120, y + 21, catalog["kinds"][k], 11, c)
    text(20, y + 46, "역할 막대: 진행 위치에 따라 구역을 놓고 역할 색으로 칠함, 빨간 점 = 그 구역의 새 경험 수. 검사 칸: G = 오류, W = 경고(guide-map.md 4장).", 11, "#444")
    out.append("</svg>")
    os.makedirs(os.path.dirname(os.path.abspath(out_path)), exist_ok=True)
    with open(out_path, "w", encoding="utf-8") as f:
        f.write("\n".join(out))


def main(argv):
    if not argv or argv[0] not in ("check", "board"):
        print(__doc__)
        return 2
    catalog = load(CATALOG)
    if argv[0] == "board":
        board(argv[1], argv[2], catalog)
        print(f"board -> {argv[2]}")
        return 0
    strict = "--strict" in argv
    rep = check([a for a in argv[1:] if not a.startswith("--")], catalog)
    for line in rep.infos:
        print(line)
    for line in rep.errors:
        print("오류 " + line)
    for line in rep.warnings:
        print("경고 " + line)
    print(f"GUIDE_CHECK errors {len(rep.errors)} warnings {len(rep.warnings)}")
    return 1 if rep.errors or (strict and rep.warnings) else 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main(sys.argv[1:]))
