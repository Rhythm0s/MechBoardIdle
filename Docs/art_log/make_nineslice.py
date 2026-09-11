# -*- coding: utf-8 -*-
"""9-슬라이스 미리보기 — 원본 64 · 모서리 16 을 임의 크기로 늘린다 (W02 9장)."""
from PIL import Image
def nine(src, w, h, m=16):
    s=src.convert('RGBA'); S=s.size[0]
    out=Image.new('RGBA',(w,h),(0,0,0,0))
    iw,ih=w-2*m, h-2*m; sw=S-2*m
    boxes={'tl':(0,0,m,m),'tr':(S-m,0,S,m),'bl':(0,S-m,m,S),'br':(S-m,S-m,S,S)}
    for k,(x,y) in (('tl',(0,0)),('tr',(w-m,0)),('bl',(0,h-m)),('br',(w-m,h-m))):
        out.paste(s.crop(boxes[k]),(x,y))
    out.paste(s.crop((m,0,S-m,m)).resize((iw,m),Image.NEAREST),(m,0))
    out.paste(s.crop((m,S-m,S-m,S)).resize((iw,m),Image.NEAREST),(m,h-m))
    out.paste(s.crop((0,m,m,S-m)).resize((m,ih),Image.NEAREST),(0,m))
    out.paste(s.crop((S-m,m,S,S-m)).resize((m,ih),Image.NEAREST),(w-m,m))
    out.paste(s.crop((m,m,S-m,S-m)).resize((iw,ih),Image.NEAREST),(m,m))
    return out
