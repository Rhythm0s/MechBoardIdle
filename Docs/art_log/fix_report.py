# -*- coding: utf-8 -*-
"""fix_nineslice.py 가 바꾼 자리를 좌표로 남긴다 (플랜 09-11 「좌표·스크립트 로그」).
쓰기: python Docs/art_log/fix_report.py <원본> <고침본> <낼 파일.md>"""
import sys, io, collections
from PIL import Image

def report(a_path, b_path, out_path, m=16):
    A=Image.open(a_path).convert('RGBA'); B=Image.open(b_path).convert('RGBA')
    S=A.size[0]; pa=A.load(); pb=B.load()
    zones={'모서리':[], '윗변':[], '아랫변':[], '왼변':[], '오른변':[], '가운데':[]}
    def zone(x,y):
        ix = 0 if x<m else (2 if x>=S-m else 1)
        iy = 0 if y<m else (2 if y>=S-m else 1)
        if ix!=1 and iy!=1: return '모서리'
        if iy==0: return '윗변'
        if iy==2: return '아랫변'
        if ix==0: return '왼변'
        if ix==2: return '오른변'
        return '가운데'
    pal_a=set(pa[x,y] for y in range(S) for x in range(S))
    for y in range(S):
        for x in range(S):
            if pa[x,y]!=pb[x,y]: zones[zone(x,y)].append((x,y,pa[x,y],pb[x,y]))
    pal_b=set(pb[x,y] for y in range(S) for x in range(S))
    L=[]
    L.append(u'# 9-슬라이스 수리 기록 — `%s` -> `%s`' % (a_path.split('/')[-1], out_path.split('/')[-1]))
    L.append(u'')
    L.append(u'도구 `Docs/art_log/fix_nineslice.py` · 캔버스 %d · 모서리 여백 %d' % (S, m))
    L.append(u'')
    L.append(u'| 구역 | 바뀐 화소 |')
    L.append(u'|---|---|')
    for k in ('모서리','윗변','아랫변','왼변','오른변','가운데'):
        L.append(u'| %s | **%d** |' % (k, len(zones[k])))
    tot=sum(len(v) for v in zones.values())
    L.append(u'| **합계** | **%d / %d = %.1f%%** |' % (tot, S*S, tot/float(S*S)*100))
    L.append(u'')
    L.append(u'**모서리 %d개 — 0 이어야 규격을 지킨 것이다.**' % len(zones['모서리']))
    L.append(u'**새로 생긴 색 %d개 — 0 이어야 새 그림을 안 그린 것이다.** (원본 %d색 · 고침본 %d색)'
             % (len(pal_b - pal_a), len(pal_a), len(pal_b)))
    L.append(u'')
    L.append(u'## 바뀐 자리 전부 (x, y, 이전 -> 이후)')
    L.append(u'')
    for k in ('윗변','아랫변','왼변','오른변','가운데','모서리'):
        if not zones[k]: continue
        L.append(u'### %s (%d개)' % (k, len(zones[k])))
        L.append(u'')
        L.append(u'```')
        for x,y,o,n in zones[k]:
            L.append(u'(%2d,%2d)  #%02X%02X%02X -> #%02X%02X%02X' % (x,y,o[0],o[1],o[2],n[0],n[1],n[2]))
        L.append(u'```')
        L.append(u'')
    io.open(out_path,'w',encoding='utf-8',newline='\n').write(u'\n'.join(L)+u'\n')
    print('wrote', out_path, 'changed', tot, 'corner', len(zones['모서리']), 'newcolors', len(pal_b-pal_a))

if __name__ == '__main__':
    report(sys.argv[1], sys.argv[2], sys.argv[3])
