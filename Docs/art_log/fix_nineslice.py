# -*- coding: utf-8 -*-
"""9-슬라이스가 되도록 그릇 자산을 고친다 (사용자 지시 09-11 「유사하기만 하면 됨」).
  변 조각 : 늘어나는 축을 따라 최빈 줄 하나를 골라 그 줄로 전부 채운다 (새 색 0 · 원본 팔레트만 씀)
  가운데   : 최빈색 하나로 채운다
  모서리   : 손대지 않는다
결과는 변 표준편차 0.000 · 가운데 표준편차 0.000 이 되어 어떤 크기로 늘려도 무늬가 안 생긴다."""
import sys, collections
from PIL import Image

def fix(src, m=16):
    im = src.convert('RGBA').copy(); S = im.size[0]; px = im.load()
    # 위·아래 : 열 단위
    for y0 in (0, S-m):
        cols = [tuple(px[x, y0+k] for k in range(m)) for x in range(m, S-m)]
        best = collections.Counter(cols).most_common(1)[0][0]
        for x in range(m, S-m):
            for k in range(m): px[x, y0+k] = best[k]
    # 왼·오른 : 행 단위
    for x0 in (0, S-m):
        rows = [tuple(px[x0+k, y] for k in range(m)) for y in range(m, S-m)]
        best = collections.Counter(rows).most_common(1)[0][0]
        for y in range(m, S-m):
            for k in range(m): px[x0+k, y] = best[k]
    # 가운데
    mid = [px[x, y] for y in range(m, S-m) for x in range(m, S-m)]
    c = collections.Counter(mid).most_common(1)[0][0]
    for y in range(m, S-m):
        for x in range(m, S-m): px[x, y] = c
    return im

if __name__ == '__main__':
    fix(Image.open(sys.argv[1])).save(sys.argv[2])
    print('wrote', sys.argv[2])
