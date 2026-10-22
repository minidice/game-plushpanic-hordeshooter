"""Build 16 flat card icons with Pillow and the Python standard library.

Reviewer command: python prototypes/ui-icons/tools/card_icons.py
Contract: doc/model-briefs/card-icons.md. Importing does not render or write.
"""

from hashlib import sha256
from math import hypot
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[3]
OUTPUT = ROOT / "game/assets/icons/cards"
SIZE = 256
SCALE = 4
INK = "#1c2230"
RED = "#e5484d"
ORANGE = "#ff9b4a"
YELLOW = "#ffd23f"
GOLD = "#ffcf5a"
LIME = "#7dff9a"
GREEN = "#4caf6a"
SKY = "#6bb8ff"
BLUE = "#3f6fd8"
PURPLE = "#c08bff"
PINK = "#ff9ccc"
WHITE = "#f3f4f1"
GRAY = "#8c96a8"


class Canvas:
    """Draw in logical pixels; only the final image is downsampled."""

    def __init__(self):
        self.image = Image.new("RGBA", (SIZE * SCALE, SIZE * SCALE))
        self.draw = ImageDraw.Draw(self.image)

    def ellipse(self, box, fill, outline=INK, width=5):
        self.draw.ellipse(tuple(round(v * SCALE) for v in box), fill=fill,
                          outline=outline, width=round(width * SCALE))

    def rect(self, box, fill, radius=10, outline=INK, width=5):
        self.draw.rounded_rectangle(
            tuple(round(v * SCALE) for v in box), radius=round(radius * SCALE),
            fill=fill, outline=outline, width=round(width * SCALE))

    def line(self, points, fill=INK, width=5):
        points = [(round(x * SCALE), round(y * SCALE)) for x, y in points]
        self.draw.line(points, fill=fill, width=round(width * SCALE), joint="curve")
        # Round caps also soften polygon corners at card-display size.
        r = width * SCALE / 2
        for x, y in points:
            self.draw.ellipse((round(x - r), round(y - r),
                               round(x + r), round(y + r)), fill=fill)

    def polygon(self, points, fill, outline=INK, width=5):
        self.draw.polygon([(round(x * SCALE), round(y * SCALE))
                           for x, y in points], fill=fill)
        if outline:
            self.line(points + points[:1], outline, width)

    def curve(self, points, fill=INK, width=5):
        """One cubic Bezier, sampled deterministically (no extra packages)."""
        a, b, c, d = points
        samples = []
        for step in range(41):
            t = step / 40
            u = 1 - t
            samples.append(tuple(u**3 * a[i] + 3 * u*u*t * b[i]
                                 + 3 * u*t*t * c[i] + t**3 * d[i]
                                 for i in (0, 1)))
        self.line(samples, fill, width)

    def finish(self):
        return self.image.resize((SIZE, SIZE), Image.Resampling.LANCZOS)


def star(c, x, y, r=13, color=YELLOW):
    c.polygon([(x, y-r), (x+4, y-4), (x+r, y), (x+4, y+4),
               (x, y+r), (x-4, y+4), (x-r, y), (x-4, y-4)], color, width=4)


def balloon(c, x, y, w, h, color):
    c.ellipse((x, y, x+w, y+h), color)
    c.polygon([(x+w/2, y+h-2), (x+w/2-8, y+h+10),
               (x+w/2+8, y+h+10)], color, width=4)
    c.curve([(x+w*.24, y+h*.39), (x+w*.21, y+h*.26),
             (x+w*.29, y+h*.18), (x+w*.40, y+h*.16)], WHITE, 5)


def bullet(c, x, y, w=42, h=76):
    c.rect((x, y+h*.35, x+w, y+h), GOLD, radius=6)
    c.polygon([(x, y+h*.44), (x+2, y+h*.25), (x+w*.26, y+6),
               (x+w/2, y), (x+w*.74, y+6), (x+w-2, y+h*.25),
               (x+w, y+h*.44)], ORANGE)
    c.line([(x+4, y+h*.80), (x+w-4, y+h*.80)])
    c.line([(x+w*.27, y+h*.50), (x+w*.27, y+h*.65)], WHITE, 4)


def rocket(c, x, y, w=62, h=148):
    # Soft toy fins and a single flat flame, all safely inside this box.
    c.polygon([(x+w*.36, y+h*.78), (x+w*.50, y+h),
               (x+w*.64, y+h*.78)], ORANGE, width=4)
    c.polygon([(x+w*.24, y+h*.53), (x, y+h*.83),
               (x+w*.34, y+h*.76)], RED)
    c.polygon([(x+w*.76, y+h*.53), (x+w, y+h*.83),
               (x+w*.66, y+h*.76)], RED)
    c.rect((x+w*.22, y+h*.19, x+w*.78, y+h*.81), WHITE, radius=12)
    c.polygon([(x+w*.22, y+h*.26), (x+w*.29, y+h*.12),
               (x+w*.50, y), (x+w*.71, y+h*.12),
               (x+w*.78, y+h*.26)], RED)
    c.ellipse((x+w*.36, y+h*.37, x+w*.64, y+h*.37+w*.28), SKY, width=4)
    c.line([(x+w*.32, y+h*.69), (x+w*.68, y+h*.69)], GRAY, 4)


def card_balloon(c):
    c.curve([(101, 173), (126, 190), (77, 206), (105, 231)], WHITE)
    balloon(c, 35, 27, 125, 146, PINK)
    bullet(c, 175, 131, 44, 86)


def card_velcro(c):
    c.rect((31, 41, 156, 161), GREEN, radius=22)
    c.line([(49, 65), (49, 119)], LIME, 5)
    c.rect((94, 96, 224, 217), LIME, radius=22)
    # Paired open loops make the surface read as hook-and-loop fabric.
    for x in (117, 150, 183):
        for y in (119, 149, 179):
            c.ellipse((x, y, x+15, y+17), None, GREEN, 4)
    c.line([(102, 88), (158, 88), (165, 96)], WHITE, 5)


def card_stuffing_burst(c):
    # Seven detached tufts, plus a continuous scalloped central puff.
    for x, y, r in ((55, 57, 15), (123, 34, 14), (198, 59, 16),
                    (221, 132, 13), (190, 207, 17),
                    (108, 222, 13), (40, 171, 15)):
        c.ellipse((x-r, y-r, x+r, y+r), WHITE, width=4)
    lobes = ((73, 85, 133, 147), (108, 67, 163, 132),
             (135, 90, 189, 153), (126, 128, 183, 185),
             (72, 126, 137, 189), (58, 107, 115, 166))
    for box in lobes:
        c.ellipse(box, WHITE)
    # Refill the interiors to remove overlapping outlines.
    for x0, y0, x1, y1 in lobes:
        c.ellipse((x0+5, y0+5, x1-5, y1-5), WHITE, None)
    c.ellipse((83, 99, 163, 165), WHITE, None)
    c.curve([(91, 128), (87, 113), (100, 105), (111, 110)], GRAY, 4)


def card_billiards(c):
    c.ellipse((22, 56, 134, 168), YELLOW)
    c.ellipse((123, 112, 235, 224), RED)
    c.ellipse((54, 89, 103, 138), WHITE, width=4)
    c.ellipse((155, 144, 204, 193), WHITE, width=4)
    # No lettering: one and three ink pips identify the two balls.
    c.ellipse((73, 108, 84, 119), INK, None)
    for x, y in ((168, 157), (180, 169), (192, 181)):
        c.ellipse((x-4, y-4, x+4, y+4), INK, None)
    c.line([(48, 83), (61, 77)], WHITE, 5)
    c.line([(150, 140), (159, 135)], WHITE, 5)
    c.line([(142, 91), (155, 70)], WHITE, 5)
    c.line([(154, 105), (178, 96)], WHITE, 5)
    c.line([(117, 183), (105, 201)], WHITE, 5)


def card_tap_dance(c):
    # INK is the black shoe; a gray edge keeps it visible on dark cards.
    c.polygon([(34, 125), (53, 108), (92, 116), (111, 146),
               (151, 151), (184, 162), (207, 181), (207, 204),
               (177, 216), (88, 214), (34, 198)], INK, GRAY)
    c.rect((39, 191, 92, 217), GRAY, radius=7)
    c.rect((131, 198, 209, 217), GRAY, radius=7)
    c.line([(48, 205), (79, 205)], WHITE, 4)
    c.line([(145, 207), (194, 207)], WHITE, 4)
    c.curve([(114, 166), (146, 163), (171, 172), (180, 181)], WHITE, 5)
    for x, y in ((86, 137), (95, 150)):
        c.line([(x-9, y+3), (x+9, y-3)], GRAY, 4)
    for x, y in ((142, 38), (201, 64)):
        c.line([(x, y+49), (x, y), (x+20, y+7)], INK, 11)
        c.line([(x, y+49), (x, y), (x+20, y+7)], GOLD, 5)
        c.ellipse((x-20, y+38, x+5, y+57), GOLD, width=4)


def card_bouncing_rounds(c):
    # Exactly three dashed segments = two changes of direction.
    points = [(31, 206), (81, 65), (146, 184), (198, 99)]
    for a, b in zip(points, points[1:]):
        length = hypot(b[0]-a[0], b[1]-a[1])
        for start in range(0, int(length)-8, 24):
            end = min(start+12, length)
            segment = [(a[0]+(b[0]-a[0])*t/length,
                        a[1]+(b[1]-a[1])*t/length) for t in (start, end)]
            c.line(segment, INK, 12)
            c.line(segment, WHITE, 6)
    star(c, 81, 65, 20)
    star(c, 146, 184, 20)
    bullet(c, 173, 23, 58, 83)


def card_honk_charge(c):
    c.ellipse((23, 133, 103, 213), RED)
    c.rect((87, 148, 118, 178), GOLD, radius=9)
    c.polygon([(110, 146), (143, 136), (182, 99),
               (195, 173), (151, 166), (114, 177)], GOLD)
    c.ellipse((169, 97, 206, 181), GOLD)
    c.ellipse((180, 113, 197, 165), INK, None)
    c.curve([(42, 161), (40, 149), (52, 144), (61, 145)], WHITE, 5)
    c.polygon([(134, 26), (101, 76), (126, 76),
               (111, 111), (164, 58), (138, 58), (153, 26)], YELLOW)


def card_destiny_gun(c):
    c.rect((37, 104, 176, 231), WHITE, radius=24)
    for x, y in ((69, 135), (144, 135), (106, 167),
                 (69, 201), (144, 201)):
        c.ellipse((x-9, y-9, x+9, y+9), INK, None)
    rocket(c, 151, 23, 68, 113)


def card_helium_party(c):
    for x, y in ((61, 139), (128, 116), (195, 146)):
        c.curve([(x, y), (x-10, 183), (143, 202), (128, 229)], WHITE, 4)
    balloon(c, 88, 21, 80, 97, YELLOW)
    balloon(c, 24, 56, 79, 99, SKY)
    balloon(c, 153, 62, 79, 100, PINK)
    c.line([(119, 218), (138, 218)], INK, 5)


def card_last_shot(c):
    c.polygon([(40, 52), (102, 52), (104, 127), (90, 213),
               (29, 204), (41, 122)], GRAY)
    c.rect((52, 62, 89, 90), INK, radius=5)
    for y in (110, 135, 160, 185):
        c.line([(53, y), (80, y+3)], INK, 5)
    # The orange flame surrounds the tip without a glow or gradient.
    c.polygon([(161, 100), (139, 75), (156, 79), (163, 37),
               (181, 59), (195, 25), (208, 60), (223, 47),
               (216, 88), (198, 108)], ORANGE)
    bullet(c, 157, 89, 54, 126)
    for a, b in (((129, 113), (117, 108)), ((225, 126), (237, 123)),
                 ((129, 182), (119, 189))):
        c.line([a, b], WHITE, 5)


def card_glass_cannon(c):
    c.polygon([(70, 168), (162, 157), (197, 204), (53, 204)], BLUE)
    c.polygon([(47, 108), (173, 62), (199, 126), (76, 172),
               (51, 157), (39, 129)], SKY)
    c.ellipse((162, 55, 218, 133), SKY)
    c.ellipse((177, 70, 205, 117), BLUE, width=4)
    c.line([(62, 113), (154, 80)], WHITE, 6)
    c.line([(125, 98), (114, 122), (133, 132), (120, 153)], INK, 4)
    c.line([(114, 122), (96, 119)], INK, 4)
    c.ellipse((71, 158, 140, 227), SKY)
    c.ellipse((94, 181, 117, 204), BLUE, width=4)
    c.curve([(83, 185), (83, 176), (92, 170), (100, 170)], WHITE, 4)


def card_double_special(c):
    rocket(c, 34, 34, 80, 190)
    rocket(c, 142, 34, 80, 190)


def card_mystery(c):
    c.rect((47, 130, 210, 227), PURPLE, radius=13)
    c.rect((115, 132, 143, 226), PINK, radius=3)
    c.rect((36, 113, 221, 143), PURPLE, radius=9)
    c.rect((115, 114, 143, 142), PINK, radius=3)
    # A thick curling ribbon, with a separate loop as its dot. No font.
    curves = ([(91, 57), (88, 20), (170, 19), (173, 54)],
              [(173, 54), (178, 78), (130, 76), (129, 91)])
    for curve in curves:
        c.curve(curve, INK, 21)
        c.curve(curve, PINK, 11)
    c.ellipse((121, 99, 137, 111), PINK, width=4)
    c.line([(57, 157), (57, 193)], WHITE, 5)


def card_r_damage(c):
    # Three overlapping arrows at increasing heights, all pointing up.
    for x, top, bottom, color in ((27, 98, 221, ORANGE),
                                 (90, 61, 211, GOLD),
                                 (151, 25, 199, ORANGE)):
        c.polygon([(x+37, top), (x+72, top+43), (x+53, top+43),
                   (x+53, bottom), (x+21, bottom), (x+21, top+43),
                   (x, top+43)], color)
    c.line([(181, 113), (181, 168)], WHITE, 5)


def cross(c, x, y, size, color):
    arm = size / 3
    c.polygon([(x+arm, y), (x+2*arm, y), (x+2*arm, y+arm),
               (x+size, y+arm), (x+size, y+2*arm),
               (x+2*arm, y+2*arm), (x+2*arm, y+size),
               (x+arm, y+size), (x+arm, y+2*arm), (x, y+2*arm),
               (x, y+arm), (x+arm, y+arm)], color, outline=None)


def card_r_heal(c):
    # Sample a rounded heart contour using the same cubic construction.
    segments = (
        [(128, 78), (73, 12), (16, 56), (28, 110)],
        [(28, 110), (34, 150), (91, 198), (128, 226)],
        [(128, 226), (166, 198), (222, 150), (228, 110)],
        [(228, 110), (240, 56), (183, 12), (128, 78)],
    )
    points = []
    for a, b, cc, d in segments:
        for step in range(31):
            t = step / 30
            u = 1-t
            points.append(tuple(u**3*a[i] + 3*u*u*t*b[i]
                                + 3*u*t*t*cc[i] + t**3*d[i] for i in (0, 1)))
    c.polygon(points, RED)
    cross(c, 93, 103, 70, WHITE)
    c.curve([(49, 94), (47, 77), (59, 65), (73, 65)], WHITE, 5)


def card_r_medkit(c):
    c.rect((88, 30, 168, 86), WHITE, radius=14)
    c.rect((103, 45, 153, 78), INK, radius=6, outline=None)
    c.rect((26, 72, 230, 216), WHITE, radius=24)
    c.line([(31, 108), (225, 108)], GRAY, 5)
    c.rect((48, 94, 65, 121), GRAY, radius=5, width=4)
    c.rect((191, 94, 208, 121), GRAY, radius=5, width=4)
    cross(c, 96, 126, 64, RED)


BUILDERS = (
    ("card_balloon", card_balloon),
    ("card_velcro", card_velcro),
    ("card_stuffing_burst", card_stuffing_burst),
    ("card_billiards", card_billiards),
    ("card_tap_dance", card_tap_dance),
    ("card_bouncing_rounds", card_bouncing_rounds),
    ("card_honk_charge", card_honk_charge),
    ("card_destiny_gun", card_destiny_gun),
    ("card_helium_party", card_helium_party),
    ("card_last_shot", card_last_shot),
    ("card_glass_cannon", card_glass_cannon),
    ("card_double_special", card_double_special),
    ("card_mystery", card_mystery),
    ("card_r_damage", card_r_damage),
    ("card_r_heal", card_r_heal),
    ("card_r_medkit", card_r_medkit),
)


def main():
    assert len(BUILDERS) == 16, "expected exactly 16 card icons"
    assert len({name for name, _ in BUILDERS}) == 16, "duplicate filenames"
    assets = []
    hashes = set()
    # Validate the complete set before creating the output directory or PNGs.
    for name, builder in BUILDERS:
        canvas = Canvas()
        builder(canvas)
        image = canvas.finish()
        assert image.size == (256, 256), f"{name}: expected 256x256"
        assert image.mode == "RGBA", f"{name}: expected RGBA"
        for corner in ((0, 0), (255, 0), (0, 255), (255, 255)):
            assert image.getpixel(corner)[3] == 0, f"{name}: opaque corner"
        opaque = image.getchannel("A").histogram()[255] / (256 * 256)
        assert 0.08 <= opaque <= 0.60, f"{name}: opaque coverage {opaque:.2%}"
        digest = sha256(image.tobytes()).digest()
        assert digest not in hashes, f"{name}: duplicate pixel hash"
        hashes.add(digest)
        assets.append((name, image))
    assert len(hashes) == 16, "expected 16 distinct pixel hashes"

    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, image in assets:
        path = OUTPUT / f"{name}.png"
        image.save(path, format="PNG")
        print(f"CARD_ICON_READY {path.relative_to(ROOT).as_posix()}")
    print("CARD_ICONS_BUILT 16")


if __name__ == "__main__":
    main()
