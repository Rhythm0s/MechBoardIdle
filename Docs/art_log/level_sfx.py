# -*- coding: utf-8 -*-
"""효과음 평준화 — 머리 0.5초의 RMS 를 공통 목표로 맞춘다 (사용자 지시 2026-09-14).

왜 머리 0.5초인가: 소리의 세기는 사람이 **첫 순간**으로 판단하고, 꼬리 울림은 길이만 늘린다.
전체 RMS 로 맞추면 꼬리가 긴 소리가 과하게 증폭된다.

왜 「가장 조용한 것」이 아니라 계산된 목표인가: 목표를 너무 높이면 조용한 파일이 **클리핑**한다.
그래서 목표 = min(각 파일이 최대 0.95 까지 올릴 수 있는 RMS) 로 잡는다 — 아무도 안 깨지는 가장 높은 자리다.
"""
import glob, os, math, sys
import soundfile as sf

HEAD = 0.5
CEIL = 0.95

def head_rms(m, sr):
    k = min(len(m), int(HEAD*sr))
    return math.sqrt(sum(v*v for v in m[:k])/k)

def run(paths, apply=False):
    info = []
    for p in paths:
        d, sr = sf.read(p, always_2d=True)
        m = d.mean(axis=1)
        pk = max(abs(m.min()), abs(m.max()))
        info.append((p, d, sr, head_rms(m, sr), pk))
    # 아무도 안 깨지는 가장 높은 목표
    target = min(r * (CEIL/pk) for _,_,_,r,pk in info)
    out = []
    for p, d, sr, r, pk in info:
        g = target / r
        if pk*g > CEIL: g = CEIL/pk          # 안전망
        out.append((os.path.basename(p), r, pk, g, 20*math.log10(g), pk*g))
        if apply:
            sf.write(p, d*g, sr, format='OGG', subtype='VORBIS')
    return target, out

if __name__ == '__main__':
    paths = sorted(glob.glob('Assets/_Project/Audio/sfx/*.ogg'))
    t, rows = run(paths, apply=('--apply' in sys.argv))
    print('목표 머리 RMS %.4f (클리핑 없이 도달 가능한 최고치)'%t)
    print('%-18s %8s %7s %8s %9s %8s'%('파일','머리RMS','최대','이득','dB','적용후최대'))
    for n,r,pk,g,db,np_ in rows:
        print('%-18s %8.4f %7.3f %8.2f %+9.1f %8.3f'%(n,r,pk,g,db,np_))
