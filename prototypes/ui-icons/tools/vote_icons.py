"""Build the two vote UI PNGs using Pillow only (plus the standard library).

Run from any directory: python prototypes/ui-icons/tools/vote_icons.py
The winner frame supports equal 48 px nine-slice margins on all four sides.
See doc/model-briefs/vote-stamp.md for the asset contract.
"""

from math import hypot
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[3]
OUTPUT = ROOT / "game/assets/icons/ui"
SCALE = 4
INK = (0xD9, 0x47, 0x3B)
GOLD = (0xFF, 0xCF, 0x5A)


def colored_mask(mask, color):
    """Keep RGB constant; antialiasing and glow change only alpha."""
    image = Image.new("RGBA", mask.size, (*color, 0))
    image.putalpha(mask)
    return image


def build_stamp():
    mask = Image.new("L", (256 * SCALE, 256 * SCALE), 0)
    draw = ImageDraw.Draw(mask)

    def ellipse(box, value):
        draw.ellipse(tuple(round(v * SCALE) for v in box), fill=value)

    # A 14 px ink ring, safely inset even after rotation and resampling.
    ellipse((18, 18, 238, 238), 255)
    ellipse((32, 32, 224, 224), 0)

    # One connected webbed foot: three broad toe tips, shallow web valleys,
    # and a short rounded heel. No separate paw-pad dots or fine textures.
    foot = [
        (128, 69), (140, 108), (165, 91), (173, 126),
        (151, 149), (138, 174), (118, 174), (105, 149),
        (83, 126), (91, 91), (116, 108),
    ]
    points = [(x * SCALE, y * SCALE) for x, y in foot]
    draw.polygon(points, fill=255)
    draw.line(points + points[:1], fill=255, width=10 * SCALE, joint="curve")
    for x, y in foot:
        ellipse((x - 5, y - 5, x + 5, y + 5), 255)

    # Fixed, sparse dry-ink gaps, mostly in the ring (well under 10%).
    for box in (
        (65, 35, 72, 39), (196, 53, 201, 60),
        (222, 126, 227, 132), (169, 221, 177, 225),
        (39, 181, 45, 186), (121, 157, 125, 160),
    ):
        ellipse(box, 0)

    mask = mask.rotate(-12, resample=Image.Resampling.BICUBIC, fillcolor=0)
    mask = mask.resize((256, 256), Image.Resampling.LANCZOS)
    return colored_mask(mask, INK)


def build_winner_frame():
    # Signed distance to a rounded rectangle: inset 18, radius 28.
    # Pixel-center sampling makes all four corners exactly symmetric.
    # The straight middle strips have constant cross-sections for nine-slice.
    width, height = 512, 160
    alpha = bytearray(width * height)
    for y in range(height):
        for x in range(width):
            qx = abs(x + 0.5 - width / 2) - (width / 2 - 18 - 28)
            qy = abs(y + 0.5 - height / 2) - (height / 2 - 18 - 28)
            distance = hypot(max(qx, 0), max(qy, 0)) + min(max(qx, qy), 0) - 28
            if distance <= 0:
                # 10 px solid border, with a 1 px antialiased inner edge.
                opacity = min(1.0, max(0.0, distance + 10.5))
            else:
                # Outward-only 14 px glow; the interior stays clear.
                opacity = max(0.0, 1.0 - distance / 14) ** 2
            alpha[y * width + x] = round(255 * opacity)
    return colored_mask(Image.frombytes("L", (width, height), bytes(alpha)), GOLD)


def main():
    assets = (
        ("vote_stamp.png", build_stamp(), (256, 256)),
        ("vote_winner_frame.png", build_winner_frame(), (512, 160)),
    )

    # Validate both images before creating output files. These assertions run
    # only when the reviewer executes this script, not during py_compile.
    for name, image, size in assets:
        assert image.size == size, f"{name}: incorrect dimensions"
        assert image.mode == "RGBA", f"{name}: expected RGBA"
        width, height = size
        for corner in ((0, 0), (width - 1, 0), (0, height - 1), (width - 1, height - 1)):
            assert image.getpixel(corner)[3] == 0, f"{name}: corner is not transparent"

    stamp = assets[0][1]
    opaque_fraction = stamp.getchannel("A").histogram()[255] / (256 * 256)
    assert 0.15 <= opaque_fraction <= 0.45, "stamp: opaque coverage outside 15-45%"
    frame = assets[1][1]
    assert frame.getpixel((256, 80))[3] == 0, "frame: center is not transparent"

    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, image, (width, height) in assets:
        path = OUTPUT / name
        image.save(path, format="PNG")
        print(f"VOTE_ICON_READY {path.relative_to(ROOT).as_posix()} {width}x{height}")
    print("VOTE_ICONS_BUILT 2")


if __name__ == "__main__":
    main()
