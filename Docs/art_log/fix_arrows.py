# -*- coding: utf-8 -*-
"""화살촉 방향만 뒤집는다 — 화살촉이 차지한 띠를 제자리에서 되접는다.
새 화소를 그리지 않고 있던 화소를 거울로 뒤집을 뿐이라 색·모양이 그대로 남는다."""
import sys, colorsys
from PIL import Image

def is_arrow(c):
    if c[3] < 16: return False
    h,s,v = colorsys.rgb_to_hsv(c[0]/255., c[1]/255., c[2]/255.)
    return 15/360. <= h <= 45/360. and s > 0.35 and v > 0.45

def flip(im, sides, minwidth=5):
    """sides: 'T','B' 는 위아래로, 'L','R' 는 좌우로 되접는다."""
    W,H = im.size; px = im.load()
    pts = [(x,y) for y in range(H) for x in range(W) if is_arrow(px[x,y])]
    quad = {'T':[], 'B':[], 'L':[], 'R':[]}
    for x,y in pts:
        dx,dy = x-W/2., y-H/2.
        if abs(dy) > abs(dx): quad['T' if dy<0 else 'B'].append((x,y))
        else: quad['L' if dx<0 else 'R'].append((x,y))
    out = im.copy()
    for s in sides:
        v = quad[s]
        if not v: continue
        if s in 'TB':
            rows = {}
            for x,y in v: rows.setdefault(y,0); rows[y]+=1
            band = sorted(y for y,n in rows.items() if n >= minwidth)
            if not band: continue
            y0,y1 = band[0], band[-1]
            xs = [x for x,y in v if y0 <= y <= y1]
            x0,x1 = min(xs), max(xs)
            box = (x0, y0, x1+1, y1+1)
            out.paste(im.crop(box).transpose(Image.FLIP_TOP_BOTTOM), box)
        else:
            cols = {}
            for x,y in v: cols.setdefault(x,0); cols[x]+=1
            band = sorted(x for x,n in cols.items() if n >= minwidth)
            if not band: continue
            x0,x1 = band[0], band[-1]
            ys = [y for x,y in v if x0 <= x <= x1]
            y0,y1 = min(ys), max(ys)
            box = (x0, y0, x1+1, y1+1)
            out.paste(im.crop(box).transpose(Image.FLIP_LEFT_RIGHT), box)
    return out

if __name__ == '__main__':
    src, dst, sides = sys.argv[1], sys.argv[2], sys.argv[3]
    flip(Image.open(src).convert('RGBA'), sides).save(dst)
    print('wrote', dst, 'sides', sides)
