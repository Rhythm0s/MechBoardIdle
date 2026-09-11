# -*- coding: utf-8 -*-
"""그릇 후보 시트 — 원본 4배 · 이름 · 변 늘림 왜곡값."""
import sys, glob, os, collections
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from measure_plate import measure

def sheet(files, out, cols=4, z=4):
    cell = 64*z; pad = 8; lab = 16
    rows = (len(files)+cols-1)//cols
    W = cols*(cell+pad)+pad; H = rows*(cell+pad+lab)+pad
    im = Image.new('RGBA',(W,H),(24,24,26,255)); d = ImageDraw.Draw(im)
    for i,f in enumerate(files):
        r,c = divmod(i,cols)
        x = pad+c*(cell+pad); y = pad+r*(cell+pad+lab)
        s = Image.open(f).convert('RGBA').resize((cell,cell),Image.NEAREST)
        im.paste(s,(x,y),s)
        cen,edge,_ = measure(f)
        d.text((x,y+cell+3), '%s  cen %.2f edge %.2f'
               % (os.path.basename(f)[:-4], cen, edge), fill=(220,216,208,255))
    im.save(out); print(out, im.size)

if __name__ == '__main__':
    sheet(sorted(glob.glob(sys.argv[1])), sys.argv[2])
