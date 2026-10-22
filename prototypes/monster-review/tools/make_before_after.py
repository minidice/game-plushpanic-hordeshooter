"""Before/after boards from actual Godot renders. Same camera, crop and scale per column."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[3]
BEFORE = ROOT / 'game/output/monster-review-before/captures'
AFTER = ROOT / 'game/output/monster-review-after'
font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 18)
title_font = ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', 24)


def board(filename, rows, columns, crop=(320, 80, 1200, 800), width=350):
    height = round(width * (crop[3] - crop[1]) / (crop[2] - crop[0]))
    image = Image.new('RGB', (width * len(columns), 58 + len(rows) * (height + 44)), '#17202b')
    draw = ImageDraw.Draw(image)
    draw.text((16, 14), '몬스터 개선 · 수정 전 / 수정 후 실제 게임 렌더', font=title_font, fill='white')
    for y, (model, label) in enumerate(rows):
        for x, (root, phase, phase_label) in enumerate(columns):
            tile = Image.open(root / f'{model}-{phase}.png').convert('RGB').crop(crop)
            tile = tile.resize((width, height), Image.Resampling.LANCZOS)
            top = 58 + y * (height + 44)
            image.paste(tile, (x * width, top))
            draw.text((x * width + 10, top + height + 10), f'{label} · {phase_label}', font=font, fill='white')
    image.save(AFTER / filename)


board('death-before-after.png', [('bull', '황소'), ('boss', '황소 두목'), ('boss_spitter_mother', '토사물 어미')],
      [(BEFORE, 'rest', '이전'), (AFTER, 'rest', '개선'),
       (BEFORE, 'rest-low', '이전 접지'), (AFTER, 'rest-low', '개선 접지')])
board('surface-before-after.png', [('bull', '황소'), ('boss', '황소 두목'), ('boss_balloon_king', '풍선 대왕')],
      [(BEFORE, 'alive', '이전'), (AFTER, 'alive', '개선')], crop=(500, 130, 1080, 710), width=420)
board('collapse-sequence.png', [('bull', '황소'), ('boss', '황소 두목'), ('boss_spitter_mother', '토사물 어미'), ('boss_balloon_king', '풍선 대왕')],
      [(AFTER, 'alive', '생존'), (AFTER, 'fall', '무게 이동'), (AFTER, 'land', '넘어짐'), (AFTER, 'rest', '정지')], width=310)
print('BEFORE_AFTER_COMPLETE 3 boards')
