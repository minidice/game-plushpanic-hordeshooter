"""Draw eight flat play-setup illustrations using Pillow only.

Contract: doc/model-briefs/play-setup-cards.md.
Reviewer: python prototypes/ui-icons/tools/play_setup_cards.py
Importing this module does not draw, create directories, or write files.
"""

from hashlib import sha256
from pathlib import Path

from PIL import Image, ImageDraw, ImageStat


ROOT = Path(__file__).resolve().parents[3]
OUTPUT = ROOT / "game/assets/ui/play-setup"
SIZE = (640, 400)
SCALE = 2
INK = "#1c2230"
NIGHT = "#0d1114"
NAVY = "#1a2433"
BLUE = "#2b3a52"
SKY = "#b8e5ff"
YELLOW = "#ffcf5c"
ORANGE = "#ff9b4a"
RED = "#ff5b4f"
PURPLE = "#7a5ea8"
SAGE = "#7f8f6a"
GREEN = "#9fb36a"
BROWN = "#8a6a46"
WHITE = "#f3f4f1"
GRAY = "#8c96a8"


def rgb(color):
    return tuple(int(color[i:i + 2], 16) for i in (1, 3, 5))


def mix(a, b=NIGHT, amount=0.5):
    """Flat palette mixtures; no random colors or external assets."""
    return tuple(round(x * (1 - amount) + y * amount)
                 for x, y in zip(rgb(a) if isinstance(a, str) else a,
                                 rgb(b) if isinstance(b, str) else b))


class Canvas:
    """Logical coordinates at 2x resolution, with optional local transforms."""

    def __init__(self, image=None, x=0, y=0, scale=1, facing=1):
        self.image = image if image is not None else Image.new(
            "RGB", (SIZE[0] * SCALE, SIZE[1] * SCALE), NIGHT)
        self.draw = ImageDraw.Draw(self.image)
        self.x, self.y, self.scale, self.facing = x, y, scale, facing

    def point(self, point):
        x, y = point
        return (round((self.x + x * self.scale * self.facing) * SCALE),
                round((self.y + y * self.scale) * SCALE))

    def box(self, box):
        a, b = self.point(box[:2]), self.point(box[2:])
        return (min(a[0], b[0]), min(a[1], b[1]),
                max(a[0], b[0]), max(a[1], b[1]))

    def line(self, points, fill=INK, width=5):
        points = [self.point(p) for p in points]
        w = max(1, round(width * self.scale * SCALE))
        self.draw.line(points, fill=fill, width=w, joint="curve")
        r = w / 2
        for x, y in points:
            self.draw.ellipse((x-r, y-r, x+r, y+r), fill=fill)

    def polygon(self, points, fill, outline=INK, width=5):
        self.draw.polygon([self.point(p) for p in points], fill=fill)
        if outline is not None:
            self.line(points + points[:1], outline, width)

    def rect(self, box, fill, radius=5, outline=INK, width=5):
        self.draw.rounded_rectangle(
            self.box(box), radius=round(radius * self.scale * SCALE),
            fill=fill, outline=outline,
            width=max(1, round(width * self.scale * SCALE)))

    def ellipse(self, box, fill, outline=INK, width=5):
        self.draw.ellipse(self.box(box), fill=fill, outline=outline,
                          width=max(1, round(width * self.scale * SCALE)))

    def local(self, x, y, scale=1, facing=1):
        return Canvas(self.image, x, y, scale, facing)

    def finish(self):
        # The only gradient is the requested bottom-quarter caption scrim.
        overlay = Image.new("RGB", self.image.size, NIGHT)
        mask = Image.new("L", self.image.size, 0)
        draw = ImageDraw.Draw(mask)
        start = 300 * SCALE
        for y in range(start, 400 * SCALE):
            alpha = round(255 * 0.35 * (y-start) / (400*SCALE-1-start))
            draw.line((0, y, 640*SCALE, y), fill=alpha)
        image = Image.composite(overlay, self.image, mask)
        return image.resize(SIZE, Image.Resampling.LANCZOS)


def gun(c, x=13, y=-40, heavy=False, flash=False):
    length = 57 if heavy else 37
    c.rect((x, y, x+length, y+10), BLUE, radius=3, width=4)
    c.line([(x+length, y+4), (x+length+12, y+4)], INK, 6)
    c.polygon([(x+15, y+8), (x+25, y+8),
               (x+22, y+21), (x+13, y+19)], GRAY, width=4)
    c.line([(x+7, y-3), (x+21, y-3)], GRAY, 3)
    if heavy:
        c.ellipse((x+24, y+6, x+44, y+25), NAVY, width=4)
    if flash:
        x += length + 18
        c.polygon([(x-5, y+5), (x+2, y-7), (x+6, y),
                   (x+19, y+4), (x+6, y+9), (x+2, y+16)],
                  YELLOW, outline=ORANGE, width=3)


def hero(c, x, y, species="goose", scale=1, facing=1, back=False):
    c = c.local(x, y, scale, facing)
    fur = {"goose": WHITE, "duck": YELLOW,
           "raccoon": GRAY, "panda": ORANGE}[species]
    c.ellipse((-32, -5, 35, 8), NIGHT, outline=None)
    if species in ("raccoon", "panda"):
        c.ellipse((-39, -38, -8, -6), fur, width=4)
        for k in range(3):
            c.line([(-33+k*6, -28+k*5), (-24+k*5, -33+k*5)], INK, 4)
    c.rect((-21, -22, -4, 0), NAVY, radius=7, width=4)
    c.rect((6, -22, 24, -2), NAVY, radius=7, width=4)
    c.ellipse((-28, -66, 29, -13), fur)
    c.rect((-25, -60, 25, -19), SAGE, radius=12)
    c.line([(-14, -53), (-14, -28)], mix(SAGE, WHITE, .35), 4)
    if species in ("raccoon", "panda"):
        c.ellipse((-27, -98, -7, -76), fur, width=4)
        c.ellipse((8, -98, 27, -76), fur, width=4)
    c.ellipse((-25, -92, 28, -52), fur)
    if species in ("goose", "duck"):
        c.ellipse((16, -74, 43, -60), ORANGE, width=4)
    elif not back:
        c.ellipse((3, -79, 27, -65), INK, outline=None)
        c.ellipse((16, -68, 34, -55), WHITE, width=4)
    if not back:
        c.ellipse((16, -78, 21, -72), INK, outline=None)
    c.ellipse((-28, -101, 28, -69), BLUE)
    c.rect((-28, -81, 31, -72), BLUE, radius=4, width=4)
    c.line([(-16, -93), (2, -95)], SKY, 4)
    if back:
        c.rect((-17, -59, 19, -24), BROWN, radius=9, width=4)
        c.line([(-8, -49), (10, -49)], mix(BROWN, YELLOW, .3), 4)
        c.line([(24, -60), (36, -82)], INK, 7)
        c.rect((20, -64, 32, -37), BLUE, radius=4, width=4)
        c.ellipse((20, -43, 33, -30), fur, width=4)
    else:
        gun(c)
        c.ellipse((7, -43, 24, -28), fur, width=4)


def enemy(c, x, y, family, index=0, scale=.6, facing=1,
          boss=False, flash=False):
    c = c.local(x, y, scale, facing)
    zombie = family == "zombie"
    species = ("eagle", "tiger", "lion")[index % 3]
    fur = mix(GREEN, GRAY, .45) if zombie else (
        WHITE if species == "eagle" else ORANGE if species == "tiger" else YELLOW)
    if boss:
        fur = mix(fur, NAVY, .48)
    cloth = (PURPLE, BROWN, BLUE)[index % 3]
    c.ellipse((-29, -4, 33, 6), NIGHT, outline=None)
    c.rect((-21, -27, -5, 0), BLUE, radius=5, width=4)
    c.rect((7, -27, 23, 0), BLUE, radius=5, width=4)
    c.rect((-25, -65, 27, -20), mix(cloth, NAVY, .2), radius=13)
    if zombie:
        # Ordinary clothes, bare human head, and two reaching arms.
        c.line([(-15, -53), (6, -47), (34, -58)], INK, 12)
        c.line([(-15, -53), (6, -47), (34, -58)], cloth, 6)
        c.ellipse((29, -64, 43, -52), fur, width=4)
        c.line([(21, -51), (39, -45), (54, -56)], INK, 12)
        c.line([(21, -51), (39, -45), (54, -56)], fur, 6)
        c.rect((-22, -102, 26, -60), fur, radius=15)
        c.polygon([(-20, -85), (-19, -100), (4, -105),
                   (26, -93), (8, -94), (-2, -87)], NAVY, width=4)
        c.line([(-10, -28), (-2, -33), (6, -27)], SAGE, 3)
    else:
        if species == "lion" or (boss and family == "animals"):
            c.ellipse((-34, -111, 37, -48), mix(BROWN, NAVY, .15))
        c.ellipse((-26, -104, -7, -85), fur, width=4)
        c.ellipse((11, -104, 29, -85), fur, width=4)
        c.ellipse((-25, -100, 29, -58), fur)
        if species == "eagle" and not (boss and family == "animals"):
            c.polygon([(18, -82), (42, -76), (30, -62), (22, -68)],
                      YELLOW, width=4)
        else:
            c.ellipse((9, -80, 34, -58), mix(fur, WHITE, .25), width=4)
            c.ellipse((25, -76, 33, -69), INK, outline=None)
        if species == "tiger":
            for yy in (-91, -80):
                c.line([(-20, yy), (-9, yy+5)], INK, 4)
        if boss and family == "animals":
            # A broad, horned bull replaces the ordinary animal head.
            c.ellipse((-31, -107, 31, -55), mix(BROWN, NAVY, .3))
            c.polygon([(-24, -92), (-43, -106), (-40, -126),
                       (-29, -110), (-14, -104)], GRAY, width=4)
            c.polygon([(24, -92), (43, -106), (40, -126),
                       (29, -110), (14, -104)], GRAY, width=4)
            c.ellipse((-6, -78, 34, -54), BROWN, width=4)
            c.ellipse((16, -71, 22, -64), INK, outline=None)
        if family == "armed":
            c.rect((-26, -61, 28, -24), NAVY, radius=6, width=4)
            for xx in (-17, 3):
                c.rect((xx, -48, xx+14, -30), BLUE, radius=3, width=3)
            c.line([(-17, -57), (15, -57)], GRAY, 3)
            gun(c, heavy=boss, flash=flash)
        elif not boss:
            if index % 3 == 0:
                # Bent wooden bow and taut string, with a plain arrow.
                c.line([(37, -84), (48, -69), (51, -48), (40, -26)], INK, 8)
                c.line([(37, -84), (48, -69), (51, -48), (40, -26)], BROWN, 4)
                c.line([(37, -84), (32, -53), (40, -26)], GRAY, 2)
                c.line([(25, -53), (60, -53)], BROWN, 4)
                c.polygon([(60, -53), (53, -58), (53, -48)], GRAY, width=2)
            elif index % 3 == 1:
                c.line([(29, -34), (40, -84)], INK, 11)
                c.line([(29, -34), (40, -84)], BROWN, 5)
                c.ellipse((29, -101, 51, -68), BROWN, width=4)
            else:
                c.line([(30, -33), (37, -60)], BROWN, 9)
                c.polygon([(32, -59), (36, -94), (48, -104), (44, -57)],
                          GRAY, width=4)
        if not (boss and family == "animals"):
            c.ellipse((11, -84, 17, -77), INK, outline=None)
        c.ellipse((17, -44, 31, -31), fur, width=4)
    if zombie:
        c.ellipse((12, -87, 18, -80), INK, outline=None)
        c.line([(11, -70), (22, -70)], INK, 3)
    if boss:
        c.line([(-21, -96), (-12, -100)], mix(SKY, BLUE, .4), 4)


def crate(c, x, y, w=48, h=38):
    c.rect((x, y, x+w, y+h), mix(BROWN, NAVY, .4), radius=4)
    c.line([(x+8, y+8), (x+w-8, y+h-8)], BROWN, 4)
    c.line([(x+8, y+h-8), (x+w-8, y+8)], BROWN, 4)


def sandbags(c, x, y, count=4):
    for row in (0, 1):
        for col in range(count):
            xx = x + col*29 + row*12
            yy = y - row*15
            c.rect((xx, yy, xx+34, yy+19), mix(BROWN, SAGE, .35),
                   radius=8, width=4)
            c.line([(xx+8, yy+5), (xx+22, yy+5)], BROWN, 2)


def tree(c, x, y, size=1):
    c = c.local(x, y, size)
    c.line([(0, -8), (0, -66)], mix(BROWN, NAVY, .5), 10)
    c.ellipse((-39, -92, 23, -45), mix(SAGE, NAVY, .72))
    c.ellipse((-17, -112, 43, -55), mix(SAGE, NAVY, .61))
    c.line([(-7, -92), (11, -98)], mix(SAGE, BLUE, .6), 4)


def campaign():
    c = Canvas()
    c.rect((0, 0, 640, 400), mix(PURPLE, NAVY, .55), radius=0, outline=None)
    c.rect((0, 88, 640, 181), mix(ORANGE, NAVY, .53), radius=0, outline=None)
    c.ellipse((310, 67, 398, 155), mix(YELLOW, ORANGE, .6), outline=None)
    for x, y, w, h in ((234, 88, 24, 85), (267, 108, 28, 65),
                       (399, 96, 29, 80), (436, 77, 24, 100)):
        c.rect((x, y, x+w, y+h), mix(PURPLE, NAVY, .7), radius=0, outline=None)
    c.rect((200, 170, 470, 210), mix(SKY, BLUE, .7), outline=None)
    for x in range(244, 435, 32):
        c.line([(x, 157), (x, 184)], NAVY, 6)
    c.line([(218, 159), (460, 159)], NAVY, 9)
    c.line([(246, 145), (425, 145)], BLUE, 4)
    c.polygon([(0, 198), (640, 198), (640, 400), (0, 400)], NAVY, outline=None)
    c.polygon([(291, 185), (354, 185), (516, 400), (110, 400)], BLUE, outline=None)
    for y, half, length in ((202, 3, 10), (227, 4, 15), (264, 5, 20), (321, 7, 28)):
        c.polygon([(321-half, y), (321+half, y),
                   (321+half+2, y+length), (321-half-2, y+length)],
                  mix(WHITE, BLUE, .25), outline=None)
    for x, y, w, h in ((-12, 35, 112, 210), (104, 75, 83, 143),
                       (183, 122, 57, 90), (452, 99, 68, 123),
                       (526, 44, 126, 215)):
        c.rect((x, y, x+w, y+h), mix(BLUE, NAVY, .25), radius=3)
        c.line([(x+7, y+7), (x+w-7, y+7)], mix(PURPLE, GRAY, .3), 4)
        for xx in range(x+15, x+w-14, 29):
            for yy in range(y+22, y+h-20, 36):
                c.rect((xx, yy, xx+13, yy+18), mix(YELLOW, BLUE, .73),
                       radius=2, width=3)
    for x, y, s, f in ((259, 214, .31, 1), (372, 211, .32, -1),
                        (430, 238, .44, -1), (188, 247, .42, 1)):
        enemy(c, x, y, "zombie", scale=s, facing=f)
    # Chain-link panels along both sidewalks.
    for left, right in ((0, 166), (494, 640)):
        c.rect((left, 227, right, 298), None, radius=0, outline=GRAY, width=4)
        for x in range(left, right-24, 23):
            c.line([(x, 260), (x+12, 237), (x+24, 260),
                    (x+12, 286), (x, 260)], mix(GRAY, BLUE, .55), 2)
    hero(c, 273, 286, "goose", .83, back=True)
    hero(c, 372, 294, "panda", .86, facing=-1, back=True)
    hero(c, 314, 320, "raccoon", 1.04, back=True)
    c.polygon([(0, 348), (139, 326), (188, 400), (0, 400)], NAVY, outline=None)
    c.polygon([(504, 327), (640, 339), (640, 400), (466, 400)], NAVY, outline=None)
    return c.finish()


def short_campaign():
    c = Canvas()
    c.rect((0, 0, 640, 400), NAVY, radius=0, outline=None)
    # Warm dusk outside occupies the right half of the open warehouse.
    c.rect((314, 23, 640, 265), mix(PURPLE, NAVY, .5), radius=0, outline=None)
    c.rect((314, 89, 640, 166), mix(ORANGE, NAVY, .58), radius=0, outline=None)
    c.polygon([(314, 174), (640, 158), (640, 291), (314, 298)],
              mix(SAGE, NAVY, .65), outline=None)
    for x, y, s in ((354, 184, .72), (400, 173, .6), (608, 187, 1.1), (550, 171, .7)):
        tree(c, x, y, s)
    c.polygon([(437, 180), (467, 180), (587, 295), (331, 295)],
              mix(BROWN, NAVY, .5), outline=None)
    c.rect((466, 169, 536, 206), mix(GRAY, BLUE, .5), radius=12)
    c.rect((480, 174, 524, 207), NIGHT, radius=14, width=4)
    sandbags(c, 447, 204, 4)
    c.polygon([(0, 251), (310, 237), (640, 294), (640, 400), (0, 400)],
              mix(BLUE, NAVY, .6), outline=None)
    c.polygon([(322, 243), (555, 243), (478, 325), (203, 325)],
              mix(BROWN, BLUE, .75), outline=None)
    for x in (18, 145):
        c.rect((x, 42, x+113, 253), mix(BLUE, NAVY, .4), radius=3)
        for y in (92, 158, 225):
            crate(c, x+10, y-38, 41, 36)
            crate(c, x+60, y-29, 42, 27)
            c.line([(x, y), (x+113, y)], GRAY, 5)
        c.line([(x+4, 43), (x+4, 263)], BLUE, 8)
    c.rect((292, 0, 317, 274), BLUE, radius=0)
    c.rect((314, 0, 640, 42), BLUE, radius=0)
    for y in (8, 19, 30):
        c.line([(324, y), (640, y)], GRAY, 4)
    c.line([(299, 48), (299, 209)], mix(SKY, BLUE, .55), 4)
    for x, y in ((32, 246), (93, 263)):
        c.rect((x, y-44, x+47, y+19), mix(PURPLE, NAVY, .55), radius=8)
        c.ellipse((x, y-51, x+47, y-34), GRAY, width=4)
        c.line([(x+5, y-12), (x+42, y-12)], BLUE, 5)
    hero(c, 251, 310, "duck", .95, back=True)
    hero(c, 359, 284, "raccoon", .83, back=True)
    crate(c, -8, 321, 115, 80)
    return c.finish()


def warehouse():
    c = Canvas()
    c.rect((0, 0, 640, 400), mix(BLUE, NAVY, .35), radius=0, outline=None)
    c.polygon([(0, 178), (640, 178), (640, 400), (0, 400)], NAVY, outline=None)
    c.polygon([(117, 196), (523, 196), (620, 344), (20, 344)],
              mix(BROWN, NAVY, .73), outline=None)
    for box in ((11, 77, 111, 226), (266, 61, 375, 172), (529, 76, 634, 226)):
        c.rect(box, NIGHT, radius=8, outline=GRAY, width=5)
    for x in (143, 409):
        c.rect((x, 76, x+80, 139), mix(SKY, BLUE, .65), radius=4)
        c.line([(x+40, 80), (x+40, 136)], INK, 5)
        c.line([(x+4, 107), (x+76, 107)], INK, 5)
    for x in (126, 245, 395, 514):
        c.line([(x, 0), (x, 183)], BLUE, 9)
    c.line([(0, 32), (640, 32)], INK, 12)
    for x in (191, 449):
        c.line([(x, 0), (x, 39)], INK, 5)
        c.polygon([(x-11, 40), (x+11, 40), (x+25, 57), (x-25, 57)],
                  mix(BROWN, YELLOW, .5), width=4)
        c.ellipse((x-17, 52, x+17, 64), YELLOW, width=4)
    # Flat pools of lamplight, not blurred or photographic shading.
    c.ellipse((164, 170, 476, 309), mix(YELLOW, NAVY, .84), outline=None)
    for points in ([(292, 244), (260, 283), (174, 290), (146, 312)],
                   [(351, 241), (391, 273), (469, 269), (501, 295)]):
        c.line(points, INK, 8)
        c.line(points, mix(YELLOW, BROWN, .6), 3)
    crate(c, 0, 263, 65, 47)
    crate(c, 575, 267, 70, 43)
    return c


def generator(c):
    c.ellipse((266, 245, 376, 266), NIGHT, outline=None)
    c.rect((274, 194, 367, 252), SAGE, radius=10)
    c.rect((283, 203, 328, 240), NAVY, radius=5, width=4)
    for y in (212, 222, 232):
        c.line([(290, y), (319, y)], GRAY, 3)
    c.rect((338, 214, 356, 231), YELLOW, radius=4, width=4)
    c.line([(282, 190), (282, 181), (348, 181), (348, 190)], INK, 6)
    c.line([(353, 196), (353, 149)], INK, 6)
    c.rect((340, 137, 367, 159), YELLOW, radius=7, width=4)
    c.line([(345, 142), (355, 142)], WHITE, 3)
    for x, y in ((325, 150), (377, 150), (351, 126)):
        c.line([(x, y), (x+4, y-3)], YELLOW, 3)


def survival(family):
    c = warehouse()
    positions = ((52, 189, .57, 1), (90, 224, .65, 1),
                 (309, 170, .52, 1), (353, 177, .54, -1),
                 (548, 211, .61, -1), (602, 187, .55, -1),
                 (34, 278, .71, 1), (603, 280, .72, -1))
    for i, (x, y, size, facing) in enumerate(positions):
        enemy(c, x, y, family, i, size, facing,
              flash=family == "armed" and i in (1, 4, 6))
    hero(c, 260, 210, "duck", .69, -1)
    hero(c, 406, 210, "panda", .69)
    generator(c)
    hero(c, 225, 301, "goose", .9, -1)
    hero(c, 423, 301, "raccoon", .9)
    sandbags(c, 266, 298, 4)
    return c.finish()


def meadow():
    c = Canvas()
    c.rect((0, 0, 640, 400), BLUE, radius=0, outline=None)
    c.ellipse((466, 29, 530, 93), SKY, outline=None)
    c.ellipse((474, 42, 486, 54), mix(SKY, GRAY, .3), outline=None)
    c.ellipse((498, 66, 516, 81), mix(SKY, GRAY, .25), outline=None)
    for x, y in ((63, 37), (145, 65), (258, 32), (392, 48), (567, 53)):
        c.ellipse((x, y, x+3, y+3), SKY, outline=None)
    c.polygon([(0, 145), (86, 110), (182, 139), (296, 116),
               (409, 143), (528, 115), (640, 142), (640, 238), (0, 238)],
              mix(SAGE, NAVY, .76), outline=None)
    c.polygon([(0, 217), (177, 176), (402, 183), (640, 208),
               (640, 400), (0, 400)], mix(SAGE, NAVY, .83), outline=None)
    c.ellipse((115, 192, 526, 319), mix(SAGE, BLUE, .65), outline=None)
    for x, y, s in ((35, 191, .85), (132, 172, .6), (585, 184, .85)):
        tree(c, x, y, s)
    for x, y, h in ((163, 183, 67), (532, 228, 85), (64, 277, 62)):
        c.polygon([(x-15, y), (x-12, y-h), (x+11, y-h-5),
                   (x+20, y-8), (x+13, y+4)], mix(GRAY, BLUE, .64))
        c.line([(x-7, y-h+5), (x+6, y-h+2)], mix(SKY, GRAY, .55), 4)
    # Fixed grass tufts leave the lower caption zone spacious and dark.
    for x, y in ((102, 243), (193, 276), (447, 273), (566, 307),
                 (93, 333), (240, 347), (403, 340), (522, 362)):
        c.line([(x-6, y-8), (x, y), (x+5, y-12)], mix(SAGE, NAVY, .55), 3)
    return c


def survivor(family):
    c = meadow()
    enemy(c, 337, 196, family, index=2, scale=1.18, facing=-1, boss=True)
    # A complete ring, with the front arc below the lone player's feet.
    positions = ((206, 206, .53, 1), (430, 207, .54, -1),
                 (139, 248, .62, 1), (492, 251, .63, -1),
                 (113, 304, .74, 1), (528, 305, .74, -1))
    for i, (x, y, size, facing) in enumerate(positions):
        enemy(c, x, y, family, i, size, facing,
              flash=family == "armed" and i in (2, 5))
    hero(c, 322, 279, "goose", .94, back=True)
    for i, (x, y, facing) in enumerate(((221, 349, 1), (424, 348, -1))):
        enemy(c, x, y, family, i+6, .72, facing)
    return c.finish()


def validate(cards):
    assert len(cards) == 8, "Expected eight play-setup cards"
    hashes = set()
    for name, image in cards.items():
        assert image.size == SIZE, (name, image.size)
        assert image.mode == "RGB", (name, image.mode)
        digest = sha256(image.tobytes()).hexdigest()
        assert digest not in hashes, (name, "duplicate pixels")
        hashes.add(digest)
        upper = ImageStat.Stat(image.crop((0, 0, 640, 300)).convert("L")).mean[0]
        lower = ImageStat.Stat(image.crop((0, 300, 640, 400)).convert("L")).mean[0]
        assert lower < upper, (name, "caption band must be darker", lower, upper)
        reduced = image.quantize(colors=32, method=Image.Quantize.MEDIANCUT,
                                 dither=Image.Dither.NONE)
        # Ignore tiny antialiasing-only palette entries when counting colors.
        colors = reduced.getcolors(maxcolors=32)
        assert colors is not None
        used = sum(count >= 64 for count, _ in colors)
        assert used >= 12, (name, "too few substantial palette colors", used)


def main():
    cards = {
        "play_scenario_campaign.png": campaign(),
        "play_scenario_short.png": short_campaign(),
    }
    for family in ("zombie", "animals", "armed"):
        cards[f"play_survival_{family}.png"] = survival(family)
    for family in ("zombie", "animals", "armed"):
        cards[f"play_survivor_{family}.png"] = survivor(family)
    # Validate every image before creating the output folder or writing any PNG.
    validate(cards)
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, image in cards.items():
        image.save(OUTPUT / name, format="PNG")
        print(f"PLAY_CARD_READY {name}")
    print("PLAY_CARDS_BUILT 8")


if __name__ == "__main__":
    main()
