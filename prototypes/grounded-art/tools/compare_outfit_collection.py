"""Arrange actual Blender renders, without generated or retouched model imagery."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'output/outfit-collection'
NAMES={'basic':'기본 방탄형 04','light':'경량형 05','recon':'정찰형 06','heavy':'중장비형 07'}
DETAILS={'basic':'녹색 투구 · 가로 띠 조끼','light':'고글 모자 · 하네스와 세 개의 파우치',
         'recon':'둥근 투구 · 청회색 조끼와 큰 앞주머니','heavy':'전술 투구 · 높은 보호 면과 옆판'}
def font(n):return ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',n)
def read(lighting,kind,view):return Image.open(OUT/f'{lighting}-{kind}-{view}.png').convert('RGB')

def lineup(kinds,lighting,view,filename,width=720):
    height=round((480 if view=='small' else 840)*width/720)
    board=Image.new('RGB',(len(kinds)*width,height+160),'#1d282e');d=ImageDraw.Draw(board)
    for i,kind in enumerate(kinds):
        x=i*width
        d.text((x+24,16),NAMES[kind],font=font(30),fill='#e5d5a6')
        d.text((x+24,65),DETAILS[kind],font=font(19),fill='#c4d1d5')
        im=read(lighting,kind,view)
        if im.size!=(width,height):im=im.resize((width,height),Image.Resampling.LANCZOS)
        board.paste(im,(x,108))
    d.text((24,height+123),'실제 모델 렌더 · 동일 카메라 / '+('중립 조명' if lighting=='neutral' else '차가운 주변광 + 따뜻한 주광')+' · 배낭 제외 · 게임 미적용',font=font(20),fill='#b0c0c8')
    board.save(OUT/filename)

new=('light','recon','heavy')
for lighting in ('neutral','mood'):
    for view in ('quarter','top'):
        lineup(new,lighting,view,f'new-{lighting}-{view}.png')
    lineup(('basic',)+new,lighting,'quarter',f'lineup-{lighting}.png',540)
lineup(('basic',)+new,'mood','small','lineup-small.png')
for kind in new:
    board=Image.new('RGB',(1440,1515),'#1d282e');d=ImageDraw.Draw(board)
    d.text((24,18),NAMES[kind]+' / 방향별 검토',font=font(34),fill='#e5d5a6')
    for (view,label),(x,y) in zip([('quarter','사선'),('front','정면'),('side','측면'),('rear','후면'),('top','위쪽')],[(0,92),(480,92),(960,92),(0,735),(480,735)]):
        d.text((x+20,y),label,font=font(24),fill='#d6e1e5')
        board.paste(read('neutral',kind,view).resize((480,560),Image.Resampling.LANCZOS),(x,y+40))
    d.text((26,1420),DETAILS[kind],font=font(26),fill='#e5d5a6')
    d.text((26,1470),'몸·표정 유지 / 투구와 조끼 분리 / 기존 게임 및 이전 후보 보존',font=font(21),fill='#afc0c6')
    board.save(OUT/f'{kind}-overview.png')
print('OUTFIT_COMPARISONS_READY: 10 boards')
