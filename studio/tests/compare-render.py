"""GPU edge coverage can differ by a few values at one pixel after mesh recreation."""
import sys
from PIL import Image, ImageChops, ImageStat

with Image.open(sys.argv[1]) as first, Image.open(sys.argv[2]) as second:
    a, b = first.convert("RGB"), second.convert("RGB")
    assert a.size == b.size == (900, 900)
    assert max(ImageStat.Stat(a).stddev) > 10, "Blank or nearly uniform render"
    assert max(ImageStat.Stat(b).stddev) > 10, "Blank or nearly uniform reset render"
    diff = ImageChops.difference(a, b)
    changed = sum(any(channel) for channel in diff.get_flattened_data())
    mean = max(ImageStat.Stat(diff).mean)
    assert changed / (900 * 900) < 0.0005 and mean < 0.02, (changed, mean)
    print(f"PIXEL_RESET_PASS changed={changed} max_channel_mean={mean:.6f}")
