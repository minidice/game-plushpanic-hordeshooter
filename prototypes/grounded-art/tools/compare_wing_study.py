"""Arrange actual Blender renders, without retouching model appearance."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'output/wing-study'
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',28)
small=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',21)
def board(view,title,subtitle):
    result=Image.new('RGB',(1464,960),'#1d282e');d=ImageDraw.Draw(result)
    d.text((24,12),title,font=font,fill='#e5d5a6')
    d.text((24,54),subtitle,font=small,fill='#d5dedf')
    for i,kind in enumerate(('before','after')):
        result.paste(Image.open(OUT/f'{kind}-{view}.png').convert('RGB'),(i*732+6,116))
        d.text((i*732+24,83),'기존 날개' if i==0 else '개선 후보 · 접힌 날개',font=small,fill='white')
    result.save(OUT/f'comparison-{view}.png')
board('quarter','날개 전후 비교','몸·복장·재질·조명·카메라는 동일 / 날개 형태만 변경')
board('detail','날개 형태 확대','좁은 어깨 연결 → 납작한 면 → 둥근 깃털 끝 세 갈래')
board('side','측면 비교','옆으로 튀어나온 두께를 줄이고 아래로 이어지는 실루엣을 보강')
board('top','위쪽 시점 비교','실제 모델 렌더 / 게임 내 카메라 캡처 아님')
board('held','무기 착용 형태 미리보기','Blender에서 배치한 참고 포즈 / 게임의 별도 무기 팔 생성 코드에는 미적용')
print('WING_COMPARISONS_READY')
