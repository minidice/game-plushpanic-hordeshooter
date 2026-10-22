"""Arrange actual Godot captures for inspection; no generated/retouched model imagery."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / 'game/output/monster-review'
FONT = 'C:/Windows/Fonts/malgun.ttf'
font = ImageFont.truetype(FONT, 20)
small = ImageFont.truetype(FONT, 16)


def sheet(name, rows, columns, cell=340):
    canvas = Image.new('RGB', (len(columns) * cell, 65 + len(rows) * (cell + 48)), '#17202b')
    draw = ImageDraw.Draw(canvas)
    draw.text((18, 15), '몬스터 점검 · 현재 게임 모델 / 수정 전 실제 렌더', font=font, fill='white')
    for y, (model, label) in enumerate(rows):
        for x, (phase, phase_label) in enumerate(columns):
            image = Image.open(OUT / f'{model}-{phase}.png').convert('RGB')
            # Identical crop/scale across subjects preserves their relative size.
            tile = image.crop((440, 80, 1160, 800)).resize((cell, cell), Image.Resampling.LANCZOS)
            top = 65 + y * (cell + 48)
            canvas.paste(tile, (x * cell, top))
            draw.text((x * cell + 12, top + cell + 8), f'{label} · {phase_label}', font=small, fill='#e2e8ef')
    canvas.save(OUT / name)


sheet('death-contact-sheet.png', [('boss', '황소 두목'), ('boss_spitter_mother', '토사물 어미')],
      [('alive', '생존'), ('fall', '넘어지는 중'), ('rest', '사망 정지'), ('rest-low', '낮은 시점')])
sheet('named-contact-sheet.png', [('bull', '황소'), ('tether', '끌개'), ('leaper', '덮치는 적'), ('spitter', '토사물 적')],
      [('alive', '생존'), ('rest', '사망 정지'), ('rest-low', '낮은 시점')], cell=300)
sheet('boss-shape-sheet.png', [('boss', '황소 두목'), ('boss_bull_giant', '거대 황소'),
                             ('boss_balloon_king', '풍선 대왕'), ('boss_spitter_mother', '토사물 어미')],
      [('alive', '정면 쿼터뷰'), ('side', '측면 쿼터뷰')], cell=350)
print('CONTACT_SHEETS_COMPLETE 3')
