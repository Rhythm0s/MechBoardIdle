# -*- coding: utf-8 -*-
"""화살촉 자리를 비운다 — 같은 홈의 깨끗한 조각을 복사해 덮는다.
새 화소를 그리지 않고 그 홈에 이미 있는 줄을 늘려 채울 뿐이라 색·무늬가 그대로 남는다.
화살표는 별도 자산으로 뽑아 코드가 방향에 맞춰 얹는다 (사용자 제안 2026-09-14)."""
import sys, colorsys
from PIL import Image

def is_arrow(c):
    if c[3] < 16: return False
    h,s,v = colorsys.rgb_to_hsv(c[0]/255., c[1]/255., c[2]/255.)
    return 15/360. <= h <= 45/360. and s > 0.35 and v > 0.45

def clear(im, minwidth=5):
    W,H = im.size; px = im.load()
    pts = [(x,y) for y in range(H) for x in range(W) if is_arrow(px[x,y])]
    quad = {'T':[], 'B':[], 'L':[], 'R':[]}
    for x,y in pts:
        dx,dy = x-W/2., y-H/2.
        if abs(dy) > abs(dx): quad['T' if dy<0 else 'B'].append((x,y))
        else: quad['L' if dx<0 else 'R'].append((x,y))
    out = im.copy(); changed = 0
    for s,v in quad.items():
        if not v: continue
        if s in 'TB':
            rows = {}
            for x,y in v: rows[y] = rows.get(y,0)+1
            band = sorted(y for y,n in rows.items() if n >= minwidth)
            if not band: continue
            y0,y1 = max(0, band[0]-3), min(H-1, band[-1]+3)   # 꼬리까지 넉넉히 문다
            # 띠 바깥에서 가장 가까운 깨끗한 줄 하나를 골라 그 줄로 띠 전체를 채운다
            src = y0-1 if y0-1 >= 0 and rows.get(y0-1,0) < minwidth else y1+1
            src = max(0, min(H-1, src))
            xs = [x for x,y in v]
            x0,x1 = min(xs), max(xs)
            line = im.crop((x0, src, x1+1, src+1))
            for y in range(y0, y1+1):
                out.paste(line, (x0, y)); changed += (x1-x0+1)
        else:
            cols = {}
            for x,y in v: cols[x] = cols.get(x,0)+1
            band = sorted(x for x,n in cols.items() if n >= minwidth)
            if not band: continue
            x0,x1 = max(0, band[0]-3), min(W-1, band[-1]+3)   # 꼬리까지 넉넉히 문다
            src = x0-1 if x0-1 >= 0 and cols.get(x0-1,0) < minwidth else x1+1
            src = max(0, min(W-1, src))
            ys = [y for x,y in v]
            y0,y1 = min(ys), max(ys)
            line = im.crop((src, y0, src+1, y1+1))
            for x in range(x0, x1+1):
                out.paste(line, (x, y0)); changed += (y1-y0+1)
    return out

if __name__ == '__main__':
    clear(Image.open(sys.argv[1]).convert('RGBA')).save(sys.argv[2])
    print('wrote', sys.argv[2])
