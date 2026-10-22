from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'output/fitted-species'
font=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',26);small=ImageFont.truetype('C:/Windows/Fonts/malgun.ttf',19)
names={'goose':'거위 · 기준 핏 유지','duck':'오리','raccoon':'라쿤','redpanda':'레서판다'}
def board(file,title,subtitle,rows):
 cols=len(rows[0]);cw=480;ch=540;im=Image.new('RGB',(cols*cw,len(rows)*(ch+34)+100),'#1d282e');d=ImageDraw.Draw(im)
 d.text((20,10),title,font=font,fill='#e5d5a6');d.text((20,52),subtitle,font=small,fill='#ccd7d9')
 for r,row in enumerate(rows):
  for c,(label,filename) in enumerate(row):
   x=c*cw;y=100+r*(ch+34);d.text((x+16,y),label,font=small,fill='white')
   pic=Image.open(OUT/(filename+'.png')).convert('RGB').resize((cw,ch),Image.Resampling.LANCZOS);im.paste(pic,(x,y+34))
 im.save(OUT/file)
board('fit-comparison.png','종별 조끼 핏 · 같은 크기와 조준 자세','위: 이전 후보 / 아래: 수정 후보 · 거위 기준 유지, 다른 종 목선 조정',[[ (names[s]+(' · 이전' if k=='before' else ' · 수정'),s+'-'+k+'-front') for s in names] for k in ('before','after')])
board('surface-comparison.png','귀·꼬리의 갈라진 표면 복원','같은 조명과 후면 시점 · 원래 윤곽과 줄무늬 유지',[[ (names[s]+(' · 이전' if k=='before' else ' · 수정'),s+'-'+k+'-rear') for s in ('raccoon','redpanda') for k in ('before','after')]])
board('size-comparison.png','몸·복장·장착물 함께 크기 조절','위: 폭 88% / 키 110% · 아래: 폭 112% / 키 90% · 같은 카메라',[[ (names[s],s+'-'+tag) for s in names] for tag in ('slim-tall','wide-short')])
print('FITTED_BOARDS_READY')
