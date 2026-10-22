"""Arrange actual accessory renders; no image retouching or generation."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'output/face-accessories'
KINDS=('bare','glasses','sunglasses','headset','before','goggles','refined','combo')
NAMES={'bare':'기준 · 모자 없는 얼굴','glasses':'15 일반 안경','sunglasses':'16 선글라스','headset':'17 헤드셋',
       'before':'기존 고글 모자 10','goggles':'18 얼굴 착용 고글','refined':'19 개선한 고글 모자','combo':'선글라스 + 헤드셋'}
SUB={'bare':'기존 몸과 표정 유지','glasses':'얇은 테 / 투명 렌즈','sunglasses':'작고 어두운 렌즈 / 얇은 다리',
     'headset':'독립 이어컵 / 머리띠 / 마이크','before':'큰 타원 렌즈 / 낮은 정수리',
     'goggles':'얕은 곡면 / 얼굴을 두르는 밴드','refined':'작고 얇은 고글 / 높인 정수리','combo':'각각 분리된 두 액세서리'}
def font(n):return ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',n)
def read(light,kind,view):return Image.open(OUT/f'{light}-{kind}-{view}.png').convert('RGB')
def grid(light,view,name):
    w=480;h=320 if view=='small' else 560;step=h+90
    b=Image.new('RGB',(w*4,step*2+106),'#1d282e');d=ImageDraw.Draw(b)
    d.text((24,15),'얼굴 액세서리 추가 · 고글과 모자 비율 개선',font=font(31),fill='#e5d5a6')
    for i,k in enumerate(KINDS):
        x=(i%4)*w;y=70+(i//4)*step
        d.text((x+18,y+4),NAMES[k],font=font(24),fill='#e5d5a6')
        d.text((x+18,y+43),SUB[k],font=font(17),fill='#bac9ce')
        b.paste(read(light,k,view).resize((w,h),Image.Resampling.LANCZOS),(x,y+82))
    d.text((24,b.height-30),'실제 Blender 렌더 · 같은 몸·조끼·카메라·조명 · 게임 미적용',font=font(18),fill='#bac9ce')
    b.save(OUT/name)
grid('neutral','detail','accessories-details.png')
grid('neutral','quarter','accessories-neutral.png')
grid('mood','quarter','accessories-mood.png')
grid('mood','small','accessories-small.png')

for view in ('detail','side','front'):
    b=Image.new('RGB',(1440,990),'#1d282e');d=ImageDraw.Draw(b)
    for i,k in enumerate(('before','refined')):
        d.text((i*720+24,16),NAMES[k],font=font(32),fill='#e5d5a6')
        d.text((i*720+24,64),SUB[k],font=font(22),fill='#bac9ce')
        b.paste(read('neutral',k,view),(i*720,108))
    d.text((24,957),'같은 카메라 비교 · 고글 중앙 돌출 28 → 3 mm / 정수리 높이 +100 mm',font=font(19),fill='#bac9ce')
    b.save(OUT/f'goggles-before-after-{view}.png')
for kind in ('glasses','sunglasses','headset','goggles','refined','combo'):
    b=Image.new('RGB',(1440,1495),'#1d282e');d=ImageDraw.Draw(b)
    d.text((24,16),NAMES[kind]+' / 방향별 확인',font=font(33),fill='#e5d5a6')
    for (v,label),(x,y) in zip([('quarter','사선'),('front','정면'),('side','측면'),('rear','후면'),('top','위쪽')],[(0,90),(480,90),(960,90),(0,730),(480,730)]):
        d.text((x+18,y),label,font=font(23),fill='#c5d0d5')
        b.paste(read('neutral',kind,v).resize((480,560),Image.Resampling.LANCZOS),(x,y+38))
    d.text((24,1410),SUB[kind],font=font(25),fill='#e5d5a6')
    d.text((24,1460),'기존 몸·조끼 유지 / 장비 노드 분리 / 게임 미적용',font=font(20),fill='#bac9ce')
    b.save(OUT/f'{kind}-overview.png')
print('FACE_COMPARISONS_READY: 13 boards')
