"""Arrange unretouched Blender renders into headwear contact sheets."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'output/headwear-collection'
KINDS=('before','stripe','cap','goggles','motorcycle','nvg','bunny','flower')
NAMES={'before':'이전 고글 05','stripe':'08 중앙 줄무늬 투구','cap':'09 챙 있는 캡',
       'goggles':'10 곡면 고글 모자','motorcycle':'11 오토바이 헬멧',
       'nvg':'12 쌍안 야시경 투구','bunny':'13 토끼귀 모자','flower':'14 꽃 장식 모자'}
SUB={'before':'사각 렌즈 · 비교 기준','stripe':'짧은 둘레 챙 / 중앙 줄무늬','cap':'긴 앞챙 / 천 패치',
     'goggles':'좌우 볼록 렌즈 / 곡면 프레임','motorcycle':'옆면 보호 / 올려 쓴 바이저',
     'nvg':'원통형 광학부 두 개 / 연결 암','bunny':'한쪽이 접힌 귀 / 분홍 캡','flower':'넓은 챙 / 큰 꽃 한 송이'}
def font(n):return ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',n)
def read(light,kind,view):return Image.open(OUT/f'{light}-{kind}-{view}.png').convert('RGB')

def grid(light,view,filename):
    w=480;h=320 if view=='small' else 560;step=h+90
    b=Image.new('RGB',(w*4,step*2+106),'#1d282e');d=ImageDraw.Draw(b)
    d.text((24,15),'모자 7종 추가 · 사각 고글 개선',font=font(31),fill='#e5d5a6')
    for i,k in enumerate(KINDS):
        x=(i%4)*w;y=70+(i//4)*step
        d.text((x+18,y+4),NAMES[k],font=font(24),fill='#e5d5a6' if k!='before' else '#bac9ce')
        d.text((x+18,y+43),SUB[k],font=font(17),fill='#bac9ce')
        b.paste(read(light,k,view).resize((w,h),Image.Resampling.LANCZOS),(x,y+82))
    note='모자별 확대 시점 · 토끼귀를 포함하도록 크기 조정' if view=='detail' else '동일 몸·조끼·카메라·조명으로 비교'
    d.text((24,b.height-30),'실제 Blender 렌더 · '+note+' · 게임 미적용',font=font(18),fill='#bac9ce')
    b.save(OUT/filename)
grid('neutral','detail','headwear-details.png')
grid('neutral','quarter','headwear-neutral.png')
grid('mood','quarter','headwear-mood.png')
grid('mood','small','headwear-small.png')

b=Image.new('RGB',(1440,980),'#1d282e');d=ImageDraw.Draw(b)
for i,k in enumerate(('before','goggles')):
    d.text((i*720+24,16),NAMES[k],font=font(32),fill='#e5d5a6')
    d.text((i*720+24,63),SUB[k],font=font(23),fill='#bac9ce')
    b.paste(read('neutral',k,'detail'),(i*720,108))
d.text((24,951),'실제 모델 비교 · 얇은 사각 판 → 좌우 곡면 렌즈와 둥근 프레임',font=font(20),fill='#bac9ce')
b.save(OUT/'goggles-before-after.png')

for kind in KINDS[1:]:
    b=Image.new('RGB',(1440,1495),'#1d282e');d=ImageDraw.Draw(b)
    d.text((24,16),NAMES[kind]+' / 방향별 확인',font=font(33),fill='#e5d5a6')
    for (v,label),(x,y) in zip([('quarter','사선'),('front','정면'),('side','측면'),('rear','후면'),('top','위쪽')],[(0,90),(480,90),(960,90),(0,730),(480,730)]):
        d.text((x+18,y),label,font=font(23),fill='#c5d0d5')
        b.paste(read('neutral',kind,v).resize((480,560),Image.Resampling.LANCZOS),(x,y+38))
    d.text((24,1410),SUB[kind],font=font(25),fill='#e5d5a6')
    d.text((24,1460),'기존 몸·조끼 유지 / 머리 장비만 교체 / 게임 미적용',font=font(20),fill='#bac9ce')
    b.save(OUT/f'{kind}-overview.png')
print('HEADWEAR_COMPARISONS_READY: 12 boards')
