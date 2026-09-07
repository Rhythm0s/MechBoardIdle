"""한 벌을 여러 재생 속도로 뽑아 나란히 비교한다. (구현-아트 세션 소유 · 플랜 §20-1)

돌리는 법 — 리포 루트에서:
    python Docs/art_log/make_fps_sweep.py <벌 폴더> [fps...]

    예) python Docs/art_log/make_fps_sweep.py Assets/_Project/Art/Anim/robot_a_Idle/south 9 12 15 24

**왜 있는가.** 프레임 재생 속도를 정한 기획 문서가 없다(`260907_V01` ❓2-2). 지금 코드 값
(대기 6fps · 나머지 8fps)은 **구현 가정**이며, 속도는 숫자로 못 고르고 **움직이는 것을 봐야** 고른다.

`Docs/art_log/preview/fps/<clip>_<dir>_<fps>fps.gif` 로 낸다.

**대기는 되감기로 낸다** — 0·1·2·3·4·3·2·1 (2026-09-07 사용자 확정). 실제 재생 방식과 같아야
속도 판정이 화면과 어긋나지 않는다. 이동·사망·태그는 그냥 반복이다.

⚠️ **여기서 고른 속도는 코드에 들어가야 확정이다.** `CombatAssetGenerator.DefaultFps` 가 그 자리이며
`Assets/_Project/Scripts/**` 는 아트 세션 소유가 아니다 — 고른 값은 「→ 플랜」 문서로 올린다.
"""
import os
import sys

from PIL import Image, ImageDraw

OUT_DIR = "Docs/art_log/preview/fps"
ALPHA = 16
CANVAS = 256
PINGPONG_STATES = {"Idle"}


def frames_of(folder):
    return sorted(f for f in os.listdir(folder)
                  if f.startswith("frame_") and f.endswith(".png"))


def top_edge(img):
    alpha = img.split()[-1]
    w, h = img.size
    px = alpha.load()
    for y in range(h):
        for x in range(w):
            if px[x, y] > ALPHA:
                return y
    return None


def checker(size, box=8):
    im = Image.new("RGB", (size, size), (58, 58, 62))
    d = ImageDraw.Draw(im)
    for y in range(0, size, box):
        for x in range(0, size, box):
            if (x // box + y // box) % 2 == 0:
                d.rectangle([x, y, x + box - 1, y + box - 1], fill=(48, 48, 52))
    return im


def pingpong(frames):
    """0·1·2·3·4·3·2·1 — 양 끝은 겹치지 않는다."""
    if len(frames) < 3:
        return list(frames)
    return list(frames) + list(reversed(frames[1:-1]))


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 1

    folder = sys.argv[1].replace("\\", "/").rstrip("/")
    rates = [int(x) for x in sys.argv[2:]] or [9, 12, 15, 24]

    if not os.path.isdir(folder):
        print("폴더가 없다:", folder)
        return 1

    files = frames_of(folder)
    if not files:
        print("프레임이 없다:", folder)
        return 1

    parts = folder.split("/")
    direction = parts[-1]
    clip = parts[-2]
    state = clip.rsplit("_", 1)[1]

    os.makedirs(OUT_DIR, exist_ok=True)

    cell = Image.open(os.path.join(folder, files[0])).size[0]
    tile = checker(cell)
    base_top = top_edge(Image.open(os.path.join(folder, files[0])).convert("RGBA"))

    cells = []
    for name in files:
        src = Image.open(os.path.join(folder, name)).convert("RGBA")
        bg = tile.copy()
        bg.paste(src, (0, 0), src)
        if state in PINGPONG_STATES and base_top is not None:
            gy = int(base_top * cell / CANVAS)
            ImageDraw.Draw(bg).line([(0, gy), (cell - 1, gy)], fill=(230, 90, 70), width=1)
        cells.append(bg)

    if state in PINGPONG_STATES:
        cells = pingpong(cells)

    for fps in rates:
        ms = int(round(1000.0 / fps))
        page = [c.copy() for c in cells]
        d = ImageDraw.Draw(page[0])
        name = "%s_%s_%02dfps.gif" % (clip, direction, fps)
        p = os.path.join(OUT_DIR, name)
        page[0].save(p, save_all=True, append_images=page[1:],
                     duration=ms, loop=0, optimize=True)
        cycle = len(cells) * ms
        print("  %-32s %df · %dfps · 프레임당 %dms · 한 바퀴 %dms  %d bytes"
              % (name, len(cells), fps, ms, cycle, os.path.getsize(p)))

    print("%s/%s · %s · 되감기 %s"
          % (clip, direction, "·".join("%dfps" % r for r in rates),
             "적용" if state in PINGPONG_STATES else "안 함"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
