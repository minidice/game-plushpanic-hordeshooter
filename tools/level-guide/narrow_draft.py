"""Draft of the large Stage 1 with its big zones B, D and F rebuilt as lanes (doc/features/level/stage1-narrow-plan.md, L-36 · L-37).

    python tools/level-guide/narrow_draft.py OUT_DIR

Writes OUT_DIR/stage1_large_layout.json: the current layout with the big open regions of B (main_street), D (depot) and
F (freight_yard) replaced by lanes - a main lane along the route, side lanes, cross alleys and dead-end pockets. What is
left between lanes becomes city blocks (the game fills void near the floor with buildings and fences). Raised regions,
links, markers and the small zones stay. Proposed new meaningful things for L-37 are listed under "proposal_meaning".
"""
import json
import math
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SRC = os.path.join(ROOT, "game", "content", "data", "stages", "stage1_large_layout.json")


def band(points, half):
    """Quads around each segment of a polyline, a little longer so the joints overlap (regions add up as floor)."""
    quads = []
    for (x0, z0), (x1, z1) in zip(points, points[1:]):
        dx, dz = x1 - x0, z1 - z0
        d = math.hypot(dx, dz)
        ux, uz = dx / d, dz / d
        nx, nz = -uz, ux
        a = (x0 - ux * half * .6, z0 - uz * half * .6)
        b = (x1 + ux * half * .6, z1 + uz * half * .6)
        quads.append([[round(a[0] + nx * half, 1), round(a[1] + nz * half, 1)], [round(b[0] + nx * half, 1), round(b[1] + nz * half, 1)],
                      [round(b[0] - nx * half, 1), round(b[1] - nz * half, 1)], [round(a[0] - nx * half, 1), round(a[1] - nz * half, 1)]])
    return quads


def rect(x0, z0, x1, z1):
    return [[x0, z0], [x1, z0], [x1, z1], [x0, z1]]


# Lanes per zone: (name, polyline or rect, half width or None). Route points as in the current layout.
LANES = {
    "B": [
        ("b_main", [[16, 0], [40, -6], [80, -16], [120, -30], [131, -54]], 11),      # the street along the route, 22 m
        ("b_back", [[22, 10], [70, 8], [118, 2]], 5),                              # back lane south (a flank, 10 m)
        ("b_cross1", [[44, 6], [42, -6]], 4),                                       # cross alleys joining it
        ("b_cross2", [[112, 2], [114, -24]], 4),
        ("b_pharmacy", [[66, -14], [69, -40]], 4),                                  # to the pharmacy branch
        ("c1_base", rect(133, -58, 147, -50), None),                                # floor under base C1 (now the way into the alley)
    ],
    "D": [
        ("d_main", [[198, -73], [240, -80], [290, -86], [340, -89]], 12),
        ("d_dock", [[229, -84], [229, -102]], 8),                                   # up to the dock ramp and ladder
        ("d_stack", [[269, -86], [269, -92]], 5),                                   # to the container-top ladder
        ("d_mission", [[312, -94], [300, -106]], 8),                                # toward the panel
        ("d_mission_pocket", rect(284, -128, 316, -104), None),                     # the panel's pocket (L-33 #5)
        ("d_yard", [[214, -62], [250, -58], [322, -58]], 6),                        # pallet lane south (crates, a flank)
        ("d_cross", [[250, -58], [252, -78]], 4),
        ("d_cross2", [[322, -58], [326, -82]], 4),
    ],
    "F": [
        ("f_main", [[374, -91], [420, -100], [470, -110], [522, -118]], 12),
        ("f_stack3_foot", [[477, -110], [477, -127]], 4),                           # to the stack 3 ladder
        ("f_north", [[404, -150], [452, -152], [500, -150]], 5),                    # behind the container stacks, under the crane
        ("f_cross1", [[440, -106], [440, -150]], 4),                                # east of stack 2, up to the north lane
        ("f_cross2", [[497, -114], [497, -150]], 4),                                # east of stack 3
        ("f_truck_pocket", rect(484, -104, 500, -92), None),                        # the alarm truck's bay
    ],
}

# L-37 fill: something in every quarter screen. Positions on the lanes above; kinds are what each spot holds.
PROPOSAL_MEANING = [
    # B
    {"zone": "B", "at": [100, -14], "what": "가게 앞 상자(탄약 1 · 투척물 1)"},
    {"zone": "B", "at": [70, 8], "what": "뒷길 경계형 3(웨이브 때 옆에서 오는 길이 됨)"},
    {"zone": "B", "at": [28, -4], "what": "드럼통 2(첫 웨이브 연습)"},
    # D
    {"zone": "D", "at": [286, -58], "what": "남쪽 팔레트 길 끝 보상 상자(구급상자 1)"},
    {"zone": "D", "at": [229, -96], "what": "하역장 경사로 아래 잠든 3"},
    {"zone": "D", "at": [326, -70], "what": "교차 골목 버튼(옆 셔터를 열어 지름길 — 선택 임무)"},
    # F
    {"zone": "F", "at": [452, -150], "what": "크레인 아래 보급(탄약 · 파편 수류탄)"},
    {"zone": "F", "at": [410, -150], "what": "북쪽 길 시작 잠든 4"},
    {"zone": "F", "at": [497, -146], "what": "북쪽 길 끝 드럼통 · 경계형 2"},
    {"zone": "F", "at": [440, -128], "what": "2번 더미 옆 교차 골목 잠든 2"},
    {"zone": "F", "at": [497, -130], "what": "3번 더미 옆 교차 골목 탄약 상자"},
    # small zones that stay as they are (L-37 only)
    {"zone": "P5", "at": [-32, 10], "what": "남쪽 길 가운데 차 트렁크 탄약(조용히 돌아가는 길의 보상)"},
    {"zone": "P5", "at": [-55, 10], "what": "서쪽 입구 경계형 1(남쪽 길로 가도 하나는 만남)"},
    {"zone": "P5", "at": [-12, -10], "what": "동쪽 구석 투척물 1(곁길 보상)"},
    {"zone": "P5", "at": [-55, -10], "what": "북서 구석 잠든 무리 가장자리 드럼통"},
    {"zone": "H", "at": [600, -140], "what": "세이프하우스 앞 경계형 3 — 문 3초를 누를 때 압박(L-35)"},
    {"zone": "H", "at": [628, -124], "what": "세이프하우스 옆 구석 탄약"},
    {"zone": "E", "at": [350, -98], "what": "초소 북쪽 구석 1회 산탄총(R870) — 무기 고르기"},
    {"zone": "P6", "at": [12, -8], "what": "기지 밖 북쪽 쓰레기통 뒤 탄약"},
    {"zone": "P2", "at": [-120, -8], "what": "첫 마당 북서 구석 상자(근접으로 부수면 탄약 — 다음 권총 대비)"},
]

ZONE_REGION = {"B": "main_street", "D": "depot", "F": "freight_yard"}
# Small lots trimmed to the floor they use (L-37: empty corners go instead of being stuffed).
TRIM = {
    "p_lot": [[-61, -12], [-4, -12], [-4, 12], [-61, 12]],               # P5: the corners beyond the sleeping pack and the south path
    "safehouse_deck": [[590, -144], [627, -144], [627, -122], [590, -122]],  # H: the deck around the safehouse, not the far strips
}


def main(argv):
    out_dir = argv[0] if argv else "."
    with open(SRC, encoding="utf-8") as f:
        layout = json.load(f)
    regions = []
    for r in layout["regions"]:
        zone = next((z for z, rid in ZONE_REGION.items() if rid == r["id"]), None)
        if r["id"] in TRIM:
            regions.append(dict(r, poly=TRIM[r["id"]]))
            continue
        if zone is None:
            regions.append(r)
            continue
        for name, shape, half in LANES[zone]:
            polys = [shape] if half is None else band(shape, half)
            for i, poly in enumerate(polys):
                regions.append({"id": f"{name}_{i}", "label": f"{zone} {name}", "tier": 0, "poly": poly})
    layout["regions"] = regions
    layout["proposal_meaning"] = PROPOSAL_MEANING
    os.makedirs(out_dir, exist_ok=True)
    out = os.path.join(out_dir, "stage1_large_layout.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(layout, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print("draft ->", out)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
