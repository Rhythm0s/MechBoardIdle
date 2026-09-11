# -*- coding: utf-8 -*-
"""9-슬라이스 그릇 후보 잣대 (W02 9장 · 원본 64 · 모서리 16).
  center : 가운데 32x32 에서 최빈색과 다른 화소 비율 (0.0 = 완전 평탄)
  edge   : 네 변 띠에서, 늘어나는 축을 따라 최빈 줄과 다른 줄의 비율 (0.0 = 늘려도 안 변함)
둘 다 0.0 이어야 9-슬라이스로 늘릴 때 무늬가 안 생긴다."""
import sys, collections
from PIL import Image

def modal_ratio(cells):
    c = collections.Counter(cells)
    top = c.most_common(1)[0][1]
    return 1.0 - top / float(len(cells))

def measure(path, m=16):
    im = Image.open(path).convert('RGBA'); S = im.size[0]
    px = im.load()
    center = [px[x, y] for y in range(m, S-m) for x in range(m, S-m)]
    cen = modal_ratio(center)
    lines = []
    # top / bottom : 세로 줄(열)들이 서로 같아야 한다
    for y0 in (0, S-m):
        lines.append(modal_ratio([tuple(px[x, y] for y in range(y0, y0+m)) for x in range(m, S-m)]))
    # left / right : 가로 줄(행)들이 서로 같아야 한다
    for x0 in (0, S-m):
        lines.append(modal_ratio([tuple(px[x, y] for x in range(x0, x0+m)) for y in range(m, S-m)]))
    return cen, max(lines), lines

if __name__ == '__main__':
    for p in sys.argv[1:]:
        cen, edge, lines = measure(p)
        print('%-40s center %.3f  edge %.3f  (T %.2f B %.2f L %.2f R %.2f)'
              % (p.split('/')[-1], cen, edge, lines[0], lines[1], lines[2], lines[3]))
