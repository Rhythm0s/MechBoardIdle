"""정면에서 보는가, 비스듬히 돌아 있는가를 잰다. (구현-아트 세션 소유)

돌리는 법 — 리포 루트에서:
    python Docs/art_log/measure_frontness.py <png> [<png> ...]

**아트는 판정하지 않는다.** 여기 숫자는 사용자가 어디를 볼지 좁히는 데 쓴다.

09-15 사용자 반려: 「현행 보병이 **아이소메트릭으로 읽힌다**」. 「아이소로 읽힌다」를
눈이 아니라 수로 붙들려고 만들었다. 아이소의 표시는 둘이다 —

1. **좌우 대칭도** — 실루엣을 제 가로 중심에 대해 뒤집어 겹친 IoU.
   정면이면 좌우가 거울이라 높고, 바닥면이 돌아 있으면 한쪽이 더 넓게 보여 낮아진다.

2. **두 발 높이 차** — 실루엣 아래쪽 15% 띠를 가로 중심으로 갈라 좌우의 **가장 아래 y** 를 뺀다.
   **아이소는 한 발이 반드시 더 낮다** — 바닥면이 기울어 보이기 때문이다.
   정면·탑다운이면 두 발이 같은 줄에 선다.

곁들여 **여백 L R T B** 를 낸다. FRAMING 절이 「아래 여백 = 위 여백 · 왼 = 오른」을 요구하므로
좌우 여백 차도 정면의 방증이다.
"""
import os
import sys

from PIL import Image

ALPHA = 16


def measure(path):
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    a = im.split()[-1].load()
    m = {(x, y) for y in range(h) for x in range(w) if a[x, y] > ALPHA}
    if not m:
        return None
    xs = [p[0] for p in m]
    ys = [p[1] for p in m]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)

    # 1. 좌우 대칭도 — 실루엣 제 중심에 대해 뒤집는다
    flip = {(x0 + x1 - x, y) for x, y in m}
    sym = len(m & flip) / float(len(m | flip))

    # 2. 두 발 높이 차 — 아래 15% 띠
    band_top = y1 - int((y1 - y0) * 0.15)
    cx = (x0 + x1) / 2.0
    lo = [y for x, y in m if y >= band_top and x < cx]
    ro = [y for x, y in m if y >= band_top and x >= cx]
    feet = (max(lo) - max(ro)) if (lo and ro) else None

    return {
        "sym": sym, "feet": feet,
        "sil": (x1 - x0 + 1, y1 - y0 + 1),
        "margin": (x0, w - 1 - x1, y0, h - 1 - y1),
        "canvas": (w, h),
    }


if __name__ == "__main__":
    print("%-44s %7s %6s %11s %-16s" % (
        "file", "대칭도", "발 차", "실루엣", "여백 L R T B"))
    for p in sys.argv[1:]:
        r = measure(p)
        if r is None:
            print("%-44s  (빈 그림)" % os.path.basename(p))
            continue
        print("%-44s %7.3f %6s %5dx%-5d %2d %2d %2d %2d" % (
            os.path.basename(p), r["sym"],
            ("%d" % r["feet"]) if r["feet"] is not None else "-",
            r["sil"][0], r["sil"][1],
            r["margin"][0], r["margin"][1], r["margin"][2], r["margin"][3]))
