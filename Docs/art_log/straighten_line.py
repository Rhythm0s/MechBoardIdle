# -*- coding: utf-8 -*-
"""홈 가운데 주황 선을 지우거나, 폭 2의 곧은 줄로 다시 놓는다 (사용자 지시 2026-09-14).
지우기 = 홈 바닥의 이웃 화소로 덮는다(새 색 0).
다시 놓기 = 지운 뒤 원래 팔레트의 최빈 주황 하나로 x 95~96 / y 95~96 을 곧게 채운다."""
import sys, colorsys, collections
from PIL import Image

def is_o(c):
    if c[3] < 16: return False
    h,s,v = colorsys.rgb_to_hsv(c[0]/255., c[1]/255., c[2]/255.)
    return 12/360. <= h <= 50/360. and s > 0.22 and v > 0.35

def run(src, redraw=True, half=2):
    im = src.convert('RGBA').copy(); W,H = im.size; px = im.load()
    pts = [(x,y) for y in range(H) for x in range(W) if is_o(px[x,y])]
    tone = collections.Counter(px[x,y] for x,y in pts).most_common(1)[0][0]
    cx, cy = W//2, H//2
    # 홈 구분은 「가운데에서 얼마나 먼가」가 아니라 「어느 축에 붙어 있는가」로 한다 —
    # 앞 판은 거리로 갈라서 수집조 곁의 화소가 어느 쪽에도 안 들어갔다.
    vert = [(x,y) for x,y in pts if abs(x-cx) <= 8]   # 세로 홈
    horz = [(x,y) for x,y in pts if abs(y-cy) <= 8]   # 가로 홈
    # 1) 지운다 — 홈 바닥의 이웃(주황이 아닌 가장 가까운 화소)으로 덮는다
    for x,y in vert:
        for d in range(1, 12):
            if x+d < W and not is_o(px[x+d,y]): px[x,y] = px[x+d,y]; break
            if x-d >= 0 and not is_o(px[x-d,y]): px[x,y] = px[x-d,y]; break
    for x,y in horz:
        for d in range(1, 12):
            if y+d < H and not is_o(px[x,y+d]): px[x,y] = px[x,y+d]; break
            if y-d >= 0 and not is_o(px[x,y-d]): px[x,y] = px[x,y-d]; break
    if not redraw:
        return im
    # 2) 곧게 다시 놓는다 — 원래 있던 구간에만, 폭 2 로
    # 홈 하나가 쓰는 구간의 처음과 끝만 보고 그 사이를 **끊김 없이** 채운다 —
    # 있던 화소 자리에만 놓으면 화살촉이 있던 자리가 빈 칸으로 남는다(12시 방향이 그랬다).
    def run_(vals, mid):
        a = [v for v in vals if v < mid]
        b = [v for v in vals if v > mid]
        out = []
        if a: out += list(range(min(a), max(a)+1))
        if b: out += list(range(min(b), max(b)+1))
        return out
    for y in run_(sorted(set(y for x,y in vert)), cy):
        for x in range(cx-half//2-1, cx-half//2-1+half): px[x,y] = tone
    for x in run_(sorted(set(x for x,y in horz)), cx):
        for y in range(cy-half//2-1, cy-half//2-1+half): px[x,y] = tone
    return im

if __name__ == '__main__':
    src = Image.open(sys.argv[1])
    run(src, redraw=(sys.argv[3] != 'erase')).save(sys.argv[2])
    print('wrote', sys.argv[2])
