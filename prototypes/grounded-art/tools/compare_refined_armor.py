"""Native renders arranged without model retouching; explicit model/light comparison."""
from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageFont
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'output/refined-armor'
manifest=json.loads((ROOT/'refined-armor-manifest.json').read_text(encoding='utf-8'))
def font(size): return ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',size)
def pair(light,view):
    images=[Image.open(OUT/f'{light}-{variant}-{view}.png').convert('RGB') for variant in ('before','after')]
    w,h=images[0].size
    board=Image.new('RGB',(w*2,h+140),'#1d282e')
    d=ImageDraw.Draw(board)
    for i,(im,title) in enumerate(zip(images,('기존 방탄조끼 03','기본 방탄형 04'))):
        board.paste(im,(w*i,108))
        d.text((w*i+26,16),title,font=font(30),fill='#e5d5a6' if i else '#d5dfe4')
        d.text((w*i+26,62),'중립 조명' if light=='neutral' else '분위기 조명 · 차가운 주변광 / 따뜻한 주광',font=font(18),fill='#afc0c6')
    d.text((26,h+114),'실제 Blender 렌더 · 같은 시점과 조명으로 비교 · 게임 미적용',font=font(17),fill='#afc0c6')
    board.save(OUT/f'comparison-{light}-{view}.png')
    return board
for light,views in [('neutral',('quarter','front','side','rear','top','small')),('mood',('quarter','top','small'))]:
    for v in views: pair(light,v)

# Four corners separate model changes from lighting changes.
board=Image.new('RGB',(1440,2000),'#1d282e')
d=ImageDraw.Draw(board)
d.text((26,17),'같은 모델, 두 가지 조명',font=font(34),fill='#e5d5a6')
d.text((26,65),'윗줄: 중립 / 아랫줄: 분위기 · 왼쪽: 후보 03 / 오른쪽: 후보 04',font=font(21),fill='#d5dfe4')
for y,light in [(110,'neutral'),(1055,'mood')]:
    for i,(variant,label) in enumerate([('before','후보 03'),('after','후보 04')]):
        x=i*720
        d.text((x+26,y+8),label+' / '+('중립' if light=='neutral' else '분위기'),font=font(27),fill='#e5d5a6')
        board.paste(Image.open(OUT/f'{light}-{variant}-quarter.png').convert('RGB'),(x,y+55))
d.text((26,1960),'모델 형태·재질 변경과 조명 효과를 함께 확인하는 비교판',font=font(21),fill='#afc0c6')
board.save(OUT/'lighting-matrix.png')

board=Image.new('RGB',(1440,1450),'#1d282e')
d=ImageDraw.Draw(board)
d.text((26,16),'BASIC ARMOR / 04',font=font(34),fill='#e5d5a6')
for (v,label),(x,y) in zip([('quarter','사선'),('front','정면'),('side','측면'),('rear','후면'),('top','위쪽')],[(0,90),(480,90),(960,90),(0,690),(480,690)]):
    d.text((x+20,y),label,font=font(23),fill='#d5dfe4')
    im=Image.open(OUT/f'neutral-after-{v}.png').convert('RGB').resize((480,560),Image.Resampling.LANCZOS)
    board.paste(im,(x,y+35))
counts=f"조끼 {manifest['before_vest']['triangles']:,} → {manifest['vest']['triangles']:,} 삼각형"
d.text((26,1330),counts+' · 넓은 면 / 얇은 가장자리 / 재질 구분',font=font(24),fill='#e5d5a6')
d.text((26,1380),'기존 몸·얼굴·날개·발·헬멧 및 배낭 형상 유지 / 게임 미적용',font=font(20),fill='#afc0c6')
board.save(OUT/'candidate-overview.png')
print('REFINED_COMPARISONS_READY 11')
