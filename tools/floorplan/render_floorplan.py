"""Draw a stage floor plan draft (JSON) as SVG and report route lengths and the L-1 ratio.

    python tools/floorplan/render_floorplan.py tools/floorplan/stage2-layout-draft.json doc/images/stage2-floorplan.svg

Plan view: x to the right, the far side (-z) up, the camera side (+z) down. Standard library only.
"""
import json
import math
import sys
from xml.sax.saxutils import escape

SCALE = 3.0  # px per metre
MARGIN = 40
LEGEND_H = 250
TIER_COLORS = ["#e9e3cf", "#d6cba4", "#bda97a", "#9c8657", "#7a6640"]
VOID = "#3d4b40"
CAMERA_YAW_DEG = -30.0  # CameraRig.Yaw, decisions.md L-2


def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)


def length(points):
    return sum(math.dist(a, b) for a, b in zip(points, points[1:]))


def up_right_share(points):
    """L-1 as StageTransitionCheck measures it: floor distance whose camera-relative angle is 15-75 degrees."""
    yaw = math.radians(CAMERA_YAW_DEG)
    right = (math.cos(yaw), -math.sin(yaw))  # camera basis X on the floor (x, z)
    up = (-math.sin(yaw), -math.cos(yaw))    # -basis Z on the floor
    total = good = 0.0
    for (x0, z0), (x1, z1) in zip(points, points[1:]):
        dx, dz = x1 - x0, z1 - z0
        d = math.hypot(dx, dz)
        angle = math.degrees(math.atan2(dx * up[0] + dz * up[1], dx * right[0] + dz * right[1]))
        total += d
        if 15 <= angle <= 75:
            good += d
    return good / total if total else 0.0


class Plan:
    def __init__(self, data):
        self.d = data
        x0, z0, x1, z1 = data["bounds"]
        self.x0, self.z0 = x0, z0
        self.w = (x1 - x0) * SCALE + MARGIN * 2
        self.map_h = (z1 - z0) * SCALE + MARGIN * 2
        self.h = self.map_h + LEGEND_H
        self.out = []

    def p(self, pt):
        return (MARGIN + (pt[0] - self.x0) * SCALE, MARGIN + (pt[1] - self.z0) * SCALE)

    def pts(self, points):
        return " ".join(f"{x:.1f},{y:.1f}" for x, y in map(self.p, points))

    def add(self, s):
        self.out.append(s)

    def text(self, x, y, s, size=11, color="#1f1f1f", anchor="start", weight="normal", halo=True):
        stroke = 'stroke="#ffffffcc" stroke-width="3" paint-order="stroke"' if halo else ""
        self.add(f'<text x="{x:.1f}" y="{y:.1f}" font-size="{size}" fill="{color}" text-anchor="{anchor}" '
                 f'font-weight="{weight}" {stroke}>{escape(s)}</text>')

    def arrow_line(self, a, b, color, width, dash="", head=True):
        (x0, y0), (x1, y1) = self.p(a), self.p(b)
        dash_attr = f'stroke-dasharray="{dash}"' if dash else ""
        self.add(f'<line x1="{x0:.1f}" y1="{y0:.1f}" x2="{x1:.1f}" y2="{y1:.1f}" stroke="{color}" '
                 f'stroke-width="{width}" {dash_attr} stroke-linecap="round"/>')
        if head:
            ang = math.atan2(y1 - y0, x1 - x0)
            size = 4 + width * 1.5
            pa = (x1 - size * math.cos(ang - .45), y1 - size * math.sin(ang - .45))
            pb = (x1 - size * math.cos(ang + .45), y1 - size * math.sin(ang + .45))
            self.add(f'<polygon points="{x1:.1f},{y1:.1f} {pa[0]:.1f},{pa[1]:.1f} {pb[0]:.1f},{pb[1]:.1f}" fill="{color}"/>')

    def polyline(self, points, color, width, dash="", arrows=False):
        dash_attr = f'stroke-dasharray="{dash}"' if dash else ""
        self.add(f'<polyline points="{self.pts(points)}" fill="none" stroke="{color}" stroke-width="{width}" '
                 f'{dash_attr} stroke-linejoin="round" stroke-linecap="round"/>')
        if arrows:
            for a, b in zip(points, points[1:]):
                mid = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
                ahead = (mid[0] + (b[0] - a[0]) * .02, mid[1] + (b[1] - a[1]) * .02)
                self.arrow_line(mid, ahead, color, width, head=True)

    def draw(self):
        d = self.d
        self.add(f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.w:.0f}" height="{self.h:.0f}" '
                 f'viewBox="0 0 {self.w:.0f} {self.h:.0f}" font-family="Noto Sans KR, Malgun Gothic, WenQuanYi Zen Hei, sans-serif">')
        self.add(f'<rect width="100%" height="100%" fill="#f7f5ef"/>')
        self.add(f'<rect x="{MARGIN}" y="{MARGIN}" width="{self.w - MARGIN * 2:.0f}" height="{self.map_h - MARGIN * 2:.0f}" fill="{VOID}"/>')
        self.grid()

        for r in d["regions"]:
            self.add(f'<polygon points="{self.pts(r["poly"])}" fill="{TIER_COLORS[r["tier"]]}" stroke="#00000033" stroke-width="1"/>')
        for r in d["regions"]:
            cx = sum(p[0] for p in r["poly"]) / len(r["poly"])
            cz = sum(p[1] for p in r["poly"]) / len(r["poly"])
            x, y = self.p((cx, cz))
            self.text(x, y + 4, f'{r["tier"]}단', size=10, color="#5a4a2a", anchor="middle", halo=False)

        for e in d["edges"]:
            color = "#7a3e12" if e["climbable"] else "#2b2b2b"
            dash = "6,3" if e["climbable"] else ""
            dash_attr = f'stroke-dasharray="{dash}"' if dash else ""
            self.add(f'<polyline points="{self.pts(e["line"])}" fill="none" stroke="{color}" stroke-width="3" {dash_attr}/>')

        self.screen_footprint((285, -60))

        rt = d["routes"]
        self.polyline(rt["reward_tower"], "#2f6fb5", 3, "2,4")
        self.polyline(rt["reward_workshop"], "#2f6fb5", 3, "2,4")
        self.polyline(rt["mission"], "#e07b12", 3, "7,4", arrows=True)
        self.polyline(rt["mission_return"], "#e07b12", 3, "7,4", arrows=True)
        self.polyline(rt["main"], "#c8322a", 5, arrows=True)
        self.polyline(rt["main_after_gondola"], "#c8322a", 5, arrows=True)

        for l in d["links"]:
            self.link(l)
        for s in d["supplies"]:
            self.supply(s)
        for m in d["markers"]:
            self.marker(m)
        for r in d["regions"]:
            if r["label"]:
                xs = [p[0] for p in r["poly"]]
                zs = [p[1] for p in r["poly"]]
                x, y = self.p(r.get("label_at", (min(xs) + 1, min(zs))))
                self.text(x + 2, y + 15, r["label"], size=13, weight="bold")

        self.orientation()
        self.legend()
        self.add("</svg>")
        return "\n".join(self.out)

    def grid(self):
        x0, z0, x1, z1 = self.d["bounds"]
        for gx in range(int(math.ceil(x0 / 50) * 50), int(x1) + 1, 50):
            a, b = self.p((gx, z0)), self.p((gx, z1))
            self.add(f'<line x1="{a[0]:.1f}" y1="{a[1]:.1f}" x2="{b[0]:.1f}" y2="{b[1]:.1f}" stroke="#ffffff18" stroke-width="1"/>')
            self.text(a[0], MARGIN - 6, f"x {gx}", size=9, color="#666", anchor="middle", halo=False)
        for gz in range(int(math.ceil(z0 / 50) * 50), int(z1) + 1, 50):
            a, b = self.p((x0, gz)), self.p((x1, gz))
            self.add(f'<line x1="{a[0]:.1f}" y1="{a[1]:.1f}" x2="{b[0]:.1f}" y2="{b[1]:.1f}" stroke="#ffffff18" stroke-width="1"/>')
            self.text(MARGIN - 4, a[1] + 3, f"z {gz}", size=9, color="#666", anchor="end", halo=False)

    def screen_footprint(self, center):
        """One default outdoor screen of floor (about 34 m x 19 m, CameraView.Outdoor), turned by the camera yaw."""
        yaw = math.radians(CAMERA_YAW_DEG)
        right = (math.cos(yaw), -math.sin(yaw))
        up = (-math.sin(yaw), -math.cos(yaw))
        hw, hh = 17, 9.5
        corners = []
        for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            corners.append((center[0] + right[0] * hw * sx + up[0] * hh * sy, center[1] + right[1] * hw * sx + up[1] * hh * sy))
        self.add(f'<polygon points="{self.pts(corners)}" fill="none" stroke="#ffffff" stroke-width="1.5" stroke-dasharray="4,3"/>')
        x, y = self.p((center[0] - 16, center[1] + 22))
        self.text(x, y, "기본 시점 한 화면(약 34×19 m)", size=10, color="#ffffff", halo=False)

    def link(self, l):
        a, b = l["from"], l["to"]
        t = l["type"]
        if t == "ramp":
            self.arrow_line(a, b, "#2e8b3e", 5)
        elif t == "drop":
            self.arrow_line(a, b, "#e07b12", 3)
            x, y = self.p(a)
            self.add(f'<polygon points="{x - 6:.1f},{y - 5:.1f} {x + 6:.1f},{y - 5:.1f} {x:.1f},{y + 6:.1f}" fill="#e07b12" stroke="#fff" stroke-width="1"/>')
        elif t == "ladder":
            (x0, y0), (x1, y1) = self.p(a), self.p(b)
            ang = math.atan2(y1 - y0, x1 - x0)
            nx, ny = -math.sin(ang) * 3, math.cos(ang) * 3
            for s in (1, -1):
                self.add(f'<line x1="{x0 + nx * s:.1f}" y1="{y0 + ny * s:.1f}" x2="{x1 + nx * s:.1f}" y2="{y1 + ny * s:.1f}" stroke="#111" stroke-width="1.6"/>')
            for k in range(1, 4):
                f = k / 4
                mx, my = x0 + (x1 - x0) * f, y0 + (y1 - y0) * f
                self.add(f'<line x1="{mx + nx:.1f}" y1="{my + ny:.1f}" x2="{mx - nx:.1f}" y2="{my - ny:.1f}" stroke="#111" stroke-width="1.4"/>')
        elif t == "zipline":
            self.arrow_line(a, b, "#0aa5b8", 2.5, dash="10,4")
            x, y = self.p(((a[0] + b[0]) / 2, (a[1] + b[1]) / 2))
            self.text(x + 14, y - 8, "짚라인(사격 불가)", size=11, color="#066f7c", anchor="middle")
        elif t == "gondola":
            self.arrow_line(a, b, "#7d3fb8", 3, dash="12,5")
            x, y = self.p(((a[0] + b[0]) / 2, (a[1] + b[1]) / 2))
            self.text(x, y + 30, "곤돌라(연출 · 가짜 배경)", size=11, color="#e8d9ff", anchor="middle", halo=False)
            self.text(x, y + 46, "협곡 — 걸을 수 없음", size=10, color="#d9d9d9", anchor="middle", halo=False)

    def supply(self, s):
        colors = {"start": "#555555", "main": "#1f8f4a", "optional": "#2f6fb5", "event": "#d4a017", "arrival": "#555555"}
        x, y = self.p(s["pos"])
        self.add(f'<rect x="{x - 5:.1f}" y="{y - 5:.1f}" width="10" height="10" fill="{colors[s["kind"]]}" stroke="#fff" stroke-width="1.5"/>')
        dx, dy = s.get("label_offset", [8, 4])
        self.text(x + dx, y + dy, s["label"], size=10, color=colors[s["kind"]])

    def marker(self, m):
        x, y = self.p(m["pos"])
        t = m["type"]
        if t == "start":
            self.add(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="8" fill="#c8322a" stroke="#fff" stroke-width="2"/>')
            self.text(x, y + 4, "S", size=10, color="#fff", anchor="middle", weight="bold", halo=False)
        elif t == "safehouse":
            self.add(f'<rect x="{x - 9:.1f}" y="{y - 9:.1f}" width="18" height="18" fill="#c8322a" stroke="#fff" stroke-width="2"/>')
            self.text(x, y + 4, "H", size=10, color="#fff", anchor="middle", weight="bold", halo=False)
        elif t == "safe_zone":
            self.add(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="10" fill="#ffffff" stroke="#1f8f4a" stroke-width="3"/>')
            self.text(x, y + 4, "+", size=13, color="#1f8f4a", anchor="middle", weight="bold", halo=False)
        elif t == "mission":
            self.add(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="8" fill="#e07b12" stroke="#fff" stroke-width="2"/>')
            self.text(x, y + 4, "!", size=11, color="#fff", anchor="middle", weight="bold", halo=False)
        elif t == "blocked":
            self.add(f'<line x1="{x - 7:.1f}" y1="{y - 7:.1f}" x2="{x + 7:.1f}" y2="{y + 7:.1f}" stroke="#c8322a" stroke-width="3"/>')
            self.add(f'<line x1="{x - 7:.1f}" y1="{y + 7:.1f}" x2="{x + 7:.1f}" y2="{y - 7:.1f}" stroke="#c8322a" stroke-width="3"/>')
        elif t == "noise":
            self.add(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="7" fill="none" stroke="#b5179e" stroke-width="2.5"/>')
            self.add(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="12" fill="none" stroke="#b5179e" stroke-width="1.2"/>')
        elif t == "landmark":
            self.add(f'<polygon points="{x:.1f},{y - 8:.1f} {x + 7:.1f},{y + 5:.1f} {x - 7:.1f},{y + 5:.1f}" fill="#333" stroke="#fff" stroke-width="1"/>')
        dx, dy = m.get("label_offset", [12, -8])
        self.text(x + dx, y + dy, m["label"], size=11, weight="bold" if t in ("safe_zone", "mission", "safehouse") else "normal")

    def orientation(self):
        x, y = self.w - MARGIN - 150, self.map_h - MARGIN - 70
        self.add(f'<rect x="{x - 10:.1f}" y="{y - 22:.1f}" width="160" height="88" fill="#f7f5efee" rx="4"/>')
        self.text(x, y - 8, "↑ 먼 쪽(−z)", size=10, halo=False)
        self.text(x, y + 6, "↓ 카메라 쪽(+z)", size=10, halo=False)
        # +x points 30 degrees above screen-right on screen (L-1, L-2)
        self.add(f'<line x1="{x:.1f}" y1="{y + 50:.1f}" x2="{x + 40:.1f}" y2="{y + 50:.1f}" stroke="#c8322a" stroke-width="2"/>')
        self.text(x + 46, y + 40, "+x = 화면에서 우측 상단 30°", size=9, halo=False)
        self.text(x + 46, y + 54, "(카메라 수평 −30°, L-2)", size=9, color="#666", halo=False)
        # scale bar
        sx, sy = MARGIN + 10, self.map_h - MARGIN - 14
        self.add(f'<rect x="{sx - 4}" y="{sy - 16}" width="{50 * SCALE + 50:.0f}" height="26" fill="#f7f5efee" rx="3"/>')
        self.add(f'<line x1="{sx}" y1="{sy}" x2="{sx + 50 * SCALE:.0f}" y2="{sy}" stroke="#111" stroke-width="3"/>')
        self.text(sx + 50 * SCALE + 6, sy + 4, "50 m", size=10, halo=False)

    def legend(self):
        d = self.d
        y0 = self.map_h + 4
        x = MARGIN
        self.text(x, y0 + 14, "Stage 2 평면도 초안 (제안, 미승인) — tools/floorplan/stage2-layout-draft.json에서 생성", size=14, weight="bold", halo=False)
        main = d["routes"]["main"] + d["routes"]["main_after_gondola"]
        walk = length(d["routes"]["main"]) + length(d["routes"]["main_after_gondola"])
        share = (up_right_share(d["routes"]["main"]) * length(d["routes"]["main"]) +
                 up_right_share(d["routes"]["main_after_gondola"]) * length(d["routes"]["main_after_gondola"])) / walk
        mission = length(d["routes"]["mission"]) + length(d["routes"]["mission_return"])
        self.text(x, y0 + 34, f"주 경로 걷는 거리 약 {walk:.0f} m(곤돌라 제외) · 순수 이동 약 {walk / 4.4 / 60:.1f}분(4.4 m/s) · "
                  f"L-1 우측 상단 비율(그린 경로 기준) {share:.0%} · 임무 왕복 약 {mission:.0f} m · 1단 = {d['tier_height_m']} m", size=11, halo=False)
        rows = [
            [("line", "#c8322a", ""), "주 경로"], [("line", "#e07b12", "7,4"), "임무 왕복(다리 내리기)"],
            [("line", "#2f6fb5", "2,4"), "보상 분기"], [("line", "#2e8b3e", ""), "경사로(단 오르내림)"],
            [("line", "#7a3e12", "6,3"), "좀비가 기어오르는 벽"], [("line", "#2b2b2b", ""), "못 오르는 벽(컨테이너 · 콘크리트)"],
            [("line", "#0aa5b8", "10,4"), "짚라인"], [("line", "#7d3fb8", "12,5"), "곤돌라"],
        ]
        for i, (sym, label) in enumerate(rows):
            cx = x + (i % 4) * 300
            cy = y0 + 62 + (i // 4) * 22
            _, color, dash = sym
            dash_attr = f'stroke-dasharray="{dash}"' if dash else ""
            self.add(f'<line x1="{cx}" y1="{cy}" x2="{cx + 34}" y2="{cy}" stroke="{color}" stroke-width="4" {dash_attr}/>')
            self.text(cx + 42, cy + 4, label, size=11, halo=False)
        cy = y0 + 118
        for i, (tier, color) in enumerate(enumerate(TIER_COLORS)):
            cx = x + i * 90
            self.add(f'<rect x="{cx}" y="{cy - 9}" width="22" height="16" fill="{color}" stroke="#0003"/>')
            self.text(cx + 28, cy + 4, f"{tier}단 ({tier * d['tier_height_m']:.1f} m)", size=10, halo=False)
        self.add(f'<rect x="{x + 470}" y="{cy - 9}" width="22" height="16" fill="{VOID}"/>')
        self.text(x + 498, cy + 4, "갈 수 없음(외곽 · 협곡 · 숲)", size=10, halo=False)
        cy += 26
        icons = "S 도착 해치 · + 중간 안전 구역(L-24) · ! 임무 장치 · ✕ 막힌 길 · ▼ 뛰어내리기(한 방향) · ‖ 사다리 · △ 랜드마크 · ◎ 소음 장치 · H 세이프하우스"
        self.text(x, cy + 4, icons, size=11, halo=False)
        cy += 22
        self.text(x, cy + 4, "보급: 회색 시작 · 도착 / 초록 주요 / 파랑 선택(보상 분기) / 노랑 이벤트 보상", size=11, halo=False)
        cy += 22
        self.text(x, cy + 4, "단 높이 · 경로 · 크기는 모두 크기 시안 전 제안값. 실제 흐름장 경로로 L-1을 다시 잰다(StageTransitionCheck).", size=11, color="#8a2a20", halo=False)


def main():
    src, dst = sys.argv[1], sys.argv[2]
    data = load(src)
    svg = Plan(data).draw()
    with open(dst, "w", encoding="utf-8") as f:
        f.write(svg)
    rt = data["routes"]
    walk = length(rt["main"]) + length(rt["main_after_gondola"])
    print(f"main walk {walk:.0f} m, mission {length(rt['mission']) + length(rt['mission_return']):.0f} m, "
          f"tower spur {length(rt['reward_tower']):.0f} m, workshop spur {length(rt['reward_workshop']):.0f} m")
    for name in ("main", "main_after_gondola"):
        print(f"L-1 {name}: {up_right_share(rt[name]):.0%}")


if __name__ == "__main__":
    main()
