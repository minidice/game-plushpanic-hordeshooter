"""Comparison sheets composed from actual Blender renders only."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'output/species-weapon-grips'
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',27);small=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',20)
species={'duck':'오리 · 접힌 날개','raccoon':'라쿤 · 짧고 가는 앞발','redpanda':'레서판다 · 둥근 앞발'}
poses={'ready':'무기 대기','aim':'수평 조준','aim_up':'위쪽 조준'}
for s,name in species.items():
    for view in ('quarter','detail'):
        im=Image.new('RGB',(1464,960),'#1d282e');d=ImageDraw.Draw(im)
        d.text((24,12),name+' · 조준 전후',font=font,fill='#e5d5a6')
        d.text((24,53),'같은 몸·조끼·총 위치·조명 / 별도 정적 후보, 게임 미적용',font=small,fill='#d5dedf')
        for i,kind in enumerate(('before','after')):
            im.paste(Image.open(OUT/f'{s}-aim-{kind}-{view}.png').convert('RGB'),(i*732+6,116))
            d.text((i*732+24,84),'기존 원본 팔 · 통째 회전' if i==0 else '개선 후보 · 종별 형태 + 손잡이 감싸기',font=small,fill='white')
        im.save(OUT/f'comparison-{s}-{view}.png')

def row(pose,title):
    im=Image.new('RGB',(1476,696),'#1d282e');d=ImageDraw.Draw(im)
    d.text((24,12),title,font=font,fill='#e5d5a6')
    d.text((24,53),'동일한 M4와 기본 조끼 / 몸·얼굴·귀·꼬리는 원본 형상 보존',font=small,fill='#d5dedf')
    for i,(s,name) in enumerate(species.items()):
        d.text((i*492+20,91),name,font=small,fill='white')
        pic=Image.open(OUT/f'{s}-{pose}-after-quarter.png').convert('RGB').resize((480,560),Image.Resampling.LANCZOS)
        im.paste(pic,(i*492+6,132))
    im.save(OUT/f'lineup-{pose}.png')
for pose,label in poses.items():row(pose,'종별 무기 잡는 형태 · '+label)
im=Image.new('RGB',(1476,1924),'#1d282e');d=ImageDraw.Draw(im)
d.text((24,12),'세 종 × 세 자세 · 독립 모델 후보',font=font,fill='#e5d5a6')
d.text((24,53),'오리: 깃털 세 갈래 / 라쿤: 좁은 앞발 / 레서판다: 넓고 둥근 앞발',font=small,fill='#d5dedf')
for col,(s,name) in enumerate(species.items()):d.text((col*492+24,90),name,font=small,fill='white')
for row,(pose,label) in enumerate(poses.items()):
    y=132+row*596
    for col,s in enumerate(species):
        d.text((col*492+24,y),label,font=small,fill='#e5d5a6')
        pic=Image.open(OUT/f'{s}-{pose}-after-quarter.png').convert('RGB').resize((480,560),Image.Resampling.LANCZOS)
        im.paste(pic,(col*492+6,y+30))
im.save(OUT/'all-poses.png')
print('SPECIES_GRIP_COMPARISONS_READY')
