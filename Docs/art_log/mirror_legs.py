# -*- coding: utf-8 -*-
"""규칙 15 — 반대쪽 자세를 **다리 구간만 되접어** 만든다 (새 획 0).

왜 그림 전체를 안 되접는가: 로봇 B 는 좌우 대칭이 아니다(되접기 겹침 0.73).
그림 전체를 뒤집으면 어깨 포드·무장까지 좌우가 바뀌어 걷는 도중 몸이 뒤바뀐다.
그래서 **inpaint 로 고친 다리 띠만** 대칭축으로 되접고 윗몸은 원본 그대로 둔다.

쓰기: python mirror_legs.py <inpaint결과> <원본프레임> <낼파일> <띠 y0> <띠 y1> <축 x>
"""
import sys
from PIL import Image

def mirror_band(edited, base, y0, y1, axis):
    out = base.convert('RGBA').copy()
    W, H = out.size
    band = edited.convert('RGBA').crop((0, y0, W, y1))
    flipped = band.transpose(Image.FLIP_LEFT_RIGHT)
    # 되접기 축을 실루엣 축에 맞춘다 — 좌우로 (2*axis - (W-1)) 만큼 민다
    shift = int(round(2*axis - (W-1)))
    canvas = Image.new('RGBA', (W, y1-y0), (0,0,0,0))
    canvas.paste(flipped, (shift, 0))
    # 띠 구간을 통째로 갈아 끼운다 (윗몸은 원본 유지)
    out.paste(Image.new('RGBA', (W, y1-y0), (0,0,0,0)), (0, y0))
    out.paste(canvas, (0, y0), canvas)
    return out

if __name__ == '__main__':
    ed, ba, dst = sys.argv[1], sys.argv[2], sys.argv[3]
    y0, y1, axis = int(sys.argv[4]), int(sys.argv[5]), float(sys.argv[6])
    mirror_band(Image.open(ed), Image.open(ba), y0, y1, axis).save(dst)
    print('wrote', dst, 'band y%d~%d axis %.1f' % (y0, y1, axis))
