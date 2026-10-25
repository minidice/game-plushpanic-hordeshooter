from PIL import Image,ImageOps,ImageDraw
from pathlib import Path
p=Path(__file__).resolve().parents[1]/'output'
species=['eagle','parrot','lion','tiger','reindeer','rhino','zombie']
out=Image.new('RGB',(7*280,4*350),'#deded5');d=ImageDraw.Draw(out)
for x,s in enumerate(species):
 for y,v in enumerate(['front','quarter','side','back']):
  f=p/f'{s}-{v}.png'
  if f.exists():
   im=Image.open(f);im.thumbnail((280,326));out.paste(im,(x*280,y*350+24));d.text((x*280+8,y*350+4),f'{s} {v}',fill='#26313b')
out.save(p/'contact-sheet.png')
names=['eagle-armed','parrot-armed','lion-armed','tiger-armed','reindeer-armed','rhino-armed',
       'zombie-chinos','zombie-torn','zombie-overalls','zombie-suit','zombie-bare','zombie-eyepatch']
out=Image.new('RGB',(6*280,2*350),'#deded5');d=ImageDraw.Draw(out)
for i,n in enumerate(names):
 im=Image.open(p/(n+'.png'));im.thumbnail((280,326))
 out.paste(im,((i%6)*280,(i//6)*350+24));d.text(((i%6)*280+8,(i//6)*350+4),n,fill='#26313b')
out.save(p/'variants-sheet.png')
