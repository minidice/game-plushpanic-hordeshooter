"""Build 15 card FX sprites with Pillow and the Python standard library.

Reviewer command: python prototypes/ui-icons/tools/card_fx.py
Contract: doc/model-briefs/card-fx.md. Importing does not render or write.
All geometry is deterministic; no random sampling or external assets.
"""

from hashlib import sha256
from math import cos, pi, radians, sin
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter


ROOT = Path(__file__).resolve().parents[3]
OUTPUT = ROOT / "game/assets/effects/cards"
SCALE = 4
INK = "#1c2230"
RED = "#e5484d"
ORANGE = "#ff9b4a"
YELLOW = "#ffd23f"
GOLD = "#ffcf5a"
LIME = "#7dff9a"
BLUE = "#3f6fd8"
PURPLE = "#c08bff"
WHITE = "#f3f4f1"


class Canvas:
    """Logical pixel drawing; downsample once after all compositing."""

    def __init__(self, size=(128, 128), mask=False):
        self.size = size
        self.image = Image.new("L" if mask else "RGBA",
                               tuple(v * SCALE for v in size), 0)
        self.draw = ImageDraw.Draw(self.image)

    def ellipse(self, box, fill, outline=None, width=3):
        self.draw.ellipse(tuple(round(v * SCALE) for v in box), fill=fill,
                          outline=outline, width=round(width * SCALE))

    def line(self, points, fill, width=3):
        points = [(round(x * SCALE), round(y * SCALE)) for x, y in points]
        self.draw.line(points, fill=fill, width=round(width * SCALE), joint="curve")
        radius = width * SCALE / 2
        for x, y in points:
            self.draw.ellipse((round(x-radius), round(y-radius),
                               round(x+radius), round(y+radius)), fill=fill)

    def polygon(self, points, fill, outline=None, width=3):
        self.draw.polygon([(round(x*SCALE), round(y*SCALE)) for x, y in points],
                          fill=fill)
        if outline:
            self.line(points + points[:1], outline, width)

    def finish(self):
        return self.image.resize(self.size, Image.Resampling.LANCZOS)


def bezier_path(segments):
    points = []
    for a, b, c, d in segments:
        for step in range(33):
            t = step / 32
            u = 1-t
            points.append(tuple(u**3*a[i] + 3*u*u*t*b[i]
                                + 3*u*t*t*c[i] + t**3*d[i] for i in (0, 1)))
    return points


def white_mask(mask, size):
    """Neutral white RGB lets the game tint soft alpha without dark fringes."""
    alpha = mask.resize(size, Image.Resampling.LANCZOS)
    image = Image.new("RGBA", size, (255, 255, 255, 0))
    image.putalpha(alpha)
    return image


def glow(mask, radius, strength):
    blurred = mask.filter(ImageFilter.GaussianBlur(radius * SCALE))
    return blurred.point(lambda value: round(value * strength))


def fx_ring():
    c = Canvas((512, 512), mask=True)
    # 22 px rim, with a second, faint band only on the inward side.
    c.ellipse((25, 25, 487, 487), 255)
    c.ellipse((47, 47, 465, 465), 0)
    inner = Canvas(c.size, mask=True)
    inner.ellipse((43, 43, 469, 469), 100)
    inner.ellipse((60, 60, 452, 452), 0)
    mask = ImageChops.lighter(glow(c.image, 1.1, 1), glow(inner.image, 6, 1))
    return white_mask(mask, c.size)


def fx_cone():
    c = Canvas((512, 512), mask=True)
    apex = (256, 500)
    # A true circular sector, not a stretched triangle: 114 degrees total.
    # Radius 280 keeps both ends of its arc inside the 512 px canvas.
    arc = [(256 + 280*sin(radians(-57 + 114*i/160)),
            500 - 280*cos(radians(-57 + 114*i/160))) for i in range(161)]
    c.polygon([apex] + arc, 89)
    c.line([apex, arc[0]], 230, 4)
    c.line(arc, 230, 4)
    c.line([arc[-1], apex], 230, 4)
    # Fade the last 110 px toward the apex without changing the fan angle.
    fade = Image.new("L", c.image.size, 255)
    draw = ImageDraw.Draw(fade)
    for y in range(390*SCALE, 512*SCALE):
        alpha = round(255 * max(0, min(1, (500-y/SCALE)/110)))
        draw.line((0, y, 512*SCALE-1, y), fill=alpha)
    mask = ImageChops.multiply(c.image, fade)
    return white_mask(glow(mask, 0.7, 1), c.size)


def flame_shape():
    return bezier_path((
        [(65, 9), (76, 38), (101, 54), (101, 81)],
        [(101, 81), (102, 123), (27, 124), (27, 82)],
        [(27, 82), (25, 59), (56, 44), (65, 9)],
    ))


def fx_flame():
    c = Canvas(mask=True)
    c.polygon(flame_shape(), 180)
    core = Canvas(mask=True)
    core.polygon(bezier_path((
        [(65, 48), (72, 70), (85, 79), (84, 92)],
        [(84, 92), (83, 111), (45, 111), (45, 91)],
        [(45, 91), (46, 75), (60, 64), (65, 48)],
    )), 255)
    mask = ImageChops.lighter(glow(c.image, 3, 1), glow(core.image, 2, 1))
    return white_mask(mask, c.size)


def fx_steam():
    c = Canvas(mask=True)
    for box in ((18, 52, 68, 106), (29, 25, 83, 88),
                (61, 34, 109, 91), (55, 62, 105, 111)):
        c.ellipse(box, 255)
    return white_mask(glow(c.image, 5, 1), c.size)


def electric_sprite(size, branches):
    c = Canvas(size, mask=True)
    for branch in branches:
        c.line(branch, 255, 2.5)
    mask = ImageChops.lighter(c.image, glow(c.image, 3, 0.85))
    return white_mask(mask, size)


def fx_spark():
    branches = []
    for i, length in enumerate((47, 43, 49, 45, 44)):
        angle = -pi/2 + i*2*pi/5
        dx, dy = cos(angle), sin(angle)
        branches.append([(64, 64), (64+dx*17-dy*6, 64+dy*17+dx*6),
                         (64+dx*25+dy*5, 64+dy*25-dx*5),
                         (64+dx*length, 64+dy*length)])
    # The central point is part of the supersampled geometry above: add a
    # short cross to the branch list so the final image is still resized once.
    branches.extend(([(60, 64), (68, 64)], [(64, 60), (64, 68)]))
    return electric_sprite((128, 128), branches)


def fx_bolt():
    return electric_sprite((256, 64), [[(9, 32), (46, 29), (69, 13),
                           (88, 45), (117, 22), (140, 42), (169, 16),
                           (192, 37), (220, 28), (247, 32)]])


def star(c, x, y, radius, points=5, color=YELLOW):
    contour = []
    for i in range(points*2):
        angle = -pi/2 + i*pi/points
        r = radius if i % 2 == 0 else radius*0.46
        contour.append((x+r*cos(angle), y+r*sin(angle)))
    c.polygon(contour, color, INK, 3)


def fx_stars():
    c = Canvas()
    c.ellipse((13, 39, 115, 91), None, INK, 6)
    c.ellipse((13, 39, 115, 91), None, GOLD, 2.5)
    for x, y, r in ((27, 72, 17), (64, 39, 19), (103, 73, 17)):
        star(c, x, y, r)
    return c.finish()


def pole(positive):
    c = Canvas()
    c.ellipse((13, 13, 115, 115), RED if positive else BLUE, INK, 4)
    c.line([(41, 64), (87, 64)], WHITE, 12)
    if positive:
        c.line([(64, 41), (64, 87)], WHITE, 12)
    c.line([(34, 34), (43, 29)], WHITE, 3)
    return c.finish()


def fx_pole_plus():
    return pole(True)


def fx_pole_minus():
    return pole(False)


def fx_gather_mark():
    c = Canvas()
    # Wide bell at the top, narrow throat and handle below: points upward.
    c.polygon([(53, 85), (75, 85), (74, 111), (54, 111)], GOLD, INK)
    c.polygon([(35, 46), (93, 46), (75, 90), (53, 90)], GOLD, INK)
    c.ellipse((32, 38, 96, 57), GOLD, INK)
    c.ellipse((41, 42, 87, 51), INK)
    for y in (17, 29):
        c.line(bezier_path(([(41, y+5), (51, y-3), (77, y-3),
                              (87, y+5)],)), INK, 6)
        c.line(bezier_path(([(41, y+5), (51, y-3), (77, y-3),
                              (87, y+5)],)), GOLD, 2.5)
    c.line([(48, 61), (56, 80)], WHITE, 3)
    return c.finish()


def arrow(c, x, y, color, down=False):
    points = [(23, 0), (45, 25), (32, 25), (32, 53),
              (14, 53), (14, 25), (1, 25)]
    c.polygon([(x+px, y+(53-py if down else py)) for px, py in points],
              color, INK)


def bullet(c, x, y):
    c.polygon([(x, y+17), (x+5, y+5), (x+12, y), (x+19, y+5),
               (x+24, y+17), (x+24, y+51), (x, y+51)], GOLD, INK)
    c.line([(x+2, y+18), (x+22, y+18)], INK, 3)
    c.line([(x+2, y+43), (x+22, y+43)], INK, 3)
    c.line([(x+7, y+23), (x+7, y+34)], WHITE, 3)


def fx_gather_damage():
    c = Canvas()
    arrow(c, 19, 24, ORANGE)
    bullet(c, 79, 57)
    return c.finish()


def fx_gather_crit():
    c = Canvas()
    star(c, 64, 64, 49, points=8)
    c.line([(58, 48), (53, 62)], WHITE, 4)
    return c.finish()


def fx_gather_fire():
    c = Canvas()
    c.polygon([(65, 10), (76, 45), (91, 34), (99, 61), (108, 82),
               (96, 106), (67, 117), (36, 108), (21, 85), (29, 58),
               (40, 69), (47, 35), (56, 47)], RED, INK)
    c.polygon([(65, 39), (84, 72), (89, 95), (65, 109),
               (41, 96), (44, 77)], ORANGE)
    bullet(c, 53, 60)
    return c.finish()


def flask(c, color):
    # Compact round laboratory flask beside the two arrows.
    c.polygon([(84, 63), (100, 63), (100, 78), (110, 89),
               (112, 102), (106, 113), (78, 113), (72, 102),
               (74, 89), (84, 78)], WHITE, INK)
    c.polygon([(78, 94), (106, 94), (107, 102), (102, 108),
               (82, 108), (77, 102)], color)
    c.line([(82, 63), (102, 63)], INK, 4)
    c.ellipse((87, 84, 92, 89), color)
    c.line([(80, 96), (80, 101)], WHITE, 2)


def mad(down):
    c = Canvas()
    color = LIME if down else PURPLE
    arrow(c, 17, 17, color, down)
    arrow(c, 31, 54, color, down)
    flask(c, color)
    return c.finish()


def fx_mad_big():
    return mad(False)


def fx_mad_small():
    return mad(True)


BUILDERS = (
    ("fx_ring", fx_ring, (512, 512)),
    ("fx_cone", fx_cone, (512, 512)),
    ("fx_flame", fx_flame, (128, 128)),
    ("fx_steam", fx_steam, (128, 128)),
    ("fx_spark", fx_spark, (128, 128)),
    ("fx_bolt", fx_bolt, (256, 64)),
    ("fx_stars", fx_stars, (128, 128)),
    ("fx_pole_plus", fx_pole_plus, (128, 128)),
    ("fx_pole_minus", fx_pole_minus, (128, 128)),
    ("fx_gather_mark", fx_gather_mark, (128, 128)),
    ("fx_gather_damage", fx_gather_damage, (128, 128)),
    ("fx_gather_crit", fx_gather_crit, (128, 128)),
    ("fx_gather_fire", fx_gather_fire, (128, 128)),
    ("fx_mad_big", fx_mad_big, (128, 128)),
    ("fx_mad_small", fx_mad_small, (128, 128)),
)


def main():
    assert len(BUILDERS) == 15, "expected exactly 15 card FX sprites"
    assert len({name for name, _, _ in BUILDERS}) == 15, "duplicate filenames"
    assets = []
    hashes = set()
    # Validate everything before writing any PNG. Semi-transparent FX such
    # as the cone intentionally have no alpha=255 pixels: require alpha>0.
    for name, builder, size in BUILDERS:
        image = builder()
        assert image.size == size, f"{name}: expected {size}"
        assert image.mode == "RGBA", f"{name}: expected RGBA"
        width, height = size
        corners = [(0, 0), (width-1, 0)]
        if name != "fx_cone":
            corners += [(0, height-1), (width-1, height-1)]
        for corner in corners:
            assert image.getpixel(corner)[3] == 0, f"{name}: opaque corner"
        assert image.getchannel("A").getbbox() is not None, f"{name}: empty sprite"
        digest = sha256(image.tobytes()).digest()
        assert digest not in hashes, f"{name}: duplicate pixel hash"
        hashes.add(digest)
        assets.append((name, image))
    assert len(hashes) == 15, "expected 15 distinct pixel hashes"
    assert assets[0][1].getpixel((256, 256))[3] == 0, "ring: center must be clear"

    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, image in assets:
        path = OUTPUT / f"{name}.png"
        image.save(path, format="PNG")
        print(f"CARD_FX_READY {path.relative_to(ROOT).as_posix()}")
    print("CARD_FX_BUILT 15")


if __name__ == "__main__":
    main()
