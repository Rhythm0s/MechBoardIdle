# -*- coding: utf-8 -*-
"""효과음 자르기 — 머리를 남기고 꼬리를 코사인으로 닫는다 (sfx_fire_a 09-10 방식 그대로).

  남기는 것 : 앞 `keep` 초
  페이드     : 끝 `fade` 초에 0.5*(1+cos(pi*t)) 를 곱한다.
               양 끝 기울기가 0 이라 이어 붙는 자리에서 딸깍이 안 난다.
  안 건드리는 것 : 표본율 · 채널 수 · 음량(평준화본 위에서 자르므로 이득을 다시 안 먹인다)

쓰기: python Docs/art_log/cut_sfx.py <파일> [--keep 0.5] [--fade 0.10] [--apply]
      --apply 가 없으면 버릴 구간의 세기만 재서 보여 준다.
"""
import sys, math, os
import soundfile as sf

def analyse(path, keep, fade):
    d, sr = sf.read(path, always_2d=True)
    m = d.mean(axis=1); n = len(m)
    k = min(n, int(keep*sr))
    if k >= n: return None
    pk_all = max(abs(m.min()), abs(m.max()))
    tail = m[k:]
    pk_tail = max(abs(tail.min()), abs(tail.max()))
    return dict(sr=sr, dur=n/sr, keep=k/sr, cut=(n-k)/sr,
                tail_ratio=pk_tail/pk_all if pk_all else 0.0,
                head_peak_at=(abs(m[:k]).argmax())/sr)

def cut(path, keep, fade):
    d, sr = sf.read(path, always_2d=True)
    k = min(len(d), int(keep*sr))
    out = d[:k].copy()
    f = min(k, int(fade*sr))
    for i in range(f):
        t = i/float(f)
        out[k-f+i] *= 0.5*(1+math.cos(math.pi*t))
    sf.write(path, out, sr, format='OGG', subtype='VORBIS')
    chk, _ = sf.read(path, always_2d=True)
    return k/sr, float(abs(chk[-1]).max())

if __name__ == '__main__':
    p = sys.argv[1]
    keep = float(sys.argv[sys.argv.index('--keep')+1]) if '--keep' in sys.argv else 0.5
    fade = float(sys.argv[sys.argv.index('--fade')+1]) if '--fade' in sys.argv else 0.10
    a = analyse(p, keep, fade)
    if a is None:
        print(os.path.basename(p), '이미 상한 안 - 자를 것 없음'); raise SystemExit
    print('%-18s %.2f초 -> %.2f초 (버림 %.2f초) · 버리는 구간 최대가 전체의 %.1f%% · 머리 최대는 %.3f초'
          % (os.path.basename(p)[:-4], a['dur'], a['keep'], a['cut'], a['tail_ratio']*100, a['head_peak_at']))
    if '--apply' in sys.argv:
        dur, last = cut(p, keep, fade)
        print('  잘랐다 — 길이 %.3f초 · 마지막 표본 진폭 %.2f' % (dur, last))
