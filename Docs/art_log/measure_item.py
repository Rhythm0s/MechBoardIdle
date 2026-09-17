"""물건 자산이 **작은 칸에서 형태가 남는가**를 잰다. (구현-아트 세션 소유)

돌리는 법 — 리포 루트에서:
    python Docs/art_log/measure_item.py <png> [<png> ...]

**아트는 판정하지 않는다.** 여기 숫자는 사용자가 어디를 볼지 좁히는 데 쓴다.

물건은 보드 칸 위에 **아주 작게** 얹힌다(§73-27 ① 기준 = 20px 상자). 그래서 물건 자산의
관건은 예쁨이 아니라 **줄여도 무엇인지 알아보는가**이다. 재는 것 넷 —

1. **캔버스 · 실루엣 · 여백** — 규격이 64 인가, 형제 자산과 맞는가.

2. **채움률** — 실루엣 상자 안에서 불투명 화소가 차지하는 비율.
   너무 낮으면 20px 로 줄였을 때 **속이 비어 사라진다.**

3. **20px 생존률** — 실제로 20 × 20 으로 NEAREST 축소한 뒤 남는 불투명 화소 수.
   400 칸 중 몇 칸이 켜지는가. **이것이 이 도구의 핵심이다** — 상상이 아니라 실측이다.

4. **20px 에서의 색 수** — 줄인 뒤에도 명암이 남는가. 1 이면 실루엣 한 덩어리로 뭉갠 것이다.

5. **최대 채도** — 무채색을 요구하는 자산에 쓴다.
   ⚠️ **거의 검정은 뺀다**(`DARK_SUM`). 2026-09-16 에 포탄 후보 열여섯을
   전부 「색 있음」으로 **오판**했는데, 그 값은 **`#030302`** 에서 나온 것이었다 —
   3·3·2 라는 **한 눈금 차이가 채도 (3−2)/3 = 0.33** 이 된다. 눈에는 그냥 검정이다.
   **어두운 색에서는 채도·색상이 믿을 값이 못 된다** — 분모가 작아 한 눈금이 큰 비율이 된다.
   같은 함정을 `measure_bg.py` 는 **명도 조건**으로 막았다(갈색 = 어두운 주황).
"""
import colorsys
import os
import sys

from PIL import Image

ALPHA = 16
BOX = 20

# 채도를 쟰 때 R+G+B 가 이 값 이하인 색은 **뺀다** — 거의 검정이라
# 한 눈금 차이가 큰 채도로 읽힌다. 24 는 평균 8 수준의 진한 검정이며,
# `#030302`(합 8) 는 빼고 `#101010`(합 48) 은 남기는 자리다.
DARK_SUM = 24


def measure(path):
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    a = im.split()[-1].load()
    px = im.load()
    pts = [(x, y) for y in range(h) for x in range(w) if a[x, y] > ALPHA]
    if not pts:
        return None
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    x0, x1, y0, y1 = min(xs), max(xs), min(ys), max(ys)
    sw, sh = x1 - x0 + 1, y1 - y0 + 1
    fill = len(pts) / float(sw * sh) * 100.0

    # 실루엣만 잘라 20px 상자에 맞춰 줄인다 — 칸 위에 얹히는 그대로
    crop = im.crop((x0, y0, x1 + 1, y1 + 1))
    s = max(sw, sh)
    small = crop.resize((max(1, BOX * sw // s), max(1, BOX * sh // s)), Image.NEAREST)
    sa = small.split()[-1].load()
    sp = small.load()
    live = [(x, y) for y in range(small.height) for x in range(small.width)
            if sa[x, y] > ALPHA]
    cols = {sp[x, y][:3] for x, y in live}

    lively = [c for c in {px[x, y][:3] for x, y in pts} if sum(c) > DARK_SUM]
    sat = max((colorsys.rgb_to_hsv(*[q / 255.0 for q in c])[1] for c in lively),
              default=0.0)

    return {
        "sat": sat,
        "canvas": (w, h), "sil": (sw, sh),
        "margin": (x0, w - 1 - x1, y0, h - 1 - y1),
        "fill": fill, "small": (small.width, small.height),
        "live": len(live), "cells": small.width * small.height,
        "cols": len(cols),
    }


if __name__ == "__main__":
    print("%-24s %8s %9s %-14s %6s %14s %5s %6s" % (
        "file", "캔버스", "실루엣", "여백 L R T B", "채움", "20px 생존", "색", "채도"))
    for p in sys.argv[1:]:
        r = measure(p)
        if r is None:
            print("%-24s  (빈 그림)" % os.path.basename(p))
            continue
        print("%-24s %4dx%-4d %4dx%-4d %2d %2d %2d %2d  %5.1f%%  %3d/%-3d (%2dx%-2d) %4d %6.2f" % (
            os.path.basename(p), r["canvas"][0], r["canvas"][1],
            r["sil"][0], r["sil"][1],
            r["margin"][0], r["margin"][1], r["margin"][2], r["margin"][3],
            r["fill"], r["live"], r["cells"], r["small"][0], r["small"][1],
            r["cols"], r["sat"]))
