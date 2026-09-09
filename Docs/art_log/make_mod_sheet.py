# -*- coding: utf-8 -*-
"""모듈 기호 후보 시트 — 64 원본 · 4배 확대 · 실루엣. 260909_W01 5장."""
import sys, os
from PIL import Image, ImageDraw, ImageFont
ALPHA=16
def F(s,b=False):
    try: return ImageFont.truetype('C:/Windows/Fonts/malgunbd.ttf' if b else 'C:/Windows/Fonts/malgun.ttf',s)
    except: return ImageFont.load_default()
def mask(im):
    p=im.convert('RGBA').load(); w,h=im.size
    return [[p[x,y][3]>=ALPHA for x in range(w)] for y in range(h)],w,h
def box(m,w,h):
    xs=[x for x in range(w) if any(m[y][x] for y in range(h))]
    ys=[y for y in range(h) if any(m[y])]
    return (min(xs),max(xs),min(ys),max(ys)) if xs else (0,0,0,0)
def foldx(m,w,h):
    """세로축 기준 좌우 접기 일치도 — 1.0 이면 완전 대칭(X 는 여기가 높다)"""
    x0,x1,y0,y1=box(m,w,h); inter=uni=0
    for y in range(y0,y1+1):
        for x in range(x0,x1+1):
            a=m[y][x]; b=m[y][x0+x1-x]
            if a or b: uni+=1
            if a and b: inter+=1
    return inter/uni if uni else 0.0
def foldy(m,w,h):
    x0,x1,y0,y1=box(m,w,h); inter=uni=0
    for y in range(y0,y1+1):
        for x in range(x0,x1+1):
            a=m[y][x]; b=m[y0+y1-y][x]
            if a or b: uni+=1
            if a and b: inter+=1
    return inter/uni if uni else 0.0
def sheet(folder,out,title):
    fs=sorted(f for f in os.listdir(folder) if f.endswith('.png'))
    BG=(24,26,32); FG=(230,232,238); DIM=(148,153,166)
    Z=4; CELL=64*Z; PAD=16; COLS=8; ROWH=CELL+78
    W=PAD+COLS*(CELL+PAD); H=90+2*ROWH
    cv=Image.new('RGB',(W,H),BG); d=ImageDraw.Draw(cv)
    d.text((PAD,20),title,font=F(22,True),fill=FG)
    d.text((PAD,52),'64×64 원본을 4배로 · 알파 문턱 16 · 아래 숫자는 실루엣 크기와 접기 일치도(좌우 | 상하)',font=F(13),fill=DIM)
    rows=[]
    for i,f in enumerate(fs):
        im=Image.open(os.path.join(folder,f)).convert('RGBA')
        m,w,h=mask(im); x0,x1,y0,y1=box(m,w,h)
        fx,fy=foldx(m,w,h),foldy(m,w,h)
        rows.append((f,x1-x0+1,y1-y0+1,fx,fy))
        cx=PAD+(i%COLS)*(CELL+PAD); cy=90+(i//COLS)*ROWH
        flat=Image.new('RGB',(CELL,CELL),(38,41,49))
        flat.paste(im.resize((CELL,CELL),Image.NEAREST),(0,0),im.resize((CELL,CELL),Image.NEAREST))
        cv.paste(flat,(cx,cy)); d.rectangle((cx,cy,cx+CELL,cy+CELL),outline=(60,64,74))
        d.text((cx,cy+CELL+6),f[:-4],font=F(15,True),fill=FG)
        d.text((cx,cy+CELL+28),f'{x1-x0+1}×{y1-y0+1}',font=F(13),fill=DIM)
        col=(255,95,95) if fx>=0.90 else DIM
        d.text((cx,cy+CELL+48),f'접기 {fx*100:.0f} | {fy*100:.0f}',font=F(13),fill=col)
    cv.save(out)
    return rows
if __name__=='__main__':
    rows=sheet(sys.argv[1],sys.argv[2],sys.argv[3])
    for r in rows: print(r[0],r[1],r[2],round(r[3],3),round(r[4],3))
