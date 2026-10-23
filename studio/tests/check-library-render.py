"""Flat props compress very well: inspect pixels instead of PNG file size."""
import sys
from PIL import Image, ImageStat

for path in sys.argv[1:]:
    with Image.open(path) as image:
        rgb = image.convert('RGB')
        assert rgb.size == (900, 900), path
        assert max(ImageStat.Stat(rgb).stddev) > 3, f'Blank or nearly uniform render: {path}'
print(f'LIBRARY_PIXELS_PASS {len(sys.argv) - 1}')
