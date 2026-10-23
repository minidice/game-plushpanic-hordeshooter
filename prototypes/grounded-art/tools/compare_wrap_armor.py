"""Labeled side-by-side boards from actual Blender renders."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT/'output/wrap-armor'
def font(size):
    return ImageFont.truetype('C:/Windows/Fonts/malgun.ttf', size)

boards = {}
for view, label in [('quarter', '사선'), ('front', '정면'), ('side', '측면'), ('rear', '후면'), ('top', '위쪽')]:
    board = Image.new('RGB', (1440, 980), '#1d2822')
    draw = ImageDraw.Draw(board)
    for i, (variant, title, detail) in enumerate([
            ('before', '기존 군장 후보 01', '가슴판 + 좁은 옆 끈'),
            ('after', '방탄조끼 후보 03', '앞·옆·등 연결 패널 + 날개 개구부')]):
        im = Image.open(OUT/f'{variant}-{view}.png').convert('RGB')
        assert im.size == (720, 840)
        board.paste(im, (720*i, 108))
        draw.text((720*i+25, 16), title, font=font(30), fill='#e9d4a3' if i else '#d5dfd7')
        draw.text((720*i+25, 62), detail, font=font(19), fill='#aabbb0')
    draw.text((25, 954), label+' / 같은 몸·헬멧·배낭·조명·카메라 / 게임 미적용', font=font(17), fill='#c5d0c8')
    board.save(OUT/f'comparison-{view}.png')
    boards[view] = board

board = Image.new('RGB', (1440, 1100), '#1d2822')
draw = ImageDraw.Draw(board)
draw.text((25, 15), 'WRAP ARMOR / 03', font=font(32), fill='#e9d4a3')
draw.text((25, 60), '각 칸 왼쪽: 기존 후보 / 오른쪽: 옆면 보강 후보', font=font(22), fill='#c5d0c8')
for name, xy in zip(('quarter', 'side', 'front', 'top'), ((0, 110), (720, 110), (0, 605), (720, 605))):
    board.paste(boards[name].resize((720, 490), Image.Resampling.LANCZOS), xy)
board.save(OUT/'comparison-overview.png')

board = Image.new('RGB', (2160, 980), '#1d2822')
draw = ImageDraw.Draw(board)
for i, (file, label) in enumerate([('after-quarter', '기본 자세'), ('probe-forward20', '날개 앞쪽 20°'), ('probe-outward20', '날개 바깥쪽 20°')]):
    board.paste(Image.open(OUT/(file+'.png')).convert('RGB'), (720*i, 108))
    draw.text((720*i+25, 22), label, font=font(30), fill='#e9d4a3')
draw.text((25, 954), '원래 날개 피벗의 제한된 자세 검사 / 실제 무기용 팔·게임 애니메이션 검증은 별도', font=font(19), fill='#c5d0c8')
board.save(OUT/'wing-probes.png')
print('WRAP_COMPARISONS_READY 7')
