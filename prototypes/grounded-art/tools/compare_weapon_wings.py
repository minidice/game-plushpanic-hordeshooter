"""Label and arrange actual pose renders; no retouching of model pixels."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'output/weapon-wings'
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',28);small=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',21)
names={'ready':'무기 대기','aim':'수평 조준','aim_up':'위쪽 조준'}
def pair(pose,view):
    im=Image.new('RGB',(1464,960),'#1d282e');d=ImageDraw.Draw(im)
    d.text((24,12),names[pose]+' · 날개 전후 비교',font=font,fill='#e5d5a6')
    d.text((24,54),'동일한 총 위치·몸·장비·조명 / 별도 Blender 후보, 게임 미적용',font=small,fill='#d5dedf')
    for i,kind in enumerate(('before','after')):
        im.paste(Image.open(OUT/f'{pose}-{kind}-{view}.png').convert('RGB'),(i*732+6,116))
        d.text((i*732+24,83),'기존 후보 · 통째 회전' if i==0 else '개선 후보 · 굽힘 + 깃털 끝 감싸기',font=small,fill='white')
    im.save(OUT/f'comparison-{pose}-{view}.png')
for pose in names:
    for view in ('quarter','side','top'):pair(pose,view)
    if pose!='aim_up':pair(pose,'detail')
im=Image.new('RGB',(1476,696),'#1d282e');d=ImageDraw.Draw(im)
d.text((24,12),'무기 전용 날개 · 세 자세',font=font,fill='#e5d5a6')
d.text((24,54),'납작한 접힌 면 + 손잡이를 감싸는 깃털 / 정적 후보, 애니메이션 미적용',font=small,fill='#d5dedf')
for i,pose in enumerate(names):
    d.text((i*492+24,91),names[pose],font=font,fill='white')
    image=Image.open(OUT/f'{pose}-after-quarter.png').convert('RGB').resize((480,560),Image.Resampling.LANCZOS)
    im.paste(image,(i*492+6,132))
im.save(OUT/'pose-lineup.png')
print('WEAPON_WINGS_COMPARISONS_READY')
