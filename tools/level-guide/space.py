"""Space check for a stage layout (doc/features/level/guide-map.md 10장, decisions.md L-36 · L-37).

    python tools/level-guide/space.py game/content/data/stages/stage1_large_layout.json [out.svg] [--proposals]

For every walkable 1 m cell, seen at the widest default view (about 37 x 24 m, turned by the camera yaw, L-2):
  A (L-36) a wall shows: some edge of the walkable floor (a wall, a building, the map's border) lies on that screen;
  B (L-37) something is there: within a quarter of that screen (about 18.7 x 12.1 m) around the cell lies at least one
    meaningful thing - a supply item, a marker (mission, base, landmark, alarm, safe zone, blocked way, safehouse),
    a monster group or a named one, a breach wall, a ladder or ramp, a gameplay prop (barrel, alarm vehicle).
Prints the share of floor failing each rule, whole stage and per zone, and draws them (red: no wall on screen,
orange: nothing meaningful near) when an SVG path is given. Needs numpy.
"""
import json
import math
import os
import sys

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
STAGES = os.path.join(ROOT, "game", "content", "data", "stages")
CAMERA_YAW_DEG = -30.0          # CameraRig.Yaw (L-2), as tools/floorplan/render_floorplan.py
SCREEN_HALF = (18.7, 12.1)      # half of the widest default screen on the floor, metres (right, up)
QUARTER_HALF = (SCREEN_HALF[0] / 2, SCREEN_HALF[1] / 2)
MEANING_PROPS = {"explosive_barrel", "alarm_truck", "car_alarm", "wall_button_panel"}


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def inside(points, poly):
    """Even-odd test of many points (N x 2) against one polygon."""
    x, z = points[:, 0], points[:, 1]
    hit = np.zeros(len(points), bool)
    j = len(poly) - 1
    for i in range(len(poly)):
        xi, zi = poly[i]
        xj, zj = poly[j]
        cross = ((zi > z) != (zj > z)) & (x < (xj - xi) * (z - zi) / ((zj - zi) or 1e-9) + xi)
        hit ^= cross
        j = i
    return hit


def features(layout, stem, proposals=False):
    """Meaningful things of the stage as (x, z, kind); with proposals also a draft's "proposal_meaning" spots."""
    out = []
    if proposals:
        for p in layout.get("proposal_meaning", []):
            out.append((p["at"][0], p["at"][1], "proposal"))
    for m in layout.get("markers", []):
        out.append((m["pos"][0], m["pos"][1], m["type"]))
    for link in layout.get("links", []):
        for p in (link.get("from"), link.get("to")):
            if p:
                out.append((p[0], p[1], link.get("type", "link")))
    for p in layout.get("props", []):
        if p["id"] in MEANING_PROPS:
            out.append((p["pos"][0], p["pos"][1], p["id"]))
    enc = os.path.join(STAGES, stem + "_encounters.json")
    if os.path.exists(enc):
        for g in load(enc).get("groups", []):
            if "at" in g:
                out.append((g["at"][0], g["at"][1], "special"))
            elif "rect" in g:
                # A pack spreads over its rect: every part of it counts, sampled every 4 m.
                x0, x1, z0, z1 = g["rect"]
                for x in np.arange(x0, x1 + .1, 4.0):
                    for z in np.arange(z0, z1 + .1, 4.0):
                        out.append((float(x), float(z), "pack"))
    sup = os.path.join(STAGES, stem + "_supplies.json")
    if os.path.exists(sup):
        for point in load(sup).get("points", []):
            for it in point["items"]:
                out.append((it["at"][0], it["at"][1], "supply"))
    key = "stage1_large" if stem == "stage1_large" else stem
    breach = os.path.join(STAGES, "destruction_placements.json")
    if os.path.exists(breach):
        for p in load(breach).get("placements", []):
            if p["map"] == key:
                out.append((p["at"][0], p["at"][2], "breach"))
    return out


def analyse(layout_path, proposals=False):
    layout = load(layout_path)
    stem = os.path.basename(layout_path).replace("_layout.json", "")
    x0, z0, x1, z1 = layout["bounds"]
    xs = np.arange(x0 + .5, x1, 1.0)
    zs = np.arange(z0 + .5, z1, 1.0)
    gx, gz = np.meshgrid(xs, zs)
    cells = np.column_stack([gx.ravel(), gz.ravel()])
    walk = np.zeros(len(cells), bool)
    for r in layout["regions"]:
        walk |= inside(cells, r["poly"])
    grid = walk.reshape(gz.shape)
    # Edge cells: not walkable, next to a walkable one (walls, buildings, the map border).
    pad = np.pad(grid, 1, constant_values=False)
    near = pad[:-2, 1:-1] | pad[2:, 1:-1] | pad[1:-1, :-2] | pad[1:-1, 2:]
    edge = (~grid & near).ravel()
    yaw = math.radians(CAMERA_YAW_DEG)
    right = np.array([math.cos(yaw), -math.sin(yaw)])
    up = np.array([-math.sin(yaw), -math.cos(yaw)])
    W = cells[walk]
    E = cells[edge]
    # Only what stands on the floor counts (a pack's rect may reach past it; a prop may sit in a building block).
    F = np.array([(f[0], f[1]) for f in features(layout, stem, proposals)] or [(1e9, 1e9)])
    on_floor = np.zeros(len(F), bool)
    for s0 in range(0, len(F), 500):
        d = np.abs(F[s0:s0 + 500, None, :] - W[None, :, :]).max(axis=2)
        on_floor[s0:s0 + 500] = (d <= 2.0).any(axis=1)
    F = F[on_floor] if on_floor.any() else np.array([(1e9, 1e9)])

    def seen(points, targets, half):
        ok = np.zeros(len(points), bool)
        tr, tu = targets @ right, targets @ up
        pr, pu = points @ right, points @ up
        for s in range(0, len(points), 2000):
            dr = np.abs(pr[s:s + 2000, None] - tr[None, :]) <= half[0]
            du = np.abs(pu[s:s + 2000, None] - tu[None, :]) <= half[1]
            ok[s:s + 2000] = (dr & du).any(axis=1)
        return ok

    wall = seen(W, E, SCREEN_HALF)
    meaning = seen(W, F, QUARTER_HALF)
    zones = []
    for z in layout.get("zones", []):
        mask = inside(W, z["poly"])
        n = int(mask.sum())
        if n:
            zones.append((z["id"], z["size"], n, float((~wall[mask]).mean()), float((~meaning[mask]).mean())))
    return layout, W, wall, meaning, zones


def svg(layout, W, wall, meaning, out):
    x0, z0, x1, z1 = layout["bounds"]
    s = 2.0
    w, h = (x1 - x0) * s, (z1 - z0) * s
    parts = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{w:.0f}" height="{h + 40:.0f}" font-family="Noto Sans KR, Malgun Gothic, sans-serif">',
             f'<rect width="100%" height="100%" fill="#2f3a33"/>']
    for r in layout["regions"]:
        pts = " ".join(f"{(p[0] - x0) * s:.1f},{(p[1] - z0) * s:.1f}" for p in r["poly"])
        parts.append(f'<polygon points="{pts}" fill="#d8d2bf" stroke="#8a8370" stroke-width="1"/>')
    for (x, z), a, b in zip(W, wall, meaning):
        if a and b:
            continue
        color = "#d6342c" if not a else "#f0922b"
        parts.append(f'<rect x="{(x - .5 - x0) * s:.1f}" y="{(z - .5 - z0) * s:.1f}" width="{s:.1f}" height="{s:.1f}" fill="{color}" fill-opacity=".6"/>')
    for z in layout.get("zones", []):
        cx = sum(p[0] for p in z["poly"]) / len(z["poly"])
        cz = sum(p[1] for p in z["poly"]) / len(z["poly"])
        parts.append(f'<text x="{(cx - x0) * s:.0f}" y="{(cz - z0) * s:.0f}" font-size="12" fill="#1f1f1f" text-anchor="middle">{z["id"]}</text>')
    parts.append(f'<text x="8" y="{h + 26:.0f}" font-size="13" fill="#ffffff">빨강: 가장 넓은 화면에 벽이 안 보임(L-36) · 주황: 화면 1/4 안에 의미 있는 것이 없음(L-37)</text>')
    parts.append("</svg>")
    with open(out, "w", encoding="utf-8") as f:
        f.write("\n".join(parts))


def main(argv):
    if not argv:
        print(__doc__)
        return 2
    proposals = "--proposals" in argv
    argv = [a for a in argv if a != "--proposals"]
    layout, W, wall, meaning, zones = analyse(argv[0], proposals)
    print(f"바닥 {len(W)} m² · 벽이 안 보이는 곳 {(~wall).mean() * 100:.0f}% · 의미 없는 곳 {(~meaning).mean() * 100:.0f}%")
    for zid, size, n, no_wall, empty in zones:
        print(f"  {zid:<11} {size:<5} {n:>6} m²  벽 안 보임 {no_wall * 100:>3.0f}%  빈 곳 {empty * 100:>3.0f}%")
    if len(argv) > 1:
        svg(layout, W, wall, meaning, argv[1])
        print("svg ->", argv[1])
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
