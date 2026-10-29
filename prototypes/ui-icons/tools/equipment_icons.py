"""Build nine toy-like equipment icons with Pillow and the standard library.

Run from any directory: python prototypes/ui-icons/tools/equipment_icons.py
Contract: doc/model-briefs/equipment-icons.md. No game effects or text are drawn.
"""

from itertools import combinations
from math import cos, dist, pi, sin
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[3]
OUTPUT = ROOT / "game/assets/icons/equipment"
SIZE = 256
SCALE = 4
INK = (27, 34, 48, 179)


def rgb(value):
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def tint(color, amount):
    target = 255 if amount > 0 else 0
    return tuple(round(c + (target - c) * abs(amount)) for c in color)


def curve(start, *segments):
    """Sample cubic Beziers in logical pixels; pairs are straight segments."""
    points = [start]
    for segment in segments:
        if len(segment) == 2:
            points.append(segment)
            continue
        x0, y0 = points[-1]
        x1, y1, x2, y2, x3, y3 = segment
        for step in range(1, 25):
            t = step / 24
            u = 1 - t
            points.append((u**3*x0 + 3*u*u*t*x1 + 3*u*t*t*x2 + t**3*x3,
                           u**3*y0 + 3*u*u*t*y1 + 3*u*t*t*y2 + t**3*y3))
    return points


class Canvas:
    """Shared 4 px contours and three broad upper-left lighting planes."""

    def __init__(self):
        self.image = Image.new("RGBA", (SIZE * SCALE, SIZE * SCALE))

    def shape(self, points, color, shaded=True, outline=True):
        points = [(round(x * SCALE), round(y * SCALE)) for x, y in points]
        mask = Image.new("L", self.image.size)
        ImageDraw.Draw(mask).polygon(points, fill=255)
        color = rgb(color)
        layer = Image.new("RGBA", self.image.size, (*color, 255))
        if shaded:
            left, top, right, bottom = mask.getbbox()
            width, height = right - left, bottom - top
            draw = ImageDraw.Draw(layer)
            draw.polygon([(left, top), (left + width * .79, top),
                          (left, top + height * .85)], fill=(*tint(color, .22), 255))
            draw.polygon([(right, top + height * .35), (right, bottom),
                          (left + width * .30, bottom)], fill=(*tint(color, -.20), 255))
        layer.putalpha(mask)
        self.image.alpha_composite(layer)
        if outline:
            edge = Image.new("RGBA", self.image.size)
            ImageDraw.Draw(edge).line(points + points[:1], fill=INK,
                                      width=4 * SCALE, joint="curve")
            self.image.alpha_composite(edge)

    def ellipse(self, box, color, shaded=True, outline=True):
        x0, y0, x1, y1 = box
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        self.shape([(cx + (x1 - x0) / 2 * cos(i * pi / 48),
                     cy + (y1 - y0) / 2 * sin(i * pi / 48))
                    for i in range(96)], color, shaded, outline)

    def rounded(self, box, radius, color, shaded=True, outline=True):
        x0, y0, x1, y1 = box
        points = []
        for cx, cy, start in ((x1-radius, y0+radius, -pi/2),
                              (x1-radius, y1-radius, 0),
                              (x0+radius, y1-radius, pi/2),
                              (x0+radius, y0+radius, pi)):
            points.extend((cx + radius*cos(start + i*pi/24),
                           cy + radius*sin(start + i*pi/24)) for i in range(13))
        self.shape(points, color, shaded, outline)

    def line(self, points, color, width=3):
        ImageDraw.Draw(self.image).line(
            [(round(x*SCALE), round(y*SCALE)) for x, y in points],
            fill=(*rgb(color), 255), width=round(width*SCALE), joint="curve")

    def finish(self):
        return self.image.resize((SIZE, SIZE), Image.Resampling.LANCZOS)


def keen_cap():
    c = Canvas()
    c.shape(curve((40, 158), (33, 96, 62, 46, 120, 43),
                  (171, 40, 198, 86, 195, 153),
                  (164, 177, 88, 181, 40, 158)), "f08a3c")
    c.shape(curve((47, 151), (85, 159, 150, 151, 182, 141),
                  (231, 177), (181, 211),
                  (121, 205, 75, 187, 47, 165)), "f08a3c")
    c.line(curve((54, 156), (101, 170, 157, 157, 181, 149)), "ffc284", 4)
    c.line(curve((116, 51), (144, 70, 154, 103, 152, 145)), "ba622c", 3)
    c.ellipse((108, 37, 130, 50), "ffb367")
    return c.finish()


def scope_goggles():
    c = Canvas()
    # Visible curved rear strap establishes the same slightly elevated view.
    c.shape(curve((30, 126), (31, 46, 211, 43, 226, 126),
                  (207, 140), (190, 81, 66, 80, 49, 143)), "5b6472")
    c.rounded((22, 110, 51, 171), 9, "5b6472")
    c.rounded((205, 110, 234, 171), 9, "5b6472")
    c.shape(curve((109, 122), (122, 108, 137, 108, 149, 122),
                  (148, 151), (135, 140, 123, 140, 110, 152)), "5b6472")
    for box in ((37, 99, 121, 193), (137, 97, 220, 189)):
        c.rounded(box, 24, "5b6472")
        x0, y0, x1, y1 = box
        c.rounded((x0+8, y0+8, x1-8, y1-8), 18, "47c6d6")
        c.line([(x0+17, y0+32), (x0+31, y0+18), (x0+46, y0+18)], "c4f4ef", 5)
    # Just one small reticle, deliberately legible at HUD scale.
    c.line([(177, 130), (177, 162)], "234b61", 3)
    c.line([(161, 146), (193, 146)], "234b61", 3)
    return c.finish()


def lucky_cap():
    c = Canvas()
    c.shape(curve((43, 176), (39, 102, 67, 63, 126, 61),
                  (183, 60, 210, 110, 211, 180),
                  (168, 203, 84, 205, 43, 176)), "5cb85c")
    c.ellipse((102, 28, 151, 75), "7bd575")
    c.rounded((37, 160, 218, 220), 19, "5cb85c")
    c.line([(53, 171), (185, 171)], "9fe28e", 4)
    c.rounded((98, 169, 153, 211), 12, "d3e8a0", shaded=False)
    for x, y in ((117, 183), (133, 183), (117, 196), (133, 196)):
        c.ellipse((x-9, y-8, x+9, y+8), "287341", False, False)
    c.line([(126, 192), (131, 204)], "287341", 3)
    return c.finish()


def hunter_hat():
    c = Canvas()
    for box in ((29, 123, 80, 223), (177, 121, 225, 218)):
        c.rounded(box, 19, "9c352f")
        x0, y0, x1, y1 = box
        c.rounded((x0+8, y0+17, x1-8, y1-10), 13, "b28c68")
    c.shape(curve((40, 154), (36, 83, 63, 40, 126, 39),
                  (184, 38, 211, 83, 210, 154),
                  (164, 181, 84, 181, 40, 154)), "c8473f")
    # Large two-tone checks, kept inside the rounded crown: no fine plaid.
    for box in ((83, 55, 126, 96), (126, 96, 171, 137), (43, 98, 83, 137)):
        c.rounded(box, 3, "36292d", False, False)
    c.shape(curve((41, 143), (78, 130, 169, 130, 209, 143),
                  (216, 170), (180, 191, 73, 193, 36, 172)), "c8473f")
    c.line(curve((47, 149), (88, 138, 164, 139, 199, 149)), "eb8572", 5)
    return c.finish()


def focus_band():
    c = Canvas()
    # Broad folded tails have a small fork at the end, not a ribbon bow.
    c.shape(curve((163, 121), (184, 143, 215, 145, 233, 171),
                  (211, 174), (216, 201),
                  (180, 192, 158, 167, 147, 144)), "d9d6cc")
    c.shape(curve((151, 137), (172, 161, 169, 197, 191, 228),
                  (162, 217), (145, 228),
                  (129, 188, 126, 163, 133, 138)), "f2efe6")
    # Rear loop and front band leave the head opening transparent.
    c.shape(curve((29, 105), (41, 37, 183, 34, 205, 100),
                  (180, 112), (161, 73, 72, 72, 54, 116)), "d9d6cc")
    c.shape(curve((26, 94), (76, 110, 155, 103, 200, 87),
                  (211, 142), (168, 171, 73, 175, 27, 149),
                  (26, 94)), "f2efe6")
    c.ellipse((90, 112, 133, 153), "d9473b", False, False)
    c.line(curve((38, 109), (83, 120, 146, 113, 186, 102)), "fffdf5", 4)
    c.ellipse((187, 120, 217, 151), "f2efe6")
    return c.finish()


def vest_body(c, color):
    # Neck and arm openings are real silhouette cutouts, not painted skin.
    c.shape(curve((74, 32), (103, 32),
                  (104, 75, 151, 75, 153, 32), (183, 32), (197, 80),
                  (196, 97, 205, 110, 220, 113), (219, 213),
                  (218, 223, 203, 225, 193, 225), (63, 225),
                  (48, 225, 37, 221, 37, 209), (36, 113),
                  (52, 106, 59, 93, 58, 78), (74, 32)), color)


def steady_vest():
    c = Canvas()
    vest_body(c, "7d8a4e")
    c.rounded((66, 38, 93, 100), 8, "9ca963")
    c.rounded((163, 38, 190, 100), 8, "6a763e")
    c.line([(128, 80), (128, 215)], "485334", 5)
    for x in (53, 143):
        for y in (109, 164):
            c.rounded((x, y, x+60, y+46), 8, "8e9a55")
            c.rounded((x, y, x+60, y+15), 5, "abb66c")
            c.rounded((x+25, y+12, x+35, y+23), 3, "576238", False)
    return c.finish()


def marksman_vest():
    c = Canvas()
    vest_body(c, "3e5c8a")
    for box in ((54, 30, 99, 95), (156, 30, 201, 95)):
        c.rounded(box, 12, "597aab")
        x0, y0, x1, y1 = box
        c.line([(x0+10, y0+13), (x1-10, y0+13)], "90afd6", 4)
    c.line([(128, 80), (128, 218)], "243d62", 5)
    c.rounded((53, 163, 111, 211), 9, "496c9c")
    c.rounded((145, 163, 203, 211), 9, "496c9c")
    c.ellipse((153, 105, 193, 145), "d8d8c7", False)
    c.ellipse((162, 114, 184, 136), "c74b45", False, False)
    c.ellipse((169, 121, 177, 129), "f3e8ce", False, False)
    return c.finish()


def brace_vest():
    c = Canvas()
    vest_body(c, "7b8794")
    c.rounded((65, 31, 98, 92), 11, "a0acb6")
    c.rounded((158, 31, 191, 92), 11, "8a96a3")
    for y in (91, 133, 175):
        c.rounded((46, y, 210, y+39), 13, "7b8794")
        c.line([(61, y+9), (188, y+9)], "b1bdc7", 4)
    c.rounded((114, 104, 142, 202), 7, "64717e")
    c.rounded((118, 141, 138, 164), 4, "b6bdc1")
    return c.finish()


def shoulder_harness():
    c = Canvas()
    # Open X silhouette; wide leather straps keep coverage above 20%.
    c.shape(curve((49, 37), (78, 30), (197, 197),
                  (210, 218), (176, 227), (46, 65),
                  (39, 48, 42, 41, 49, 37)), "9a6a3e")
    c.shape(curve((178, 30), (209, 42),
                  (216, 46, 216, 58, 208, 70),
                  (79, 225), (43, 213), (178, 30)), "9a6a3e")
    c.rounded((34, 191, 222, 226), 10, "9a6a3e")
    c.line([(47, 199), (205, 199)], "ce9a63", 4)
    # The thick buttstock pad is on just the viewer's right shoulder.
    c.shape(curve((169, 30), (184, 23, 205, 30, 212, 43),
                  (223, 91), (222, 109, 206, 121, 191, 116),
                  (167, 101), (156, 90, 161, 49, 169, 30)), "704625")
    c.line(curve((176, 43), (174, 59, 173, 79, 180, 92)), "b08351", 6)
    c.rounded((111, 119, 146, 152), 7, "c39b60")
    c.rounded((120, 128, 137, 143), 3, "6d482b", False)
    c.rounded((110, 192, 149, 224), 5, "c39b60")
    c.rounded((120, 200, 139, 216), 3, "6d482b", False)
    return c.finish()


def main():
    builders = (keen_cap, scope_goggles, lucky_cap, hunter_hat, focus_band,
                steady_vest, marksman_vest, brace_vest, shoulder_harness)
    assets = [(build.__name__, build()) for build in builders]

    # Validate every image before writing any PNG. py_compile does not run this.
    assert len(assets) == 9 and len({name for name, _ in assets}) == 9
    means = {}
    for name, image in assets:
        assert image.size == (256, 256), f"{name}: expected 256x256"
        assert image.mode == "RGBA", f"{name}: expected RGBA"
        for corner in ((0, 0), (255, 0), (0, 255), (255, 255)):
            assert image.getpixel(corner)[3] == 0, f"{name}: opaque corner"
        # Strictly opaque pixels only: antialiased borders are not counted.
        opaque = [pixel[:3] for pixel in image.getdata() if pixel[3] == 255]
        coverage = len(opaque) / (256 * 256)
        assert .20 <= coverage <= .65, f"{name}: opaque coverage {coverage:.3%}"
        means[name] = tuple(sum(pixel[k] for pixel in opaque) / len(opaque)
                            for k in range(3))
    for first, second in combinations(means, 2):
        distance = dist(means[first], means[second])
        assert distance >= 20, f"{first}/{second}: RGB distance {distance:.2f} < 20"

    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, image in assets:
        path = OUTPUT / f"{name}.png"
        image.save(path, format="PNG")
        print(f"EQUIPMENT_ICON_READY {path.relative_to(ROOT).as_posix()} 256x256")
    print("EQUIPMENT_ICONS_BUILT 9")


if __name__ == "__main__":
    main()
