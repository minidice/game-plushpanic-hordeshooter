import cv2
from PIL import Image,ImageDraw
from pathlib import Path
out=Path('D:/Project/Game/plushpanic-outdoor-kit/output/reference')
for j,name in enumerate(['지형참고.mp4','전투및지형참고.mp4']):
 c=cv2.VideoCapture('D:/Recored/커스텀/'+name)
 fps=c.get(cv2.CAP_PROP_FPS); count=c.get(cv2.CAP_PROP_FRAME_COUNT); dur=count/fps
 print(name,'seconds',round(dur,2),'fps',fps,'frames',count)
 sheet=Image.new('RGB',(1440,870),'#182025'); d=ImageDraw.Draw(sheet)
 for i in range(9):
  sec=dur*(i+.5)/9;c.set(cv2.CAP_PROP_POS_MSEC,sec*1000);ok,f=c.read()
  if ok:
   im=Image.fromarray(cv2.cvtColor(f,cv2.COLOR_BGR2RGB));im.thumbnail((480,260));x=(i%3)*480;y=(i//3)*290
   sheet.paste(im,(x,y));d.text((x+8,y+263),f'{sec:.1f}s',fill='white')
 sheet.save(out/f'video-{j+1}.jpg');c.release()
