"""Apply the narrow-lanes plan to the large Stage 1, one zone at a time (doc/features/level/stage1-narrow-plan.md, L-36 · L-37).

    python tools/level-guide/apply_narrow.py B|D|F|small

Replaces the zone's open region with the plan's lanes (narrow_draft.LANES), adds the plan's fill (supplies, packs,
explosive barrels, a switch gate), and moves what the narrower floor would leave off the floor: hidden wave entries,
monster packs, small props and street lamps snap to the nearest lane floor; the city buildings stay (they become blocks).
"small" trims P5 and the safehouse deck, fills the small zones and turns the G barrels into real explosive ones.
Prints what it moved. Idempotent per zone (a zone already in lanes is left as it is).
"""
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
import narrow_draft as plan  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
STAGES = os.path.join(ROOT, "game", "content", "data", "stages")
KEEP_PROPS = {"urban_rowhouse", "urban_shop", "urban_warehouse", "warehouse_sign"}

FILL = {
    "B": {
        "supplies": [{"id": "b_shop_cache", "kind": "optional", "area": "street", "items": [
            {"item": "ammo", "at": [98.5, -14]}, {"item": "throwable", "id": "frag", "at": [101, -14]}]},
            {"id": "b_back_west", "kind": "optional", "area": "street", "items": [{"item": "ammo", "at": [20, 12]}]},
            {"id": "b_terrace", "kind": "optional", "area": "street", "items": [{"item": "ammo", "at": [104, -46]}]}],
        "groups": [{"area": "street", "reaction": "watcher", "count": 3, "rect": [64, 76, 4, 12]},
                   {"area": "street", "reaction": "dormant", "count": 2, "rect": [30, 40, 7, 12]},
                   {"area": "street", "reaction": "dormant", "count": 2, "rect": [51, 59, -20, -15]}],
        "props": [{"id": "explosive_barrel", "pos": [27, -5], "yaw": 0, "solid": True}, {"id": "explosive_barrel", "pos": [30, -3], "yaw": 0, "solid": True},
                  {"id": "explosive_barrel", "pos": [92, 6], "yaw": 0, "solid": True}, {"id": "explosive_barrel", "pos": [114, 1], "yaw": 0, "solid": True}],
        "markers": [],
    },
    "D": {
        "supplies": [{"id": "d_pallet_end", "kind": "optional", "area": "depot", "items": [{"item": "medkit", "at": [286, -58]}]},
                     {"id": "d_shutter_ammo", "kind": "optional", "area": "depot", "items": [{"item": "ammo", "at": [333, -80]}]}],
        "groups": [{"area": "depot", "reaction": "dormant", "count": 3, "rect": [223, 235, -98, -90]},
                   {"area": "depot", "reaction": "dormant", "count": 3, "rect": [216, 228, -64, -56]},
                   {"area": "depot", "reaction": "dormant", "count": 3, "rect": [258, 270, -63, -55]},
                   {"area": "depot", "reaction": "dormant", "count": 3, "rect": [300, 312, -63, -55]}],
        "props": [],
        "markers": [{"type": "switch_gate", "pos": [324.5, -70], "width": 9, "button": [316, -63.6], "label": "팔레트 길 셔터(안에서 버튼)"}],
    },
    "F": {
        "supplies": [
            {"id": "f_crane_cache", "kind": "optional", "area": "yard", "items": [{"item": "ammo", "at": [450, -150]}, {"item": "throwable", "id": "frag", "at": [452.5, -150]}]},
            {"id": "f_cross_ammo", "kind": "optional", "area": "yard", "items": [{"item": "ammo", "at": [497, -130]}]}],
        "groups": [
            {"area": "yard", "reaction": "dormant", "count": 4, "rect": [404, 416, -154, -146]},
            {"area": "yard", "reaction": "watcher", "count": 2, "rect": [462, 472, -156, -148]},
            {"area": "yard", "reaction": "dormant", "count": 2, "rect": [437, 443, -130, -122]},
            {"area": "yard", "reaction": "dormant", "count": 4, "rect": [376, 388, -96, -86]},
            {"area": "yard", "reaction": "dormant", "count": 4, "rect": [462, 474, -114, -104]},
            {"area": "yard", "reaction": "dormant", "count": 3, "rect": [428, 438, -154, -146]}],
        "props": [{"id": "explosive_barrel", "pos": [500, -147], "yaw": 0, "solid": True}, {"id": "explosive_barrel", "pos": [486, -152], "yaw": 0, "solid": True}],
        "markers": [],
    },
    "small": {
        "supplies": [
            {"id": "p5_trunk", "kind": "optional", "area": "p_lot", "items": [{"item": "ammo", "at": [-32, 9]}]},
            {"id": "p5_corner", "kind": "optional", "area": "p_lot", "items": [{"item": "throwable", "id": "molotov", "at": [-12, -9]}]},
            {"id": "h_corner", "kind": "optional", "area": "safehouse", "items": [{"item": "ammo", "at": [624, -125]}]},
            {"id": "e_shotgun", "kind": "optional", "area": "post", "items": [{"item": "weapon", "id": "r870", "at": [350, -97]}]},
            {"id": "p6_outside", "kind": "optional", "area": "saferoom", "items": [{"item": "ammo", "at": [12, -7.5]}]},
            {"id": "p2_corner", "kind": "optional", "area": "p_yard", "items": [{"item": "ammo", "at": [-120, -7]}]}],
        "groups": [
            {"area": "p_lot", "reaction": "watcher", "count": 1, "rect": [-57, -53, 8, 11], "fixed": True},
            {"area": "safehouse", "reaction": "watcher", "count": 3, "rect": [596, 604, -142, -136]}],
        "props": [{"id": "explosive_barrel", "pos": [-55, -10], "yaw": 0, "solid": True}],
        "markers": [],
    },
}
ZONE_AREA = {"B": ("main_street",), "D": ("depot",), "F": ("freight_yard",)}
SMALL_TRIMMED = ("P5", "H")
# Wide old packs split over the lanes instead of all snapping onto the main lane, and a few moved into what would stay
# empty (by their old rect; count, new rect). No barrels beyond the plan: L-33 #4 keeps D and F free of them.
# The new lane packs in FILL hold the rest of each split pack.
PACKS = {
    "D": [([202, 250, -100, -56], 5, [208, 220, -80, -70]), ([210, 250, -96, -60], 2, [236, 246, -64, -56]),
          ([252, 300, -110, -50], 5, [274, 286, -90, -80]), ([233, 239, -83, -77], 3, [246, 254, -83, -77]), ([302, 332, -120, -46], 3, [286, 298, -126, -114]),
          ([316, 332, -116, -100], 3, [304, 314, -126, -116])],
    "F": [([378, 420, -130, -72], 4, [393, 405, -106, -96]), ([390, 420, -110, -80], 2, [424, 436, -100, -92]),
          ([422, 470, -140, -80], 4, [440, 452, -115, -105]), ([472, 518, -150, -90], 5, [489, 501, -125, -115])],
}


def load(name):
    path = os.path.join(STAGES, name)
    with open(path, encoding="utf-8") as f:
        raw = f.read()
    return path, raw, json.loads(raw)


def save(path, raw, data):
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(json.dumps(data, ensure_ascii=False, indent=2) + ("\n" if raw.endswith("\n") else ""))


def inside(p, poly):
    x, z = p
    hit = False
    j = len(poly) - 1
    for i in range(len(poly)):
        xi, zi = poly[i]
        xj, zj = poly[j]
        if (zi > z) != (zj > z) and x < (xj - xi) * (z - zi) / ((zj - zi) or 1e-9) + xi:
            hit = not hit
        j = i
    return hit


class Floor:
    def __init__(self, regions):
        self.polys = [r["poly"] for r in regions]

    def on(self, p, margin=0.0):
        if margin <= 0:
            return any(inside(p, poly) for poly in self.polys)
        return all(any(inside((p[0] + dx, p[1] + dz), poly) for poly in self.polys)
                   for dx, dz in ((0, 0), (margin, 0), (-margin, 0), (0, margin), (0, -margin)))

    def snap(self, p, margin=2.0, reach=45):
        """Nearest point at least margin inside the floor, searched on a 1 m spiral."""
        if self.on(p, margin):
            return p
        best = None
        for r in range(1, reach):
            for k in range(max(8, r * 6)):
                a = k * 2 * math.pi / max(8, r * 6)
                q = (round(p[0] + r * math.cos(a), 1), round(p[1] + r * math.sin(a), 1))
                if self.on(q, margin):
                    best = q
                    break
            if best:
                return best
        return p


def apply(zone):
    lp, lraw, L = load("stage1_large_layout.json")
    ep, eraw, E = load("stage1_large_encounters.json")
    sp, sraw, S = load("stage1_large_supplies.json")
    moved = []
    # "small" only moves what its trims leave off the floor (P5, H); the rest of the map stays as it is.
    zone_polys = [z["poly"] for z in L["zones"] if z["id"] in (SMALL_TRIMMED if zone == "small" else (zone,))]

    if zone in ZONE_AREA:
        old = [r for r in L["regions"] if r["id"] in ZONE_AREA[zone]]
        if not old:
            print(f"{zone}: already in lanes (fill only)")
        regions = []
        for r in L["regions"]:
            if r["id"] not in ZONE_AREA[zone]:
                regions.append(r)
                continue
            for name, shape, half in plan.LANES[zone]:
                polys = [shape] if half is None else plan.band(shape, half)
                for i, poly in enumerate(polys):
                    regions.append({"id": f"{name}_{i}", "label": f"{zone} {name}", "tier": 0, "poly": poly})
        L["regions"] = regions
    else:
        for r in L["regions"]:
            if r["id"] in plan.TRIM:
                r["poly"] = plan.TRIM[r["id"]]
        for p in L["props"]:
            if p["id"] == "barrel" and 522 <= p["pos"][0] <= 592:
                p["id"] = "explosive_barrel"
                moved.append(f"G barrel {p['pos']} -> explosive_barrel")
    floor = Floor(L["regions"])

    def in_zone(p):
        return any(inside(p, poly) for poly in zone_polys)

    # hidden wave entries
    for e in E["entries"]:
        if e.get("kind") == "hidden" and in_zone(e["at"]) and not floor.on(e["at"], 1.5):
            q = floor.snap(e["at"], 2.0)
            moved.append(f"entry {e['at']} -> {list(q)}")
            e["at"] = list(q)
    # packs the plan places by hand, then the rest: off the floor -> centred on the nearest lane floor, at most 12 x 10 m
    for old, count, rect in PACKS.get(zone, []):
        for g in E["groups"]:
            if g.get("rect") == old:
                g["count"], g["rect"] = count, rect
                moved.append(f"pack {g['area']} {g['reaction']} {old} -> {count} at {rect}")
    for g in E["groups"]:
        if "rect" not in g:
            continue
        x0, x1, z0, z1 = g["rect"]
        c = ((x0 + x1) / 2, (z0 + z1) / 2)
        if not in_zone(c):
            continue
        corners = [(x0, z0), (x1, z0), (x1, z1), (x0, z1), c]
        if sum(floor.on(p) for p in corners) >= 4:
            continue
        q = floor.snap(c, 4.0)
        w, d = min(12, x1 - x0) / 2, min(10, z1 - z0) / 2
        g["rect"] = [round(q[0] - w, 1), round(q[0] + w, 1), round(q[1] - d, 1), round(q[1] + d, 1)]
        moved.append(f"pack {g['area']} {g['reaction']} {g['count']} -> {g['rect']}")
    # small props and lamps
    props = []
    for p in L["props"]:
        if in_zone(p["pos"]) and p["id"] not in KEEP_PROPS and not floor.on(p["pos"], 0.5):
            moved.append(f"prop {p['id']} {p['pos']} removed (now inside a block)")
            continue
        props.append(p)
    L["props"] = props
    lamps = []
    for l in L["lamps"]:
        pos = l["pos"] if isinstance(l, dict) else l
        if in_zone(pos) and not floor.on(pos, 0.5):
            q = list(floor.snap(pos, 1.5))
            moved.append(f"lamp {pos} -> {q}")
            l = dict(l, pos=q) if isinstance(l, dict) else q
        lamps.append(l)
    L["lamps"] = lamps

    fill = FILL[zone]
    known = {p["id"] for p in S["points"]}
    S["points"] += [p for p in fill["supplies"] if p["id"] not in known]
    E["groups"] += [g for g in fill["groups"] if g not in E["groups"]]
    L["props"] += [p for p in fill["props"] if p not in L["props"]]
    L["markers"] += [m for m in fill["markers"] if m not in L["markers"]]
    save(lp, lraw, L)
    save(ep, eraw, E)
    save(sp, sraw, S)
    print(f"{zone}: applied; moved {len(moved)}")
    for m in moved:
        print("  " + m)


if __name__ == "__main__":
    apply(sys.argv[1])
