"""벌의 모든 칸을 정해진 캔버스로 **손실 없이** 가운데 자르기 한다. (구현-아트 세션 소유)

돌리는 법 — 리포 루트에서:
    python Docs/art_log/crop_to_canvas.py 128 <프레임 폴더> [<프레임 폴더> ...]

**왜 필요한가.** `animate_character` v3 는 동작이 캔버스를 넘칠 것 같으면 **캔버스를 스스로 키운다.**
09-15 보병 벌 여섯이 144 · 152 · 148 · 128 로 제각각 나왔다 — 사용자 확정은 「전부 128 통일」이었다.

**어떻게 안전한가.** 자르기 전에 **그 벌의 모든 칸을 합친 실루엣**을 먼저 잰다.
합친 실루엣이 목표 캔버스 안에 들어가지 않으면 **한 장도 건드리지 않고 그만둔다.**
들어가면 가운데를 기준으로 잘라 **기계의 화소는 한 점도 안 잃는다.**

**왜 벌마다 같은 오프셋인가.** 칸마다 다르게 자르면 그림이 칸 사이에서 **튄다** —
움직임 자체가 달라진다. 그래서 오프셋은 벌 하나에 하나뿐이다.
"""
import os
import sys

from PIL import Image

ALPHA = 16


def frames(folder):
    return [f for f in sorted(os.listdir(folder))
            if f.startswith("frame_") and f.endswith(".png")]


def union_box(folder, fs):
    x0 = y0 = 10 ** 9
    x1 = y1 = -1
    size = None
    for f in fs:
        im = Image.open(os.path.join(folder, f)).convert("RGBA")
        size = im.size
        w, h = size
        a = im.split()[-1].load()
        for y in range(h):
            for x in range(w):
                if a[x, y] > ALPHA:
                    if x < x0: x0 = x
                    if x > x1: x1 = x
                    if y < y0: y0 = y
                    if y > y1: y1 = y
    return (x0, y0, x1, y1), size


def crop(folder, target):
    fs = frames(folder)
    if not fs:
        print("%s -- 칸 0" % folder)
        return
    (x0, y0, x1, y1), (w, h) = union_box(folder, fs)
    if w == target and h == target:
        print("%s -- 이미 %d, 그대로 둔다" % (folder, target))
        return

    ox, oy = (w - target) // 2, (h - target) // 2
    if ox < 0 or oy < 0:
        print("%s -- 캔버스 %d 가 목표 %d 보다 작다. 그만둔다" % (folder, w, target))
        return
    if x0 < ox or y0 < oy or x1 >= ox + target or y1 >= oy + target:
        print("%s -- 합친 실루엣 (%d,%d)-(%d,%d) 이 자를 창 밖으로 나간다. "
              "한 장도 안 건드리고 그만둔다" % (folder, x0, y0, x1, y1))
        return

    for f in fs:
        p = os.path.join(folder, f)
        im = Image.open(p).convert("RGBA")
        im.crop((ox, oy, ox + target, oy + target)).save(p)
    print("%s -- %d -> %d  (오프셋 %d,%d · 칸 %d장 · 손실 0)" % (
        folder, w, target, ox, oy, len(fs)))


if __name__ == "__main__":
    t = int(sys.argv[1])
    for folder in sys.argv[2:]:
        crop(folder, t)
