from PIL import Image,ImageDraw
import json
from pathlib import Path
r=Path('.');a=json.loads((r/'pack/manifest.json').read_text(encoding='utf-8'))['assets'];s=Image.new('RGB',(1400,((len(a)+4)//5)*240),'#1f2b30');d=ImageDraw.Draw(s)
for i,item in enumerate(a):
 im=Image.open(r/'gallery/images'/f"{item['id']}.png");im.thumbnail((280,180));x=i%5*280;y=i//5*240;s.paste(im,(x,y));d.text((x+8,y+185),item['id'],fill='white')
s.save(r/'output/contact-sheet.jpg')
