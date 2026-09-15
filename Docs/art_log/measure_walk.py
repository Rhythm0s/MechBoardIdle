"""걷기 한 벌을 잰다 — 다리가 엇갈리는가 · 팔이 움직이는가 · 한 바퀴가 이어지는가.

돌리는 법 — 리포 루트에서:
    python Docs/art_log/measure_walk.py <프레임 폴더> [<프레임 폴더> ...]

**아트는 판정하지 않는다.** 여기 숫자는 사용자가 어디를 볼지 좁히는 데 쓴다.

재는 것 다섯:

1. **실루엣 넓이** — 알파 > 16 인 화소 수. 걷기 한 바퀴에서 거의 안 변해야 한다.
   09-15 템플릿 벌이 이 값 하나로 무너졌다(한 장이 0 · 나머지가 절반).

2. **좌우 발 밑변 차** — 실루엣을 가로 중심으로 갈라 왼쪽·오른쪽의 **가장 아래 y** 를 각각 찾고
   `왼쪽 - 오른쪽` 을 낸다. 한 발이 딛고 다른 발이 들리면 이 값이 0 이 아니다.

3. **부호 반전 수** — 위 값의 부호가 프레임을 돌며 몇 번 뒤집히는가.
   **이것이 「걷는가」의 핵심이다** — 반전이 없으면 두 발이 늘 같은 쪽이라 **미끄러지는 것**이지
   걷는 것이 아니다. 현행 남면이 0/6 이라 이 작업이 시작됐다.

4. **팔 띠 움직임** — 어깨~허리 띠(실루엣 위 25%~55%)에서 이웃 프레임끼리 다른 화소의 비율.
   09-14 반려 사유 「팔이 움직이지 않음」을 숫자로 두려고 넣었다. 마스크로 아래만 뽑으면 이 값이 0 에 붙는다.

5. **이음새 · wrap** — 마지막 장과 첫 장이 얼마나 겹치는가(IoU). 한 바퀴가 이어지려면
   이웃 프레임끼리의 평균 겹침(wrap)과 비슷해야 한다. **이음새가 wrap 보다 크게 낮으면 튄다.**
"""
import os
import sys

from PIL import Image

ALPHA = 16


def load(folder):
    fs = [f for f in sorted(os.listdir(folder))
          if f.startswith("frame_") and f.endswith(".png")]
    return [Image.open(os.path.join(folder, f)).convert("RGBA") for f in fs]


def mask(im):
    w, h = im.size
    a = im.split()[-1].load()
    return {(x, y) for y in range(h) for x in range(w) if a[x, y] > ALPHA}


def iou(m1, m2):
    u = len(m1 | m2)
    return len(m1 & m2) / float(u) if u else 0.0


def feet(m):
    """실루엣을 가로 중심으로 갈라 좌우 각각의 가장 아래 y 를 낸다."""
    xs = [p[0] for p in m]
    cx = (min(xs) + max(xs)) / 2.0
    left = [y for x, y in m if x < cx]
    right = [y for x, y in m if x >= cx]
    if not left or not right:
        return None
    return max(left) - max(right)


def arm_band(m, im):
    """어깨~허리 띠의 화소 집합. 실루엣 세로의 25%~55% 구간."""
    ys = [p[1] for p in m]
    top, bot = min(ys), max(ys)
    span = bot - top
    lo = top + int(span * 0.25)
    hi = top + int(span * 0.55)
    return {(x, y) for x, y in m if lo <= y <= hi}


def report(folder):
    ims = load(folder)
    if not ims:
        print("%s -- frame 0" % folder)
        return
    ms = [mask(im) for im in ims]
    n = len(ms)

    areas = [len(m) for m in ms]
    spread = (max(areas) - min(areas)) / float(max(areas)) * 100.0

    ds = [feet(m) for m in ms]
    flips = 0
    for i in range(n):
        a, b = ds[i], ds[(i + 1) % n]
        if a is None or b is None:
            continue
        if (a > 0 and b < 0) or (a < 0 and b > 0):
            flips += 1

    arms = [arm_band(m, im) for m, im in zip(ms, ims)]
    arm_moves = []
    for i in range(n):
        a, b = arms[i], arms[(i + 1) % n]
        u = len(a | b)
        arm_moves.append(len(a ^ b) / float(u) * 100.0 if u else 0.0)

    wrap = sum(iou(ms[i], ms[(i + 1) % n]) for i in range(n - 1)) / float(n - 1)
    seam = iou(ms[-1], ms[0])

    print("%s  (%d frames)" % (folder, n))
    print("  1 실루엣 넓이      %s   폭 %.1f%%" % (
        " ".join("%d" % a for a in areas), spread))
    print("  2 좌우 발 밑변 차  %s" % " ".join(
        ("%d" % d if d is not None else "-") for d in ds))
    print("  3 부호 반전        %d/%d" % (flips, n))
    print("  4 팔 띠 움직임     %s   평균 %.1f%%" % (
        " ".join("%.1f" % v for v in arm_moves),
        sum(arm_moves) / float(n)))
    print("  5 이음새 %.3f   wrap %.3f   (이음새/wrap %.3f)" % (
        seam, wrap, seam / wrap if wrap else 0.0))


if __name__ == "__main__":
    for folder in sys.argv[1:]:
        report(folder)
        print("")
