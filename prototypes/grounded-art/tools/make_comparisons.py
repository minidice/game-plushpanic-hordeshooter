"""Lay out unretouched Blender renders with labels. No generated/painted model pixels."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'output'
FONT=Path('C:/Windows/Fonts/malgun.ttf')


def font(size): return ImageFont.truetype(str(FONT),size)


def pair(view):
    a=Image.open(OUT/f'original-{view}.png').convert('RGB')
    b=Image.open(OUT/f'candidate-{view}.png').convert('RGB')
    assert a.size==b.size
    w,h=a.size
    board=Image.new('RGB',(w*2,h+140),'#17221f')
    board.paste(a,(0,108));board.paste(b,(w,108))
    draw=ImageDraw.Draw(board)
    draw.text((38,18),'기존 군장',font=font(32),fill='#d3dcd4')
    draw.text((w+38,18),'새 군장 후보 01',font=font(32),fill='#e9d4a3')
    draw.text((38,65),'원본 거위 · 기본 헬멧 / 조끼 / 배낭',font=font(18),fill='#a9b8ad')
    draw.text((w+38,65),'같은 몸 · 얼굴 · 조명 · 카메라 / 군장만 변경',font=font(18),fill='#a9b8ad')
    draw.line((w,108,w,h+108),fill='#38473d',width=2)
    draw.text((38,h+114),'Blender 실제 모델 렌더 · 게임 미적용 · 고정 자세 비교',font=font(16),fill='#a9b8ad')
    path=OUT/f'comparison-{view}.png'
    board.save(path)
    return board


boards={v:pair(v) for v in ('quarter','front','side','rear','rear-quarter','gameplay')}
sheet=Image.new('RGB',(1800,1330),'#17221f')
draw=ImageDraw.Draw(sheet)
draw.text((36,17),'GOOSE / FIELD KIT 01',font=font(34),fill='#e9d4a3')
draw.text((36,64),'각 칸 왼쪽: 기존 / 오른쪽: 후보 — 동일 조건의 실제 GLB 제작 렌더',font=font(20),fill='#d3dcd4')
for (view,label),(x,y) in zip([('quarter','사선'),('front','정면'),('side','측면'),('rear','후면')],[(0,110),(900,110),(0,720),(900,720)]):
    # Keep full render, scale the pair as a unit; never distort either model.
    tile=boards[view].resize((900,595),Image.Resampling.LANCZOS)
    sheet.paste(tile,(x,y))
    ImageDraw.Draw(sheet).text((x+15,y+73),label,font=font(18),fill='#e9d4a3')
sheet.save(OUT/'comparison-overview.png')
print('COMPARISONS_READY 7')
