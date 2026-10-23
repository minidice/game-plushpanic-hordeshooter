"""Arrange real Blender renders without retouching character pixels."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT/'output/workwear'
FONT = 'C:/Windows/Fonts/malgun.ttf'


def font(size):
    return ImageFont.truetype(FONT, size)


labels = [('original', '기존 기본 군장', '원본 모델'),
          ('field', '군장 후보 01', '헬멧 · 가슴판 · 배낭'),
          ('workwear', '작업복 후보 02', '천 모자 · 작업 조끼 / 배낭 없음')]
views = [('quarter', '사선'), ('front', '정면'), ('side', '측면'),
         ('rear', '후면'), ('top', '위쪽 시점'), ('small', '위쪽 시점 · 작은 화면 크기')]
for view, description in views:
    images = [Image.open(OUT/f'{key}-{view}.png').convert('RGB') for key, _, _ in labels]
    w, h = images[0].size
    assert all(im.size == (w, h) for im in images)
    sheet = Image.new('RGB', (w*3, h+140), '#1e2b30')
    draw = ImageDraw.Draw(sheet)
    for index, (im, (_, title, detail)) in enumerate(zip(images, labels)):
        x = index*w
        sheet.paste(im, (x, 110))
        draw.text((x+25, 15), title, font=font(30), fill='#f1dba9' if index == 2 else '#d6e0df')
        draw.text((x+25, 60), detail, font=font(19), fill='#b8c7c8')
        draw.line((x, 110, x, h+110), fill='#556065', width=1)
    draw.text((25, h+115), description+' / 같은 몸 · 조명 · 카메라 / Blender 렌더, 게임 미적용',
              font=font(17), fill='#c6d1d2')
    sheet.save(OUT/f'comparison-{view}.png')

sheet = Image.new('RGB', (1440, 1850), '#1e2b30')
draw = ImageDraw.Draw(sheet)
draw.text((28, 18), 'GOOSE / WORKWEAR 02', font=font(34), fill='#f1dba9')
draw.text((28, 65), '같은 몸과 얼굴 · 군장 없이 완성되는 복장', font=font(22), fill='#d6e0df')
for (view, title), (x, y) in zip(views[:5], [(0, 110), (480, 110), (960, 110), (0, 710), (480, 710)]):
    im = Image.open(OUT/f'workwear-{view}.png').convert('RGB').resize((480, 560), Image.Resampling.LANCZOS)
    sheet.paste(im, (x, y+32))
    draw.text((x+18, y), title, font=font(21), fill='#d6e0df')
small = Image.open(OUT/'workwear-small.png').convert('RGB')
sheet.paste(small, (360, 1350))
draw.text((28, 1340), '작은 화면 크기 확인', font=font(23), fill='#f1dba9')
draw.text((28, 1820), '실제 게임 화면은 아니며 고정 자세의 아트 검토용입니다.', font=font(18), fill='#b8c7c8')
sheet.save(OUT/'workwear-overview.png')
print('WORKWEAR_COMPARISONS_READY 7')
