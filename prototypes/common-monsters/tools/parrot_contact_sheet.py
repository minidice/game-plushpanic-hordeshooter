"""Assemble the same-camera review renders after review_parrot.py before/after runs."""
import argparse
from pathlib import Path
from PIL import Image,ImageDraw

parser=argparse.ArgumentParser()
parser.add_argument('--species',choices=['parrot','eagle','lion','tiger','reindeer','rhino','zombie'],default='parrot')
parser.add_argument('--review-name',help='Optional local evidence folder name')
args=parser.parse_args()
folder=Path(__file__).resolve().parents[1]/'output'/(args.review_name or f'{args.species}-quality')
views=['color-quarter','neutral-quarter','silhouette-side','vest-quarter','gameplay']
sheet=Image.new('RGB',(5*288,2*366),'#deded5')
draw=ImageDraw.Draw(sheet)
for row,label in enumerate(['before','after']):
    for column,view in enumerate(views):
        with Image.open(folder/f'{args.species}-{label}-{view}.png') as source:
            source.thumbnail((288,336))
            sheet.paste(source,(column*288,row*366+28))
        draw.text((column*288+8,row*366+8),f'{label} / {view}',fill='#26313b')
sheet.save(folder/'comparison-sheet.png')
print(folder/'comparison-sheet.png')
