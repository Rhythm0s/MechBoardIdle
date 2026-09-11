# -*- coding: utf-8 -*-
"""배경 타일 잣대 — 명도 · 채도 · 이음매.
  평균 휘도  : 0.299R+0.587G+0.114B 평균, 알파 문턱 16 위 화소만 (%로 환산)
  최대 채도  : HSV S 의 상위 1% 값 (저채도 확인용)
  주황 화소  : 색상 10~45도 · 채도 0.45 위 · 명도 0.55 위 화소 비율. 명도 조건이 없으면 갈색이 전부 걸린다(갈색 = 어두운 주황 색상)
  이음매 LR  : 오른쪽 끝 열과 왼쪽 첫 열의 평균 화소차 (한 바퀴 감았을 때 생기는 단차)
  이음매 TB  : 아래 끝 행과 위 첫 행의 평균 화소차
  이음매는 이웃 열/행 사이 평균 화소차와 나란히 적는다 — 같은 수준이면 이음매가 안 보인다."""
import sys, os, colorsys
from PIL import Image

def measure(path):
    im = Image.open(path).convert('RGBA'); W,H = im.size; px = im.load()
    lum=[]; sat=[]; orange=0; n=0
    for y in range(H):
        for x in range(W):
            r,g,b,a = px[x,y]
            if a < 16: continue
            n += 1
            lum.append(0.299*r+0.587*g+0.114*b)
            h,s,v = colorsys.rgb_to_hsv(r/255.,g/255.,b/255.)
            sat.append(s)
            if 10/360. <= h <= 45/360. and s > 0.45 and v > 0.55: orange += 1
    sat.sort()
    def coldiff(c0,c1):
        return sum(abs(px[c0,y][i]-px[c1,y][i]) for y in range(H) for i in range(3))/float(H*3)
    def rowdiff(r0,r1):
        return sum(abs(px[x,r0][i]-px[x,r1][i]) for x in range(W) for i in range(3))/float(W*3)
    nb_c = sum(coldiff(x,x+1) for x in range(W-1))/float(W-1)
    nb_r = sum(rowdiff(y,y+1) for y in range(H-1))/float(H-1)
    return dict(lum=sum(lum)/n, lumpct=sum(lum)/n/255*100,
                sat99=sat[int(len(sat)*0.99)], orange=orange/float(n)*100,
                seam_lr=coldiff(W-1,0), nb_lr=nb_c,
                seam_tb=rowdiff(H-1,0), nb_tb=nb_r)

if __name__ == '__main__':
    for p in sys.argv[1:]:
        m = measure(p)
        print('%-24s lum %5.1f (%4.1f%%)  sat99 %.2f  orange %4.1f%%  seam LR %5.2f (nb %4.2f)  TB %5.2f (nb %4.2f)'
              % (os.path.basename(p), m['lum'], m['lumpct'], m['sat99'], m['orange'],
                 m['seam_lr'], m['nb_lr'], m['seam_tb'], m['nb_tb']))
